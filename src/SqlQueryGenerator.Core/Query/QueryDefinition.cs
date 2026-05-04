using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Query;

public sealed class QueryDefinition
{
    public string? BaseTable { get; set; }
    public bool Distinct { get; set; }
    public Collection<ColumnReference> SelectedColumns { get; } = new();
    public Collection<JoinDefinition> Joins { get; } = new();
    public Collection<FilterCondition> Filters { get; } = new();
    public Collection<ColumnReference> GroupBy { get; } = new();
    public Collection<OrderByItem> OrderBy { get; } = new();
    public Collection<AggregateSelection> Aggregates { get; } = new();
    public Collection<CustomColumnSelection> CustomColumns { get; } = new();
    public Collection<string> DisabledAutoJoinKeys { get; } = new();
    public int? LimitRows { get; set; }
}

public sealed record JoinDefinition
{
    public required string FromTable { get; init; }
    public required string FromColumn { get; init; }
    public required string ToTable { get; init; }
    public required string ToColumn { get; init; }
    public JoinType JoinType { get; init; } = JoinType.Inner;
    public bool AutoInferred { get; init; }
}

public sealed record FilterCondition
{
    public required ColumnReference Column { get; init; }
    public string Operator { get; init; } = "=";
    public string? Value { get; init; }
    public string? SecondValue { get; init; }
    public LogicalConnector Connector { get; init; } = LogicalConnector.And;
}

public sealed record OrderByItem
{
    public required ColumnReference Column { get; init; }
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
