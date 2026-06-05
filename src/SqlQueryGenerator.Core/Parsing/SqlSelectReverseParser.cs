using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Query;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Performs a best-effort reverse engineering of a read-only SELECT statement into a <see cref="QueryDefinition"/>.
/// </summary>
public sealed class SqlSelectReverseParser
{
    /// <summary>
    /// Converts a raw SELECT statement into the visual query model used by the application.
    /// </summary>
    /// <param name="sql">Raw SELECT statement pasted by the user.</param>
    /// <returns>A query definition filled with the clauses that could be recognized safely.</returns>
    public QueryDefinition Parse(string sql)
    {
        string normalized = SqlSafety.NormalizeRawSelectQuery(sql);
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
        QueryDefinition query = new();

        ThrowIfUnsupported(normalized);

        int selectIndex = FindTopLevelKeyword(normalized, "SELECT", 0);
        int fromIndex = FindTopLevelKeyword(normalized, "FROM", selectIndex + 6);
        if (selectIndex < 0 || fromIndex < 0)
        {
            throw new InvalidOperationException("Reverse SQL impossible: la requête doit contenir SELECT et FROM.");
        }

        int whereIndex = FindTopLevelKeyword(normalized, "WHERE", fromIndex + 4);
        int groupIndex = FindTopLevelKeyword(normalized, "GROUP BY", fromIndex + 4);
        int havingIndex = FindTopLevelKeyword(normalized, "HAVING", fromIndex + 4);
        int orderIndex = FindTopLevelKeyword(normalized, "ORDER BY", fromIndex + 4);
        int limitIndex = FindTopLevelKeyword(normalized, "LIMIT", fromIndex + 4);
        int fetchIndex = FindTopLevelKeyword(normalized, "FETCH FIRST", fromIndex + 4);
        int fetchNextIndex = FindTopLevelKeyword(normalized, "FETCH NEXT", fromIndex + 4);

        int fromEnd = FirstPositive(whereIndex, groupIndex, havingIndex, orderIndex, limitIndex, fetchIndex, fetchNextIndex, normalized.Length);
        if (selectIndex > 0 && normalized.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            query.WithClauseSql = normalized[..selectIndex].Trim();
        }

        string selectText = normalized[(selectIndex + 6)..fromIndex].Trim();
        string fromText = normalized[(fromIndex + 4)..fromEnd].Trim();
        string whereText = SliceClause(normalized, whereIndex, 5, groupIndex, havingIndex, orderIndex, limitIndex, fetchIndex, fetchNextIndex);
        string groupText = SliceClause(normalized, groupIndex, 8, havingIndex, orderIndex, limitIndex, fetchIndex, fetchNextIndex);
        string havingText = SliceClause(normalized, havingIndex, 6, orderIndex, limitIndex, fetchIndex, fetchNextIndex);
        string orderText = SliceClause(normalized, orderIndex, 8, limitIndex, fetchIndex, fetchNextIndex);
        string limitText = SliceClause(normalized, limitIndex, 5, fetchIndex, fetchNextIndex);
        int fetchStart = fetchIndex >= 0 ? fetchIndex : fetchNextIndex;
        int alternateFetchIndex = fetchStart == fetchIndex ? fetchNextIndex : fetchIndex;
        string fetchText = SliceClause(normalized, fetchStart, alternateFetchIndex);

        ParseFromAndJoins(fromText, query, aliases);
        ParseSelectItems(selectText, query, aliases);
        ParsePredicates(whereText, query, aliases, asHaving: false);
        ParseGroupBy(groupText, query, aliases);
        ParsePredicates(havingText, query, aliases, asHaving: true);
        ParseOrderBy(orderText, query, aliases);
        query.LimitRows ??= ParseLimit(limitText, fetchText);
        query.Parameters = new System.Collections.ObjectModel.Collection<QueryParameterDefinition>(ExtractParameters(normalized).ToList());
        return query;
    }

    /// <summary>
    /// Extracts parameter placeholders from raw SQL for display and saved query metadata.
    /// </summary>
    /// <param name="sql">Raw SQL text to scan.</param>
    /// <returns>Detected parameter definitions.</returns>
    public static IReadOnlyList<QueryParameterDefinition> ExtractParameters(string sql)
    {
        List<QueryParameterDefinition> parameters = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(sql, @"(?<![\w])([:@][A-Za-z_][A-Za-z0-9_]*|\?|&&?[A-Za-z0-9_]+)"))
        {
            string placeholder = match.Groups[1].Value;
            if (seen.Add(placeholder))
            {
                parameters.Add(new QueryParameterDefinition
                {
                    Name = placeholder,
                    Description = "Paramètre détecté dans le SQL brut",
                    Required = true
                });
            }
        }

        return parameters;
    }

    /// <summary>
    /// Parses the FROM clause and explicit joins.
    /// </summary>
    /// <param name="fromText">FROM clause content without the FROM keyword.</param>
    /// <param name="query">Query being populated.</param>
    /// <param name="aliases">Alias-to-table map filled while parsing tables.</param>
    private static void ParseFromAndJoins(string fromText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        if (ContainsTopLevelSeparator(fromText, ','))
        {
            throw new InvalidOperationException("Reverse SQL impossible: les jointures implicites via FROM a, b ne sont pas prises en charge.");
        }

        Match firstJoin = Regex.Match(fromText, @"\b(?:INNER\s+|LEFT\s+|LEFT\s+OUTER\s+)?JOIN\b", RegexOptions.IgnoreCase);
        string basePart = firstJoin.Success ? fromText[..firstJoin.Index].Trim() : fromText.Trim();
        string baseTable = ParseTableReference(basePart, aliases);
        query.BaseTable = baseTable;

        if (!firstJoin.Success)
        {
            return;
        }

        string joinsText = fromText[firstJoin.Index..];
        Regex joinRegex = new(@"(?is)\b(?:(INNER|LEFT(?:\s+OUTER)?)\s+)?JOIN\s+(.+?)\s+ON\s+(.+?)(?=\b(?:INNER\s+|LEFT\s+|LEFT\s+OUTER\s+)?JOIN\b|$)");
        foreach (Match joinMatch in joinRegex.Matches(joinsText))
        {
            JoinType joinType = joinMatch.Groups[1].Value.StartsWith("LEFT", StringComparison.OrdinalIgnoreCase) ? JoinType.Left : JoinType.Inner;
            string toTable = ParseTableReference(joinMatch.Groups[2].Value.Trim(), aliases);
            string onClause = joinMatch.Groups[3].Value.Trim();
            string[] predicates = SplitTopLevelByKeyword(onClause, "AND").ToArray();
            JoinDefinition? join = null;
            foreach (string predicate in predicates)
            {
                Match eq = Regex.Match(predicate, @"(?is)^(.+?)\s*=\s*(.+?)$");
                if (!eq.Success)
                {
                    continue;
                }

                ColumnReference? left = ParseColumnReference(eq.Groups[1].Value.Trim(), aliases);
                ColumnReference? right = ParseColumnReference(eq.Groups[2].Value.Trim(), aliases);
                if (left is null || right is null)
                {
                    continue;
                }

                if (join is null)
                {
                    join = new JoinDefinition
                    {
                        FromTable = left.Table,
                        FromColumn = left.Column,
                        ToTable = right.Table,
                        ToColumn = right.Column,
                        JoinType = joinType
                    };
                }
                else
                {
                    join.AdditionalColumnPairs.Add(new JoinColumnPair { FromColumn = left.Column, ToColumn = right.Column, Enabled = true });
                }
            }

            if (join is not null)
            {
                query.Joins.Add(join);
            }
        }
    }

    /// <summary>
    /// Parses SELECT projection items into selected columns, aggregates or custom raw expressions.
    /// </summary>
    /// <param name="selectText">SELECT clause content without the SELECT keyword.</param>
    /// <param name="query">Query being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    private static void ParseSelectItems(string selectText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;

            if (selectText.StartsWith("DISTINCT ", StringComparison.OrdinalIgnoreCase))
            {
                query.Distinct = true;
                selectText = selectText[8..].Trim();
                changed = true;
            }

            Match topMatch = Regex.Match(selectText, @"(?is)^TOP\s*\(?(\d+)\)?\s+");
            if (topMatch.Success)
            {
                query.LimitRows = int.Parse(topMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                selectText = selectText[topMatch.Length..].Trim();
                changed = true;
            }
        }

        foreach (string item in SplitTopLevelComma(selectText))
        {
            (string expression, string? alias) = SplitAlias(item);
            Match aggregate = Regex.Match(expression, @"(?is)^(COUNT|SUM|AVG|AVERAGE|MIN|MAX|MINIMUM|MAXIMUM)\s*\(\s*(DISTINCT\s+)?(.+?)\s*\)$");
            if (aggregate.Success)
            {
                string functionName = aggregate.Groups[1].Value;
                string inner = aggregate.Groups[3].Value.Trim();
                query.Aggregates.Add(new AggregateSelection
                {
                    Function = ParseAggregateFunction(functionName),
                    Distinct = aggregate.Groups[2].Success,
                    Column = inner == "*" ? null : ParseColumnReference(inner, aliases),
                    Alias = alias
                });
                continue;
            }

            ColumnReference? column = ParseColumnReference(expression, aliases);
            if (column is not null)
            {
                query.SelectedColumns.Add(column with { Alias = alias });
            }
            else
            {
                query.CustomColumns.Add(new CustomColumnSelection { RawExpression = expression, Alias = alias ?? "colonne_calculee" });
            }
        }
    }

    /// <summary>
    /// Parses WHERE or HAVING predicates into filter rows.
    /// </summary>
    /// <param name="text">Predicate text.</param>
    /// <param name="query">Query being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="asHaving">Whether the predicates belong to HAVING.</param>
    private static void ParsePredicates(string text, QueryDefinition query, Dictionary<string, string> aliases, bool asHaving)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (string predicate in SplitTopLevelByKeyword(text, "AND"))
        {
            FilterCondition? condition = ParsePredicate(predicate.Trim(), query, aliases, asHaving);
            if (condition is not null)
            {
                query.Filters.Add(condition);
            }
        }
    }

    /// <summary>
    /// Parses a single predicate.
    /// </summary>
    /// <param name="predicate">Predicate text.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="asHaving">Whether the predicate belongs to HAVING.</param>
    /// <returns>A filter condition, or <c>null</c> when the predicate cannot be understood.</returns>
    private static FilterCondition? ParsePredicate(string predicate, QueryDefinition query, Dictionary<string, string> aliases, bool asHaving)
    {
        Match isNull = Regex.Match(predicate, @"(?is)^(.+?)\s+(IS\s+NOT\s+NULL|IS\s+NULL)$");
        if (isNull.Success)
        {
            return BuildFilter(query, isNull.Groups[1].Value, isNull.Groups[2].Value, null, aliases, asHaving);
        }

        Match between = Regex.Match(predicate, @"(?is)^(.+?)\s+BETWEEN\s+(.+?)\s+AND\s+(.+)$");
        if (between.Success)
        {
            FilterCondition? filter = BuildFilter(query, between.Groups[1].Value, "BETWEEN", between.Groups[2].Value, aliases, asHaving);
            return filter is null ? null : filter with { SecondValue = UnquoteValue(between.Groups[3].Value.Trim()) };
        }

        Match inSubquery = Regex.Match(predicate, @"(?is)^(.+?)\s+(IN|NOT\s+IN)\s*\((\s*(?:SELECT|WITH)\b.+)\)$");
        if (inSubquery.Success)
        {
            FilterCondition? filter = BuildFilter(query, inSubquery.Groups[1].Value, inSubquery.Groups[2].Value, null, aliases, asHaving);
            return filter is null ? null : filter with
            {
                ValueKind = FilterValueKind.Subquery,
                RawSubquerySql = SqlSafety.NormalizeRawSelectQuery(inSubquery.Groups[3].Value.Trim()),
                SubqueryName = "sous_requete_importee"
            };
        }

        Match binary = Regex.Match(predicate, @"(?is)^(.+?)\s*(=|<>|!=|>=|<=|>|<|LIKE|NOT\s+LIKE|ILIKE|NOT\s+ILIKE|IN|NOT\s+IN)\s*(.+)$");
        if (binary.Success)
        {
            return BuildFilter(query, binary.Groups[1].Value, binary.Groups[2].Value, binary.Groups[3].Value, aliases, asHaving);
        }

        return null;
    }

    /// <summary>
    /// Builds a filter from a parsed left expression, operator and value.
    /// </summary>
    /// <param name="leftExpression">Left expression.</param>
    /// <param name="operatorText">SQL operator.</param>
    /// <param name="valueText">Right value.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="asHaving">Whether the filter is a HAVING filter.</param>
    /// <returns>Filter condition.</returns>
    private static FilterCondition? BuildFilter(QueryDefinition query, string leftExpression, string operatorText, string? valueText, Dictionary<string, string> aliases, bool asHaving)
    {
        string normalizedOperator = Regex.Replace(operatorText.Trim(), @"\s+", " ").ToUpperInvariant();
        FilterValueKind valueKind = DetectValueKind(valueText);
        string? value = valueKind == FilterValueKind.Literal ? UnquoteValue(valueText?.Trim()) : valueText?.Trim();
        string resolvedAlias = TrimIdentifierQuotes(leftExpression.Trim());
        if (TryResolveFieldAlias(query, resolvedAlias, asHaving, out QueryFieldKind fieldKind))
        {
            return new FilterCondition
            {
                FieldKind = fieldKind,
                FieldAlias = resolvedAlias,
                Operator = normalizedOperator,
                Value = value,
                ValueKind = valueKind
            };
        }

        ColumnReference? column = ParseColumnReference(leftExpression.Trim(), aliases);
        if (column is not null)
        {
            return new FilterCondition
            {
                Column = column,
                FieldKind = asHaving ? QueryFieldKind.Aggregate : QueryFieldKind.Column,
                Operator = normalizedOperator,
                Value = value,
                ValueKind = valueKind
            };
        }

        (string expression, string? alias) = SplitAlias(leftExpression);
        return new FilterCondition
        {
            FieldKind = asHaving ? QueryFieldKind.Aggregate : QueryFieldKind.CustomColumn,
            FieldAlias = alias ?? expression,
            Operator = normalizedOperator,
            Value = value,
            ValueKind = valueKind
        };
    }

    /// <summary>
    /// Parses GROUP BY items.
    /// </summary>
    /// <param name="groupText">GROUP BY clause content.</param>
    /// <param name="query">Query being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    private static void ParseGroupBy(string groupText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        foreach (string item in SplitTopLevelComma(groupText))
        {
            ColumnReference? column = ParseColumnReference(item.Trim(), aliases);
            if (column is not null)
            {
                query.GroupBy.Add(column);
            }
        }
    }

    /// <summary>
    /// Parses ORDER BY items.
    /// </summary>
    /// <param name="orderText">ORDER BY clause content.</param>
    /// <param name="query">Query being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    private static void ParseOrderBy(string orderText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        foreach (string item in SplitTopLevelComma(orderText))
        {
            Match match = Regex.Match(item.Trim(), @"(?is)^(.+?)(?:\s+(ASC|DESC))?$");
            if (!match.Success)
            {
                continue;
            }

            string expression = match.Groups[1].Value.Trim();
            SortDirection direction = match.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Descending
                : SortDirection.Ascending;
            string resolvedAlias = TrimIdentifierQuotes(expression);
            if (TryResolveOrderByAlias(query, resolvedAlias, out QueryFieldKind fieldKind))
            {
                query.OrderBy.Add(new OrderByItem { FieldAlias = resolvedAlias, FieldKind = fieldKind, Direction = direction });
                continue;
            }

            ColumnReference? column = ParseColumnReference(expression, aliases);
            if (column is not null)
            {
                query.OrderBy.Add(new OrderByItem { Column = column, Direction = direction });
            }
            else
            {
                query.OrderBy.Add(new OrderByItem { FieldAlias = TrimIdentifierQuotes(expression), FieldKind = QueryFieldKind.CustomColumn, Direction = direction });
            }
        }
    }

    /// <summary>
    /// Parses a table reference and records its alias.
    /// </summary>
    /// <param name="raw">Raw table reference.</param>
    /// <param name="aliases">Alias-to-table map to update.</param>
    /// <returns>Resolved table name.</returns>
    private static string ParseTableReference(string raw, Dictionary<string, string> aliases)
    {
        string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string table = TrimIdentifierQuotes(parts.FirstOrDefault() ?? string.Empty);
        if (parts.Length >= 2)
        {
            string alias = parts[^1].Equals("AS", StringComparison.OrdinalIgnoreCase) ? table : TrimIdentifierQuotes(parts[^1]);
            aliases[alias] = table;
        }

        aliases[table] = table;
        aliases[SqlObjectTail(table)] = table;
        return table;
    }

    /// <summary>
    /// Parses a column reference if it is simple enough to map to a table and column.
    /// </summary>
    /// <param name="raw">Raw expression.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <returns>Column reference, or <c>null</c> for non-column expressions.</returns>
    private static ColumnReference? ParseColumnReference(string raw, Dictionary<string, string> aliases)
    {
        string cleaned = TrimIdentifierQuotes(raw.Trim());
        if (cleaned.Contains('(') || cleaned.Contains(' ') || cleaned.Contains('+') || cleaned.Contains('-') || cleaned.Contains('/') || cleaned.Contains('*') && !cleaned.EndsWith(".*", StringComparison.Ordinal))
        {
            return null;
        }

        string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return aliases.Count == 1
                ? new ColumnReference { Table = aliases.Values.First(), Column = parts[0] }
                : null;
        }

        string column = parts[^1];
        string prefix = string.Join('.', parts[..^1]);
        string table = aliases.TryGetValue(prefix, out string? resolved) ? resolved : prefix;
        return new ColumnReference { Table = table, Column = column };
    }

    /// <summary>
    /// Splits a projection expression from its alias.
    /// </summary>
    /// <param name="raw">Raw select item.</param>
    /// <returns>Expression and optional alias.</returns>
    private static (string Expression, string? Alias) SplitAlias(string raw)
    {
        string trimmed = raw.Trim();
        Match asMatch = Regex.Match(trimmed, @"(?is)^(.+?)\s+AS\s+(.+)$");
        if (asMatch.Success)
        {
            return (asMatch.Groups[1].Value.Trim(), TrimIdentifierQuotes(asMatch.Groups[2].Value.Trim()));
        }

        List<string> parts = SplitWhitespaceTopLevel(trimmed).ToList();
        if (parts.Count >= 2)
        {
            string last = parts[^1];
            string expression = string.Join(' ', parts.Take(parts.Count - 1));
            if (!IsSqlKeyword(last) && !last.Contains(')'))
            {
                return (expression, TrimIdentifierQuotes(last));
            }
        }

        return (trimmed, null);
    }

    /// <summary>
    /// Converts an aggregate function token into the domain enum.
    /// </summary>
    /// <param name="functionName">Function token.</param>
    /// <returns>Aggregate function enum value.</returns>
    private static AggregateFunction ParseAggregateFunction(string functionName) => functionName.ToUpperInvariant() switch
    {
        "SUM" => AggregateFunction.Sum,
        "AVG" or "AVERAGE" => AggregateFunction.Average,
        "MIN" or "MINIMUM" => AggregateFunction.Minimum,
        "MAX" or "MAXIMUM" => AggregateFunction.Maximum,
        _ => AggregateFunction.Count
    };

    /// <summary>
    /// Detects how a parsed filter value should be emitted.
    /// </summary>
    /// <param name="valueText">Raw value text.</param>
    /// <returns>Detected value kind.</returns>
    private static FilterValueKind DetectValueKind(string? valueText)
    {
        if (string.IsNullOrWhiteSpace(valueText))
        {
            return FilterValueKind.Literal;
        }

        string value = valueText.Trim();
        if (value == "?" || value.StartsWith(':') || value.StartsWith('@'))
        {
            return FilterValueKind.Parameter;
        }

        if (value.StartsWith('&'))
        {
            return FilterValueKind.Parameter;
        }

        if (value.StartsWith('(') && value.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return FilterValueKind.RawSql;
        }

        return FilterValueKind.Literal;
    }

    /// <summary>
    /// Removes string quotes from a literal when reverse-loading filters.
    /// </summary>
    /// <param name="value">Raw SQL literal.</param>
    /// <returns>Unquoted literal text when possible.</returns>
    private static string? UnquoteValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            return trimmed[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return trimmed;
    }

    /// <summary>
    /// Extracts a clause between a known start keyword and the first following clause keyword.
    /// </summary>
    /// <param name="sql">Full SQL text.</param>
    /// <param name="startIndex">Start keyword index.</param>
    /// <param name="keywordLength">Keyword length.</param>
    /// <param name="candidateEnds">Possible following keyword indexes.</param>
    /// <returns>Clause text, or an empty string.</returns>
    private static string SliceClause(string sql, int startIndex, int keywordLength, params int[] candidateEnds)
    {
        if (startIndex < 0)
        {
            return string.Empty;
        }

        int end = FirstPositive(candidateEnds.Append(sql.Length).ToArray());
        return sql[(startIndex + keywordLength)..end].Trim();
    }

    /// <summary>
    /// Extracts a FETCH FIRST/NEXT clause body while handling absent clauses.
    /// </summary>
    /// <param name="sql">Full SQL text.</param>
    /// <param name="startIndex">Clause start index.</param>
    /// <param name="otherFetchIndex">Alternate FETCH index.</param>
    /// <returns>Clause content, or an empty string.</returns>
    private static string SliceClause(string sql, int startIndex, int otherFetchIndex)
    {
        if (string.IsNullOrEmpty(sql) || startIndex < 0)
        {
            return string.Empty;
        }

        int keywordLength = string.Compare(sql, startIndex, "FETCH NEXT", 0, "FETCH NEXT".Length, StringComparison.OrdinalIgnoreCase) == 0
            ? "FETCH NEXT".Length
            : "FETCH FIRST".Length;
        return SliceClause(sql, startIndex, keywordLength, otherFetchIndex);
    }

    /// <summary>
    /// Finds a keyword occurring outside strings and parentheses.
    /// </summary>
    /// <param name="sql">SQL text.</param>
    /// <param name="keyword">Keyword to find.</param>
    /// <param name="start">Start index.</param>
    /// <returns>Keyword index or -1.</returns>
    private static int FindTopLevelKeyword(string sql, string keyword, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int i = Math.Max(0, start); i <= sql.Length - keyword.Length; i++)
        {
            char current = sql[i];
            if (current == '\'')
            {
                inString = !inString;
            }
            else if (!inString && current == '(')
            {
                depth++;
            }
            else if (!inString && current == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (depth == 0 && !inString && string.Compare(sql, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                bool beforeOk = i == 0 || !char.IsLetterOrDigit(sql[i - 1]);
                bool afterOk = i + keyword.Length >= sql.Length || !char.IsLetterOrDigit(sql[i + keyword.Length]);
                if (beforeOk && afterOk)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the smallest non-negative candidate.
    /// </summary>
    /// <param name="candidates">Candidate indexes.</param>
    /// <returns>Smallest positive candidate.</returns>
    private static int FirstPositive(params int[] candidates) => candidates.Where(c => c >= 0).DefaultIfEmpty(-1).Min();

    /// <summary>
    /// Splits text on top-level commas.
    /// </summary>
    /// <param name="text">Text to split.</param>
    /// <returns>Segments.</returns>
    private static IEnumerable<string> SplitTopLevelComma(string text) => SplitTopLevel(text, ',');

    /// <summary>
    /// Splits text by a top-level keyword.
    /// </summary>
    /// <param name="text">Text to split.</param>
    /// <param name="keyword">Keyword separator.</param>
    /// <returns>Segments.</returns>
    private static IEnumerable<string> SplitTopLevelByKeyword(string text, string keyword)
    {
        int start = 0;
        while (true)
        {
            int index = FindTopLevelKeyword(text, keyword, start);
            if (index < 0)
            {
                yield return text[start..].Trim();
                yield break;
            }

            yield return text[start..index].Trim();
            start = index + keyword.Length;
        }
    }

    /// <summary>
    /// Splits text on a top-level character separator.
    /// </summary>
    /// <param name="text">Text to split.</param>
    /// <param name="separator">Separator character.</param>
    /// <returns>Segments.</returns>
    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        int depth = 0;
        bool inString = false;
        StringBuilder current = new();
        foreach (char c in text)
        {
            if (c == '\'')
            {
                inString = !inString;
            }
            else if (!inString && c == '(')
            {
                depth++;
            }
            else if (!inString && c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (!inString && depth == 0 && c == separator)
            {
                yield return current.ToString().Trim();
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        string last = current.ToString().Trim();
        if (last.Length > 0)
        {
            yield return last;
        }
    }

    /// <summary>
    /// Splits top-level whitespace tokens without breaking quoted strings or function calls.
    /// </summary>
    /// <param name="text">Text to split.</param>
    /// <returns>Tokens.</returns>
    private static IEnumerable<string> SplitWhitespaceTopLevel(string text)
    {
        int depth = 0;
        bool inString = false;
        StringBuilder current = new();
        foreach (char c in text)
        {
            if (c == '\'')
            {
                inString = !inString;
            }
            else if (!inString && c == '(')
            {
                depth++;
            }
            else if (!inString && c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (!inString && depth == 0 && char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    /// <summary>
    /// Removes common identifier delimiters.
    /// </summary>
    /// <param name="identifier">Identifier text.</param>
    /// <returns>Undelimited identifier.</returns>
    private static string TrimIdentifierQuotes(string identifier)
    {
        string value = identifier.Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '[' && value[^1] == ']') || (value[0] == '`' && value[^1] == '`')))
        {
            return value[1..^1];
        }

        return value;
    }

    /// <summary>
    /// Returns the last part of a possibly schema-qualified object name.
    /// </summary>
    /// <param name="name">Object name.</param>
    /// <returns>Last name segment.</returns>
    private static string SqlObjectTail(string name) => name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? name;

    /// <summary>
    /// Parses LIMIT or FETCH FIRST/NEXT into QueryDefinition.LimitRows.
    /// </summary>
    /// <param name="limitText">LIMIT clause content.</param>
    /// <param name="fetchText">FETCH clause content.</param>
    /// <returns>Parsed row limit when found.</returns>
    private static int? ParseLimit(string limitText, string fetchText)
    {
        Match limitMatch = Regex.Match(limitText, @"(?is)^(\d+)\b");
        if (limitMatch.Success)
        {
            return int.Parse(limitMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        Match fetchMatch = Regex.Match(fetchText, @"(?is)^(\d+)\s+ROWS?\s+ONLY$");
        if (fetchMatch.Success)
        {
            return int.Parse(fetchMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>
    /// Rejects SQL constructs that reverse parsing does not support deterministically.
    /// </summary>
    /// <param name="sql">Normalized SQL statement.</param>
    private static void ThrowIfUnsupported(string sql)
    {
        if (sql.Contains("(+)", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Reverse SQL impossible: les jointures Oracle legacy '(+)' ne sont pas prises en charge.");
        }

        if (FindTopLevelKeyword(sql, "UNION", 0) >= 0
            || FindTopLevelKeyword(sql, "INTERSECT", 0) >= 0
            || FindTopLevelKeyword(sql, "EXCEPT", 0) >= 0)
        {
            throw new InvalidOperationException("Reverse SQL impossible: UNION, INTERSECT et EXCEPT ne sont pas pris en charge.");
        }

        if (FindTopLevelKeyword(sql, "CONNECT BY", 0) >= 0
            || FindTopLevelKeyword(sql, "START WITH", 0) >= 0)
        {
            throw new InvalidOperationException("Reverse SQL impossible: CONNECT BY / START WITH n'est pas pris en charge.");
        }
    }

    /// <summary>
    /// Determines whether a filter left-hand side should reference a known alias instead of a table column.
    /// </summary>
    /// <param name="query">Current query model.</param>
    /// <param name="expression">Left expression to resolve.</param>
    /// <param name="asHaving">Whether the predicate belongs to HAVING.</param>
    /// <param name="fieldKind">Resolved field kind.</param>
    /// <returns><c>true</c> when a known alias was found.</returns>
    private static bool TryResolveFieldAlias(QueryDefinition query, string expression, bool asHaving, out QueryFieldKind fieldKind)
    {
        if (asHaving && query.Aggregates.Any(a => string.Equals(a.Alias, expression, StringComparison.OrdinalIgnoreCase)))
        {
            fieldKind = QueryFieldKind.Aggregate;
            return true;
        }

        if (query.CustomColumns.Any(c => string.Equals(c.Alias, expression, StringComparison.OrdinalIgnoreCase)))
        {
            fieldKind = QueryFieldKind.CustomColumn;
            return true;
        }

        fieldKind = QueryFieldKind.Column;
        return false;
    }

    /// <summary>
    /// Resolves ORDER BY aliases from known projection aliases before falling back to column parsing.
    /// </summary>
    /// <param name="query">Current query model.</param>
    /// <param name="expression">ORDER BY expression.</param>
    /// <param name="fieldKind">Resolved field kind.</param>
    /// <returns><c>true</c> when the ORDER BY item targets a known alias.</returns>
    private static bool TryResolveOrderByAlias(QueryDefinition query, string expression, out QueryFieldKind fieldKind)
    {
        if (query.Aggregates.Any(a => string.Equals(a.Alias, expression, StringComparison.OrdinalIgnoreCase)))
        {
            fieldKind = QueryFieldKind.Aggregate;
            return true;
        }

        if (query.CustomColumns.Any(c => string.Equals(c.Alias, expression, StringComparison.OrdinalIgnoreCase)))
        {
            fieldKind = QueryFieldKind.CustomColumn;
            return true;
        }

        if (query.SelectedColumns.Any(c => string.Equals(c.Alias, expression, StringComparison.OrdinalIgnoreCase)))
        {
            fieldKind = QueryFieldKind.Column;
            return true;
        }

        fieldKind = QueryFieldKind.Column;
        return false;
    }

    /// <summary>
    /// Detects a separator outside strings and parentheses.
    /// </summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="separator">Separator character.</param>
    /// <returns><c>true</c> when the separator is found at top level.</returns>
    private static bool ContainsTopLevelSeparator(string text, char separator)
    {
        int depth = 0;
        bool inString = false;
        foreach (char c in text)
        {
            if (c == '\'')
            {
                inString = !inString;
            }
            else if (!inString && c == '(')
            {
                depth++;
            }
            else if (!inString && c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (!inString && depth == 0 && c == separator)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a token is a SQL keyword that should not be treated as an alias.
    /// </summary>
    /// <param name="token">Token to test.</param>
    /// <returns><c>true</c> for SQL keywords.</returns>
    private static bool IsSqlKeyword(string token) => token.ToUpperInvariant() is "ASC" or "DESC" or "NULL" or "TRUE" or "FALSE";
}
