using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Produces index-aware performance hints from the current query shape and loaded schema metadata.
/// </summary>
public sealed class QueryPerformanceAnalyzer
{
    /// <summary>
    /// Analyzes the current query and emits conservative performance hints grouped by severity.
    /// </summary>
    /// <param name="query">Current query model.</param>
    /// <param name="schema">Loaded schema metadata.</param>
    /// <returns>Heuristic performance report.</returns>
    public QueryPerformanceReport Analyze(QueryDefinition query, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        QueryPerformanceReport report = new();
        HashSet<string> usedTables = CollectUsedTables(query);
        if (!string.IsNullOrWhiteSpace(query.BaseTable))
        {
            usedTables.Add(query.BaseTable!);
        }

        AnalyzeViews(report, schema, usedTables);
        AnalyzeProjectionShape(report, query, usedTables);
        AnalyzeFilters(report, schema, query);
        AnalyzeJoins(report, schema, query, usedTables);
        AnalyzeGrouping(report, schema, query);
        AnalyzeOrderBy(report, schema, query);
        AnalyzeSubqueries(report, schema, query);
        AnalyzeRowLimiting(report, query, usedTables);

        if (report.Hints.Count == 0)
        {
            report.Add(QueryPerformanceSeverity.Good, "Aucun risque évident détecté à partir des métadonnées disponibles.");
        }

        return report;
    }

    private static void AnalyzeViews(QueryPerformanceReport report, DatabaseSchema schema, IEnumerable<string> usedTables)
    {
        foreach (string tableName in usedTables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            TableDefinition? table = schema.FindTable(tableName);
            if (table?.IsView == true)
            {
                report.Add(QueryPerformanceSeverity.Warning, $"{table.FullName} est une vue : son coût réel dépend de la requête interne de la vue. Vérifie le plan d'exécution réel si la vue masque des jointures ou agrégats.");
            }
        }
    }

    private static void AnalyzeProjectionShape(QueryPerformanceReport report, QueryDefinition query, IReadOnlySet<string> usedTables)
    {
        if (query.SelectedColumns.Any(c => c.Column.Trim() == "*"))
        {
            report.Add(QueryPerformanceSeverity.Warning, "SELECT * détecté : projection large, transferts inutiles possibles et usage d'index covering moins probable.");
        }

        int projectedCount = query.SelectedColumns.Count + query.Aggregates.Count + query.CustomColumns.Count;
        if (projectedCount >= 10)
        {
            report.Add(QueryPerformanceSeverity.Info, $"Projection large ({projectedCount} champs) : vérifie que toutes les colonnes retournées sont réellement utiles.");
        }

        if (usedTables.Count > 1 && query.Joins.Count == 0)
        {
            report.Add(QueryPerformanceSeverity.Critical, "Plusieurs tables sont utilisées sans jointure explicite détectée : risque de produit cartésien ou de requête incohérente.");
        }
    }

    private static void AnalyzeFilters(QueryPerformanceReport report, DatabaseSchema schema, QueryDefinition query)
    {
        foreach (FilterCondition filter in query.Filters)
        {
            if (filter.Column is not null)
            {
                AnalyzeColumnFilter(report, schema, filter);
                continue;
            }

            if (filter.FieldKind == QueryFieldKind.CustomColumn || LooksLikeExpression(filter.FieldAlias))
            {
                report.Add(QueryPerformanceSeverity.Warning, $"Filtre sur expression `{filter.FieldAlias}` : les fonctions appliquées aux colonnes (UPPER, TO_CHAR, SUBSTR, etc.) empêchent souvent l'usage optimal des index.");
            }
        }
    }

    private static void AnalyzeColumnFilter(QueryPerformanceReport report, DatabaseSchema schema, FilterCondition filter)
    {
        ColumnReference col = filter.Column!;
        if (col.Column.Trim() == "*" || schema.FindColumn(col.Table, col.Column) is null)
        {
            return;
        }

        if (schema.IsColumnIndexed(col.Table, col.Column))
        {
            report.Add(QueryPerformanceSeverity.Good, $"Filtre sur {col.Table}.{col.Column}: colonne indexée, bon signal pour WHERE/HAVING.");
        }
        else
        {
            report.Add(QueryPerformanceSeverity.Warning, $"Filtre sur {col.Table}.{col.Column}: aucun index détecté. Sur une grosse table, risque de scan.");
        }

        if (IsLeadingWildcardLike(filter))
        {
            report.Add(QueryPerformanceSeverity.Warning, $"Filtre LIKE sur {col.Table}.{col.Column} avec wildcard en tête : l'index sera souvent inutilisable.");
        }

        if (filter.ValueKind == FilterValueKind.RawSql)
        {
            report.Add(QueryPerformanceSeverity.Info, $"Filtre sur {col.Table}.{col.Column} basé sur SQL brut : vérifie que l'expression côté droit reste sélective et index-friendly.");
        }
    }

    private static void AnalyzeJoins(QueryPerformanceReport report, DatabaseSchema schema, QueryDefinition query, IReadOnlySet<string> usedTables)
    {
        foreach (JoinDefinition join in query.Joins)
        {
            AnalyzeJoinSide(report, schema, join.FromTable, join.FromColumn, "gauche");
            AnalyzeJoinSide(report, schema, join.ToTable, join.ToColumn, "droite");

            bool leftUnique = IsUniqueJoinSide(schema, join.FromTable, join.FromColumn);
            bool rightUnique = IsUniqueJoinSide(schema, join.ToTable, join.ToColumn);
            if (!leftUnique && !rightUnique)
            {
                report.Add(QueryPerformanceSeverity.Warning, $"Jointure {join.FromTable}.{join.FromColumn} -> {join.ToTable}.{join.ToColumn}: aucune extrémité PK/unique détectée, risque de cardinalité large.");
            }

            foreach (JoinColumnPair pair in join.AdditionalColumnPairs.Where(p => p.Enabled))
            {
                AnalyzeJoinSide(report, schema, join.FromTable, pair.FromColumn, "gauche composite");
                AnalyzeJoinSide(report, schema, join.ToTable, pair.ToColumn, "droite composite");
            }
        }

        if (usedTables.Count > 1 && query.Joins.Count < usedTables.Count - 1)
        {
            report.Add(QueryPerformanceSeverity.Info, "Certaines tables semblent utilisées sans chaîne de jointure complète. Vérifie les relations ajoutées par le constructeur.");
        }
    }

    private static void AnalyzeGrouping(QueryPerformanceReport report, DatabaseSchema schema, QueryDefinition query)
    {
        foreach (ColumnReference group in query.GroupBy)
        {
            if (group.Column.Trim() == "*")
            {
                continue;
            }

            if (schema.IsColumnIndexed(group.Table, group.Column))
            {
                report.Add(QueryPerformanceSeverity.Info, $"GROUP BY {group.Table}.{group.Column}: colonne indexée détectée, peut aider selon le moteur et le plan.");
            }
            else
            {
                report.Add(QueryPerformanceSeverity.Info, $"GROUP BY {group.Table}.{group.Column}: aucun index détecté ; tri/hash aggregate probable sur gros volume.");
            }
        }

        if (query.GroupBy.Count >= 4)
        {
            report.Add(QueryPerformanceSeverity.Warning, $"GROUP BY sur {query.GroupBy.Count} colonnes : coût de tri/hash potentiellement élevé et cardinalité de groupes possiblement importante.");
        }
    }

    private static void AnalyzeOrderBy(QueryPerformanceReport report, DatabaseSchema schema, QueryDefinition query)
    {
        foreach (OrderByItem order in query.OrderBy.Where(o => o.Column is not null))
        {
            ColumnReference col = order.Column!;
            if (schema.IsColumnIndexed(col.Table, col.Column))
            {
                report.Add(QueryPerformanceSeverity.Info, $"ORDER BY {col.Table}.{col.Column}: colonne indexée détectée, peut éviter un tri selon le plan.");
            }
            else
            {
                report.Add(QueryPerformanceSeverity.Warning, $"ORDER BY {col.Table}.{col.Column}: aucun index détecté ; tri explicite probable.");
            }
        }
    }

    private static void AnalyzeSubqueries(QueryPerformanceReport report, DatabaseSchema schema, QueryDefinition query)
    {
        foreach (FilterCondition sub in query.Filters.Where(f => f.ValueKind == FilterValueKind.Subquery && f.Subquery is not null))
        {
            report.Add(QueryPerformanceSeverity.Info, "Sous-requête utilisée dans un filtre: vérifie que la sous-requête retourne peu de lignes ou qu'elle filtre sur des colonnes indexées.");
            QueryPerformanceReport subReport = new QueryPerformanceAnalyzer().Analyze(sub.Subquery!, schema);
            foreach (QueryPerformanceHint hint in subReport.Hints.Take(6))
            {
                report.Add(hint.Severity, "Sous-requête: " + hint.Message);
            }
        }
    }

    private static void AnalyzeRowLimiting(QueryPerformanceReport report, QueryDefinition query, IReadOnlySet<string> usedTables)
    {
        bool looksLarge = usedTables.Count > 1
            || query.SelectedColumns.Any(c => c.Column.Trim() == "*")
            || query.OrderBy.Count > 0
            || query.GroupBy.Count > 0;

        if (query.LimitRows is > 0 && query.OrderBy.Count == 0)
        {
            report.Add(QueryPerformanceSeverity.Info, "LIMIT/FETCH sans ORDER BY: rapide, mais résultat non déterministe.");
        }

        if (query.LimitRows is null && looksLarge)
        {
            report.Add(QueryPerformanceSeverity.Info, "Requête potentiellement volumineuse sans LIMIT/FETCH/TOP : pense à limiter les lignes lors de l'exploration.");
        }
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

    private static bool IsUniqueJoinSide(DatabaseSchema schema, string table, string column)
    {
        ColumnDefinition? col = schema.FindColumn(table, column);
        return col?.IsPrimaryKey == true || schema.IsColumnUniqueIndexed(table, column);
    }

    private static bool IsLeadingWildcardLike(FilterCondition filter)
    {
        if (!string.Equals(filter.Operator, "LIKE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(filter.Operator, "NOT LIKE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter.Value))
        {
            return false;
        }

        return filter.Value.TrimStart().StartsWith("%", StringComparison.Ordinal);
    }

    private static bool LooksLikeExpression(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains('(')
            || Regex.IsMatch(value, @"\b(UPPER|LOWER|TO_CHAR|SUBSTR|TRIM|COALESCE|NVL|DECODE|CAST)\b", RegexOptions.IgnoreCase);
    }

    private static HashSet<string> CollectUsedTables(QueryDefinition query)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (ColumnReference c in query.SelectedColumns) result.Add(c.Table);
        foreach (FilterCondition f in query.Filters.Where(f => f.Column is not null)) result.Add(f.Column!.Table);
        foreach (ColumnReference g in query.GroupBy) result.Add(g.Table);
        foreach (OrderByItem o in query.OrderBy.Where(o => o.Column is not null)) result.Add(o.Column!.Table);
        foreach (AggregateSelection a in query.Aggregates.Where(a => a.Column is not null)) result.Add(a.Column!.Table);
        foreach (AggregateSelection a in query.Aggregates.Where(a => a.ConditionColumn is not null)) result.Add(a.ConditionColumn!.Table);
        foreach (CustomColumnSelection c in query.CustomColumns.Where(c => c.CaseColumn is not null)) result.Add(c.CaseColumn!.Table);
        foreach (JoinDefinition j in query.Joins)
        {
            result.Add(j.FromTable);
            result.Add(j.ToTable);
        }

        return result;
    }
}

/// <summary>
/// Heuristic performance report produced from the current query and schema metadata.
/// </summary>
public sealed class QueryPerformanceReport
{
    private readonly List<QueryPerformanceHint> _hints = [];

    /// <summary>
    /// Gets the ordered list of hints.
    /// </summary>
    public IReadOnlyList<QueryPerformanceHint> Hints => _hints;

    /// <summary>
    /// Adds a unique hint to the report.
    /// </summary>
    /// <param name="severity">Hint severity.</param>
    /// <param name="message">Hint message.</param>
    public void Add(QueryPerformanceSeverity severity, string message)
    {
        if (_hints.Any(h => h.Severity == severity && string.Equals(h.Message, message, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _hints.Add(new QueryPerformanceHint(severity, message));
    }

    /// <summary>
    /// Formats the report grouped by severity so risky hints are easy to spot.
    /// </summary>
    /// <returns>Human-readable report.</returns>
    public override string ToString()
    {
        if (_hints.Count == 0)
        {
            return string.Empty;
        }

        IEnumerable<IGrouping<QueryPerformanceSeverity, QueryPerformanceHint>> groups = _hints
            .OrderByDescending(h => h.Severity)
            .ThenBy(h => h.Message, StringComparer.OrdinalIgnoreCase)
            .GroupBy(h => h.Severity);

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            groups.Select(group =>
                $"[{group.Key}]{Environment.NewLine}" +
                string.Join(Environment.NewLine, group.Select(h => "- " + h.Message))));
    }
}

/// <summary>
/// One heuristic performance hint.
/// </summary>
/// <param name="Severity">Hint severity.</param>
/// <param name="Message">Hint message.</param>
public sealed record QueryPerformanceHint(QueryPerformanceSeverity Severity, string Message);

/// <summary>
/// Severity levels used by the heuristic performance report.
/// </summary>
public enum QueryPerformanceSeverity
{
    Good,
    Info,
    Warning,
    Critical
}
