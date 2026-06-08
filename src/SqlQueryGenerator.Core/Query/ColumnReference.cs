namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Représente une colonne sélectionnée dans une requête SQL.
/// </summary>
public sealed record ColumnReference
{
    /// <summary>
    /// Obtient ou initialise la table contenant la colonne.
    /// </summary>
    /// <value>Nom de table complet utilisé par le générateur SQL.</value>
    public required string Table { get; init; }

    /// <summary>
    /// Obtient ou initialise le nom de la colonne.
    /// </summary>
    /// <value>Nom de colonne SQL.</value>
    public required string Column { get; init; }

    /// <summary>
    /// Obtient ou initialise l'alias optionnel de sortie.
    /// </summary>
    /// <value>Alias SQL optionnel.</value>
    public string? Alias { get; init; }

    /// <summary>
    /// Obtient ou initialise si la colonne peut produire une valeur <c>NULL</c>.
    /// </summary>
    /// <value><c>true</c> par défaut ; <c>false</c> force une valeur par défaut via NVL/COALESCE.</value>
    public bool NullAllowed { get; init; } = true;

    /// <summary>
    /// Obtient ou initialise si la colonne doit être formatée en longueur fixe.
    /// </summary>
    /// <value><c>true</c> pour produire une chaîne de longueur fixe.</value>
    public bool UseFixedLength { get; init; }

    /// <summary>
    /// Obtient ou initialise la longueur fixe souhaitée.
    /// </summary>
    /// <value>Nombre de caractères attendus dans le champ de sortie.</value>
    public int? FixedLength { get; init; }

    /// <summary>
    /// Obtient la clé logique de la colonne.
    /// </summary>
    /// <value>Clé sous forme <c>table.colonne</c>.</value>
    public string Key => $"{Table}.{Column}";
}
