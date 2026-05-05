using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Query;

public sealed class QueryDefinition
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? BaseTable { get; set; }
    public bool Distinct { get; set; }
    public Collection<ColumnReference> SelectedColumns { get; set; } = [];
    public Collection<JoinDefinition> Joins { get; set; } = [];
    public Collection<FilterCondition> Filters { get; set; } = [];
    public Collection<ColumnReference> GroupBy { get; set; } = [];
    public Collection<OrderByItem> OrderBy { get; set; } = [];
    public Collection<AggregateSelection> Aggregates { get; set; } = [];
    public Collection<CustomColumnSelection> CustomColumns { get; set; } = [];
    public Collection<QueryParameterDefinition> Parameters { get; set; } = [];
    public Collection<string> DisabledAutoJoinKeys { get; set; } = [];
    public int? LimitRows { get; set; }
}

public sealed record QueryParameterDefinition
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public bool Required { get; init; } = true;

    public string Placeholder => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name.StartsWith(':') || Name.StartsWith('@') || Name.StartsWith('?')
            ? Name
            : ":" + Name;
}

public enum QueryFieldKind
{
    Column,
    Aggregate,
    CustomColumn
}

public enum FilterValueKind
{
    Literal,
    Parameter,
    RawSql,
    Subquery
}

public sealed record JoinColumnPair
{
    public string FromColumn { get; init; } = string.Empty;
    public string ToColumn { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}

public sealed record JoinDefinition
{
    public required string FromTable { get; init; }
    public required string FromColumn { get; init; }
    public required string ToTable { get; init; }
    public required string ToColumn { get; init; }
    public JoinType JoinType { get; init; } = JoinType.Inner;
    public bool AutoInferred { get; init; }

    /// <summary>
    /// Optional additional column pairs for composite joins. FromColumn/ToColumn remain the primary pair
    /// for backward compatibility; enabled pairs in this collection are appended with AND.
    /// </summary>
    public Collection<JoinColumnPair> AdditionalColumnPairs { get; init; } = [];
}

public sealed record FilterCondition
{
    public ColumnReference? Column { get; init; }
    public QueryFieldKind FieldKind { get; init; } = QueryFieldKind.Column;
    public string? FieldAlias { get; init; }
    public string Operator { get; init; } = "=";
    public string? Value { get; init; }
    public string? SecondValue { get; init; }
    public FilterValueKind ValueKind { get; init; } = FilterValueKind.Literal;
    public QueryDefinition? Subquery { get; init; }
    public string? SubqueryName { get; init; }
    public LogicalConnector Connector { get; init; } = LogicalConnector.And;
}

public sealed record OrderByItem
{
    public ColumnReference? Column { get; init; }
    public QueryFieldKind FieldKind { get; init; } = QueryFieldKind.Column;
    public string? FieldAlias { get; init; }
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}

public sealed record AggregateSelection
{
    public AggregateFunction Function { get; init; } = AggregateFunction.Count;
    public ColumnReference? Column { get; init; }
    public bool Distinct { get; init; }
    public string? Alias { get; init; }

    /// <summary>
    /// Optional condition used to generate conditional aggregates without a subquery, for example:
    /// COUNT(CASE WHEN STATUS = 'PAID' THEN PAYMENT_ID END)
    /// SUM(CASE WHEN STATUS = 'PAID' THEN AMOUNT ELSE 0 END)
    /// </summary>
    public ColumnReference? ConditionColumn { get; init; }
    public string? ConditionOperator { get; init; }
    public string? ConditionValue { get; init; }
    public string? ConditionSecondValue { get; init; }
}

public sealed record CustomColumnSelection
{
    public string? Alias { get; init; }
    public string? RawExpression { get; init; }
    public ColumnReference? CaseColumn { get; init; }
    public string? CaseOperator { get; init; }
    public string? CaseCompareValue { get; init; }
    public string? CaseThenValue { get; init; }
    public string? CaseElseValue { get; init; }
}
