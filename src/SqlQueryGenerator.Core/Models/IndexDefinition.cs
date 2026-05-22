using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente IndexDefinition dans SQL Query Generator.
/// </summary>
public sealed class IndexDefinition
{
    /// <summary>
    /// Initialise une nouvelle instance de IndexDefinition.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <param name="table">Paramètre table.</param>
    /// <param name="isUnique">Paramètre isUnique.</param>
    /// <param name="columns">Paramètre columns.</param>
    public IndexDefinition(string name, string table, bool isUnique, IEnumerable<string> columns)
    {
        Name = name.Trim();
        Table = table.Trim();
        IsUnique = isUnique;
        Columns = new Collection<string>(columns.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList());
    }

    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; }
    /// <summary>
    /// Stocke la valeur interne Table.
    /// </summary>
    /// <value>Valeur de Table.</value>
    public string Table { get; }
    /// <summary>
    /// Stocke la valeur interne IsUnique.
    /// </summary>
    /// <value>Valeur de IsUnique.</value>
    public bool IsUnique { get; }
    /// <summary>
    /// Stocke la valeur interne Columns.
    /// </summary>
    /// <value>Valeur de Columns.</value>
    public Collection<string> Columns { get; }

    /// <summary>
    /// Exécute le traitement ContainsColumn.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool ContainsColumn(string columnName)
    {
        string normalized = SqlNameNormalizer.Normalize(columnName);
        return Columns.Any(c => SqlNameNormalizer.Normalize(c) == normalized);
    }

    /// <summary>
    /// Exécute le traitement IsSingleColumnOn.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool IsSingleColumnOn(string tableName, string columnName)
    {
        return SqlNameNormalizer.EqualsName(Table, tableName)
            && Columns.Count == 1
            && ContainsColumn(columnName);
    }
}
