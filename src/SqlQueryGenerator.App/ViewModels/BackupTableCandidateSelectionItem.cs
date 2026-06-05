using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Heuristics;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// UI row used by the DDL import review window to confirm backup-table exclusions.
/// </summary>
public sealed class BackupTableCandidateSelectionItem : ObservableObject
{
    private bool _isExcluded;

    public BackupTableCandidateSelectionItem(BackupTableCandidate candidate)
    {
        Candidate = candidate;
        _isExcluded = candidate.ExcludeByDefault;
    }

    public BackupTableCandidate Candidate { get; }

    public string TableName => Candidate.TableName;

    public string BaseTableName => Candidate.BaseTableName;

    public string DetectionReason => Candidate.DetectionReason;

    public bool IsExcluded
    {
        get => _isExcluded;
        set => SetProperty(ref _isExcluded, value);
    }
}
