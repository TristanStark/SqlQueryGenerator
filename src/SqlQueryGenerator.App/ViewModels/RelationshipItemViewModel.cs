using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class RelationshipItemViewModel
{
    public RelationshipItemViewModel(InferredRelationship relationship)
    {
        FromTable = relationship.FromTable;
        FromColumn = relationship.FromColumn;
        ToTable = relationship.ToTable;
        ToColumn = relationship.ToColumn;
        Confidence = relationship.Confidence;
        Source = relationship.Source.ToString();
        Reason = relationship.Reason;
    }

    public string FromTable { get; }
    public string FromColumn { get; }
    public string ToTable { get; }
    public string ToColumn { get; }
    public double Confidence { get; }
    public string Source { get; }
    public string Reason { get; }
    public string Display => $"{FromTable}.{FromColumn} → {ToTable}.{ToColumn}";
}
