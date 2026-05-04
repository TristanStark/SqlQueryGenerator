using System.Text;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Heuristics;

public sealed class QueryPurposeDescriber
{
    public string Describe(QueryDefinition query, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        var subject = HumanizeTable(query.BaseTable ?? FirstUsedTable(query) ?? "les données");
        var groupings = query.GroupBy.Select(g => HumanizeColumn(g.Column)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var aggregates = query.Aggregates.Select(DescribeAggregate).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var selected = query.SelectedColumns.Select(c => HumanizeColumn(c.Column)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var filters = query.Filters.Select(DescribeFilter).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var orders = query.OrderBy.Select(o => $"trié par {HumanizeColumn(o.Column.Column)} {(o.Direction == SortDirection.Descending ? "décroissant" : "croissant")}").ToArray();

        var sb = new StringBuilder();
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

    private static string? FirstUsedTable(QueryDefinition query)
    {
        return query.SelectedColumns.FirstOrDefault()?.Table
            ?? query.Filters.FirstOrDefault()?.Column.Table
            ?? query.GroupBy.FirstOrDefault()?.Table
            ?? query.OrderBy.FirstOrDefault()?.Column.Table
            ?? query.Aggregates.FirstOrDefault(a => a.Column is not null)?.Column?.Table;
    }

    private static string DescribeAggregate(AggregateSelection aggregate)
    {
        var column = aggregate.Column?.Column ?? "lignes";
        var humanColumn = HumanizeColumn(column);
        var prefix = aggregate.Distinct ? "distincts de " : string.Empty;
        var text = aggregate.Function switch
        {
            AggregateFunction.Count => aggregate.Column is null
                ? "le nombre de lignes"
                : $"le nombre de {prefix}{humanColumn}",
            AggregateFunction.Sum => $"le total de {humanColumn}",
            AggregateFunction.Average => $"la moyenne de {humanColumn}",
            AggregateFunction.Minimum => $"le minimum de {humanColumn}",
            AggregateFunction.Maximum => $"le maximum de {humanColumn}",
            _ => $"{aggregate.Function} de {humanColumn}"
        };

        if (aggregate.ConditionColumn is not null && !string.IsNullOrWhiteSpace(aggregate.ConditionOperator))
        {
            text += $" lorsque {HumanizeColumn(aggregate.ConditionColumn.Column)} {HumanizeOperator(aggregate.ConditionOperator)} {aggregate.ConditionValue}";
        }

        return text;
    }

    private static string DescribeFilter(FilterCondition filter)
    {
        return $"{HumanizeColumn(filter.Column.Column)} {HumanizeOperator(filter.Operator)} {filter.Value}".Trim();
    }

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

    private static string HumanizeTable(string name) => HumanizeName(name);
    private static string HumanizeColumn(string name) => HumanizeName(name);

    private static string HumanizeName(string name)
    {
        return name.Replace('_', ' ').Trim().ToLowerInvariant();
    }

    private static string JoinFrench(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1) return values[0];
        if (values.Count == 2) return values[0] + " et " + values[1];
        return string.Join(", ", values.Take(values.Count - 1)) + " et " + values[^1];
    }
}
