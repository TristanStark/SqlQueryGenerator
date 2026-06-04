using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Query;
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

        int fromEnd = FirstPositive(whereIndex, groupIndex, havingIndex, orderIndex, limitIndex, fetchIndex, normalized.Length);
        string selectText = normalized[(selectIndex + 6)..fromIndex].Trim();
        string fromText = normalized[(fromIndex + 4)..fromEnd].Trim();
        string whereText = SliceClause(normalized, whereIndex, 5, groupIndex, havingIndex, orderIndex, limitIndex, fetchIndex);
        string groupText = SliceClause(normalized, groupIndex, 8, havingIndex, orderIndex, limitIndex, fetchIndex);
        string havingText = SliceClause(normalized, havingIndex, 6, orderIndex, limitIndex, fetchIndex);
        string orderText = SliceClause(normalized, orderIndex, 8, limitIndex, fetchIndex);

        ParseFromAndJoins(fromText, query, aliases);
        whereText = ExtractLegacyOracleJoinPredicates(whereText, query, aliases);
        whereText = ExtractImplicitInnerJoinPredicates(whereText, query, aliases);
        ParseSelectItems(selectText, query, aliases);
        ParsePredicates(whereText, query, aliases, asHaving: false);
        ParseGroupBy(groupText, query, aliases);
        ParsePredicates(havingText, query, aliases, asHaving: true);
        ParseOrderBy(orderText, query, aliases);
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
        foreach (Match match in Regex.Matches(sql, @"(?<![\w])([:@][A-Za-z_][A-Za-z0-9_]*|\?|&&?[A-Za-z_][A-Za-z0-9_]*|&\d+)"))
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
        Match firstJoin = Regex.Match(fromText, @"\b(?:INNER\s+|LEFT\s+|LEFT\s+OUTER\s+)?JOIN\b", RegexOptions.IgnoreCase);
        string basePart = firstJoin.Success ? fromText[..firstJoin.Index].Trim() : fromText.Trim();
        string[] baseTables = SplitTopLevelComma(basePart).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        string baseTable = ParseTableReference(baseTables.FirstOrDefault() ?? basePart, aliases, query);
        query.BaseTable = baseTable;

        foreach (string extraTable in baseTables.Skip(1))
        {
            ParseTableReference(extraTable, aliases, query);
        }

        if (!firstJoin.Success)
        {
            return;
        }

        string joinsText = fromText[firstJoin.Index..];
        Regex joinRegex = new(@"(?is)\b(?:(INNER|LEFT(?:\s+OUTER)?)\s+)?JOIN\s+(.+?)\s+ON\s+(.+?)(?=\b(?:INNER\s+|LEFT\s+|LEFT\s+OUTER\s+)?JOIN\b|$)");
        foreach (Match joinMatch in joinRegex.Matches(joinsText))
        {
            JoinType joinType = joinMatch.Groups[1].Value.StartsWith("LEFT", StringComparison.OrdinalIgnoreCase) ? JoinType.Left : JoinType.Inner;
            string toTable = ParseTableReference(joinMatch.Groups[2].Value.Trim(), aliases, query);
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
        if (selectText.StartsWith("DISTINCT ", StringComparison.OrdinalIgnoreCase))
        {
            query.Distinct = true;
            selectText = selectText[8..].Trim();
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
            FilterCondition? condition = ParsePredicate(predicate.Trim(), aliases, asHaving);
            if (condition is not null)
            {
                query.Filters.Add(condition);
            }
        }
    }

    /// <summary>
    /// Converts Oracle legacy outer-join predicates using <c>(+)</c> into explicit join definitions.
    /// Converted predicates are removed from the returned WHERE text.
    /// </summary>
    /// <param name="whereText">Raw WHERE clause text.</param>
    /// <param name="query">Query model being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <returns>WHERE text without legacy join predicates.</returns>
    private static string ExtractLegacyOracleJoinPredicates(string whereText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(whereText) || !whereText.Contains("(+)", StringComparison.Ordinal))
        {
            return whereText;
        }

        List<string> keptPredicates = [];
        foreach (string predicate in SplitTopLevelByKeyword(whereText, "AND"))
        {
            string trimmed = predicate.Trim();
            if (!TryParseLegacyOracleOuterJoinPredicate(trimmed, aliases, out JoinDefinition? join))
            {
                keptPredicates.Add(trimmed);
                continue;
            }

            AddOrMergeJoin(query, join!);
            if (string.Equals(query.BaseTable, join!.ToTable, StringComparison.OrdinalIgnoreCase))
            {
                query.BaseTable = join.FromTable;
            }
        }

        return string.Join(" AND ", keptPredicates.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Converts implicit inner join predicates written in WHERE into explicit join definitions.
    /// Converted predicates are removed from the returned WHERE text.
    /// </summary>
    /// <param name="whereText">Raw WHERE clause text.</param>
    /// <param name="query">Query model being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <returns>WHERE text without extracted join predicates.</returns>
    private static string ExtractImplicitInnerJoinPredicates(string whereText, QueryDefinition query, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(whereText))
        {
            return whereText;
        }

        List<string> keptPredicates = [];
        foreach (string predicate in SplitTopLevelByKeyword(whereText, "AND"))
        {
            string trimmed = predicate.Trim();
            if (!TryParseImplicitInnerJoinPredicate(trimmed, query, aliases, out JoinDefinition? join))
            {
                keptPredicates.Add(trimmed);
                continue;
            }

            AddOrMergeJoin(query, join!);
        }

        return string.Join(" AND ", keptPredicates.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Parses one Oracle legacy outer-join equality predicate, for example <c>A.ID = B.A_ID(+)</c>.
    /// </summary>
    /// <param name="predicate">Predicate text.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="join">Parsed join when successful.</param>
    /// <returns><c>true</c> when the predicate is a supported legacy outer join.</returns>
    private static bool TryParseLegacyOracleOuterJoinPredicate(string predicate, Dictionary<string, string> aliases, out JoinDefinition? join)
    {
        join = null;
        Match eq = Regex.Match(predicate, @"(?is)^(.+?)\s*=\s*(.+?)$");
        if (!eq.Success)
        {
            return false;
        }

        if (!TryParseLegacyJoinSide(eq.Groups[1].Value, aliases, out ColumnReference? leftColumn, out bool leftOptional)
            || !TryParseLegacyJoinSide(eq.Groups[2].Value, aliases, out ColumnReference? rightColumn, out bool rightOptional))
        {
            return false;
        }

        if (leftOptional == rightOptional)
        {
            return false;
        }

        ColumnReference mandatory = leftOptional ? rightColumn! : leftColumn!;
        ColumnReference optional = leftOptional ? leftColumn! : rightColumn!;
        join = new JoinDefinition
        {
            FromTable = mandatory.Table,
            FromColumn = mandatory.Column,
            ToTable = optional.Table,
            ToColumn = optional.Column,
            JoinType = JoinType.Left
        };
        return true;
    }

    /// <summary>
    /// Parses one side of a legacy outer-join predicate and detects whether it carries the <c>(+)</c> marker.
    /// </summary>
    /// <param name="raw">Raw side expression.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="column">Parsed column reference.</param>
    /// <param name="optionalSide">Whether this side has the legacy optional marker.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    private static bool TryParseLegacyJoinSide(string raw, Dictionary<string, string> aliases, out ColumnReference? column, out bool optionalSide)
    {
        string trimmed = raw.Trim();
        optionalSide = Regex.IsMatch(trimmed, @"\(\+\)\s*$");
        string cleaned = Regex.Replace(trimmed, @"\(\+\)\s*$", string.Empty).Trim();
        column = ParseColumnReference(cleaned, aliases);
        return column is not null;
    }

    /// <summary>
    /// Parses one implicit inner join predicate such as <c>C.ID = O.CLIENT_ID</c>.
    /// </summary>
    /// <param name="predicate">Predicate text.</param>
    /// <param name="query">Query model being populated.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="join">Parsed join when successful.</param>
    /// <returns><c>true</c> when the predicate is safely interpreted as a join.</returns>
    private static bool TryParseImplicitInnerJoinPredicate(string predicate, QueryDefinition query, Dictionary<string, string> aliases, out JoinDefinition? join)
    {
        join = null;
        if (predicate.Contains("(+)", StringComparison.Ordinal))
        {
            return false;
        }

        Match eq = Regex.Match(predicate, @"(?is)^(.+?)\s*=\s*(.+?)$");
        if (!eq.Success)
        {
            return false;
        }

        ColumnReference? left = ParseColumnReference(eq.Groups[1].Value.Trim(), aliases);
        ColumnReference? right = ParseColumnReference(eq.Groups[2].Value.Trim(), aliases);
        if (left is null || right is null || string.Equals(left.Table, right.Table, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        (ColumnReference From, ColumnReference To) oriented = OrientImplicitJoin(query, left, right);
        join = new JoinDefinition
        {
            FromTable = oriented.From.Table,
            FromColumn = oriented.From.Column,
            ToTable = oriented.To.Table,
            ToColumn = oriented.To.Column,
            JoinType = JoinType.Inner
        };
        return true;
    }

    /// <summary>
    /// Chooses a deterministic join orientation so the generated SQL starts from the current base table when possible.
    /// </summary>
    /// <param name="query">Query model being populated.</param>
    /// <param name="left">Left column from the predicate.</param>
    /// <param name="right">Right column from the predicate.</param>
    /// <returns>Oriented join endpoints.</returns>
    private static (ColumnReference From, ColumnReference To) OrientImplicitJoin(QueryDefinition query, ColumnReference left, ColumnReference right)
    {
        if (string.Equals(query.BaseTable, left.Table, StringComparison.OrdinalIgnoreCase))
        {
            return (left, right);
        }

        if (string.Equals(query.BaseTable, right.Table, StringComparison.OrdinalIgnoreCase))
        {
            return (right, left);
        }

        bool leftAlreadyConnected = query.Joins.Any(j =>
            string.Equals(j.FromTable, left.Table, StringComparison.OrdinalIgnoreCase)
            || string.Equals(j.ToTable, left.Table, StringComparison.OrdinalIgnoreCase));
        bool rightAlreadyConnected = query.Joins.Any(j =>
            string.Equals(j.FromTable, right.Table, StringComparison.OrdinalIgnoreCase)
            || string.Equals(j.ToTable, right.Table, StringComparison.OrdinalIgnoreCase));

        if (leftAlreadyConnected && !rightAlreadyConnected)
        {
            return (left, right);
        }

        if (rightAlreadyConnected && !leftAlreadyConnected)
        {
            return (right, left);
        }

        return (left, right);
    }

    /// <summary>
    /// Adds a join definition or merges it as an additional composite pair when the same table pair already exists.
    /// </summary>
    /// <param name="query">Query model being populated.</param>
    /// <param name="join">Join to add or merge.</param>
    private static void AddOrMergeJoin(QueryDefinition query, JoinDefinition join)
    {
        JoinDefinition? existing = query.Joins.FirstOrDefault(j =>
            j.JoinType == join.JoinType
            && string.Equals(j.FromTable, join.FromTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(j.ToTable, join.ToTable, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            query.Joins.Add(join);
            return;
        }

        bool samePrimaryPair = string.Equals(existing.FromColumn, join.FromColumn, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.ToColumn, join.ToColumn, StringComparison.OrdinalIgnoreCase);
        bool alreadyInAdditional = existing.AdditionalColumnPairs.Any(p =>
            string.Equals(p.FromColumn, join.FromColumn, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.ToColumn, join.ToColumn, StringComparison.OrdinalIgnoreCase));
        if (samePrimaryPair || alreadyInAdditional)
        {
            return;
        }

        existing.AdditionalColumnPairs.Add(new JoinColumnPair
        {
            FromColumn = join.FromColumn,
            ToColumn = join.ToColumn,
            Enabled = true
        });
    }

    /// <summary>
    /// Parses a single predicate.
    /// </summary>
    /// <param name="predicate">Predicate text.</param>
    /// <param name="aliases">Alias-to-table map.</param>
    /// <param name="asHaving">Whether the predicate belongs to HAVING.</param>
    /// <returns>A filter condition, or <c>null</c> when the predicate cannot be understood.</returns>
    private static FilterCondition? ParsePredicate(string predicate, Dictionary<string, string> aliases, bool asHaving)
    {
        Match isNull = Regex.Match(predicate, @"(?is)^(.+?)\s+(IS\s+NOT\s+NULL|IS\s+NULL)$");
        if (isNull.Success)
        {
            return BuildFilter(isNull.Groups[1].Value, isNull.Groups[2].Value, null, aliases, asHaving);
        }

        Match between = Regex.Match(predicate, @"(?is)^(.+?)\s+BETWEEN\s+(.+?)\s+AND\s+(.+)$");
        if (between.Success)
        {
            FilterCondition? filter = BuildFilter(between.Groups[1].Value, "BETWEEN", between.Groups[2].Value, aliases, asHaving);
            return filter is null ? null : filter with { SecondValue = UnquoteValue(between.Groups[3].Value.Trim()) };
        }

        Match inSubquery = Regex.Match(predicate, @"(?is)^(.+?)\s+(IN|NOT\s+IN)\s*\((\s*(?:SELECT|WITH)\b.+)\)$");
        if (inSubquery.Success)
        {
            FilterCondition? filter = BuildFilter(inSubquery.Groups[1].Value, inSubquery.Groups[2].Value, null, aliases, asHaving);
            return filter is null ? null : filter with
            {
                ValueKind = FilterValueKind.Subquery,
                RawSubquerySql = SqlSafety.NormalizeRawSelectQuery(inSubquery.Groups[3].Value.Trim()),
                SubqueryName = "sous_requete_importee"
            };
        }

        Match binary = Regex.Match(predicate, @"(?is)^(.+?)\s*(=|<>|!=|>=|<=|>|<|LIKE|NOT\s+LIKE|IN|NOT\s+IN)\s*(.+)$");
        if (binary.Success)
        {
            return BuildFilter(binary.Groups[1].Value, binary.Groups[2].Value, binary.Groups[3].Value, aliases, asHaving);
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
    private static FilterCondition? BuildFilter(string leftExpression, string operatorText, string? valueText, Dictionary<string, string> aliases, bool asHaving)
    {
        string normalizedOperator = Regex.Replace(operatorText.Trim(), @"\s+", " ").ToUpperInvariant();
        ColumnReference? column = ParseColumnReference(leftExpression.Trim(), aliases);
        FilterValueKind valueKind = DetectValueKind(valueText);
        string? value = valueKind == FilterValueKind.Literal ? UnquoteValue(valueText?.Trim()) : valueText?.Trim();
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
    private static string ParseTableReference(string raw, Dictionary<string, string> aliases, QueryDefinition? query = null)
    {
        string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string table = TrimIdentifierQuotes(parts.FirstOrDefault() ?? string.Empty);
        if (parts.Length >= 2)
        {
            string alias = parts[^1].Equals("AS", StringComparison.OrdinalIgnoreCase) ? table : TrimIdentifierQuotes(parts[^1]);
            aliases[alias] = table;
            if (!string.Equals(alias, table, StringComparison.OrdinalIgnoreCase))
            {
                RegisterTableAlias(query, table, alias);
            }
        }

        aliases[table] = table;
        aliases[SqlObjectTail(table)] = table;
        return table;
    }

    /// <summary>
    /// Stores one table alias on the query if it is not already registered.
    /// </summary>
    /// <param name="query">Query being populated.</param>
    /// <param name="table">Real table name.</param>
    /// <param name="alias">SQL alias.</param>
    private static void RegisterTableAlias(QueryDefinition? query, string table, string alias)
    {
        if (query is null || string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        if (query.TableAliases.Any(a => string.Equals(a.Table, table, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        query.TableAliases.Add(new TableAliasDefinition
        {
            Table = table,
            Alias = alias
        });
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
        if (value == "?" || value.StartsWith(':') || value.StartsWith('@') || value.StartsWith('&'))
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
        int searchStart = 0;
        bool skippedBetweenSeparator = false;
        while (true)
        {
            int index = FindTopLevelKeyword(text, keyword, searchStart);
            if (index < 0)
            {
                yield return text[start..].Trim();
                yield break;
            }

            if (keyword.Equals("AND", StringComparison.OrdinalIgnoreCase)
                && !skippedBetweenSeparator
                && SegmentContainsTopLevelKeyword(text, start, index, "BETWEEN"))
            {
                skippedBetweenSeparator = true;
                searchStart = index + keyword.Length;
                continue;
            }

            yield return text[start..index].Trim();
            start = index + keyword.Length;
            searchStart = start;
            skippedBetweenSeparator = false;
        }
    }

    /// <summary>
    /// Checks whether a segment contains a top-level keyword before a candidate split point.
    /// </summary>
    /// <param name="text">Text being scanned.</param>
    /// <param name="start">Segment start index.</param>
    /// <param name="end">Exclusive end index.</param>
    /// <param name="keyword">Keyword to locate.</param>
    /// <returns><c>true</c> when the keyword appears outside strings and parentheses.</returns>
    private static bool SegmentContainsTopLevelKeyword(string text, int start, int end, string keyword)
    {
        int index = FindTopLevelKeyword(text, keyword, start);
        return index >= start && index < end;
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
    /// Determines whether a token is a SQL keyword that should not be treated as an alias.
    /// </summary>
    /// <param name="token">Token to test.</param>
    /// <returns><c>true</c> for SQL keywords.</returns>
    private static bool IsSqlKeyword(string token) => token.ToUpperInvariant() is "ASC" or "DESC" or "NULL" or "TRUE" or "FALSE";
}
