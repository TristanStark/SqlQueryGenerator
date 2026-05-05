using SqlQueryGenerator.App.Infrastructure;
using System.Collections.ObjectModel;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class RelationshipGroupViewModel : ObservableObject
{
    private bool _isExpanded;

    public RelationshipGroupViewModel(string name, IEnumerable<RelationshipItemViewModel> relationships)
    {
        Name = name;
        Relationships = new ObservableCollection<RelationshipItemViewModel>(relationships);
    }

    public string Name { get; }
    public ObservableCollection<RelationshipItemViewModel> Relationships { get; }
    public int Count => Relationships.Count;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
