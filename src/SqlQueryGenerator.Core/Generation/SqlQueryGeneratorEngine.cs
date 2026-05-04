using System.Text;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

public sealed class SqlQueryGeneratorEngine
{
    public SqlGenerationResult Generate(QueryDefinition query, DatabaseSchema schema, SqlGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);
        options ??= new SqlGeneratorOptions();

        var warnings = new List<string>();
        var baseTable = ResolveBaseTable(query, warnings);
        if (baseTable is null)
        {
            return new SqlGenerationResult { Sql = "-- Sélectionne au moins une colonne ou une table de départ.", Warnings = warnings };
        }

        var usedTables = CollectUsedTables(query);
        usedTables.Add(baseTable);
        var joins = BuildJoinPlan(query, schema, baseTable, usedTables, warnings);

        var selectItems = BuildSelectItems(query, options, warnings);
        if (selectItems.Count == 0)
        {
            selectItems.Add("*");
            warnings.Add("Aucune colonne sélectionnée: SELECT * généré par défaut.");
        }

        var sb = new StringBuilder();
        if (options.EmitOptimizationComments)
        {
            sb.AppendLine("/* Requête générée sans sous-requête, avec jointures explicites lorsque possible. */");
        }

        sb.Append("SELECT ");
        if (query.Distinct)
        {
            sb.Append("DISTINCT ");
        }
        sb.AppendLine(string.Join(",\n       ", selectItems));
        sb.Append("FROM ").AppendLine(Q(baseTable, options));

        foreach (var join in joins)
        {
            sb.Append(join.JoinType == JoinType.Left ? "LEFT JOIN " : "INNER JOIN ");
            sb.Append(Q(join.ToTable, options));
            sb.Append(" ON ");
            sb.Append(Q(join.FromTable, options)).Append('.').Append(Q(join.FromColumn, options));
            sb.Append(" = ");
            sb.Append(Q(join.ToTable, options)).Append('.').Append(Q(join.ToColumn, options));
            if (join.AutoInferred && options.EmitOptimizationComments)
            {
                sb.Append(" /* jointure inférée */");
            }
            sb.AppendLine();
        }

        var where = BuildWhere(query, options, warnings);
        if (where.Count > 0)
        {
            sb.Append("WHERE ").AppendLine(where[0]);
            for (var i = 1; i < where.Count; i++)
            {
                var connector = query.Filters.Count > i ? query.Filters[i].Connector : LogicalConnector.And;
                sb.Append(connector == LogicalConnector.Or ? "   OR " : "  AND ").AppendLine(where[i]);
            }
        }

        var groupBy = BuildGroupBy(query, options);
        if (query.Aggregates.Count > 0 && options.AutoGroupSelectedColumnsWhenAggregating)
        {
            foreach (var selected in query.SelectedColumns)
            {
                var expr = ColumnSql(selected, options, includeAlias: false);
                if (!groupBy.Contains(expr, StringComparer.OrdinalIgnoreCase))
                {
                    groupBy.Add(expr);
                }
            }
        }

        if (groupBy.Count > 0)
        {
            sb.Append("GROUP BY ").AppendLine(string.Join(", ", groupBy));
        }

        if (query.OrderBy.Count > 0)
        {
            var orderItems = query.OrderBy.Select(o => $"{ColumnSql(o.Column, options, includeAlias: false)} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            sb.Append("ORDER BY ").AppendLine(string.Join(", ", orderItems));
        }

        if (query.LimitRows is > 0)
        {
            if (options.Dialect == SqlDialect.Oracle)
            {
                sb.AppendLine($"FETCH FIRST {query.LimitRows.Value} ROWS ONLY");
            }
            else
            {
                sb.AppendLine($"LIMIT {query.LimitRows.Value}");
            }
        }

        return new SqlGenerationResult
        {
            Sql = sb.ToString().TrimEnd() + Environment.NewLine,
            Warnings = warnings
        };
    }

    private static string? ResolveBaseTable(QueryDefinition query, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(query.BaseTable))
        {
            return query.BaseTable.Trim();
        }

        var first = query.SelectedColumns.FirstOrDefault()?.Table
            ?? query.Filters.FirstOrDefault()?.Column.Table
            ?? query.GroupBy.FirstOrDefault()?.Table
            ?? query.OrderBy.FirstOrDefault()?.Column.Table
            ?? query.Aggregates.FirstOrDefault(a => a.Column is not null)?.Column?.Table
            ?? query.Aggregates.FirstOrDefault(a => a.ConditionColumn is not null)?.ConditionColumn?.Table
            ?? query.CustomColumns.FirstOrDefault(c => c.CaseColumn is not null)?.CaseColumn?.Table;

        if (!string.IsNullOrWhiteSpace(first))
        {
            warnings.Add($"Table de départ non renseignée: {first} utilisée automatiquement.");
        }

        return first;
    }

    private static HashSet<string> CollectUsedTables(QueryDefinition query)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in query.SelectedColumns) result.Add(c.Table);
        foreach (var f in query.Filters) result.Add(f.Column.Table);
        foreach (var g in query.GroupBy) result.Add(g.Table);
        foreach (var o in query.OrderBy) result.Add(o.Column.Table);
        foreach (var a in query.Aggregates.Where(a => a.Column is not null)) result.Add(a.Column!.Table);
        foreach (var a in query.Aggregates.Where(a => a.ConditionColumn is not null)) result.Add(a.ConditionColumn!.Table);
        foreach (var c in query.CustomColumns.Where(c => c.CaseColumn is not null)) result.Add(c.CaseColumn!.Table);
        return result;
    }

    private static IReadOnlyList<JoinDefinition> BuildJoinPlan(QueryDefinition query, DatabaseSchema schema, string baseTable, HashSet<string> usedTables, List<string> warnings)
    {
        var joins = new List<JoinDefinition>();
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseTable };

        foreach (var explicitJoin in query.Joins)
        {
            joins.Add(explicitJoin with { AutoInferred = false });
            connected.Add(explicitJoin.FromTable);
            connected.Add(explicitJoin.ToTable);
        }

        var remaining = usedTables.Where(t => !connected.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safety = 0;
        while (remaining.Count > 0 && safety++ < 256)
        {
            var candidates = schema.Relationships
                .Where(r => (connected.Contains(r.FromTable) && remaining.Contains(r.ToTable)) || (connected.Contains(r.ToTable) && remaining.Contains(r.FromTable)))
                .OrderByDescending(r => r.Confidence)
                .ToArray();

            if (candidates.Length == 0)
            {
                foreach (var table in remaining.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"Aucune jointure fiable trouvée depuis {baseTable} vers {table}. La table n'a pas été jointe automatiquement.");
                }
                break;
            }

            var best = candidates[0];
            if (connected.Contains(best.FromTable) && remaining.Contains(best.ToTable))
            {
                joins.Add(new JoinDefinition
                {
                    FromTable = best.FromTable,
                    FromColumn = best.FromColumn,
                    ToTable = best.ToTable,
                    ToColumn = best.ToColumn,
                    JoinType = JoinType.Inner,
                    AutoInferred = true
                });
                connected.Add(best.ToTable);
                remaining.Remove(best.ToTable);
            }
            else
            {
                joins.Add(new JoinDefinition
                {
                    FromTable = best.ToTable,
                    FromColumn = best.ToColumn,
                    ToTable = best.FromTable,
                    ToColumn = best.FromColumn,
                    JoinType = JoinType.Inner,
                    AutoInferred = true
                });
                connected.Add(best.FromTable);
                remaining.Remove(best.FromTable);
            }
        }

        return joins;
    }

    private static List<string> BuildSelectItems(QueryDefinition query, SqlGeneratorOptions options, List<string> warnings)
    {
        var result = new List<string>();
        result.AddRange(query.SelectedColumns.Select(c => ColumnSql(c, options, includeAlias: true)));

        foreach (var aggregate in query.Aggregates)
        {
            var item = BuildAggregateExpression(aggregate, options, warnings);
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(aggregate.Alias))
            {
                item += " AS " + Q(aggregate.Alias, options);
            }
            result.Add(item);
        }

        foreach (var custom in query.CustomColumns)
        {
            var expression = BuildCustomExpression(custom, options);
            if (string.IsNullOrWhiteSpace(expression))
            {
                warnings.Add("Colonne personnalisée ignorée: expression vide.");
                continue;
            }
            SqlSafety.EnsureSelectExpressionIsSafe(expression);
            if (!string.IsNullOrWhiteSpace(custom.Alias))
            {
                expression += " AS " + Q(custom.Alias, options);
            }
            result.Add(expression);
        }

        return result;
    }

    private static string BuildAggregateExpression(AggregateSelection aggregate, SqlGeneratorOptions options, List<string> warnings)
    {
        var fn = aggregate.Function switch
        {
            AggregateFunction.Count => "COUNT",
            AggregateFunction.Sum => "SUM",
            AggregateFunction.Average => "AVG",
            AggregateFunction.Minimum => "MIN",
            AggregateFunction.Maximum => "MAX",
            _ => "COUNT"
        };

        var target = aggregate.Column is null ? "*" : ColumnSql(aggregate.Column, options, includeAlias: false);
        var condition = BuildAggregateCondition(aggregate, options, warnings);
        if (string.IsNullOrWhiteSpace(condition))
        {
            if (aggregate.Distinct && aggregate.Column is not null)
            {
                target = "DISTINCT " + target;
            }

            return $"{fn}({target})";
        }

        if (aggregate.Function == AggregateFunction.Count)
        {
            if (aggregate.Distinct && aggregate.Column is not null)
            {
                return $"COUNT(DISTINCT CASE WHEN {condition} THEN {target} END)";
            }

            var countTarget = aggregate.Column is null ? "1" : target;
            return $"COUNT(CASE WHEN {condition} THEN {countTarget} END)";
        }

        if (aggregate.Column is null)
        {
            warnings.Add($"Agrégat conditionnel {fn} ignoré: choisis une colonne cible. Utilise COUNT pour compter des lignes.");
            return string.Empty;
        }

        var caseExpression = aggregate.Function == AggregateFunction.Sum
            ? $"CASE WHEN {condition} THEN {target} ELSE 0 END"
            : $"CASE WHEN {condition} THEN {target} END";

        if (aggregate.Distinct)
        {
            return $"{fn}(DISTINCT {caseExpression})";
        }

        return $"{fn}({caseExpression})";
    }

    private static string BuildAggregateCondition(AggregateSelection aggregate, SqlGeneratorOptions options, List<string> warnings)
    {
        if (aggregate.ConditionColumn is null)
        {
            if (!string.IsNullOrWhiteSpace(aggregate.ConditionValue))
            {
                warnings.Add("Condition d'agrégat ignorée: aucune colonne de condition n'a été renseignée.");
            }
            return string.Empty;
        }

        var op = NormalizeOperator(aggregate.ConditionOperator);
        var column = ColumnSql(aggregate.ConditionColumn, options, includeAlias: false);
        return BuildPredicateSql(column, op, aggregate.ConditionValue, aggregate.ConditionSecondValue, aggregate.ConditionColumn.Key, warnings);
    }

    private static string BuildCustomExpression(CustomColumnSelection custom, SqlGeneratorOptions options)
    {
        if (!string.IsNullOrWhiteSpace(custom.RawExpression))
        {
            return custom.RawExpression.Trim();
        }

        if (custom.CaseColumn is not null && !string.IsNullOrWhiteSpace(custom.CaseOperator))
        {
            var op = NormalizeOperator(custom.CaseOperator);
            var column = ColumnSql(custom.CaseColumn, options, includeAlias: false);
            var compare = op is "IS NULL" or "IS NOT NULL" ? string.Empty : " " + SqlLiteralFormatter.FormatValue(custom.CaseCompareValue);
            return $"CASE WHEN {column} {op}{compare} THEN {SqlLiteralFormatter.FormatValue(custom.CaseThenValue)} ELSE {SqlLiteralFormatter.FormatValue(custom.CaseElseValue)} END";
        }

        return string.Empty;
    }

    private static List<string> BuildWhere(QueryDefinition query, SqlGeneratorOptions options, List<string> warnings)
    {
        var result = new List<string>();
        foreach (var filter in query.Filters)
        {
            var op = NormalizeOperator(filter.Operator);
            var column = ColumnSql(filter.Column, options, includeAlias: false);
            var predicate = BuildPredicateSql(column, op, filter.Value, filter.SecondValue, filter.Column.Key, warnings);
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                result.Add(predicate);
            }
        }
        return result;
    }

    private static string BuildPredicateSql(string columnSql, string op, string? value, string? secondValue, string warningKey, List<string> warnings)
    {
        if (op is "IS NULL" or "IS NOT NULL")
        {
            return $"{columnSql} {op}";
        }

        if (op == "BETWEEN")
        {
            return $"{columnSql} BETWEEN {SqlLiteralFormatter.FormatValue(value)} AND {SqlLiteralFormatter.FormatValue(secondValue)}";
        }

        if (op is "IN" or "NOT IN")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                warnings.Add($"Filtre IN ignoré sur {warningKey}: valeur vide.");
                return string.Empty;
            }
            return $"{columnSql} {op} {SqlLiteralFormatter.FormatRawList(value)}";
        }

        return $"{columnSql} {op} {SqlLiteralFormatter.FormatValue(value)}";
    }

    private static List<string> BuildGroupBy(QueryDefinition query, SqlGeneratorOptions options)
    {
        return query.GroupBy.Select(c => ColumnSql(c, options, includeAlias: false)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeOperator(string? op)
    {
        var value = string.IsNullOrWhiteSpace(op) ? "=" : op.Trim().ToUpperInvariant();
        return value switch
        {
            "==" => "=",
            "!=" => "<>",
            "ISNULL" => "IS NULL",
            "ISNOTNULL" => "IS NOT NULL",
            _ => value
        };
    }

    private static string ColumnSql(ColumnReference reference, SqlGeneratorOptions options, bool includeAlias)
    {
        var sql = $"{Q(reference.Table, options)}.{Q(reference.Column, options)}";
        if (includeAlias && !string.IsNullOrWhiteSpace(reference.Alias))
        {
            sql += " AS " + Q(reference.Alias, options);
        }
        return sql;
    }

    private static string Q(string identifier, SqlGeneratorOptions options)
    {
        return SqlIdentifierQuoter.Quote(identifier, options.Dialect, options.QuoteIdentifiers);
    }
}
