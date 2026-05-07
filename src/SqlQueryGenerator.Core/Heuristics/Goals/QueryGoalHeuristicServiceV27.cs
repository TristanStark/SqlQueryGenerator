using SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;

namespace SqlQueryGenerator.Core.Heuristics.Goals;

/// <summary>
/// Facade that lets the existing generic goal heuristic delegate aggregate-heavy queries to the v27 aggregate engine.
/// </summary>
public sealed class QueryGoalHeuristicServiceV27
{
    /// <summary>
    /// Aggregate-specific engine used when the query contains aggregate projections.
    /// </summary>
    private readonly AggregateGoalHeuristicEngine _aggregateEngine;

    /// <summary>
    /// Initializes a new v27 goal heuristic service with default dependencies.
    /// </summary>
    public QueryGoalHeuristicServiceV27()
        : this(new AggregateGoalHeuristicEngine())
    {
    }

    /// <summary>
    /// Initializes a new v27 goal heuristic service.
    /// </summary>
    /// <param name="aggregateEngine">The aggregate-specific heuristic engine.</param>
    public QueryGoalHeuristicServiceV27(AggregateGoalHeuristicEngine aggregateEngine)
    {
        _aggregateEngine = aggregateEngine ?? throw new ArgumentNullException(nameof(aggregateEngine));
    }

    /// <summary>
    /// Analyzes the aggregate-specific part of a query and returns a result that can override the generic goal text.
    /// </summary>
    /// <param name="snapshot">The aggregate query snapshot produced from the existing query model.</param>
    /// <returns>The aggregate-specific goal result.</returns>
    public AggregateGoalHeuristicResult AnalyzeAggregateGoal(AggregateQuerySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _aggregateEngine.Analyze(snapshot);
    }

    /// <summary>
    /// Decides whether the aggregate result is strong enough to replace the previous generic goal explanation.
    /// </summary>
    /// <param name="aggregateResult">The aggregate-specific result returned by <see cref="AnalyzeAggregateGoal"/>.</param>
    /// <param name="genericConfidence">The confidence score of the existing generic heuristic.</param>
    /// <returns>True when the aggregate result should be displayed first.</returns>
    public bool ShouldPreferAggregateGoal(AggregateGoalHeuristicResult aggregateResult, double genericConfidence)
    {
        ArgumentNullException.ThrowIfNull(aggregateResult);
        return aggregateResult.IsUseful && aggregateResult.Confidence >= Math.Max(0.35, genericConfidence - 0.05);
    }
}
