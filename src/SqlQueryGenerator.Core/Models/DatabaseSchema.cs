using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente DatabaseSchema dans SQL Query Generator.
/// </summary>
public sealed class DatabaseSchema
{
    /// <summary>
    /// Stocke la valeur interne Tables.
    /// </summary>
    /// <value>Valeur de Tables.</value>
    public Collection<TableDefinition> Tables { get; } = [];
    /// <summary>
    /// Obtient ou définit Views.
    /// </summary>
    /// <value>Valeur de Views.</value>
    public IEnumerable<TableDefinition> Views => Tables.Where(t => t.IsView);
    /// <summary>
    /// Obtient ou définit PhysicalTables.
    /// </summary>
    /// <value>Valeur de PhysicalTables.</value>
    public IEnumerable<TableDefinition> PhysicalTables => Tables.Where(t => !t.IsView);
    /// <summary>
    /// Stocke la valeur interne DeclaredForeignKeys.
    /// </summary>
    /// <value>Valeur de DeclaredForeignKeys.</value>
    public Collection<DeclaredForeignKey> DeclaredForeignKeys { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Indexes.
    /// </summary>
    /// <value>Valeur de Indexes.</value>
    public Collection<IndexDefinition> Indexes { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Relationships.
    /// </summary>
    /// <value>Valeur de Relationships.</value>
    public Collection<InferredRelationship> Relationships { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Warnings.
    /// </summary>
    /// <value>Valeur de Warnings.</value>
    public Collection<string> Warnings { get; } = [];

    /// <summary>
    /// Exécute le traitement FindTable.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <returns>Résultat du traitement.</returns>
    public TableDefinition? FindTable(string tableName)
    {
        string normalized = SqlNameNormalizer.Normalize(tableName);
        return Tables.FirstOrDefault(t => SqlNameNormalizer.Normalize(t.Name) == normalized || SqlNameNormalizer.Normalize(t.FullName) == normalized);
    }

    /// <summary>
    /// Exécute le traitement FindColumn.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public ColumnDefinition? FindColumn(string tableName, string columnName)
    {
        return FindTable(tableName)?.FindColumn(columnName);
    }

    /// <summary>
    /// Exécute le traitement IsColumnIndexed.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool IsColumnIndexed(string tableName, string columnName)
    {
        return Indexes.Any(index => SqlNameNormalizer.EqualsName(index.Table, tableName) && index.ContainsColumn(columnName));
    }

    /// <summary>
    /// Exécute le traitement IsColumnUniqueIndexed.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool IsColumnUniqueIndexed(string tableName, string columnName)
    {
        return Indexes.Any(index => index.IsUnique && index.IsSingleColumnOn(tableName, columnName));
    }

    /// <summary>
    /// Exécute le traitement FindIndexesForColumn.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public IReadOnlyList<IndexDefinition> FindIndexesForColumn(string tableName, string columnName)
    {
        return Indexes
            .Where(index => SqlNameNormalizer.EqualsName(index.Table, tableName) && index.ContainsColumn(columnName))
            .OrderByDescending(index => index.IsUnique)
            .ThenBy(index => index.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
