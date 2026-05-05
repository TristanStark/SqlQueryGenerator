using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;
using System.Collections.ObjectModel;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class TableItemViewModel : ObservableObject
{
    private bool _isExpanded;

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

    private static string LookupSummary(ColumnDefinition column, IReadOnlyDictionary<string, string>? summaries)
    {
        if (summaries is null)
        {
            return string.Empty;
        }

        string key = $"{column.TableName}.{column.Name}";
        return summaries.TryGetValue(key, out string? summary) ? summary : string.Empty;
    }

    private static bool IsInSet(ColumnDefinition column, IReadOnlySet<string>? values)
    {
        if (values is null)
        {
            return false;
        }

        return values.Contains($"{column.TableName}.{column.Name}");
    }
}
