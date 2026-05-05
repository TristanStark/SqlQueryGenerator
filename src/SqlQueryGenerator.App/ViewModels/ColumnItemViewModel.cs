using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class ColumnItemViewModel : ObservableObject
{
    private bool _isExpanded;
    public ColumnItemViewModel(ColumnDefinition column, string? foreignKeySummary = null, string? indexSummary = null, bool isUniqueIndexed = false)
    {
        Table = column.TableName;
        Column = column.Name;
        DataType = column.DataType ?? string.Empty;
        IsPrimaryKey = column.IsPrimaryKey;
        IsDeclaredForeignKey = column.IsDeclaredForeignKey;
        IsNullable = column.IsNullable;
        Comment = column.Comment ?? string.Empty;
        ForeignKeySummary = foreignKeySummary ?? string.Empty;
        IndexSummary = indexSummary ?? string.Empty;
        IsUniqueIndexed = isUniqueIndexed;
    }

    public string Table { get; }
    public string Column { get; }
    public string DataType { get; }
    public bool IsPrimaryKey { get; }
    public bool IsDeclaredForeignKey { get; }
    public bool IsForeignKey => IsDeclaredForeignKey || !string.IsNullOrWhiteSpace(ForeignKeySummary);
    public bool IsNullable { get; }
    public string Comment { get; }
    public string ForeignKeySummary { get; }
    public string IndexSummary { get; }
    public bool IsIndexed => !string.IsNullOrWhiteSpace(IndexSummary);
    public bool IsUniqueIndexed { get; }
    public string IndexBadge => IsUniqueIndexed ? "UX" : "IX";

    // Required because the TreeView uses a generic TreeViewItem style that binds IsExpanded.
    // Leaf nodes remain collapsed, but exposing this property prevents noisy WPF binding errors.
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }


    public string TypeCategory
    {
        get
        {
            string type = DataType.ToUpperInvariant();
            if (type.Contains("CHAR") || type.Contains("TEXT") || type.Contains("CLOB") || type.Contains("STRING")) return "TEXT";
            if (type.Contains("INT") || type.Contains("NUMBER") || type.Contains("NUMERIC") || type.Contains("DECIMAL")) return "INTEGER";
            if (type.Contains("REAL") || type.Contains("FLOAT") || type.Contains("DOUBLE") || type.Contains("BINARY_FLOAT") || type.Contains("BINARY_DOUBLE")) return "REAL";
            if (type.Contains("DATE") || type.Contains("TIME")) return "DATE";
            if (type.Contains("BOOL") || type == "BIT") return "BOOL";
            if (type.Contains("BLOB") || type.Contains("RAW") || type.Contains("BINARY")) return "BINARY";
            return string.IsNullOrWhiteSpace(type) ? "?" : type;
        }
    }

    public string TypeBackground => TypeCategory switch
    {
        "TEXT" => "#DBEAFE",
        "INTEGER" => "#DCFCE7",
        "REAL" => "#FEF3C7",
        "DATE" => "#EDE9FE",
        "BOOL" => "#FCE7F3",
        "BINARY" => "#E5E7EB",
        _ => "#F1F5F9"
    };

    public string TypeForeground => TypeCategory switch
    {
        "TEXT" => "#1D4ED8",
        "INTEGER" => "#166534",
        "REAL" => "#92400E",
        "DATE" => "#6D28D9",
        "BOOL" => "#BE185D",
        "BINARY" => "#374151",
        _ => "#334155"
    };

    public string DisplayName => string.IsNullOrWhiteSpace(Comment)
        ? $"{Column}   [{DataType}]"
        : $"{Column}   [{DataType}] — {Comment}";
}
