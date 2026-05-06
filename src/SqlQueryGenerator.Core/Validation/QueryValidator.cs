using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Validation;

/// <summary>
/// Représente QueryValidator dans SQL Query Generator.
/// </summary>
public sealed class QueryValidator
{
    /// <summary>
    /// Exécute le traitement Validate.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <returns>Résultat du traitement.</returns>
    public IReadOnlyList<string> Validate(QueryDefinition query, DatabaseSchema schema)
    {
        List<string> errors = [];
        if (schema.Tables.Count == 0)
        {
            errors.Add("Aucun schéma chargé.");
            return errors;
        }

        foreach (ColumnReference col in EnumerateColumns(query))
        {
            if (schema.FindColumn(col.Table, col.Column) is null)
            {
                errors.Add($"Colonne inconnue: {col.Table}.{col.Column}");
            }
        }

        if (query.Joins.GroupBy(j => (j.FromTable, j.ToTable, j.FromColumn, j.ToColumn)).Any(g => g.Count() > 1))
        {
            errors.Add("Des jointures identiques sont présentes plusieurs fois.");
        }

        foreach (JoinDefinition join in query.Joins)
        {
            foreach (JoinColumnPair? pair in join.AdditionalColumnPairs.Where(p => p.Enabled))
            {
                if (schema.FindColumn(join.FromTable, pair.FromColumn) is null)
                {
                    errors.Add($"Colonne de jointure inconnue: {join.FromTable}.{pair.FromColumn}");
                }

                if (schema.FindColumn(join.ToTable, pair.ToColumn) is null)
                {
                    errors.Add($"Colonne de jointure inconnue: {join.ToTable}.{pair.ToColumn}");
                }
            }
        }

        foreach (FilterCondition? subqueryFilter in query.Filters.Where(f => f.ValueKind == FilterValueKind.Subquery))
        {
            if (subqueryFilter.Subquery is null)
            {
                errors.Add($"Filtre sous-requête sans requête associée: {subqueryFilter.SubqueryName ?? "sans nom"}.");
                continue;
            }

            foreach (string subError in Validate(subqueryFilter.Subquery, schema))
            {
                errors.Add($"Sous-requête {subqueryFilter.SubqueryName ?? subqueryFilter.Subquery.Name ?? "sans nom"}: {subError}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Exécute le traitement EnumerateColumns.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IEnumerable<ColumnReference> EnumerateColumns(QueryDefinition query)
    {
        foreach (ColumnReference item in query.SelectedColumns) yield return item;
        foreach (FilterCondition? item in query.Filters.Where(f => f.Column is not null)) yield return item.Column!;
        foreach (ColumnReference item in query.GroupBy) yield return item;
        foreach (OrderByItem? item in query.OrderBy.Where(o => o.Column is not null)) yield return item.Column!;
        foreach (AggregateSelection? item in query.Aggregates.Where(a => a.Column is not null)) yield return item.Column!;
        foreach (AggregateSelection? item in query.Aggregates.Where(a => a.ConditionColumn is not null)) yield return item.ConditionColumn!;
        foreach (CustomColumnSelection? item in query.CustomColumns.Where(c => c.CaseColumn is not null)) yield return item.CaseColumn!;
    }
}
