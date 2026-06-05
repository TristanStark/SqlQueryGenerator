using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Result of applying a reviewed backup-table exclusion list to a parsed schema.
/// </summary>
public sealed class SchemaImportFilterResult
{
    /// <summary>
    /// Final schema kept after applying exclusions.
    /// </summary>
    public required DatabaseSchema Schema { get; init; }

    /// <summary>
    /// All candidates detected during the review step.
    /// </summary>
    public IReadOnlyList<BackupTableCandidate> DetectedCandidates { get; init; } = Array.Empty<BackupTableCandidate>();

    /// <summary>
    /// Candidates explicitly excluded by the user.
    /// </summary>
    public IReadOnlyList<BackupTableCandidate> ExcludedCandidates { get; init; } = Array.Empty<BackupTableCandidate>();

    /// <summary>
    /// Candidates kept in the imported schema.
    /// </summary>
    public IReadOnlyList<BackupTableCandidate> KeptCandidates { get; init; } = Array.Empty<BackupTableCandidate>();
}
