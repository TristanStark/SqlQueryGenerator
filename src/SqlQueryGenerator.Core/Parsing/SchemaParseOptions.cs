namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Représente SchemaParseOptions dans SQL Query Generator.
/// </summary>
public sealed record SchemaParseOptions
{
    /// <summary>
    /// Stocke la valeur interne MaxInputCharacters.
    /// </summary>
    /// <value>Valeur de MaxInputCharacters.</value>
    public int MaxInputCharacters { get; init; } = 15_000_000;
    /// <summary>
    /// Stocke la valeur interne InferRelationships.
    /// </summary>
    /// <value>Valeur de InferRelationships.</value>
    public bool InferRelationships { get; init; } = true;
    /// <summary>
    /// Stocke la valeur interne KeepCaseSensitiveIdentifiers.
    /// </summary>
    /// <value>Valeur de KeepCaseSensitiveIdentifiers.</value>
    public bool KeepCaseSensitiveIdentifiers { get; init; }
}
