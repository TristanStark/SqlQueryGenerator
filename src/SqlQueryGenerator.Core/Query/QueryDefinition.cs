using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Représente QueryDefinition dans SQL Query Generator.
/// </summary>
public sealed class QueryDefinition
{
    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string? Name { get; set; }
    /// <summary>
    /// Stocke la valeur interne Description.
    /// </summary>
    /// <value>Valeur de Description.</value>
    public string? Description { get; set; }
    /// <summary>
    /// Stocke la valeur interne BaseTable.
    /// </summary>
    /// <value>Valeur de BaseTable.</value>
    public string? BaseTable { get; set; }
    /// <summary>
    /// Stocke la valeur interne Distinct.
    /// </summary>
    /// <value>Valeur de Distinct.</value>
    public bool Distinct { get; set; }
    /// <summary>
    /// Stocke la valeur interne SelectedColumns.
    /// </summary>
    /// <value>Valeur de SelectedColumns.</value>
    public Collection<ColumnReference> SelectedColumns { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne Joins.
    /// </summary>
    /// <value>Valeur de Joins.</value>
    public Collection<JoinDefinition> Joins { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne Filters.
    /// </summary>
    /// <value>Valeur de Filters.</value>
    public Collection<FilterCondition> Filters { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne GroupBy.
    /// </summary>
    /// <value>Valeur de GroupBy.</value>
    public Collection<ColumnReference> GroupBy { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne OrderBy.
    /// </summary>
    /// <value>Valeur de OrderBy.</value>
    public Collection<OrderByItem> OrderBy { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne Aggregates.
    /// </summary>
    /// <value>Valeur de Aggregates.</value>
    public Collection<AggregateSelection> Aggregates { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne CustomColumns.
    /// </summary>
    /// <value>Valeur de CustomColumns.</value>
    public Collection<CustomColumnSelection> CustomColumns { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne Parameters.
    /// </summary>
    /// <value>Valeur de Parameters.</value>
    public Collection<QueryParameterDefinition> Parameters { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne DisabledAutoJoinKeys.
    /// </summary>
    /// <value>Valeur de DisabledAutoJoinKeys.</value>
    public Collection<string> DisabledAutoJoinKeys { get; set; } = [];
    /// <summary>
    /// Stocke la valeur interne LimitRows.
    /// </summary>
    /// <value>Valeur de LimitRows.</value>
    public int? LimitRows { get; set; }
}

/// <summary>
/// Représente QueryParameterDefinition dans SQL Query Generator.
/// </summary>
public sealed record QueryParameterDefinition
{
    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Stocke la valeur interne Description.
    /// </summary>
    /// <value>Valeur de Description.</value>
    public string? Description { get; init; }
    /// <summary>
    /// Stocke la valeur interne DefaultValue.
    /// </summary>
    /// <value>Valeur de DefaultValue.</value>
    public string? DefaultValue { get; init; }
    /// <summary>
    /// Stocke la valeur interne Required.
    /// </summary>
    /// <value>Valeur de Required.</value>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Obtient ou définit Placeholder.
    /// </summary>
    /// <value>Valeur de Placeholder.</value>
    public string Placeholder => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name.StartsWith(':') || Name.StartsWith('@') || Name.StartsWith('?')
            ? Name
            : ":" + Name;
}

/// <summary>
/// Liste les valeurs possibles de QueryFieldKind.
/// </summary>
public enum QueryFieldKind
{
    /// <summary>
    /// Valeur Column de l'énumération.
    /// </summary>
    Column,
    /// <summary>
    /// Valeur Aggregate de l'énumération.
    /// </summary>
    Aggregate,
    /// <summary>
    /// Valeur CustomColumn de l'énumération.
    /// </summary>
    CustomColumn
}

/// <summary>
/// Liste les valeurs possibles de FilterValueKind.
/// </summary>
public enum FilterValueKind
{
    /// <summary>
    /// Valeur Literal de l'énumération.
    /// </summary>
    Literal,
    /// <summary>
    /// Valeur Parameter de l'énumération.
    /// </summary>
    Parameter,
    /// <summary>
    /// Valeur RawSql de l'énumération.
    /// </summary>
    RawSql,
    /// <summary>
    /// Valeur Subquery de l'énumération.
    /// </summary>
    Subquery
}

/// <summary>
/// Représente JoinColumnPair dans SQL Query Generator.
/// </summary>
public sealed record JoinColumnPair
{
    /// <summary>
    /// Stocke la valeur interne FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public string FromColumn { get; init; } = string.Empty;
    /// <summary>
    /// Stocke la valeur interne ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public string ToColumn { get; init; } = string.Empty;
    /// <summary>
    /// Stocke la valeur interne Enabled.
    /// </summary>
    /// <value>Valeur de Enabled.</value>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Représente JoinDefinition dans SQL Query Generator.
/// </summary>
public sealed record JoinDefinition
{
    /// <summary>
    /// Stocke la valeur interne FromTable.
    /// </summary>
    /// <value>Valeur de FromTable.</value>
    public required string FromTable { get; init; }
    /// <summary>
    /// Stocke la valeur interne FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public required string FromColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne ToTable.
    /// </summary>
    /// <value>Valeur de ToTable.</value>
    public required string ToTable { get; init; }
    /// <summary>
    /// Stocke la valeur interne ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public required string ToColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne JoinType.
    /// </summary>
    /// <value>Valeur de JoinType.</value>
    public JoinType JoinType { get; init; } = JoinType.Inner;
    /// <summary>
    /// Stocke la valeur interne AutoInferred.
    /// </summary>
    /// <value>Valeur de AutoInferred.</value>
    public bool AutoInferred { get; init; }

    /// <summary>
    /// Optional additional column pairs for composite joins. FromColumn/ToColumn remain the primary pair
    /// for backward compatibility; enabled pairs in this collection are appended with AND.
    /// </summary>
    public Collection<JoinColumnPair> AdditionalColumnPairs { get; init; } = [];
}

/// <summary>
/// Représente FilterCondition dans SQL Query Generator.
/// </summary>
public sealed record FilterCondition
{
    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public ColumnReference? Column { get; init; }
    /// <summary>
    /// Stocke la valeur interne FieldKind.
    /// </summary>
    /// <value>Valeur de FieldKind.</value>
    public QueryFieldKind FieldKind { get; init; } = QueryFieldKind.Column;
    /// <summary>
    /// Stocke la valeur interne FieldAlias.
    /// </summary>
    /// <value>Valeur de FieldAlias.</value>
    public string? FieldAlias { get; init; }
    /// <summary>
    /// Stocke la valeur interne Operator.
    /// </summary>
    /// <value>Valeur de Operator.</value>
    public string Operator { get; init; } = "=";
    /// <summary>
    /// Stocke la valeur interne Value.
    /// </summary>
    /// <value>Valeur de Value.</value>
    public string? Value { get; init; }
    /// <summary>
    /// Stocke la valeur interne SecondValue.
    /// </summary>
    /// <value>Valeur de SecondValue.</value>
    public string? SecondValue { get; init; }
    /// <summary>
    /// Stocke la valeur interne ValueKind.
    /// </summary>
    /// <value>Valeur de ValueKind.</value>
    public FilterValueKind ValueKind { get; init; } = FilterValueKind.Literal;
    /// <summary>
    /// Stocke la valeur interne Subquery.
    /// </summary>
    /// <value>Valeur de Subquery.</value>
    public QueryDefinition? Subquery { get; init; }
    /// <summary>
    /// Stocke la valeur interne SubqueryName.
    /// </summary>
    /// <value>Valeur de SubqueryName.</value>
    public string? SubqueryName { get; init; }
    /// <summary>
    /// Stocke la valeur interne Connector.
    /// </summary>
    /// <value>Valeur de Connector.</value>
    public LogicalConnector Connector { get; init; } = LogicalConnector.And;
}

/// <summary>
/// Représente OrderByItem dans SQL Query Generator.
/// </summary>
public sealed record OrderByItem
{
    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public ColumnReference? Column { get; init; }
    /// <summary>
    /// Stocke la valeur interne FieldKind.
    /// </summary>
    /// <value>Valeur de FieldKind.</value>
    public QueryFieldKind FieldKind { get; init; } = QueryFieldKind.Column;
    /// <summary>
    /// Stocke la valeur interne FieldAlias.
    /// </summary>
    /// <value>Valeur de FieldAlias.</value>
    public string? FieldAlias { get; init; }
    /// <summary>
    /// Stocke la valeur interne Direction.
    /// </summary>
    /// <value>Valeur de Direction.</value>
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}

/// <summary>
/// Représente AggregateSelection dans SQL Query Generator.
/// </summary>
public sealed record AggregateSelection
{
    /// <summary>
    /// Stocke la valeur interne Function.
    /// </summary>
    /// <value>Valeur de Function.</value>
    public AggregateFunction Function { get; init; } = AggregateFunction.Count;
    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public ColumnReference? Column { get; init; }
    /// <summary>
    /// Stocke la valeur interne Distinct.
    /// </summary>
    /// <value>Valeur de Distinct.</value>
    public bool Distinct { get; init; }
    /// <summary>
    /// Stocke la valeur interne Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string? Alias { get; init; }

    /// <summary>
    /// Optional condition used to generate conditional aggregates without a subquery, for example:
    /// COUNT(CASE WHEN STATUS = 'PAID' THEN PAYMENT_ID END)
    /// SUM(CASE WHEN STATUS = 'PAID' THEN AMOUNT ELSE 0 END)
    /// </summary>
    public ColumnReference? ConditionColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne ConditionOperator.
    /// </summary>
    /// <value>Valeur de ConditionOperator.</value>
    public string? ConditionOperator { get; init; }
    /// <summary>
    /// Stocke la valeur interne ConditionValue.
    /// </summary>
    /// <value>Valeur de ConditionValue.</value>
    public string? ConditionValue { get; init; }
    /// <summary>
    /// Stocke la valeur interne ConditionSecondValue.
    /// </summary>
    /// <value>Valeur de ConditionSecondValue.</value>
    public string? ConditionSecondValue { get; init; }
}

/// <summary>
/// Représente CustomColumnSelection dans SQL Query Generator.
/// </summary>
public sealed record CustomColumnSelection
{
    /// <summary>
    /// Stocke la valeur interne Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string? Alias { get; init; }
    /// <summary>
    /// Stocke la valeur interne RawExpression.
    /// </summary>
    /// <value>Valeur de RawExpression.</value>
    public string? RawExpression { get; init; }
    /// <summary>
    /// Stocke la valeur interne CaseColumn.
    /// </summary>
    /// <value>Valeur de CaseColumn.</value>
    public ColumnReference? CaseColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne CaseOperator.
    /// </summary>
    /// <value>Valeur de CaseOperator.</value>
    public string? CaseOperator { get; init; }
    /// <summary>
    /// Stocke la valeur interne CaseCompareValue.
    /// </summary>
    /// <value>Valeur de CaseCompareValue.</value>
    public string? CaseCompareValue { get; init; }
    /// <summary>
    /// Stocke la valeur interne CaseThenValue.
    /// </summary>
    /// <value>Valeur de CaseThenValue.</value>
    public string? CaseThenValue { get; init; }
    /// <summary>
    /// Stocke la valeur interne CaseElseValue.
    /// </summary>
    /// <value>Valeur de CaseElseValue.</value>
    public string? CaseElseValue { get; init; }
}
