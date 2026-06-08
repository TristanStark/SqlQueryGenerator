using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente TableDefinition dans SQL Query Generator.
/// </summary>
public sealed class TableDefinition
{
    /// <summary>
    /// Initialise une nouvelle instance de TableDefinition.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <param name="schemaName">Paramètre schemaName.</param>
    /// <param name="comment">Paramètre comment.</param>
    /// <param name="isView">Paramètre isView.</param>
    /// <param name="viewSql">Paramètre viewSql.</param>
    /// <param name="isMaterializedView">Indique si l'objet est une vue matérialisée.</param>
    public TableDefinition(
        string name,
        string? schemaName = null,
        string? comment = null,
        bool isView = false,
        string? viewSql = null,
        bool isMaterializedView = false)
    {
        Name = name.Trim();
        SchemaName = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();
        Comment = comment;
        IsMaterializedView = isMaterializedView;
        IsView = isView || isMaterializedView;
        ViewSql = viewSql;
        Columns = [];
    }

    public string Name { get; }

    public string? SchemaName { get; }

    public string? Comment { get; set; }

    public bool IsView { get; }

    /// <summary>
    /// Gets whether this schema object is a materialized view.
    /// </summary>
    public bool IsMaterializedView { get; }

    public string? ViewSql { get; }

    public Collection<ColumnDefinition> Columns { get; }

    public string FullName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    public ColumnDefinition? FindColumn(string name)
    {
        string normalized = SqlNameNormalizer.Normalize(name);
        return Columns.FirstOrDefault(c => c.NormalizedName == normalized);
    }

    public IReadOnlyList<ColumnDefinition> PrimaryKeys => Columns.Where(c => c.IsPrimaryKey).ToArray();

    public override string ToString() => FullName;
}
