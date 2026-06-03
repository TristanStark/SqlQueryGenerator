using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Représente SqlSchemaParser dans SQL Query Generator.
/// </summary>
public sealed partial class SqlSchemaParser
{
    /// <summary>
    /// Stocke la valeur interne  foreignKeyInferer.
    /// </summary>
    /// <value>Valeur de _foreignKeyInferer.</value>
    private readonly ForeignKeyInferer _foreignKeyInferer;

    /// <summary>
    /// Initialise une nouvelle instance de SqlSchemaParser.
    /// </summary>
    /// <param name="ForeignKeyInferer">Paramètre ForeignKeyInferer.</param>
    public SqlSchemaParser() : this(new ForeignKeyInferer())
    {
    }

    /// <summary>
    /// Initialise une nouvelle instance de SqlSchemaParser.
    /// </summary>
    /// <param name="foreignKeyInferer">Paramètre foreignKeyInferer.</param>
    public SqlSchemaParser(ForeignKeyInferer foreignKeyInferer)
    {
        _foreignKeyInferer = foreignKeyInferer;
    }

    /// <summary>
    /// Exécute le traitement Parse.
    /// </summary>
    /// <param name="sqlText">Paramètre sqlText.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Stocke la valeur interne TryParseCreateView.
    /// </summary>
    /// <value>Valeur de TryParseCreateView.</value>
    private static void TryParseCreateView(
        string statement,
        Match viewMatch,
        IReadOnlyDictionary<string, string> commentsByTable,
        DatabaseSchema schema)
    {
        string rawViewName = viewMatch.Groups["name"].Value.Trim();
        (string? schemaName, string? viewName) = SplitQualifiedName(rawViewName);
        int asIndex = FindTopLevelKeyword(statement, "AS", viewMatch.Index + viewMatch.Length);
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
            foreach (ColumnDefinition column in InferViewColumnsFromSelect(statement, view.FullName, schema, asIndex))
            {
                view.Columns.Add(column);
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

    /// <summary>
    /// Exécute le traitement ExtractExplicitViewColumns.
    /// </summary>
    /// <param name="statement">Paramètre statement.</param>
    /// <param name="viewMatch">Paramètre viewMatch.</param>
    /// <returns>Résultat du traitement.</returns>
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
            .Where(IsReasonableViewColumnName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Exécute le traitement InferViewColumnsFromSelect.
    /// </summary>
    /// <param name="statement">Paramètre statement.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IReadOnlyList<ColumnDefinition> InferViewColumnsFromSelect(string statement, string viewFullName, DatabaseSchema schema, int viewAsIndex)
    {
        int selectIndex = FindTopLevelKeyword(statement, "SELECT", viewAsIndex + 2);
        if (selectIndex < 0)
        {
            return Array.Empty<ColumnDefinition>();
        }

        int fromIndex = FindTopLevelKeyword(statement, "FROM", selectIndex + 6);
        if (fromIndex <= selectIndex)
        {
            return Array.Empty<ColumnDefinition>();
        }

        string selectList = statement[(selectIndex + 6)..fromIndex];
        string fromClause = ExtractViewFromClause(statement, fromIndex);
        IReadOnlyDictionary<string, string> aliases = ExtractViewTableAliases(fromClause);
        List<ColumnDefinition> columns = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string selectItem in SplitTopLevelComma(selectList))
        {
            string expression = selectItem.Trim();
            if (expression.Length == 0)
            {
                continue;
            }

            if (TryExpandViewWildcard(expression, aliases, schema, viewFullName, columns, seen))
            {
                continue;
            }

            string? inferredName = TryInferSelectAlias(expression);
            if (string.IsNullOrWhiteSpace(inferredName) || !seen.Add(inferredName))
            {
                continue;
            }

            ColumnDefinition? sourceColumn = TryResolveViewSourceColumn(expression, aliases, schema);
            columns.Add(new ColumnDefinition
            {
                TableName = viewFullName,
                Name = inferredName,
                DataType = sourceColumn?.DataType ?? "VIEW_EXPR",
                Comment = sourceColumn is null
                    ? "Expression inférée depuis le SELECT de la vue"
                    : $"Colonne de vue mappée sur {sourceColumn.TableName}.{sourceColumn.Name}",
                IsNullable = sourceColumn?.IsNullable ?? true,
                IsPrimaryKey = sourceColumn?.IsPrimaryKey ?? false
            });
        }

        return columns.Take(512).ToArray();
    }

    /// <summary>
    /// Extracts the FROM/JOIN part of a view query, stopping before following top-level clauses.
    /// </summary>
    /// <param name="statement">Full CREATE VIEW statement.</param>
    /// <param name="fromIndex">Index of the top-level FROM keyword.</param>
    /// <returns>FROM clause content without the FROM keyword.</returns>
    private static string ExtractViewFromClause(string statement, int fromIndex)
    {
        int end = FirstPositiveAfter(fromIndex,
            FindTopLevelKeyword(statement, "WHERE", fromIndex + 4),
            FindTopLevelKeyword(statement, "GROUP BY", fromIndex + 4),
            FindTopLevelKeyword(statement, "HAVING", fromIndex + 4),
            FindTopLevelKeyword(statement, "ORDER BY", fromIndex + 4),
            FindTopLevelKeyword(statement, "UNION", fromIndex + 4),
            statement.Length);
        return statement[(fromIndex + 4)..end].Trim();
    }

    /// <summary>
    /// Returns the first candidate index located after a reference point.
    /// </summary>
    /// <param name="startIndex">Reference index.</param>
    /// <param name="candidates">Candidate indexes.</param>
    /// <returns>The first valid index, or the final fallback value.</returns>
    private static int FirstPositiveAfter(int startIndex, params int[] candidates)
    {
        return candidates.Where(c => c > startIndex).DefaultIfEmpty(candidates.LastOrDefault()).Min();
    }

    /// <summary>
    /// Extracts table aliases from a view FROM/JOIN clause for wildcard expansion and simple source-column mapping.
    /// </summary>
    /// <param name="fromClause">FROM clause content.</param>
    /// <returns>Alias-to-table mapping.</returns>
    private static IReadOnlyDictionary<string, string> ExtractViewTableAliases(string fromClause)
    {
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(fromClause, @"(?is)(?:^|\bJOIN\b|,)\s*(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)(?:\s+(?:AS\s+)?(?<alias>[`""\[]?\w+[`""\]]?))?"))
        {
            string table = CleanQualifiedIdentifier(match.Groups["table"].Value);
            if (string.IsNullOrWhiteSpace(table) || IsSqlKeyword(table))
            {
                continue;
            }

            aliases[table] = table;
            aliases[SqlObjectTail(table)] = table;
            if (match.Groups["alias"].Success)
            {
                string alias = CleanIdentifier(match.Groups["alias"].Value);
                if (!IsSqlKeyword(alias))
                {
                    aliases[alias] = table;
                }
            }
        }

        return aliases;
    }

    /// <summary>
    /// Expands <c>*</c> and <c>alias.*</c> projections when the source table is known in the loaded schema.
    /// </summary>
    /// <param name="expression">Projection expression.</param>
    /// <param name="aliases">Alias-to-table mapping from the view FROM clause.</param>
    /// <param name="schema">Loaded schema.</param>
    /// <param name="viewFullName">Full view name receiving inferred columns.</param>
    /// <param name="columns">Target column list.</param>
    /// <param name="seen">Set of already-emitted view column names.</param>
    /// <returns><c>true</c> when the expression was a wildcard projection.</returns>
    private static bool TryExpandViewWildcard(string expression, IReadOnlyDictionary<string, string> aliases, DatabaseSchema schema, string viewFullName, ICollection<ColumnDefinition> columns, ISet<string> seen)
    {
        string cleaned = expression.Trim();
        string? sourceTableName = null;
        if (cleaned == "*")
        {
            sourceTableName = aliases.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? aliases.Values.First()
                : null;
        }
        else if (cleaned.EndsWith(".*", StringComparison.Ordinal))
        {
            string prefix = CleanQualifiedIdentifier(cleaned[..^2]);
            sourceTableName = aliases.TryGetValue(prefix, out string? resolved) ? resolved : prefix;
        }
        else
        {
            return false;
        }

        TableDefinition? sourceTable = string.IsNullOrWhiteSpace(sourceTableName) ? null : schema.FindTable(sourceTableName);
        if (sourceTable is null)
        {
            return true;
        }

        foreach (ColumnDefinition sourceColumn in sourceTable.Columns)
        {
            if (!seen.Add(sourceColumn.Name))
            {
                continue;
            }

            columns.Add(sourceColumn with
            {
                TableName = viewFullName,
                Comment = $"Colonne exposée par la vue depuis {sourceTable.FullName}.{sourceColumn.Name}"
            });
        }

        return true;
    }

    /// <summary>
    /// Resolves a simple view projection to its source column when possible.
    /// </summary>
    /// <param name="expression">Projection expression.</param>
    /// <param name="aliases">Alias-to-table mapping.</param>
    /// <param name="schema">Loaded schema.</param>
    /// <returns>The source column, or <c>null</c> for expressions.</returns>
    private static ColumnDefinition? TryResolveViewSourceColumn(string expression, IReadOnlyDictionary<string, string> aliases, DatabaseSchema schema)
    {
        (string rawExpression, _) = SplitViewAlias(expression);
        string cleaned = CleanQualifiedIdentifier(rawExpression);
        if (cleaned.Contains('(') || cleaned.Contains(' ') || cleaned.Contains('+') || cleaned.Contains('-') || cleaned.Contains('/') || cleaned.Contains('*'))
        {
            return null;
        }

        string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            ColumnDefinition[] matches = aliases.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(table => schema.FindColumn(table, parts[0]))
                .Where(column => column is not null)
                .Cast<ColumnDefinition>()
                .Take(2)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        string prefix = string.Join('.', parts[..^1]);
        string table = aliases.TryGetValue(prefix, out string? resolved) ? resolved : prefix;
        return schema.FindColumn(table, parts[^1]);
    }

    /// <summary>
    /// Splits a view projection expression and its optional alias.
    /// </summary>
    /// <param name="expression">Projection expression.</param>
    /// <returns>Raw expression and optional alias.</returns>
    private static (string Expression, string? Alias) SplitViewAlias(string expression)
    {
        Match asMatch = Regex.Match(expression.Trim(), @"(?is)^(.+?)\s+AS\s+(?<alias>(?:`[^`]+`|""[^""]+""|\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_$#]*))\s*$");
        if (asMatch.Success)
        {
            return (asMatch.Groups[1].Value.Trim(), CleanIdentifier(asMatch.Groups["alias"].Value));
        }

        IReadOnlyList<string> tokens = TokenizeDefinition(expression.Trim());
        if (tokens.Count >= 2)
        {
            string last = CleanIdentifier(tokens[^1]);
            if (IsReasonableViewColumnName(last) && !IsSqlKeyword(last))
            {
                return (string.Join(' ', tokens.Take(tokens.Count - 1)), last);
            }
        }

        return (expression.Trim(), null);
    }

    /// <summary>
    /// Exécute le traitement FindTopLevelKeyword.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <param name="keyword">Paramètre keyword.</param>
    /// <param name="startIndex">Paramètre startIndex.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement TryInferSelectAlias.
    /// </summary>
    /// <param name="expression">Paramètre expression.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? TryInferSelectAlias(string expression)
    {
        string cleaned = expression.Trim();
        if (cleaned.Length == 0 || cleaned == "*")
        {
            return null;
        }

        (string rawExpression, string? alias) = SplitViewAlias(cleaned);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            return alias;
        }

        string[] dotParts = rawExpression.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string candidate = CleanIdentifier(dotParts[^1]);
        return IsReasonableViewColumnName(candidate) ? candidate : null;
    }

    /// <summary>
    /// Determines whether a view column name is usable in the application model.
    /// </summary>
    /// <param name="columnName">Column name candidate.</param>
    /// <returns><c>true</c> when the name is non-empty and not an obvious SQL expression.</returns>
    private static bool IsReasonableViewColumnName(string columnName)
    {
        string cleaned = CleanIdentifier(columnName);
        return cleaned.Length > 0
            && !cleaned.Contains('(')
            && !cleaned.Contains(')')
            && !cleaned.Contains(',')
            && !cleaned.Contains(';')
            && !IsSqlKeyword(cleaned);
    }

    /// <summary>
    /// Returns the last segment of a possibly schema-qualified SQL object name.
    /// </summary>
    /// <param name="name">Object name.</param>
    /// <returns>Last object segment.</returns>
    private static string SqlObjectTail(string name) => CleanIdentifier(name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? name);

    /// <summary>
    /// Determines whether a token is a clause keyword rather than a table alias or column name.
    /// </summary>
    /// <param name="token">Token to test.</param>
    /// <returns><c>true</c> for common SQL keywords.</returns>
    private static bool IsSqlKeyword(string token) => CleanIdentifier(token).ToUpperInvariant() is "AS" or "ON" or "WHERE" or "GROUP" or "BY" or "HAVING" or "ORDER" or "INNER" or "LEFT" or "RIGHT" or "FULL" or "JOIN" or "OUTER" or "CROSS" or "UNION" or "SELECT" or "FROM" or "DESC" or "ASC";

    /// <summary>
    /// Exécute le traitement TryParseCreateIndex.
    /// </summary>
    /// <param name="statement">Paramètre statement.</param>
    /// <param name="indexMatch">Paramètre indexMatch.</param>
    /// <param name="schema">Paramètre schema.</param>
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

        string[] columns = SplitTopLevelComma(statement[(open + 1)..close])
            .Select(TryExtractIndexedColumnName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (columns.Length == 0)
        {
            schema.Warnings.Add($"Index {indexName} ignoré: aucune colonne simple lisible.");
            return;
        }

        schema.Indexes.Add(new IndexDefinition(indexName, tableName, unique, columns));
    }

    /// <summary>
    /// Exécute le traitement TryExtractIndexedColumnName.
    /// </summary>
    /// <param name="expression">Paramètre expression.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Stocke la valeur interne TryParseCreateTable.
    /// </summary>
    /// <value>Valeur de TryParseCreateTable.</value>
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

    /// <summary>
    /// Stocke la valeur interne ParseColumnDefinition.
    /// </summary>
    /// <value>Valeur de ParseColumnDefinition.</value>
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

    /// <summary>
    /// Exécute le traitement ParseTableConstraint.
    /// </summary>
    /// <param name="definition">Paramètre definition.</param>
    /// <param name="currentTable">Paramètre currentTable.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="tableLevelPk">Paramètre tableLevelPk.</param>
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
            string[] sourceCols = SplitTopLevelComma(fkMatch.Groups["fromCols"].Value).Select(CleanIdentifier).ToArray();
            string targetTable = CleanQualifiedIdentifier(fkMatch.Groups["toTable"].Value);
            string[] targetCols = SplitTopLevelComma(fkMatch.Groups["toCols"].Value).Select(CleanIdentifier).ToArray();
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

    /// <summary>
    /// Exécute le traitement ExtractDataType.
    /// </summary>
    /// <param name="tokens">Paramètre tokens.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement ExtractCommentOnColumnStatements.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement ExtractCommentOnTableStatements.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement SplitStatements.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement SplitTopLevelComma.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement AppendLineCommentImmediatelyAfterComma.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <param name="commaIndex">Paramètre commaIndex.</param>
    /// <param name="currentPart">Paramètre currentPart.</param>
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

    /// <summary>
    /// Exécute le traitement AddNonEmptyPart.
    /// </summary>
    /// <param name="result">Paramètre result.</param>
    /// <param name="sb">Paramètre sb.</param>
    private static void AddNonEmptyPart(ICollection<string> result, StringBuilder sb)
    {
        if (sb.ToString().Trim().Length > 0)
        {
            result.Add(sb.ToString());
        }
    }

    /// <summary>
    /// Exécute le traitement FindMatchingParenthesis.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <param name="openIndex">Paramètre openIndex.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement TokenizeDefinition.
    /// </summary>
    /// <param name="definition">Paramètre definition.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement IsTableConstraint.
    /// </summary>
    /// <param name="definition">Paramètre definition.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsTableConstraint(string definition)
    {
        string upper = definition.TrimStart().ToUpperInvariant();
        return upper.StartsWith("CONSTRAINT ", StringComparison.Ordinal)
            || upper.StartsWith("PRIMARY KEY", StringComparison.Ordinal)
            || upper.StartsWith("FOREIGN KEY", StringComparison.Ordinal)
            || upper.StartsWith("UNIQUE", StringComparison.Ordinal)
            || upper.StartsWith("CHECK", StringComparison.Ordinal);
    }

    /// <summary>
    /// Exécute le traitement CleanIdentifier.
    /// </summary>
    /// <param name="identifier">Paramètre identifier.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement CleanQualifiedIdentifier.
    /// </summary>
    /// <param name="identifier">Paramètre identifier.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string CleanQualifiedIdentifier(string identifier)
    {
        IEnumerable<string> parts = identifier.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanIdentifier);
        return string.Join('.', parts);
    }

    /// <summary>
    /// Initialise une nouvelle instance de static.
    /// </summary>
    /// <param name="Schema">Paramètre Schema.</param>
    /// <param name="rawName">Paramètre rawName.</param>
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

    /// <summary>
    /// Initialise une nouvelle instance de static.
    /// </summary>
    /// <param name="Definition">Paramètre Definition.</param>
    /// <param name="text">Paramètre text.</param>
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

    /// <summary>
    /// Exécute le traitement RemoveBlockComments.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string RemoveBlockComments(string text)
    {
        return BlockCommentRegex().Replace(text, " ");
    }

    /// <summary>
    /// Exécute le traitement ExtractInlineCommentClause.
    /// </summary>
    /// <param name="definition">Paramètre definition.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? ExtractInlineCommentClause(string definition)
    {
        Match match = InlineCommentRegex().Match(definition);
        return match.Success ? UnescapeSqlString(match.Groups["comment"].Value) : null;
    }

    /// <summary>
    /// Exécute le traitement TryExtractConstraintName.
    /// </summary>
    /// <param name="definition">Paramètre definition.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? TryExtractConstraintName(string definition)
    {
        Match match = ConstraintNameRegex().Match(definition);
        return match.Success ? CleanIdentifier(match.Groups["name"].Value) : null;
    }

    /// <summary>
    /// Exécute le traitement NormalizeLineEndings.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    /// Exécute le traitement UnescapeSqlString.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string UnescapeSqlString(string value) => value.Replace("''", "'", StringComparison.Ordinal);


    /// <summary>
    /// Exécute le traitement CreateViewRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"CREATE\s+(?:OR\s+REPLACE\s+)?(?:FORCE\s+|NOFORCE\s+)?(?:(?:EDITIONABLE|NONEDITIONABLE)\s+)?VIEW\s+(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateViewRegex();

    /// <summary>
    /// Exécute le traitement CreateTableRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"CREATE\s+(?:GLOBAL\s+TEMPORARY\s+|TEMPORARY\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTableRegex();

    /// <summary>
    /// Exécute le traitement CreateIndexRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"CREATE\s+(?<unique>UNIQUE\s+)?(?:BITMAP\s+)?INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s+ON\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateIndexRegex();

    /// <summary>
    /// Exécute le traitement CommentOnColumnRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"COMMENT\s+ON\s+COLUMN\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\.(?<column>[`""\[]?\w+[`""\]]?)\s+IS\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CommentOnColumnRegex();

    /// <summary>
    /// Exécute le traitement CommentOnTableRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"COMMENT\s+ON\s+TABLE\s+(?<table>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s+IS\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CommentOnTableRegex();

    /// <summary>
    /// Exécute le traitement InlineCommentRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"COMMENT\s+'(?<comment>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineCommentRegex();

    /// <summary>
    /// Exécute le traitement BlockCommentRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    /// <summary>
    /// Exécute le traitement TablePrimaryKeyRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"(?:CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?\s+)?PRIMARY\s+KEY\s*\((?<cols>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TablePrimaryKeyRegex();

    /// <summary>
    /// Exécute le traitement ForeignKeyRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"(?:CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?\s+)?FOREIGN\s+KEY\s*\((?<fromCols>[^)]*)\)\s+REFERENCES\s+(?<toTable>(?:[`""\[]?\w+[`""\]]?\.)?[`""\[]?\w+[`""\]]?)\s*\((?<toCols>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ForeignKeyRegex();

    /// <summary>
    /// Exécute le traitement ConstraintNameRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"CONSTRAINT\s+[`""\[]?(?<name>\w+)[`""\]]?", RegexOptions.IgnoreCase)]
    private static partial Regex ConstraintNameRegex();
}
