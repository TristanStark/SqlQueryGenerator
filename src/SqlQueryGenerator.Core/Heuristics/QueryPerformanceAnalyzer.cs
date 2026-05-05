using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Heuristics;

public sealed class QueryPerformanceAnalyzer
{
    public QueryPerformanceReport Analyze(QueryDefinition query, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        QueryPerformanceReport report = new QueryPerformanceReport();
        HashSet<string> usedTables = CollectUsedTables(query);
        if (!string.IsNullOrWhiteSpace(query.BaseTable))
        {
            usedTables.Add(query.BaseTable!);
        }

        foreach (string? tableName in usedTables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            TableDefinition? table = schema.FindTable(tableName);
            if (table?.IsView == true)
            {
                report.Add(QueryPerformanceSeverity.Warning, $"{table.FullName} est une vue : son coût réel dépend de la requête interne de la vue. Vérifie le plan d'exécution réel si la vue masque des jointures ou agrégats.");
            }
        }

        foreach (FilterCondition? filter in query.Filters.Where(f => f.Column is not null))
        {
            ColumnReference col = filter.Column!;
            if (schema.FindColumn(col.Table, col.Column) is null)
            {
                continue;
            }

            if (schema.IsColumnIndexed(col.Table, col.Column))
            {
                report.Add(QueryPerformanceSeverity.Good, $"Filtre sur {col.Table}.{col.Column}: colonne indexée, bon signal pour WHERE/HAVING.");
            }
            else
            {
                report.Add(QueryPerformanceSeverity.Warning, $"Filtre sur {col.Table}.{col.Column}: aucun index détecté. Sur une grosse table, risque de scan.");
            }
        }

        foreach (JoinDefinition join in query.Joins)
        {
            AnalyzeJoinSide(report, schema, join.FromTable, join.FromColumn, "gauche");
            AnalyzeJoinSide(report, schema, join.ToTable, join.ToColumn, "droite");
        }

        foreach (ColumnReference group in query.GroupBy)
        {
            if (schema.IsColumnIndexed(group.Table, group.Column))
            {
                report.Add(QueryPerformanceSeverity.Info, $"GROUP BY {group.Table}.{group.Column}: colonne indexée détectée, peut aider selon le moteur et le plan.");
            }
            else
            {
                report.Add(QueryPerformanceSeverity.Info, $"GROUP BY {group.Table}.{group.Column}: aucun index détecté ; tri/hash aggregate probable sur gros volume.");
            }
        }

        foreach (OrderByItem? order in query.OrderBy.Where(o => o.Column is not null))
        {
            ColumnReference col = order.Column!;
            if (schema.IsColumnIndexed(col.Table, col.Column))
            {
                report.Add(QueryPerformanceSeverity.Info, $"ORDER BY {col.Table}.{col.Column}: colonne indexée détectée, peut éviter un tri selon le plan.");
            }
            else
            {
                report.Add(QueryPerformanceSeverity.Info, $"ORDER BY {col.Table}.{col.Column}: aucun index détecté ; tri explicite probable.");
            }
        }

        foreach (FilterCondition? sub in query.Filters.Where(f => f.ValueKind == FilterValueKind.Subquery && f.Subquery is not null))
        {
            report.Add(QueryPerformanceSeverity.Info, $"Sous-requête utilisée dans un filtre: vérifie que la sous-requête retourne peu de lignes ou qu'elle filtre sur des colonnes indexées.");
            QueryPerformanceReport subReport = Analyze(sub.Subquery!, schema);
            foreach (QueryPerformanceHint? hint in subReport.Hints.Take(6))
            {
                report.Add(hint.Severity, "Sous-requête: " + hint.Message);
            }
        }

        if (query.LimitRows is > 0 && query.OrderBy.Count == 0)
        {
            report.Add(QueryPerformanceSeverity.Info, "LIMIT/FETCH sans ORDER BY: rapide, mais résultat non déterministe.");
        }

        if (report.Hints.Count == 0)
        {
            report.Add(QueryPerformanceSeverity.Good, "Aucun risque évident détecté à partir des métadonnées disponibles.");
        }

        return report;
    }

    private static void AnalyzeJoinSide(QueryPerformanceReport report, DatabaseSchema schema, string table, string column, string side)
    {
        ColumnDefinition? col = schema.FindColumn(table, column);
        if (col is null)
        {
            return;
        }

        if (col.IsPrimaryKey || schema.IsColumnUniqueIndexed(table, column))
        {
            report.Add(QueryPerformanceSeverity.Good, $"Jointure côté {side} {table}.{column}: PK/unique détecté.");
        }
        else if (schema.IsColumnIndexed(table, column))
        {
            report.Add(QueryPerformanceSeverity.Info, $"Jointure côté {side} {table}.{column}: index détecté.");
        }
        else
        {
            report.Add(QueryPerformanceSeverity.Warning, $"Jointure côté {side} {table}.{column}: aucun index détecté.");
        }
    }

    private static HashSet<string> CollectUsedTables(QueryDefinition query)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ColumnReference c in query.SelectedColumns) result.Add(c.Table);
        foreach (FilterCondition? f in query.Filters.Where(f => f.Column is not null)) result.Add(f.Column!.Table);
        foreach (ColumnReference g in query.GroupBy) result.Add(g.Table);
        foreach (OrderByItem? o in query.OrderBy.Where(o => o.Column is not null)) result.Add(o.Column!.Table);
        foreach (AggregateSelection? a in query.Aggregates.Where(a => a.Column is not null)) result.Add(a.Column!.Table);
        foreach (AggregateSelection? a in query.Aggregates.Where(a => a.ConditionColumn is not null)) result.Add(a.ConditionColumn!.Table);
        foreach (CustomColumnSelection? c in query.CustomColumns.Where(c => c.CaseColumn is not null)) result.Add(c.CaseColumn!.Table);
        foreach (JoinDefinition j in query.Joins)
        {
            result.Add(j.FromTable);
            result.Add(j.ToTable);
        }
        return result;
    }
}

public sealed class QueryPerformanceReport
{
    private readonly List<QueryPerformanceHint> _hints = [];
    public IReadOnlyList<QueryPerformanceHint> Hints => _hints;

    public void Add(QueryPerformanceSeverity severity, string message)
    {
        if (_hints.Any(h => h.Severity == severity && string.Equals(h.Message, message, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        _hints.Add(new QueryPerformanceHint(severity, message));
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, _hints.Select(h => $"[{h.Severity}] {h.Message}"));
    }
}

public sealed record QueryPerformanceHint(QueryPerformanceSeverity Severity, string Message);

public enum QueryPerformanceSeverity
{
    Good,
    Info,
    Warning,
    Critical
}
