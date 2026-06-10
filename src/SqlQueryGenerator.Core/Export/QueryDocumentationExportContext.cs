namespace SqlQueryGenerator.Core.Export;

/// <summary>
/// Contains every user-visible element required to export a generated query documentation document.
/// </summary>
/// <remarks>
/// The context is deliberately UI-agnostic so the Markdown exporter can be tested without WPF.
/// </remarks>
public sealed class QueryDocumentationExportContext
{
    /// <summary>
    /// Gets the human-readable query name used as the Markdown title.
    /// </summary>
    /// <value>Current query name, or a safe fallback when the UI field is empty.</value>
    public string QueryName { get; init; } = "query";

    /// <summary>
    /// Gets the optional user-provided query description.
    /// </summary>
    /// <value>Free-form description text.</value>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SQL statement to include in the generated document.
    /// </summary>
    /// <value>Generated or reverse-imported SQL text.</value>
    public string GeneratedSql { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp associated with the export operation.
    /// </summary>
    /// <value>Date and time at which the Markdown was generated.</value>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets the SQL dialect selected in the builder.
    /// </summary>
    /// <value>Display name of the SQL dialect.</value>
    public string Dialect { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current base table selected in the query builder.
    /// </summary>
    /// <value>Fully qualified base table name when available.</value>
    public string BaseTable { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the query uses SELECT DISTINCT.
    /// </summary>
    /// <value><c>true</c> when DISTINCT is enabled.</value>
    public bool Distinct { get; init; }

    /// <summary>
    /// Gets the optional row limit requested by the user.
    /// </summary>
    /// <value>Maximum number of rows, or <c>null</c> when no limit is configured.</value>
    public int? LimitRows { get; init; }

    /// <summary>
    /// Gets the natural language purpose generated for the current query.
    /// </summary>
    /// <value>Query purpose summary.</value>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>
    /// Gets validation and safety warnings for the query.
    /// </summary>
    /// <value>Warning text produced by the validator.</value>
    public string Warnings { get; init; } = string.Empty;

    /// <summary>
    /// Gets performance notes and heuristic recommendations for the query.
    /// </summary>
    /// <value>Performance analysis text.</value>
    public string PerformanceNotes { get; init; } = string.Empty;

    /// <summary>
    /// Gets selected output columns.
    /// </summary>
    /// <value>Columns present in the SELECT clause.</value>
    public IReadOnlyList<QueryDocumentationColumn> SelectedColumns { get; init; } = Array.Empty<QueryDocumentationColumn>();

    /// <summary>
    /// Gets joins used by the generated query.
    /// </summary>
    /// <value>Join definitions and additional join pairs.</value>
    public IReadOnlyList<QueryDocumentationJoin> Joins { get; init; } = Array.Empty<QueryDocumentationJoin>();

    /// <summary>
    /// Gets filters used by the generated query.
    /// </summary>
    /// <value>WHERE and HAVING filter rows.</value>
    public IReadOnlyList<QueryDocumentationFilter> Filters { get; init; } = Array.Empty<QueryDocumentationFilter>();

    /// <summary>
    /// Gets group-by columns.
    /// </summary>
    /// <value>Columns present in the GROUP BY clause.</value>
    public IReadOnlyList<string> GroupByColumns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets aggregate expressions selected by the user.
    /// </summary>
    /// <value>Aggregate functions, aliases and optional conditions.</value>
    public IReadOnlyList<QueryDocumentationAggregate> Aggregates { get; init; } = Array.Empty<QueryDocumentationAggregate>();

    /// <summary>
    /// Gets sort expressions.
    /// </summary>
    /// <value>ORDER BY expressions and directions.</value>
    public IReadOnlyList<QueryDocumentationOrder> Sorting { get; init; } = Array.Empty<QueryDocumentationOrder>();

    /// <summary>
    /// Gets calculated columns.
    /// </summary>
    /// <value>Raw or CASE-based calculated column definitions.</value>
    public IReadOnlyList<QueryDocumentationCalculatedColumn> CalculatedColumns { get; init; } = Array.Empty<QueryDocumentationCalculatedColumn>();

    /// <summary>
    /// Gets declared query parameters.
    /// </summary>
    /// <value>Parameters referenced by the generated SQL.</value>
    public IReadOnlyList<QueryDocumentationParameter> Parameters { get; init; } = Array.Empty<QueryDocumentationParameter>();
}

/// <summary>
/// Describes one selected column in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationColumn(string Table, string Column, string Alias);

/// <summary>
/// Describes one join in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationJoin(
    string JoinType,
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn,
    IReadOnlyList<QueryDocumentationJoinPair> AdditionalPairs);

/// <summary>
/// Describes an additional enabled column pair attached to a join.
/// </summary>
public sealed record QueryDocumentationJoinPair(string FromColumn, string ToColumn);

/// <summary>
/// Describes one filter condition in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationFilter(string Connector, string Field, string Operator, string ValueKind, string Value);

/// <summary>
/// Describes one aggregate expression in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationAggregate(string Function, string Table, string Column, string Alias, bool Distinct, string Condition);

/// <summary>
/// Describes one sort expression in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationOrder(string Field, string Direction);

/// <summary>
/// Describes one calculated column in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationCalculatedColumn(string Alias, string Expression);

/// <summary>
/// Describes one parameter in the exported query documentation.
/// </summary>
public sealed record QueryDocumentationParameter(
    string Name,
    string DeclaredType,
    bool Required,
    bool UseCognosPrompt,
    string DefaultValue,
    string Description);
