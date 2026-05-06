namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Représente ColumnReference dans SQL Query Generator.
/// </summary>
public sealed record ColumnReference
{
    /// <summary>
    /// Stocke la valeur interne Table.
    /// </summary>
    /// <value>Valeur de Table.</value>
    public required string Table { get; init; }
    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public required string Column { get; init; }
    /// <summary>
    /// Stocke la valeur interne Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string? Alias { get; init; }

    /// <summary>
    /// Obtient ou définit Key.
    /// </summary>
    /// <value>Valeur de Key.</value>
    public string Key => $"{Table}.{Column}";
}
