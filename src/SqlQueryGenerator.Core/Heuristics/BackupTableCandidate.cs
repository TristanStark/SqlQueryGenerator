namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Represents a likely backup/archive/save table detected during DDL import review.
/// </summary>
public sealed class BackupTableCandidate
{
    /// <summary>
    /// Fully qualified table name of the detected candidate.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Existing base table that makes this candidate plausible.
    /// </summary>
    public required string BaseTableName { get; init; }

    /// <summary>
    /// Human-readable explanation of why the table was flagged.
    /// </summary>
    public required string DetectionReason { get; init; }

    /// <summary>
    /// Whether the candidate should be excluded by default in the review UI.
    /// </summary>
    public bool ExcludeByDefault { get; init; } = true;
}
