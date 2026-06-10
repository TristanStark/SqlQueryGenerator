using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;
using System.Collections.ObjectModel;
using SqlQueryGenerator.App.Services;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// View model representing one table or view in the left schema tree.
/// </summary>
public sealed class TableItemViewModel : ObservableObject
{
    /// <summary>
    /// Stores whether the table node is expanded in the tree.
    /// </summary>
    /// <value><c>true</c> when the table node is expanded; otherwise <c>false</c>.</value>
    private bool _isExpanded;

    /// <summary>
    /// Stores the complete, stable list of column view models for the table.
    /// </summary>
    /// <value>All columns belonging to the table, reused across searches to avoid WPF memory churn.</value>
    private readonly IReadOnlyList<ColumnItemViewModel> _allColumns;

    /// <summary>
    /// Stores whether the represented schema object is a view.
    /// </summary>
    /// <value><c>true</c> for a SQL view; otherwise <c>false</c>.</value>
    private readonly bool _isView;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableItemViewModel"/> class from raw schema columns.
    /// </summary>
    /// <param name="table">Schema table represented by this node.</param>
    /// <param name="visibleColumns">Optional subset of columns visible in the tree.</param>
    /// <param name="foreignKeySummaries">Optional cached FK summaries keyed by qualified column name.</param>
    /// <param name="indexSummaries">Optional cached index summaries keyed by qualified column name.</param>
    /// <param name="uniqueIndexColumns">Optional set of columns backed by a unique index.</param>
    public TableItemViewModel(
        TableDefinition table,
        IEnumerable<ColumnDefinition>? visibleColumns = null,
        IReadOnlyDictionary<string, string>? foreignKeySummaries = null,
        IReadOnlyDictionary<string, string>? indexSummaries = null,
        IReadOnlySet<string>? uniqueIndexColumns = null)
        : this(
            table,
            (visibleColumns ?? table.Columns)
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ColumnItemViewModel(
                    c,
                    LookupSummary(c, foreignKeySummaries),
                    LookupSummary(c, indexSummaries),
                    IsInSet(c, uniqueIndexColumns))))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableItemViewModel"/> class from stable prebuilt column view models.
    /// </summary>
    /// <param name="table">Schema table represented by this node.</param>
    /// <param name="columns">Stable column view models belonging to the table.</param>
    public TableItemViewModel(TableDefinition table, IEnumerable<ColumnItemViewModel> columns)
    {
        Name = table.FullName;
        Comment = table.Comment ?? string.Empty;
        _isView = table.IsView;
        _allColumns = columns.OrderBy(c => c.Column, StringComparer.OrdinalIgnoreCase).ToArray();
        Columns = new ObservableCollection<ColumnItemViewModel>(_allColumns);
    }

    /// <summary>
    /// Gets whether this schema object is a view.
    /// </summary>
    /// <value><c>true</c> when this node represents a view.</value>
    public bool IsView => _isView;

    /// <summary>
    /// Gets whether this table has imported or parsed documentation.
    /// </summary>
    /// <value><c>true</c> when a non-empty comment is available.</value>
    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);

    /// <summary>
    /// Gets the rich tooltip text shown when hovering the table in the schema tree.
    /// </summary>
    /// <value>Multiline tooltip with table metadata and documentation.</value>
    public string TooltipText => SchemaTooltipBuilder.BuildTableTooltip(
        Name,
        DisplayName,
        Comment,
        TotalColumnCount,
        ColumnCount,
        IsView);

    /// <summary>
    /// Gets the complete database table name, including schema prefix when present.
    /// </summary>
    /// <value>Fully qualified table name used by generated SQL.</value>
    public string Name { get; }

    /// <summary>
    /// Gets the display name shown in the UI, without noisy schema prefix when possible.
    /// </summary>
    /// <value>Human-readable table name.</value>
    public string DisplayName => SqlObjectDisplayName.Table(Name);

    /// <summary>
    /// Gets the imported or parsed documentation comment for the table.
    /// </summary>
    /// <value>Table description, or an empty string when no documentation is available.</value>
    public string Comment { get; }

    /// <summary>
    /// Gets the currently visible columns for this table node.
    /// </summary>
    /// <value>Subset displayed by the tree after applying the current search.</value>
    public ObservableCollection<ColumnItemViewModel> Columns { get; }

    /// <summary>
    /// Gets or sets whether the table node is expanded in the TreeView.
    /// </summary>
    /// <value><c>true</c> when expanded; otherwise <c>false</c>.</value>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// Gets the number of currently visible columns.
    /// </summary>
    /// <value>Visible column count after filtering.</value>
    public int ColumnCount => Columns.Count;

    /// <summary>
    /// Gets the total number of columns on the table before filtering.
    /// </summary>
    /// <value>Total column count.</value>
    public int TotalColumnCount => _allColumns.Count;

    /// <summary>
    /// Gets all stable column view models for the table.
    /// </summary>
    /// <value>Complete table column list reused across searches.</value>
    public IReadOnlyList<ColumnItemViewModel> AllColumns => _allColumns;

    /// <summary>
    /// Gets the text shown in table headers and tooltips.
    /// </summary>
    /// <value>Display name, column counts and optional table documentation.</value>
    public string HeaderText
    {
        get
        {
            string countText = ColumnCount == TotalColumnCount ? ColumnCount.ToString() : $"{ColumnCount}/{TotalColumnCount}";
            return string.IsNullOrWhiteSpace(Comment)
                ? $"{DisplayName} ({countText})"
                : $"{DisplayName} ({countText}) — {Comment}";
        }
    }

    /// <summary>
    /// Replaces the visible column subset without recreating column view models.
    /// </summary>
    /// <param name="columns">Columns to show for the current search/filter state.</param>
    public void SetVisibleColumns(IEnumerable<ColumnItemViewModel> columns)
    {
        ColumnItemViewModel[] nextColumns = columns.ToArray();
        if (Columns.Count == nextColumns.Length && Columns.SequenceEqual(nextColumns))
        {
            return;
        }

        Columns.Clear();
        foreach (ColumnItemViewModel column in nextColumns)
        {
            Columns.Add(column);
        }

        OnPropertyChanged(nameof(ColumnCount));
        OnPropertyChanged(nameof(HeaderText));
    }

    /// <summary>
    /// Restores all columns as visible for the table node.
    /// </summary>
    public void ResetVisibleColumns() => SetVisibleColumns(_allColumns);

    /// <summary>
    /// Looks up a cached textual summary for the supplied column.
    /// </summary>
    /// <param name="column">Column to look up.</param>
    /// <param name="summaries">Summary cache keyed by fully qualified column name.</param>
    /// <returns>Cached summary, or an empty string.</returns>
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
    /// Checks whether a column is present in a cached qualified-name set.
    /// </summary>
    /// <param name="column">Column to test.</param>
    /// <param name="values">Qualified-name set.</param>
    /// <returns><c>true</c> when the set contains the column; otherwise <c>false</c>.</returns>
    private static bool IsInSet(ColumnDefinition column, IReadOnlySet<string>? values)
    {
        if (values is null)
        {
            return false;
        }

        return values.Contains($"{column.TableName}.{column.Name}");
    }
}
