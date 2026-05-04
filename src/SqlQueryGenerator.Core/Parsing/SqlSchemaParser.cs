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

        var schema = new DatabaseSchema();
        var text = NormalizeLineEndings(sqlText);
        var commentsByColumn = ExtractCommentOnColumnStatements(text);
        var commentsByTable = ExtractCommentOnTableStatements(text);
        var statements = SplitStatements(text);

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (CreateTableRegex().Match(trimmed) is { Success: true } match)
            {
                TryParseCreateTable(trimmed, match, commentsByColumn, commentsByTable, schema);
            }
        }

        foreach (var table in schema.Tables)
        {
            var full = table.FullName;
            var tableName = table.Name;
            if (commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(full), out var fullComment) || commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(tableName), out fullComment))
            {
                table.Comment = fullComment;
            }

            for (var i = 0; i < table.Columns.Count; i++)
            {
                var column = table.Columns[i];
                var key1 = SqlNameNormalizer.Normalize($"{full}.{column.Name}");
                var key2 = SqlNameNormalizer.Normalize($"{tableName}.{column.Name}");
                if (commentsByColumn.TryGetValue(key1, out var comment) || commentsByColumn.TryGetValue(key2, out comment))
                {
                    table.Columns[i] = column with { Comment = comment };
                }
            }
        }

        if (options.InferRelationships)
        {
            foreach (var rel in _foreignKeyInferer.Infer(schema))
            {
                schema.Relationships.Add(rel);
            }
        }

        return schema;
    }

    private static void TryParseCreateTable(
        string statement,
        Match createMatch,
        IReadOnlyDictionary<string, string> commentsByColumn,
        IReadOnlyDictionary<string, string> commentsByTable,
        DatabaseSchema schema)
    {
        var rawTableName = createMatch.Groups["name"].Value.Trim();
        var (schemaName, tableName) = SplitQualifiedName(rawTableName);
        var table = new TableDefinition(tableName, schemaName);
        var bodyStart = statement.IndexOf('(', StringComparison.Ordinal);
        var bodyEnd = FindMatchingParenthesis(statement, bodyStart);
        if (bodyStart < 0 || bodyEnd <= bodyStart)
        {
            schema.Warnings.Add($"Impossible de lire le CREATE TABLE {rawTableName}: parenthèses introuvables.");
            return;
        }

        var body = statement[(bodyStart + 1)..bodyEnd];
        var parts = SplitTopLevelComma(body);
        var tableLevelPk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inlineColumns = new List<ColumnDefinition>();

        foreach (var part in parts)
        {
            var withoutBlockComments = RemoveBlockComments(part);
            var (definition, trailingComment) = ExtractTrailingLineComment(withoutBlockComments);
            var trimmed = definition.Trim().TrimEnd(',');
            if (trimmed.Length == 0)
            {
                continue;
            }

            ParseTableConstraint(trimmed, rawTableName, schema, tableLevelPk);
            if (IsTableConstraint(trimmed))
            {
                continue;
            }

            var column = ParseColumnDefinition(trimmed, table.FullName, trailingComment, commentsByColumn);
            if (column is not null)
            {
                inlineColumns.Add(column);
            }
        }

        foreach (var column in inlineColumns)
        {
            var isPk = column.IsPrimaryKey || tableLevelPk.Contains(SqlNameNormalizer.Normalize(column.Name));
            table.Columns.Add(column with { IsPrimaryKey = isPk });
        }

        if (commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(table.FullName), out var comment) || commentsByTable.TryGetValue(SqlNameNormalizer.Normalize(table.Name), out comment))
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
        var tokens = TokenizeDefinition(definition);
        if (tokens.Count < 2)
        {
            return null;
        }

        var columnName = CleanIdentifier(tokens[0]);
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        var dataType = ExtractDataType(tokens);
        var upper = definition.ToUpperInvariant();
        var isNullable = !upper.Contains(" NOT NULL", StringComparison.OrdinalIgnoreCase);
        var isPrimaryKey = upper.Contains(" PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
        var inlineComment = ExtractInlineCommentClause(definition) ?? trailingComment;
        var commentKey = SqlNameNormalizer.Normalize($"{tableFullName}.{columnName}");
        if (commentsByColumn.TryGetValue(commentKey, out var comment))
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
        var normalized = definition.Trim();
        var pkMatch = TablePrimaryKeyRegex().Match(normalized);
        if (pkMatch.Success)
        {
            foreach (var col in SplitTopLevelComma(pkMatch.Groups["cols"].Value).Select(CleanIdentifier))
            {
                if (!string.IsNullOrWhiteSpace(col))
                {
                    tableLevelPk.Add(SqlNameNormalizer.Normalize(col));
                }
            }
        }

        var fkMatch = ForeignKeyRegex().Match(normalized);
        if (fkMatch.Success)
        {
            var sourceCols = SplitTopLevelComma(fkMatch.Groups["fromCols"].Value).Select(CleanIdentifier).ToArray();
            var targetTable = CleanQualifiedIdentifier(fkMatch.Groups["toTable"].Value);
            var targetCols = SplitTopLevelComma(fkMatch.Groups["toCols"].Value).Select(CleanIdentifier).ToArray();
            var pairCount = Math.Min(sourceCols.Length, targetCols.Length);
            for (var i = 0; i < pairCount; i++)
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
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PRIMARY", "NOT", "NULL", "DEFAULT", "CONSTRAINT", "REFERENCES", "CHECK", "UNIQUE", "COMMENT", "COLLATE", "GENERATED", "IDENTITY"
        };

        var sb = new StringBuilder();
        for (var i = 1; i < tokens.Count; i++)
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
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CommentOnColumnRegex().Matches(text))
        {
            var table = CleanQualifiedIdentifier(match.Groups["table"].Value);
            var col = CleanIdentifier(match.Groups["column"].Value);
            var comment = UnescapeSqlString(match.Groups["comment"].Value);
            result[SqlNameNormalizer.Normalize($"{table}.{col}")] = comment;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ExtractCommentOnTableStatements(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CommentOnTableRegex().Matches(text))
        {
            var table = CleanQualifiedIdentifier(match.Groups["table"].Value);
            var comment = UnescapeSqlString(match.Groups["comment"].Value);
            result[SqlNameNormalizer.Normalize(table)] = comment;
        }

        return result;
    }

    public static IReadOnlyList<string> SplitStatements(string text)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();
        var inSingle = false;
        var inDouble = false;
        var inLineComment = false;
        var inBlockComment = false;
        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

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
        var result = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inSingle = false;
        var inDouble = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

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
        var i = commaIndex + 1;

        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
        {
            i++;
        }

        if (i + 1 >= text.Length || text[i] != '-' || text[i + 1] != '-')
        {
            return;
        }

        var end = text.IndexOf('\n', i);
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

        var depth = 0;
        var inSingle = false;
        var inDouble = false;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
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
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inSingle = false;
        var inDouble = false;
        foreach (var c in definition)
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
        var upper = definition.TrimStart().ToUpperInvariant();
        return upper.StartsWith("CONSTRAINT ", StringComparison.Ordinal)
            || upper.StartsWith("PRIMARY KEY", StringComparison.Ordinal)
            || upper.StartsWith("FOREIGN KEY", StringComparison.Ordinal)
            || upper.StartsWith("UNIQUE", StringComparison.Ordinal)
            || upper.StartsWith("CHECK", StringComparison.Ordinal);
    }

    private static string CleanIdentifier(string identifier)
    {
        var value = identifier.Trim().Trim(',', ';').Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            value = value[1..^1];
        }
        value = value.Trim('`', '"');
        return value;
    }

    private static string CleanQualifiedIdentifier(string identifier)
    {
        var parts = identifier.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanIdentifier);
        return string.Join('.', parts);
    }

    private static (string? Schema, string Table) SplitQualifiedName(string rawName)
    {
        var cleaned = CleanQualifiedIdentifier(rawName);
        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return (parts[^2], parts[^1]);
        }

        return (null, cleaned);
    }

    private static (string Definition, string? Comment) ExtractTrailingLineComment(string text)
    {
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < text.Length - 1; i++)
        {
            var c = text[i];
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
        var match = InlineCommentRegex().Match(definition);
        return match.Success ? UnescapeSqlString(match.Groups["comment"].Value) : null;
    }

    private static string? TryExtractConstraintName(string definition)
    {
        var match = ConstraintNameRegex().Match(definition);
        return match.Success ? CleanIdentifier(match.Groups["name"].Value) : null;
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string UnescapeSqlString(string value) => value.Replace("''", "'", StringComparison.Ordinal);

    [GeneratedRegex(@"CREATE\s+(?:GLOBAL\s+TEMPORARY\s+|TEMPORARY\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTableRegex();

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
