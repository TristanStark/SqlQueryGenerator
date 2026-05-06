using SqlQueryGenerator.App.Infrastructure;
using System.Collections.ObjectModel;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente RelationshipGroupViewModel dans SQL Query Generator.
/// </summary>
public sealed class RelationshipGroupViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  isExpanded.
    /// </summary>
    /// <value>Valeur de _isExpanded.</value>
    private bool _isExpanded;

    /// <summary>
    /// Initialise une nouvelle instance de RelationshipGroupViewModel.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <param name="relationships">Paramètre relationships.</param>
    public RelationshipGroupViewModel(string name, IEnumerable<RelationshipItemViewModel> relationships)
    {
        Name = name;
        Relationships = new ObservableCollection<RelationshipItemViewModel>(relationships);
    }

    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; }
    /// <summary>
    /// Stocke la valeur interne Relationships.
    /// </summary>
    /// <value>Valeur de Relationships.</value>
    public ObservableCollection<RelationshipItemViewModel> Relationships { get; }
    /// <summary>
    /// Obtient ou définit Count.
    /// </summary>
    /// <value>Valeur de Count.</value>
    public int Count => Relationships.Count;

    /// <summary>
    /// Stocke la valeur interne IsExpanded.
    /// </summary>
    /// <value>Valeur de IsExpanded.</value>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
