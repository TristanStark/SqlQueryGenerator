using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Validation;

public sealed class QueryValidator
{
    public IReadOnlyList<string> Validate(QueryDefinition query, DatabaseSchema schema)
    {
        var errors = new List<string>();
        if (schema.Tables.Count == 0)
        {
            errors.Add("Aucun schéma chargé.");
            return errors;
        }

        foreach (var col in EnumerateColumns(query))
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

        return errors;
    }

    private static IEnumerable<ColumnReference> EnumerateColumns(QueryDefinition query)
    {
        foreach (var item in query.SelectedColumns) yield return item;
        foreach (var item in query.Filters) yield return item.Column;
        foreach (var item in query.GroupBy) yield return item;
        foreach (var item in query.OrderBy) yield return item.Column;
        foreach (var item in query.Aggregates.Where(a => a.Column is not null)) yield return item.Column!;
        foreach (var item in query.Aggregates.Where(a => a.ConditionColumn is not null)) yield return item.ConditionColumn!;
        foreach (var item in query.CustomColumns.Where(c => c.CaseColumn is not null)) yield return item.CaseColumn!;
    }
}
