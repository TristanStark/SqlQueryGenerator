using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;
using System.Collections.ObjectModel;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente TableItemViewModel dans SQL Query Generator.
/// </summary>
public sealed class TableItemViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  isExpanded.
    /// </summary>
    /// <value>Valeur de _isExpanded.</value>
    private bool _isExpanded;

    /// <summary>
    /// Initialise une nouvelle instance de TableItemViewModel.
    /// </summary>
    public TableItemViewModel(
        TableDefinition table,
        IEnumerable<ColumnDefinition>? visibleColumns = null,
        IReadOnlyDictionary<string, string>? foreignKeySummaries = null,
        IReadOnlyDictionary<string, string>? indexSummaries = null,
        IReadOnlySet<string>? uniqueIndexColumns = null)
    {
        Name = table.FullName;
        Comment = table.Comment ?? string.Empty;
        IEnumerable<ColumnDefinition> sourceColumns = visibleColumns ?? table.Columns;
        Columns = new ObservableCollection<ColumnItemViewModel>(sourceColumns
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ColumnItemViewModel(
                c,
                LookupSummary(c, foreignKeySummaries),
                LookupSummary(c, indexSummaries),
                IsInSet(c, uniqueIndexColumns))));
    }

    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; }
    /// <summary>
    /// Obtient ou définit DisplayName.
    /// </summary>
    /// <value>Valeur de DisplayName.</value>
    public string DisplayName => SqlObjectDisplayName.Table(Name);
    /// <summary>
    /// Stocke la valeur interne Comment.
    /// </summary>
    /// <value>Valeur de Comment.</value>
    public string Comment { get; }
    /// <summary>
    /// Stocke la valeur interne Columns.
    /// </summary>
    /// <value>Valeur de Columns.</value>
    public ObservableCollection<ColumnItemViewModel> Columns { get; }

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
    /// Obtient ou définit ColumnCount.
    /// </summary>
    /// <value>Valeur de ColumnCount.</value>
    public int ColumnCount => Columns.Count;

    /// <summary>
    /// Obtient ou définit HeaderText.
    /// </summary>
    /// <value>Valeur de HeaderText.</value>
    public string HeaderText => string.IsNullOrWhiteSpace(Comment)
        ? $"{DisplayName} ({ColumnCount})"
        : $"{DisplayName} ({ColumnCount}) — {Comment}";

    /// <summary>
    /// Exécute le traitement LookupSummary.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="summaries">Paramètre summaries.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string LookupSummary(ColumnDefinition column, IReadOnlyDictionary<string, string>? summaries)
    {
        if (summaries is null)
        {
            return string.Empty;
        }

        string key = $"{column.TableName}.{column.Name}";
        return summaries.TryGetValue(key, out string? summary) ? summary : string.Empty;
    }

    /// <summary>
    /// Exécute le traitement IsInSet.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="values">Paramètre values.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsInSet(ColumnDefinition column, IReadOnlySet<string>? values)
    {
        if (values is null)
        {
            return false;
        }

        return values.Contains($"{column.TableName}.{column.Name}");
    }
}
