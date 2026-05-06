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
    public TableDefinition(string name, string? schemaName = null, string? comment = null, bool isView = false, string? viewSql = null)
    {
        Name = name.Trim();
        SchemaName = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();
        Comment = comment;
        IsView = isView;
        ViewSql = viewSql;
        Columns = [];
    }

    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; }
    /// <summary>
    /// Stocke la valeur interne SchemaName.
    /// </summary>
    /// <value>Valeur de SchemaName.</value>
    public string? SchemaName { get; }
    /// <summary>
    /// Stocke la valeur interne Comment.
    /// </summary>
    /// <value>Valeur de Comment.</value>
    public string? Comment { get; set; }
    /// <summary>
    /// Stocke la valeur interne IsView.
    /// </summary>
    /// <value>Valeur de IsView.</value>
    public bool IsView { get; }
    /// <summary>
    /// Stocke la valeur interne ViewSql.
    /// </summary>
    /// <value>Valeur de ViewSql.</value>
    public string? ViewSql { get; }
    /// <summary>
    /// Stocke la valeur interne Columns.
    /// </summary>
    /// <value>Valeur de Columns.</value>
    public Collection<ColumnDefinition> Columns { get; }
    /// <summary>
    /// Obtient ou définit FullName.
    /// </summary>
    /// <value>Valeur de FullName.</value>
    public string FullName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    /// <summary>
    /// Exécute le traitement FindColumn.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    public ColumnDefinition? FindColumn(string name)
    {
        string normalized = SqlNameNormalizer.Normalize(name);
        return Columns.FirstOrDefault(c => c.NormalizedName == normalized);
    }

    /// <summary>
    /// Obtient ou définit PrimaryKeys.
    /// </summary>
    /// <value>Valeur de PrimaryKeys.</value>
    public IReadOnlyList<ColumnDefinition> PrimaryKeys => Columns.Where(c => c.IsPrimaryKey).ToArray();

    /// <summary>
    /// Exécute le traitement ToString.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    public override string ToString() => FullName;
}
