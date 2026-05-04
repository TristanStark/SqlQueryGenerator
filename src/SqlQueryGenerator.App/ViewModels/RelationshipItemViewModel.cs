using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class RelationshipItemViewModel : ObservableObject
{
    private bool _isEnabled = true;

    public RelationshipItemViewModel(InferredRelationship relationship)
    {
        FromTable = relationship.FromTable;
        FromColumn = relationship.FromColumn;
        ToTable = relationship.ToTable;
        ToColumn = relationship.ToColumn;
        Confidence = relationship.Confidence;
        Source = relationship.Source.ToString();
        Reason = relationship.Reason;
        Key = relationship.Key;
        ReverseKey = relationship.ReverseKey;
    }

    public string FromTable { get; }
    public string FromColumn { get; }
    public string ToTable { get; }
    public string ToColumn { get; }
    public double Confidence { get; }
    public string Source { get; }
    public string Reason { get; }
    public string Key { get; }
    public string ReverseKey { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string FromDisplay => $"{FromTable}.{FromColumn}";
    public string ToDisplay => $"{ToTable}.{ToColumn}";
    public string Display => $"{FromDisplay} → {ToDisplay}";
    public string EnabledText => IsEnabled ? "Auto" : "Off";
}
