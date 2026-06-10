using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Export;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.App.Export;

/// <summary>
/// Converts the WPF query builder state into the UI-agnostic Markdown export context.
/// </summary>
public static class QueryDocumentationExportContextFactory
{
    /// <summary>
    /// Creates a Markdown export context from the current main view model state.
    /// </summary>
    /// <param name="viewModel">Current main view model.</param>
    /// <param name="generatedAt">Timestamp to include in the exported document.</param>
    /// <param name="sql">SQL text to document.</param>
    /// <returns>A complete query documentation context.</returns>
    public static QueryDocumentationExportContext FromViewModel(MainViewModel viewModel, DateTimeOffset generatedAt, string sql)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new QueryDocumentationExportContext
        {
            QueryName = string.IsNullOrWhiteSpace(viewModel.QueryName) ? "query" : viewModel.QueryName.Trim(),
            Description = viewModel.QueryDescription?.Trim() ?? string.Empty,
            GeneratedSql = sql?.Trim() ?? string.Empty,
            GeneratedAt = generatedAt,
            Dialect = viewModel.Dialect.ToString(),
            BaseTable = viewModel.BaseTable,
            Distinct = viewModel.Distinct,
            LimitRows = viewModel.LimitRows,
            Purpose = CleanGeneratedText(viewModel.QueryPurpose, "Charge un schéma"),
            Warnings = CleanGeneratedText(viewModel.Warnings, string.Empty),
            PerformanceNotes = CleanGeneratedText(viewModel.PerformanceReport, "L'analyse de performance apparaîtra ici"),
            SelectedColumns = viewModel.SelectedColumns
                .Select(column => new QueryDocumentationColumn(column.Table, column.Column, column.Alias))
                .ToList(),
            Joins = viewModel.Joins
                .Select(join => new QueryDocumentationJoin(
                    join.JoinType.ToString(),
                    join.FromTable,
                    join.FromColumn,
                    join.ToTable,
                    join.ToColumn,
                    join.AdditionalPairs
                        .Where(pair => pair.Enabled)
                        .Select(pair => new QueryDocumentationJoinPair(pair.FromColumn, pair.ToColumn))
                        .ToList()))
                .ToList(),
            Filters = viewModel.Filters
                .Select(filter => new QueryDocumentationFilter(
                    filter.Connector.ToString(),
                    BuildFilterField(filter),
                    filter.Operator,
                    filter.ValueKind.ToString(),
                    BuildFilterValue(filter)))
                .ToList(),
            GroupByColumns = viewModel.GroupBy
                .Select(group => Qualified(group.Table, group.Column))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList(),
            Aggregates = viewModel.Aggregates
                .Select(aggregate => new QueryDocumentationAggregate(
                    aggregate.Function.ToString(),
                    aggregate.Table,
                    aggregate.Column,
                    aggregate.Alias,
                    aggregate.Distinct,
                    BuildAggregateCondition(aggregate)))
                .ToList(),
            Sorting = viewModel.OrderBy
                .Select(order => new QueryDocumentationOrder(BuildOrderField(order), order.Direction.ToString()))
                .ToList(),
            CalculatedColumns = viewModel.CustomColumns
                .Select(column => new QueryDocumentationCalculatedColumn(column.Alias, BuildCalculatedColumnExpression(column)))
                .ToList(),
            Parameters = viewModel.Parameters
                .Select(parameter => new QueryDocumentationParameter(
                    parameter.Name,
                    parameter.DeclaredType,
                    parameter.Required,
                    parameter.UseCognosPrompt,
                    parameter.DefaultValue,
                    parameter.Description))
                .ToList()
        };
    }

    /// <summary>
    /// Resolves a filter field from either a column reference or a calculated alias.
    /// </summary>
    private static string BuildFilterField(FilterRowViewModel filter)
    {
        return filter.FieldKind == QueryFieldKind.Column
            ? Qualified(filter.Table, filter.Column)
            : filter.FieldAlias;
    }

    /// <summary>
    /// Resolves an ORDER BY field from either a column reference or an alias.
    /// </summary>
    private static string BuildOrderField(OrderByRowViewModel order)
    {
        return order.FieldKind == QueryFieldKind.Column
            ? Qualified(order.Table, order.Column)
            : order.FieldAlias;
    }

    /// <summary>
    /// Builds a readable filter value representation for documentation.
    /// </summary>
    private static string BuildFilterValue(FilterRowViewModel filter)
    {
        if (filter.Operator.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
        {
            return $"{filter.Value} AND {filter.SecondValue}".Trim();
        }

        if (filter.Operator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase)
            || filter.Operator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(filter.SubqueryName))
        {
            return string.IsNullOrWhiteSpace(filter.Value)
                ? filter.SubqueryName
                : $"{filter.Value} ({filter.SubqueryName})";
        }

        return filter.Value;
    }

    /// <summary>
    /// Builds the optional aggregate condition displayed in the Markdown export.
    /// </summary>
    private static string BuildAggregateCondition(AggregateRowViewModel aggregate)
    {
        string conditionColumn = Qualified(aggregate.ConditionTable, aggregate.ConditionColumn);
        if (string.IsNullOrWhiteSpace(conditionColumn))
        {
            return string.Empty;
        }

        if (aggregate.ConditionOperator.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
        {
            return $"{conditionColumn} BETWEEN {aggregate.ConditionValue} AND {aggregate.ConditionSecondValue}";
        }

        if (aggregate.ConditionOperator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase)
            || aggregate.ConditionOperator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
        {
            return $"{conditionColumn} {aggregate.ConditionOperator}";
        }

        return $"{conditionColumn} {aggregate.ConditionOperator} {aggregate.ConditionValue}".Trim();
    }

    /// <summary>
    /// Builds a raw or CASE-based calculated column expression for documentation.
    /// </summary>
    private static string BuildCalculatedColumnExpression(CustomColumnRowViewModel column)
    {
        if (!string.IsNullOrWhiteSpace(column.RawExpression))
        {
            return column.RawExpression;
        }

        string caseColumn = Qualified(column.CaseTable, column.CaseColumn);
        if (string.IsNullOrWhiteSpace(caseColumn))
        {
            return string.Empty;
        }

        return $"CASE WHEN {caseColumn} {column.CaseOperator} {column.CaseCompareValue} THEN {column.CaseThenValue} ELSE {column.CaseElseValue} END";
    }

    /// <summary>
    /// Builds a readable qualified column name from table and column parts.
    /// </summary>
    private static string Qualified(string table, string column)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return column?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(column))
        {
            return table.Trim();
        }

        return $"{table.Trim()}.{column.Trim()}";
    }

    /// <summary>
    /// Removes empty or placeholder generated UI text before export.
    /// </summary>
    private static string CleanGeneratedText(string? value, string placeholderPrefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(placeholderPrefix)
            && trimmed.StartsWith(placeholderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return trimmed;
    }
}
