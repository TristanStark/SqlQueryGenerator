using System.Text;
using System.Text.RegularExpressions;
using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.Core.Parsing;

public sealed partial class SqlSchemaParser
{
    private readonly ForeignKeyInferer _foreignKeyInferer;

    public SqlSchemaParser() : this(new ForeignKeyInferer())
    {
    }

    public SqlSchemaParser(ForeignKeyInferer foreignKeyInferer)
    {
        _foreignKeyInferer = foreignKeyInferer;
    }

    public DatabaseSchema Parse(string sqlText, SchemaParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sqlText);
        options ??= new SchemaParseOptions();
        if (sqlText.Length > options.MaxInputCharacters)
        {
            throw new InvalidOperationException($"Le fichier de schéma dépasse la taille autorisée ({options.MaxInputCharacters:N0} caractères). Ajuste SchemaParseOptions.MaxInputCharacters si nécessaire.");
        }

        DatabaseSchema schema = new();
        string text = NormalizeLineEndings(sqlText);
        IReadOnlyDictionary<string, string> commentsByColumn = ExtractCommentOnColumnStatements(text);
        IReadOnlyDictionary<string, string> commentsByTable = ExtractCommentOnTableStatements(text);
        IReadOnlyList<string> statements = SplitStatements(text);

        foreach (string statement in statements)
        {
            string trimmed = statement.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (CreateTableRegex().Match(trimmed) is { Success: true } match)
            {
                TryParseCreateTable(trimmed, match, commentsByColumn, commentsByTable, schema);
                continue;
            }

            if (CreateViewRegex().Match(trimmed) is { Success: true } viewMatch)
            {
                TryParseCreateView(trimmed, viewMatch, commentsByTable, schema);
                continue;
            }

            if (CreateIndexRegex().Match(trimmed) is { Success: true } indexMatch)
            {
                TryParseCreateIndex(trimmed, indexMatch, schema);
            }
        }

        foreach (TableDefinition table in schema.Tables)
        {
            string full = table.FullName;
            string tableName = table.Name;
            if (commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(full), out string? fullComment) || commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(tableName), out fullComment))
            {
                table.Comment = fullComment;
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                ColumnDefinition column = table.Columns[i];
                string key1 = SqlNameNormalizer.Normalize($"{full}.{column.Name}");
                string key2 = SqlNameNormalizer.Normalize($"{tableName}.{column.Name}");
                if (commentsByColumn.TryGetValue(key1, out string? comment) || commentsByColumn.TryGetValue(key2, out comment))
                {
                    table.Columns[i] = column with { Comment = comment };
                }
            }
        }

        if (options.InferRelationships)
        {
            foreach (InferredRelationship rel in _foreignKeyInferer.Infer(schema))
            {
                schema.Relationships.Add(rel);
            }
        }

        return schema;
    }

    private static void TryParseCreateView(
        string statement,
        Match viewMatch,
        IReadOnlyDictionary<string, string> commentsByTable,
        DatabaseSchema schema)
    {
        string rawViewName = viewMatch.Groups["name"].Value.Trim();
        (string? schemaName, string? viewName) = SplitQualifiedName(rawViewName);
        int asIndex = Regex.Match(statement, @"\bAS\b", RegexOptions.IgnoreCase).Index;
        if (asIndex <= 0)
        {
            schema.Warnings.Add($"Vue {rawViewName} détectée mais clause AS introuvable.");
            return;
        }

        string viewSql = statement[asIndex..].Trim();
        TableDefinition view = new(viewName, schemaName, isView: true, viewSql: viewSql);
        if (commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(view.FullName), out string? comment) || commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(view.Name), out comment))
        {
            view.Comment = comment;
        }

        IReadOnlyList<string> explicitColumns = ExtractExplicitViewColumns(statement, viewMatch);
        if (explicitColumns.Count > 0)
        {
            foreach (string column in explicitColumns)
            {
                view.Columns.Add(new ColumnDefinition
                {
                    TableName = view.FullName,
                    Name = column,
                    DataType = "VIEW_EXPR",
                    Comment = "Colonne déclarée dans CREATE VIEW",
                    IsNullable = true
                });
            }
        }
        else
        {
            foreach (string column in InferViewColumnsFromSelect(statement))
            {
                view.Columns.Add(new ColumnDefinition
                {
                    TableName = view.FullName,
                    Name = column,
                    DataType = "VIEW_EXPR",
                    Comment = "Colonne inférée depuis le SELECT de la vue",
                    IsNullable = true
                });
            }
        }

        if (view.Columns.Count == 0)
        {
            view.Columns.Add(new ColumnDefinition
            {
                TableName = view.FullName,
                Name = "view_expression",
                DataType = "VIEW_EXPR",
                Comment = "Colonnes non inférées automatiquement ; consulte le SQL de la vue.",
                IsNullable = true
            });
            schema.Warnings.Add($"Vue {rawViewName} détectée mais les colonnes n'ont pas pu être inférées précisément.");
        }

        schema.Tables.Add(view);
    }

    private static IReadOnlyList<string> ExtractExplicitViewColumns(string statement, Match viewMatch)
    {
        int afterName = viewMatch.Index + viewMatch.Length;
        while (afterName < statement.Length && char.IsWhiteSpace(statement[afterName]))
        {
            afterName++;
        }

        if (afterName >= statement.Length || statement[afterName] != '(')
        {
            return Array.Empty<string>();
        }

        int close = FindMatchingParenthesis(statement, afterName);
        if (close <= afterName)
        {
            return Array.Empty<string>();
        }

        return SplitTopLevelComma(statement[(afterName + 1)..close])
            .Select(CleanIdentifier)
            .Where(c => Regex.IsMatch(c, @"^[A-Za-z_][A-Za-z0-9_$#]*$", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> InferViewColumnsFromSelect(string statement)
    {
        Match selectMatch = Regex.Match(statement, @"\bSELECT\b", RegexOptions.IgnoreCase);
        if (!selectMatch.Success)
        {
            return Array.Empty<string>();
        }

        int fromIndex = FindTopLevelKeyword(statement, "FROM", selectMatch.Index + selectMatch.Length);
        if (fromIndex <= selectMatch.Index)
        {
            return Array.Empty<string>();
        }

        string selectList = statement[(selectMatch.Index + selectMatch.Length)..fromIndex];
        return SplitTopLevelComma(selectList)
            .Select(TryInferSelectAlias)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(512)
            .ToArray();
    }

    private static int FindTopLevelKeyword(string text, string keyword, int startIndex)
    {
        int depth = 0;
        bool inSingle = false;
        bool inDouble = false;
        for (int i = startIndex; i < text.Length - keyword.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (!inSingle && !inDouble)
            {
                if (c == '(') depth++;
                else if (c == ')' && depth > 0) depth--;
                else if (depth == 0 && string.Compare(text, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    bool beforeOk = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
                    int after = i + keyword.Length;
                    bool afterOk = after >= text.Length || !char.IsLetterOrDigit(text[after]);
                    if (beforeOk && afterOk)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    private static string? TryInferSelectAlias(string expression)
    {
        string cleaned = expression.Trim();
        if (cleaned.Length == 0 || cleaned == "*")
        {
            return null;
        }

        Match asMatch = Regex.Match(cleaned, @"\bAS\s+(?<alias>[`""\[]?\w+[`""\]]?)\s*$", RegexOptions.IgnoreCase);
        if (asMatch.Success)
        {
            return CleanIdentifier(asMatch.Groups["alias"].Value);
        }

        IReadOnlyList<string> tokens = TokenizeDefinition(cleaned);
        if (tokens.Count >= 2)
        {
            string last = CleanIdentifier(tokens[^1]);
            if (!string.Equals(last, "DESC", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(last, "ASC", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(last, @"^[A-Za-z_][A-Za-z0-9_$#]*$", RegexOptions.IgnoreCase))
            {
                return last;
            }
        }

        string[] dotParts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string candidate = CleanIdentifier(dotParts[^1]);
        if (Regex.IsMatch(candidate, @"^[A-Za-z_][A-Za-z0-9_$#]*$", RegexOptions.IgnoreCase))
        {
            return candidate;
        }

        return null;
    }

    private static void TryParseCreateIndex(string statement, Match indexMatch, DatabaseSchema schema)
    {
        string indexName = CleanQualifiedIdentifier(indexMatch.Groups["name"].Value);
        string tableName = CleanQualifiedIdentifier(indexMatch.Groups["table"].Value);
        bool unique = indexMatch.Groups["unique"].Success;

        int open = statement.IndexOf('(', StringComparison.Ordinal);
        int close = FindMatchingParenthesis(statement, open);
        if (open < 0 || close <= open)
        {
            schema.Warnings.Add($"Impossible de lire le CREATE INDEX {indexName}: parenthèses introuvables.");
            return;
        }

        string[] columns = [.. SplitTopLevelComma(statement[(open + 1)..close])
            .Select(TryExtractIndexedColumnName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (columns.Length == 0)
        {
            schema.Warnings.Add($"Index {indexName} ignoré: aucune colonne simple lisible.");
            return;
        }

        schema.Indexes.Add(new IndexDefinition(indexName, tableName, unique, columns));
    }

    private static string? TryExtractIndexedColumnName(string expression)
    {
        string trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        // Ignore functional indexes such as UPPER(NAME) for FK inference. They are not useful
        // for equality joins on the raw column and parsing them as columns would be misleading.
        if (trimmed.Contains('(') || trimmed.Contains(')'))
        {
            return null;
        }

        string? firstToken = trimmed.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return null;
        }

        string cleaned = CleanIdentifier(firstToken);
        return Regex.IsMatch(cleaned, @"^[A-Za-z_][A-Za-z0-9_$#]*$", RegexOptions.IgnoreCase) ? cleaned : null;
    }

    private static void TryParseCreateTable(
        string statement,
        Match createMatch,
        IReadOnlyDictionary<string, string> commentsByColumn,
        IReadOnlyDictionary<string, string> commentsByTable,
        DatabaseSchema schema)
    {
        string rawTableName = createMatch.Groups["name"].Value.Trim();
        (string? schemaName, string? tableName) = SplitQualifiedName(rawTableName);
        TableDefinition table = new(tableName, schemaName);
        int bodyStart = statement.IndexOf('(', StringComparison.Ordinal);
        int bodyEnd = FindMatchingParenthesis(statement, bodyStart);
        if (bodyStart < 0 || bodyEnd <= bodyStart)
        {
            schema.Warnings.Add($"Impossible de lire le CREATE TABLE {rawTableName}: parenthèses introuvables.");
            return;
        }

        string body = statement[(bodyStart + 1)..bodyEnd];
        IReadOnlyList<string> parts = SplitTopLevelComma(body);
        HashSet<string> tableLevelPk = new(StringComparer.OrdinalIgnoreCase);
        List<ColumnDefinition> inlineColumns = [];

        foreach (string part in parts)
        {
            string withoutBlockComments = RemoveBlockComments(part);
            (string? definition, string? trailingComment) = ExtractTrailingLineComment(withoutBlockComments);
            string trimmed = definition.Trim().TrimEnd(',');
            if (trimmed.Length == 0)
            {
                continue;
            }

            ParseTableConstraint(trimmed, rawTableName, schema, tableLevelPk);
            if (IsTableConstraint(trimmed))
            {
                continue;
            }

            ColumnDefinition? column = ParseColumnDefinition(trimmed, table.FullName, trailingComment, commentsByColumn);
            if (column is not null)
            {
                inlineColumns.Add(column);
            }
        }

        foreach (ColumnDefinition column in inlineColumns)
        {
            bool isPk = column.IsPrimaryKey || tableLevelPk.Contains(SqlNameNormalizer.Normalize(column.Name));
            table.Columns.Add(column with { IsPrimaryKey = isPk });
        }

        if (commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(table.FullName), out string? comment) || commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(table.Name), out comment))
        {
            table.Comment = comment;
        }

        if (table.Columns.Count == 0)
        {
            schema.Warnings.Add($"La table {rawTableName} a été détectée mais aucune colonne n'a pu être extraite.");
        }

        schema.Tables.Add(table);
    }

    private static ColumnDefinition? ParseColumnDefinition(
        string definition,
        string tableFullName,
        string? trailingComment,
        IReadOnlyDictionary<string, string> commentsByColumn)
    {
        IReadOnlyList<string> tokens = TokenizeDefinition(definition);
        if (tokens.Count < 2)
        {
            return null;
        }

        string columnName = CleanIdentifier(tokens[0]);
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        string dataType = ExtractDataType(tokens);
        string upper = definition.ToUpperInvariant();
        bool isNullable = !upper.Contains(" NOT NULL", StringComparison.OrdinalIgnoreCase);
        bool isPrimaryKey = upper.Contains(" PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
        string? inlineComment = ExtractInlineCommentClause(definition) ?? trailingComment;
        string commentKey = SqlNameNormalizer.Normalize($"{tableFullName}.{columnName}");
        if (commentsByColumn.TryGetValue(commentKey, out string? comment))
        {
            inlineComment = comment;
        }

        return new ColumnDefinition
        {
            TableName = tableFullName,
            Name = columnName,
            DataType = dataType,
            Comment = string.IsNullOrWhiteSpace(inlineComment) ? null : inlineComment.Trim(),
            IsNullable = isNullable,
            IsPrimaryKey = isPrimaryKey,
            IsDeclaredForeignKey = upper.Contains(" REFERENCES ", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static void ParseTableConstraint(string definition, string currentTable, DatabaseSchema schema, HashSet<string> tableLevelPk)
    {
        string normalized = definition.Trim();
        Match pkMatch = TablePrimaryKeyRegex().Match(normalized);
        if (pkMatch.Success)
        {
            foreach (string? col in SplitTopLevelComma(pkMatch.Groups["cols"].Value).Select(CleanIdentifier))
            {
                if (!string.IsNullOrWhiteSpace(col))
                {
                    tableLevelPk.Add(SqlNameNormalizer.Normalize(col));
                }
            }
        }

        Match fkMatch = ForeignKeyRegex().Match(normalized);
        if (fkMatch.Success)
        {
            string[] sourceCols = [.. SplitTopLevelComma(fkMatch.Groups["fromCols"].Value).Select(CleanIdentifier)];
            string targetTable = CleanQualifiedIdentifier(fkMatch.Groups["toTable"].Value);
            string[] targetCols = [.. SplitTopLevelComma(fkMatch.Groups["toCols"].Value).Select(CleanIdentifier)];
            int pairCount = Math.Min(sourceCols.Length, targetCols.Length);
            for (int i = 0; i < pairCount; i++)
            {
                schema.DeclaredForeignKeys.Add(new DeclaredForeignKey
                {
                    FromTable = CleanQualifiedIdentifier(currentTable),
                    FromColumn = sourceCols[i],
                    ToTable = targetTable,
                    ToColumn = targetCols[i],
                    ConstraintName = TryExtractConstraintName(normalized)
                });
            }
        }
    }

    private static string ExtractDataType(IReadOnlyList<string> tokens)
    {
        HashSet<string> stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "PRIMARY", "NOT", "NULL", "DEFAULT", "CONSTRAINT", "REFERENCES", "CHECK", "UNIQUE", "COMMENT", "COLLATE", "GENERATED", "IDENTITY"
        };

        StringBuilder sb = new();
        for (int i = 1; i < tokens.Count; i++)
        {
            if (stopWords.Contains(tokens[i]))
            {
                break;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(tokens[i]);
        }

        return sb.Length == 0 ? "UNKNOWN" : sb.ToString();
    }

    private static IReadOnlyDictionary<string, string> ExtractCommentOnColumnStatements(string text)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CommentOnColumnRegex().Matches(text))
        {
            string table = CleanQualifiedIdentifier(match.Groups["table"].Value);
            string col = CleanIdentifier(match.Groups["column"].Value);
            string comment = UnescapeSqlString(match.Groups["comment"].Value);
            result[SqlNameNormalizer.Normalize($"{table}.{col}")] = comment;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ExtractCommentOnTableStatements(string text)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CommentOnTableRegex().Matches(text))
        {
            string table = CleanQualifiedIdentifier(match.Groups["table"].Value);
            string comment = UnescapeSqlString(match.Groups["comment"].Value);
            result[SqlNameNormalizer.Normalize(table)] = comment;
        }

        return result;
    }

    public static IReadOnlyList<string> SplitStatements(string text)
    {
        List<string> statements = [];
        StringBuilder sb = new();
        bool inSingle = false;
        bool inDouble = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        int depth = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                sb.Append(c);
                if (c == '\n')
                {
                    inLineComment = false;
                }
                continue;
            }

            if (inBlockComment)
            {
                sb.Append(c);
                if (c == '*' && next == '/')
                {
                    sb.Append(next);
                    i++;
                    inBlockComment = false;
                }
                continue;
            }

            if (!inSingle && !inDouble && c == '-' && next == '-')
            {
                inLineComment = true;
                sb.Append(c).Append(next);
                i++;
                continue;
            }

            if (!inSingle && !inDouble && c == '/' && next == '*')
            {
                inBlockComment = true;
                sb.Append(c).Append(next);
                i++;
                continue;
            }

            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                sb.Append(c);
                continue;
            }

            if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
                sb.Append(c);
                continue;
            }

            if (!inSingle && !inDouble)
            {
                if (c == '(') depth++;
                if (c == ')' && depth > 0) depth--;
                if (c == ';' && depth == 0)
                {
                    statements.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
            }

            sb.Append(c);
        }

        if (sb.ToString().Trim().Length > 0)
        {
            statements.Add(sb.ToString());
        }

        return statements;
    }

    public static IReadOnlyList<string> SplitTopLevelComma(string text)
    {
        List<string> result = [];
        StringBuilder sb = new();
        int depth = 0;
        bool inSingle = false;
        bool inDouble = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                sb.Append(c);
                if (c == '\n')
                {
                    inLineComment = false;
                }
                continue;
            }

            if (inBlockComment)
            {
                sb.Append(c);
                if (c == '*' && next == '/')
                {
                    sb.Append(next);
                    i++;
                    inBlockComment = false;
                }
                continue;
            }

            if (!inSingle && !inDouble && c == '-' && next == '-')
            {
                inLineComment = true;
                sb.Append(c).Append(next);
                i++;
                continue;
            }

            if (!inSingle && !inDouble && c == '/' && next == '*')
            {
                inBlockComment = true;
                sb.Append(c).Append(next);
                i++;
                continue;
            }

            if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                sb.Append(c);
                continue;
            }

            if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
                sb.Append(c);
                continue;
            }

            if (!inSingle && !inDouble)
            {
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && depth > 0)
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    AppendLineCommentImmediatelyAfterComma(text, ref i, sb);
                    AddNonEmptyPart(result, sb);
                    sb.Clear();
                    continue;
                }
            }

            sb.Append(c);
        }

        AddNonEmptyPart(result, sb);
        return result;
    }

    private static void AppendLineCommentImmediatelyAfterComma(string text, ref int commaIndex, StringBuilder currentPart)
    {
        int i = commaIndex + 1;

        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
        {
            i++;
        }

        if (i + 1 >= text.Length || text[i] != '-' || text[i + 1] != '-')
        {
            return;
        }

        int end = text.IndexOf('\n', i);
        if (end < 0)
        {
            end = text.Length;
        }

        // DDL frequently uses:
        //     id INTEGER PRIMARY KEY, -- technical id
        // The comma separates columns, but the comment semantically belongs to the
        // column before it. Attach it before the split so ExtractTrailingLineComment
        // receives it with the correct column definition.
        currentPart.Append(' ');
        currentPart.Append(text[i..end].TrimEnd());
        commaIndex = end - 1;
    }

    private static void AddNonEmptyPart(ICollection<string> result, StringBuilder sb)
    {
        if (sb.ToString().Trim().Length > 0)
        {
            result.Add(sb.ToString());
        }
    }

    private static int FindMatchingParenthesis(string text, int openIndex)
    {
        if (openIndex < 0 || openIndex >= text.Length)
        {
            return -1;
        }

        int depth = 0;
        bool inSingle = false;
        bool inDouble = false;
        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (!inSingle && !inDouble)
            {
                if (c == '(') depth++;
                if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> TokenizeDefinition(string definition)
    {
        List<string> tokens = [];
        StringBuilder sb = new();
        int depth = 0;
        bool inSingle = false;
        bool inDouble = false;
        foreach (char c in definition)
        {
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            if (c == '"' && !inSingle) inDouble = !inDouble;
            if (!inSingle && !inDouble)
            {
                if (c == '(') depth++;
                if (c == ')' && depth > 0) depth--;
                if (char.IsWhiteSpace(c) && depth == 0)
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }
            }
            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }

    private static bool IsTableConstraint(string definition)
    {
        string upper = definition.TrimStart().ToUpperInvariant();
        return upper.StartsWith("CONSTRAINT ", StringComparison.Ordinal)
            || upper.StartsWith("PRIMARY KEY", StringComparison.Ordinal)
            || upper.StartsWith("FOREIGN KEY", StringComparison.Ordinal)
            || upper.StartsWith("UNIQUE", StringComparison.Ordinal)
            || upper.StartsWith("CHECK", StringComparison.Ordinal);
    }

    private static string CleanIdentifier(string identifier)
    {
        string value = identifier.Trim().Trim(',', ';').Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            value = value[1..^1];
        }
        value = value.Trim('`', '"');
        return value;
    }

    private static string CleanQualifiedIdentifier(string identifier)
    {
        IEnumerable<string> parts = identifier.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanIdentifier);
        return string.Join('.', parts);
    }

    private static (string? Schema, string Table) SplitQualifiedName(string rawName)
    {
        string cleaned = CleanQualifiedIdentifier(rawName);
        string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return (parts[^2], parts[^1]);
        }

        return (null, cleaned);
    }

    private static (string Definition, string? Comment) ExtractTrailingLineComment(string text)
    {
        bool inSingle = false;
        bool inDouble = false;
        for (int i = 0; i < text.Length - 1; i++)
        {
            char c = text[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            if (c == '"' && !inSingle) inDouble = !inDouble;
            if (!inSingle && !inDouble && c == '-' && text[i + 1] == '-')
            {
                return (text[..i], text[(i + 2)..].Trim());
            }
        }

        return (text, null);
    }

    private static string RemoveBlockComments(string text)
    {
        return BlockCommentRegex().Replace(text, " ");
    }

    private static string? ExtractInlineCommentClause(string definition)
    {
        Match match = InlineCommentRegex().Match(definition);
        return match.Success ? UnescapeSqlString(match.Groups["comment"].Value) : null;
    }

    private static string? TryExtractConstraintName(string definition)
    {
        Match match = ConstraintNameRegex().Match(definition);
        return match.Success ? CleanIdentifier(match.Groups["name"].Value) : null;
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string UnescapeSqlString(string value) => value.Replace("''", "'", StringComparison.Ordinal);


    [GeneratedRegex(@"CREATE\s+(?:OR\s+REPLACE\s+)?(?:FORCE\s+|NOFORCE\s+)?(?:(?:EDITIONABLE|NONEDITIONABLE)\s+)?VIEW\s+(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateViewRegex();

    [GeneratedRegex(@"CREATE\s+(?:GLOBAL\s+TEMPORARY\s+|TEMPORARY\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTableRegex();

    [GeneratedRegex(@"CREATE\s+(?<unique>UNIQUE\s+)?(?:BITMAP\s+)?INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s+ON\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateIndexRegex();

    [GeneratedRegex(@"COMMENT\s+ON\s+COLUMN\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\.(?<column>[`""\[]?\w+[`""\]]?)\s+IS\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CommentOnColumnRegex();

    [GeneratedRegex(@"COMMENT\s+ON\s+TABLE\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s+IS\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CommentOnTableRegex();

    [GeneratedRegex(@"COMMENT\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineCommentRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"(?:CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?\s+)?PRIMARY\s+KEY\s*\((?<cols>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TablePrimaryKeyRegex();

    [GeneratedRegex(@"(?:CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?\s+)?FOREIGN\s+KEY\s*\((?<fromCols>[^)]*)\)\s+REFERENCES\s+(?<toTable>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\((?<toCols>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ForeignKeyRegex();

    [GeneratedRegex(@"CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?", RegexOptions.IgnoreCase)]
    private static partial Regex ConstraintNameRegex();
}
