using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class RelationshipItemViewModel : ObservableObject
{
    private bool _isEnabled = true;
    private bool _isExpanded;

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

    // Required because the TreeView uses a generic TreeViewItem style that binds IsExpanded.
    // Relationship rows are leaves, but this avoids WPF BindingExpression noise.
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

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
