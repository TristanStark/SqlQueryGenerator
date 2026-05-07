namespace SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;

/// <summary>
/// Helper used to build aggregate query snapshots incrementally from the existing query model.
/// </summary>
public sealed class AggregateQuerySnapshotBuilder
{
    /// <summary>
    /// Mutable table usage list collected by the builder.
    /// </summary>
    private readonly List<TableUsageSummary> _tables = [];

    /// <summary>
    /// Mutable aggregate projection list collected by the builder.
    /// </summary>
    private readonly List<AggregateProjection> _aggregates = [];

    /// <summary>
    /// Mutable grouping projection list collected by the builder.
    /// </summary>
    private readonly List<GroupingProjection> _groupings = [];

    /// <summary>
    /// Mutable filter list collected by the builder.
    /// </summary>
    private readonly List<FilterCondition> _filters = [];

    /// <summary>
    /// Mutable HAVING list collected by the builder.
    /// </summary>
    private readonly List<HavingCondition> _havingConditions = [];

    /// <summary>
    /// Mutable ordering list collected by the builder.
    /// </summary>
    private readonly List<OrderProjection> _orderings = [];

    /// <summary>
    /// Mutable index coverage list collected by the builder.
    /// </summary>
    private readonly List<IndexCoverageSummary> _indexCoverage = [];

    /// <summary>
    /// SQL limit collected by the builder.
    /// </summary>
    private int? _limit;

    /// <summary>
    /// Generated SQL collected by the builder.
    /// </summary>
    private string? _generatedSql;

    /// <summary>
    /// Star-projection flag collected by the builder.
    /// </summary>
    private bool _hasStarProjection;

    /// <summary>
    /// Adds a table to the snapshot.
    /// </summary>
    /// <param name="table">The table usage summary to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddTable(TableUsageSummary table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _tables.Add(table);
        return this;
    }

    /// <summary>
    /// Adds an aggregate projection to the snapshot.
    /// </summary>
    /// <param name="aggregate">The aggregate projection to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddAggregate(AggregateProjection aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _aggregates.Add(aggregate);
        return this;
    }

    /// <summary>
    /// Adds a grouping projection to the snapshot.
    /// </summary>
    /// <param name="grouping">The grouping projection to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddGrouping(GroupingProjection grouping)
    {
        ArgumentNullException.ThrowIfNull(grouping);
        _groupings.Add(grouping);
        return this;
    }

    /// <summary>
    /// Adds a WHERE filter to the snapshot.
    /// </summary>
    /// <param name="filter">The filter condition to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddFilter(FilterCondition filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// Adds a HAVING predicate to the snapshot.
    /// </summary>
    /// <param name="havingCondition">The HAVING condition to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddHaving(HavingCondition havingCondition)
    {
        ArgumentNullException.ThrowIfNull(havingCondition);
        _havingConditions.Add(havingCondition);
        return this;
    }

    /// <summary>
    /// Adds an ORDER BY expression to the snapshot.
    /// </summary>
    /// <param name="ordering">The ordering projection to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddOrdering(OrderProjection ordering)
    {
        ArgumentNullException.ThrowIfNull(ordering);
        _orderings.Add(ordering);
        return this;
    }

    /// <summary>
    /// Adds index coverage metadata to the snapshot.
    /// </summary>
    /// <param name="coverage">The index coverage metadata to add.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder AddIndexCoverage(IndexCoverageSummary coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        _indexCoverage.Add(coverage);
        return this;
    }

    /// <summary>
    /// Sets the SQL limit value.
    /// </summary>
    /// <param name="limit">The SQL limit, TOP, or FETCH FIRST value.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder WithLimit(int? limit)
    {
        _limit = limit;
        return this;
    }

    /// <summary>
    /// Sets the generated SQL text.
    /// </summary>
    /// <param name="generatedSql">The generated SQL text.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder WithGeneratedSql(string? generatedSql)
    {
        _generatedSql = generatedSql;
        return this;
    }

    /// <summary>
    /// Sets whether the query has a table.* projection mixed with aggregates.
    /// </summary>
    /// <param name="hasStarProjection">True when the query projects table.*.</param>
    /// <returns>The current builder instance.</returns>
    public AggregateQuerySnapshotBuilder WithStarProjection(bool hasStarProjection)
    {
        _hasStarProjection = hasStarProjection;
        return this;
    }

    /// <summary>
    /// Builds the immutable aggregate query snapshot.
    /// </summary>
    /// <returns>The immutable aggregate query snapshot.</returns>
    public AggregateQuerySnapshot Build()
    {
        return new AggregateQuerySnapshot
        {
            Tables = _tables.ToArray(),
            Aggregates = _aggregates.ToArray(),
            Groupings = _groupings.ToArray(),
            Filters = _filters.ToArray(),
            HavingConditions = _havingConditions.ToArray(),
            Orderings = _orderings.ToArray(),
            IndexCoverage = _indexCoverage.ToArray(),
            Limit = _limit,
            GeneratedSql = _generatedSql,
            HasStarProjection = _hasStarProjection
        };
    }
}
