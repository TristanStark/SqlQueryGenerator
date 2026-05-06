namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente ColumnDefinition dans SQL Query Generator.
/// </summary>
public sealed record ColumnDefinition
{
    /// <summary>
    /// Stocke la valeur interne TableName.
    /// </summary>
    /// <value>Valeur de TableName.</value>
    public required string TableName { get; init; }
    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public required string Name { get; init; }
    /// <summary>
    /// Stocke la valeur interne DataType.
    /// </summary>
    /// <value>Valeur de DataType.</value>
    public string? DataType { get; init; }
    /// <summary>
    /// Stocke la valeur interne Comment.
    /// </summary>
    /// <value>Valeur de Comment.</value>
    public string? Comment { get; init; }
    /// <summary>
    /// Stocke la valeur interne IsNullable.
    /// </summary>
    /// <value>Valeur de IsNullable.</value>
    public bool IsNullable { get; init; } = true;
    /// <summary>
    /// Stocke la valeur interne IsPrimaryKey.
    /// </summary>
    /// <value>Valeur de IsPrimaryKey.</value>
    public bool IsPrimaryKey { get; init; }
    /// <summary>
    /// Stocke la valeur interne IsDeclaredForeignKey.
    /// </summary>
    /// <value>Valeur de IsDeclaredForeignKey.</value>
    public bool IsDeclaredForeignKey { get; init; }
    /// <summary>
    /// Obtient ou définit NormalizedName.
    /// </summary>
    /// <value>Valeur de NormalizedName.</value>
    public string NormalizedName => SqlNameNormalizer.Normalize(Name);
    /// <summary>
    /// Obtient ou définit QualifiedName.
    /// </summary>
    /// <value>Valeur de QualifiedName.</value>
    public string QualifiedName => $"{TableName}.{Name}";

    /// <summary>
    /// Exécute le traitement ToString.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Comment) ? QualifiedName : $"{QualifiedName} — {Comment}";
    }
}
