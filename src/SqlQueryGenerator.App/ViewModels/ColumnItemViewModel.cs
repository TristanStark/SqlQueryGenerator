using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class ColumnItemViewModel : ObservableObject
{
    public ColumnItemViewModel(ColumnDefinition column, string? foreignKeySummary = null)
    {
        Table = column.TableName;
        Column = column.Name;
        DataType = column.DataType ?? string.Empty;
        IsPrimaryKey = column.IsPrimaryKey;
        IsDeclaredForeignKey = column.IsDeclaredForeignKey;
        IsNullable = column.IsNullable;
        Comment = column.Comment ?? string.Empty;
        ForeignKeySummary = foreignKeySummary ?? string.Empty;
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

    public string TypeCategory
    {
        get
        {
            var type = DataType.ToUpperInvariant();
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
