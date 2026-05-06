using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente ColumnItemViewModel dans SQL Query Generator.
/// </summary>
public sealed class ColumnItemViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  isExpanded.
    /// </summary>
    /// <value>Valeur de _isExpanded.</value>
    private bool _isExpanded;
    /// <summary>
    /// Initialise une nouvelle instance de ColumnItemViewModel.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="foreignKeySummary">Paramètre foreignKeySummary.</param>
    /// <param name="indexSummary">Paramètre indexSummary.</param>
    /// <param name="isUniqueIndexed">Paramètre isUniqueIndexed.</param>
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

    /// <summary>
    /// Stocke la valeur interne Table.
    /// </summary>
    /// <value>Valeur de Table.</value>
    public string Table { get; }
    /// <summary>
    /// Obtient ou définit TableDisplayName.
    /// </summary>
    /// <value>Valeur de TableDisplayName.</value>
    public string TableDisplayName => SqlObjectDisplayName.Table(Table);
    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public string Column { get; }
    /// <summary>
    /// Stocke la valeur interne DataType.
    /// </summary>
    /// <value>Valeur de DataType.</value>
    public string DataType { get; }
    /// <summary>
    /// Stocke la valeur interne IsPrimaryKey.
    /// </summary>
    /// <value>Valeur de IsPrimaryKey.</value>
    public bool IsPrimaryKey { get; }
    /// <summary>
    /// Stocke la valeur interne IsDeclaredForeignKey.
    /// </summary>
    /// <value>Valeur de IsDeclaredForeignKey.</value>
    public bool IsDeclaredForeignKey { get; }
    /// <summary>
    /// Obtient ou définit IsForeignKey.
    /// </summary>
    /// <value>Valeur de IsForeignKey.</value>
    public bool IsForeignKey => IsDeclaredForeignKey || !string.IsNullOrWhiteSpace(ForeignKeySummary);
    /// <summary>
    /// Stocke la valeur interne IsNullable.
    /// </summary>
    /// <value>Valeur de IsNullable.</value>
    public bool IsNullable { get; }
    /// <summary>
    /// Stocke la valeur interne Comment.
    /// </summary>
    /// <value>Valeur de Comment.</value>
    public string Comment { get; }
    /// <summary>
    /// Stocke la valeur interne ForeignKeySummary.
    /// </summary>
    /// <value>Valeur de ForeignKeySummary.</value>
    public string ForeignKeySummary { get; }
    /// <summary>
    /// Stocke la valeur interne IndexSummary.
    /// </summary>
    /// <value>Valeur de IndexSummary.</value>
    public string IndexSummary { get; }
    /// <summary>
    /// Obtient ou définit IsIndexed.
    /// </summary>
    /// <value>Valeur de IsIndexed.</value>
    public bool IsIndexed => !string.IsNullOrWhiteSpace(IndexSummary);
    /// <summary>
    /// Stocke la valeur interne IsUniqueIndexed.
    /// </summary>
    /// <value>Valeur de IsUniqueIndexed.</value>
    public bool IsUniqueIndexed { get; }
    /// <summary>
    /// Obtient ou définit IndexBadge.
    /// </summary>
    /// <value>Valeur de IndexBadge.</value>
    public string IndexBadge => IsUniqueIndexed ? "UX" : "IX";

    // Required because the TreeView uses a generic TreeViewItem style that binds IsExpanded.
    // Leaf nodes remain collapsed, but exposing this property prevents noisy WPF binding errors.
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
    /// Stocke la valeur interne TypeCategory.
    /// </summary>
    /// <value>Valeur de TypeCategory.</value>
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

    /// <summary>
    /// Obtient ou définit TypeBackground.
    /// </summary>
    /// <value>Valeur de TypeBackground.</value>
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

    /// <summary>
    /// Obtient ou définit TypeForeground.
    /// </summary>
    /// <value>Valeur de TypeForeground.</value>
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

    /// <summary>
    /// Obtient ou définit SearchText.
    /// </summary>
    /// <value>Valeur de SearchText.</value>
    public string SearchText => $"{Table} {TableDisplayName} {Column} {DataType} {Comment} {ForeignKeySummary} {IndexSummary}";

    /// <summary>
    /// Obtient ou définit DisplayName.
    /// </summary>
    /// <value>Valeur de DisplayName.</value>
    public string DisplayName => string.IsNullOrWhiteSpace(Comment)
        ? $"{Column}   [{DataType}]"
        : $"{Column}   [{DataType}] — {Comment}";
}
