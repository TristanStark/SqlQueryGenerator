using System.Collections.ObjectModel;
using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class TableItemViewModel : ObservableObject
{
    private bool _isExpanded;

    public TableItemViewModel(
        TableDefinition table,
        IEnumerable<ColumnDefinition>? visibleColumns = null,
        IReadOnlyDictionary<string, string>? foreignKeySummaries = null)
    {
        Name = table.FullName;
        Comment = table.Comment ?? string.Empty;
        var sourceColumns = visibleColumns ?? table.Columns;
        Columns = new ObservableCollection<ColumnItemViewModel>(sourceColumns
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ColumnItemViewModel(c, LookupForeignKeySummary(c, foreignKeySummaries))));
    }

    public string Name { get; }
    public string Comment { get; }
    public ObservableCollection<ColumnItemViewModel> Columns { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int ColumnCount => Columns.Count;

    public string HeaderText => string.IsNullOrWhiteSpace(Comment)
        ? $"{Name} ({ColumnCount})"
        : $"{Name} ({ColumnCount}) — {Comment}";

    private static string LookupForeignKeySummary(ColumnDefinition column, IReadOnlyDictionary<string, string>? foreignKeySummaries)
    {
        if (foreignKeySummaries is null)
        {
            return string.Empty;
        }

        var key = $"{column.TableName}.{column.Name}";
        return foreignKeySummaries.TryGetValue(key, out var summary) ? summary : string.Empty;
    }
}
