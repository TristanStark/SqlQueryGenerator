using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;
using System.Text;

namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Représente QueryPurposeDescriber dans SQL Query Generator.
/// </summary>
public sealed class QueryPurposeDescriber
{
    /// <summary>
    /// Exécute le traitement Describe.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <returns>Résultat du traitement.</returns>
    public string Describe(QueryDefinition query, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        string subject = HumanizeTable(query.BaseTable ?? FirstUsedTable(query) ?? "les données");
        string[] groupings = query.GroupBy.Select(DescribeGrouping).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] aggregates = query.Aggregates.Select(a => DescribeAggregate(a, query, subject)).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        string[] selected = query.SelectedColumns.Select(DescribeSelectedColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] filters = query.Filters.Select(DescribeFilter).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        string[] orders = query.OrderBy.Select(o => $"trié par {DescribeOrderField(o)} {(o.Direction == SortDirection.Descending ? "décroissant" : "croissant")}").ToArray();

        StringBuilder sb = new();
        sb.Append("Cette requête ");

        if (aggregates.Length > 0 && groupings.Length > 0)
        {
            sb.Append("calcule ").Append(JoinFrench(aggregates)).Append(" par ").Append(JoinFrench(groupings)).Append('.');
        }
        else if (aggregates.Length > 0)
        {
            sb.Append("calcule ").Append(JoinFrench(aggregates)).Append(" sur ").Append(subject).Append('.');
        }
        else if (selected.Length > 0)
        {
            sb.Append("liste ").Append(JoinFrench(selected)).Append(" depuis ").Append(subject).Append('.');
        }
        else
        {
            sb.Append("sélectionne des lignes depuis ").Append(subject).Append('.');
        }

        if (filters.Length > 0)
        {
            sb.Append(" Elle garde seulement les lignes où ").Append(JoinFrench(filters)).Append('.');
        }

        if (orders.Length > 0)
        {
            sb.Append(" Résultat ").Append(JoinFrench(orders)).Append('.');
        }

        if (query.Distinct)
        {
            sb.Append(" Les doublons exacts sont supprimés.");
        }

        if (query.LimitRows is > 0)
        {
            sb.Append(" Le résultat est limité à ").Append(query.LimitRows.Value).Append(" lignes.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exécute le traitement FirstUsedTable.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? FirstUsedTable(QueryDefinition query)
    {
        return query.SelectedColumns.FirstOrDefault()?.Table
            ?? query.Filters.FirstOrDefault(f => f.Column is not null)?.Column?.Table
            ?? query.GroupBy.FirstOrDefault()?.Table
            ?? query.OrderBy.FirstOrDefault(o => o.Column is not null)?.Column?.Table
            ?? query.Aggregates.FirstOrDefault(a => a.Column is not null)?.Column?.Table;
    }

    /// <summary>
    /// Exécute le traitement DescribeAggregate.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    /// <param name="query">Paramètre query.</param>
    /// <param name="subject">Paramètre subject.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeAggregate(AggregateSelection aggregate, QueryDefinition query, string subject)
    {
        string prefix = aggregate.Distinct ? "distincts de " : string.Empty;
        string text = aggregate.Function switch
        {
            AggregateFunction.Count => DescribeCount(aggregate, query, subject, prefix),
            AggregateFunction.Sum => $"le total de {DescribeColumnForAggregate(aggregate.Column)}",
            AggregateFunction.Average => $"la moyenne de {DescribeColumnForAggregate(aggregate.Column)}",
            AggregateFunction.Minimum => $"le minimum de {DescribeColumnForAggregate(aggregate.Column)}",
            AggregateFunction.Maximum => $"le maximum de {DescribeColumnForAggregate(aggregate.Column)}",
            _ => $"{aggregate.Function} de {DescribeColumnForAggregate(aggregate.Column)}"
        };

        if (aggregate.ConditionColumn is not null && !string.IsNullOrWhiteSpace(aggregate.ConditionOperator))
        {
            text += $" lorsque {DescribeSelectedColumn(aggregate.ConditionColumn)} {HumanizeOperator(aggregate.ConditionOperator)} {aggregate.ConditionValue}";
        }

        return text;
    }

    /// <summary>
    /// Exécute le traitement DescribeCount.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    /// <param name="query">Paramètre query.</param>
    /// <param name="subject">Paramètre subject.</param>
    /// <param name="distinctPrefix">Paramètre distinctPrefix.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeCount(AggregateSelection aggregate, QueryDefinition query, string subject, string distinctPrefix)
    {
        if (aggregate.Column is null)
        {
            return "le nombre de lignes";
        }

        // For non-SQL users, COUNT(pnj.genre) grouped by items.name is usually meant as
        // “number of PNJ by item”, not “number of genre”. SQL-wise it counts non-null genre,
        // but the business intent is the count of base rows.
        if (!string.IsNullOrWhiteSpace(query.BaseTable) && aggregate.Column.Table.Equals(query.BaseTable, StringComparison.OrdinalIgnoreCase))
        {
            return $"le nombre de {subject}";
        }

        if (IsIdentifierColumn(aggregate.Column.Column))
        {
            return $"le nombre de {HumanizeTable(aggregate.Column.Table)}";
        }

        return $"le nombre de {distinctPrefix}{DescribeSelectedColumn(aggregate.Column)} renseigné";
    }

    /// <summary>
    /// Exécute le traitement DescribeColumnForAggregate.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeColumnForAggregate(ColumnReference? column)
    {
        return column is null ? "lignes" : DescribeSelectedColumn(column);
    }

    /// <summary>
    /// Exécute le traitement DescribeGrouping.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeGrouping(ColumnReference column)
    {
        string columnName = SqlNameNormalizer.Normalize(column.Column);
        string tableName = HumanizeTable(column.Table);

        if (columnName is "NAME" or "NOM" or "LABEL" or "LIBELLE" or "TITLE" or "TITRE")
        {
            return SingularizeHuman(tableName);
        }

        return $"{HumanizeColumn(column.Column)} de {tableName}";
    }

    /// <summary>
    /// Exécute le traitement DescribeSelectedColumn.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeSelectedColumn(ColumnReference column)
    {
        string col = HumanizeColumn(column.Column);
        string table = HumanizeTable(column.Table);
        return IsVeryGenericDisplayColumn(column.Column) ? $"{col} de {table}" : col;
    }

    /// <summary>
    /// Exécute le traitement IsVeryGenericDisplayColumn.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsVeryGenericDisplayColumn(string columnName)
    {
        string normalized = SqlNameNormalizer.Normalize(columnName);
        return normalized is "NAME" or "NOM" or "LABEL" or "LIBELLE" or "TITLE" or "TITRE" or "CODE";
    }

    /// <summary>
    /// Exécute le traitement IsIdentifierColumn.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsIdentifierColumn(string columnName)
    {
        string normalized = SqlNameNormalizer.Normalize(columnName);
        return normalized is "ID" or "IDEN" or "IDENT"
            || normalized.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("_IDEN", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("_IDENT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement DescribeFilter.
    /// </summary>
    /// <param name="filter">Paramètre filter.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeFilter(FilterCondition filter)
    {
        string field = filter.Column is not null
            ? DescribeSelectedColumn(filter.Column)
            : HumanizeName(filter.FieldAlias ?? "champ calculé");
        return $"{field} {HumanizeOperator(filter.Operator)} {filter.Value}".Trim();
    }

    /// <summary>
    /// Exécute le traitement DescribeOrderField.
    /// </summary>
    /// <param name="order">Paramètre order.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string DescribeOrderField(OrderByItem order)
    {
        return order.Column is not null
            ? DescribeSelectedColumn(order.Column)
            : HumanizeName(order.FieldAlias ?? "champ calculé");
    }

    /// <summary>
    /// Exécute le traitement HumanizeOperator.
    /// </summary>
    /// <param name="op">Paramètre op.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string HumanizeOperator(string op)
    {
        return op.ToUpperInvariant() switch
        {
            "=" => "est égal à",
            "<>" => "est différent de",
            ">" => "est supérieur à",
            ">=" => "est supérieur ou égal à",
            "<" => "est inférieur à",
            "<=" => "est inférieur ou égal à",
            "LIKE" => "ressemble à",
            "NOT LIKE" => "ne ressemble pas à",
            "IN" => "fait partie de",
            "NOT IN" => "ne fait pas partie de",
            "BETWEEN" => "est entre",
            "IS NULL" => "est vide",
            "IS NOT NULL" => "n'est pas vide",
            _ => op
        };
    }

    /// <summary>
    /// Exécute le traitement HumanizeTable.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string HumanizeTable(string name) => HumanizeName(name);
    /// <summary>
    /// Exécute le traitement HumanizeColumn.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string HumanizeColumn(string name) => HumanizeName(name);

    /// <summary>
    /// Exécute le traitement HumanizeName.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string HumanizeName(string name)
    {
        return name.Replace('_', ' ').Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Exécute le traitement SingularizeHuman.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string SingularizeHuman(string name)
    {
        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return name;
        }

        words[^1] = SingularizeToken(words[^1]);
        return string.Join(' ', words);
    }

    /// <summary>
    /// Exécute le traitement SingularizeToken.
    /// </summary>
    /// <param name="token">Paramètre token.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string SingularizeToken(string token)
    {
        if (token.Length <= 3)
        {
            return token;
        }

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && token.Length > 4)
        {
            return token[..^3] + "y";
        }

        if ((token.EndsWith("s", StringComparison.OrdinalIgnoreCase) || token.EndsWith("x", StringComparison.OrdinalIgnoreCase)) && token.Length > 3)
        {
            return token[..^1];
        }

        return token;
    }

    /// <summary>
    /// Exécute le traitement JoinFrench.
    /// </summary>
    /// <param name="values">Paramètre values.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string JoinFrench(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1) return values[0];
        if (values.Count == 2) return values[0] + " et " + values[1];
        return string.Join(", ", values.Take(values.Count - 1)) + " et " + values[^1];
    }
}
