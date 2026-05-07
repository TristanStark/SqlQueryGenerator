namespace SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;

/// <summary>
/// Configures the aggregate goal heuristic without hard-coding project-specific thresholds.
/// </summary>
public sealed class AggregateGoalHeuristicOptions
{
    /// <summary>
    /// Gets the default options used by the v27 aggregate heuristic.
    /// </summary>
    public static AggregateGoalHeuristicOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the confidence threshold from which a result is considered high confidence.
    /// </summary>
    public double HighConfidenceThreshold { get; set; } = 0.74;

    /// <summary>
    /// Gets or sets the confidence threshold from which a result is considered medium confidence.
    /// </summary>
    public double MediumConfidenceThreshold { get; set; } = 0.50;

    /// <summary>
    /// Gets or sets the maximum number of grouping names inserted into a compact sentence.
    /// </summary>
    public int MaxInlineGroupingLabels { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of metric names inserted into a compact sentence.
    /// </summary>
    public int MaxInlineMetricLabels { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether the heuristic should warn when GROUP BY columns appear to be unindexed.
    /// </summary>
    public bool WarnOnUnindexedGroupings { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the heuristic should warn when HAVING is used without a matching WHERE filter.
    /// </summary>
    public bool WarnOnHavingWithoutWhere { get; set; } = true;

    /// <summary>
    /// Gets or sets additional words considered temporal when found in a column, alias, comment, or expression.
    /// </summary>
    public ISet<string> TemporalKeywords { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "date",
        "jour",
        "mois",
        "annee",
        "année",
        "semaine",
        "trimestre",
        "periode",
        "période",
        "timestamp",
        "created",
        "updated",
        "creation",
        "modification",
        "time",
        "year",
        "month",
        "week",
        "day"
    };

    /// <summary>
    /// Gets or sets additional words considered identifiers when found in a grouping column.
    /// </summary>
    public ISet<string> IdentifierKeywords { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "iden",
        "identifier",
        "identifiant",
        "code",
        "uuid",
        "guid"
    };

    /// <summary>
    /// Gets or sets SQL function names considered temporal bucketing functions.
    /// </summary>
    public ISet<string> TemporalSqlFunctions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "trunc",
        "to_char",
        "date_trunc",
        "strftime",
        "year",
        "month",
        "week",
        "day"
    };
}
