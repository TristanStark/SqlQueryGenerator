namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente DeclaredForeignKey dans SQL Query Generator.
/// </summary>
public sealed record DeclaredForeignKey
{
    /// <summary>
    /// Stocke la valeur interne FromTable.
    /// </summary>
    /// <value>Valeur de FromTable.</value>
    public required string FromTable { get; init; }
    /// <summary>
    /// Stocke la valeur interne FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public required string FromColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne ToTable.
    /// </summary>
    /// <value>Valeur de ToTable.</value>
    public required string ToTable { get; init; }
    /// <summary>
    /// Stocke la valeur interne ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public required string ToColumn { get; init; }
    /// <summary>
    /// Stocke la valeur interne ConstraintName.
    /// </summary>
    /// <value>Valeur de ConstraintName.</value>
    public string? ConstraintName { get; init; }
}
