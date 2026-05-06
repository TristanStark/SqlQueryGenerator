using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente RelationshipItemViewModel dans SQL Query Generator.
/// </summary>
public sealed class RelationshipItemViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  isEnabled.
    /// </summary>
    /// <value>Valeur de _isEnabled.</value>
    private bool _isEnabled = true;
    /// <summary>
    /// Stocke la valeur interne  isExpanded.
    /// </summary>
    /// <value>Valeur de _isExpanded.</value>
    private bool _isExpanded;

    /// <summary>
    /// Initialise une nouvelle instance de RelationshipItemViewModel.
    /// </summary>
    /// <param name="relationship">Paramètre relationship.</param>
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

    /// <summary>
    /// Stocke la valeur interne FromTable.
    /// </summary>
    /// <value>Valeur de FromTable.</value>
    public string FromTable { get; }
    /// <summary>
    /// Stocke la valeur interne FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public string FromColumn { get; }
    /// <summary>
    /// Stocke la valeur interne ToTable.
    /// </summary>
    /// <value>Valeur de ToTable.</value>
    public string ToTable { get; }
    /// <summary>
    /// Stocke la valeur interne ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public string ToColumn { get; }
    /// <summary>
    /// Stocke la valeur interne Confidence.
    /// </summary>
    /// <value>Valeur de Confidence.</value>
    public double Confidence { get; }
    /// <summary>
    /// Stocke la valeur interne Source.
    /// </summary>
    /// <value>Valeur de Source.</value>
    public string Source { get; }
    /// <summary>
    /// Stocke la valeur interne Reason.
    /// </summary>
    /// <value>Valeur de Reason.</value>
    public string Reason { get; }
    /// <summary>
    /// Stocke la valeur interne Key.
    /// </summary>
    /// <value>Valeur de Key.</value>
    public string Key { get; }
    /// <summary>
    /// Stocke la valeur interne ReverseKey.
    /// </summary>
    /// <value>Valeur de ReverseKey.</value>
    public string ReverseKey { get; }

    // Required because the TreeView uses a generic TreeViewItem style that binds IsExpanded.
    // Relationship rows are leaves, but this avoids WPF BindingExpression noise.
    /// <summary>
    /// Stocke la valeur interne IsExpanded.
    /// </summary>
    /// <value>Valeur de IsExpanded.</value>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// Stocke la valeur interne IsEnabled.
    /// </summary>
    /// <value>Valeur de IsEnabled.</value>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Obtient ou définit FromTableDisplayName.
    /// </summary>
    /// <value>Valeur de FromTableDisplayName.</value>
    public string FromTableDisplayName => SqlObjectDisplayName.Table(FromTable);
    /// <summary>
    /// Obtient ou définit ToTableDisplayName.
    /// </summary>
    /// <value>Valeur de ToTableDisplayName.</value>
    public string ToTableDisplayName => SqlObjectDisplayName.Table(ToTable);
    /// <summary>
    /// Obtient ou définit FromDisplay.
    /// </summary>
    /// <value>Valeur de FromDisplay.</value>
    public string FromDisplay => SqlObjectDisplayName.QualifiedColumn(FromTable, FromColumn);
    /// <summary>
    /// Obtient ou définit ToDisplay.
    /// </summary>
    /// <value>Valeur de ToDisplay.</value>
    public string ToDisplay => SqlObjectDisplayName.QualifiedColumn(ToTable, ToColumn);
    /// <summary>
    /// Obtient ou définit Display.
    /// </summary>
    /// <value>Valeur de Display.</value>
    public string Display => $"{FromDisplay} → {ToDisplay}";
    /// <summary>
    /// Obtient ou définit EnabledText.
    /// </summary>
    /// <value>Valeur de EnabledText.</value>
    public string EnabledText => IsEnabled ? "Auto" : "Off";
}
