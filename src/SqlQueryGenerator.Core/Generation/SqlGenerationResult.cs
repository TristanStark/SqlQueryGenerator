namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlGenerationResult dans SQL Query Generator.
/// </summary>
public sealed record SqlGenerationResult
{
    /// <summary>
    /// Stocke la valeur interne Sql.
    /// </summary>
    /// <value>Valeur de Sql.</value>
    public required string Sql { get; init; }
    /// <summary>
    /// Stocke la valeur interne Warnings.
    /// </summary>
    /// <value>Valeur de Warnings.</value>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
