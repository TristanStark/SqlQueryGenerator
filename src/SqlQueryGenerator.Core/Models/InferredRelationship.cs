namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Liste les valeurs possibles de RelationshipSource.
/// </summary>
public enum RelationshipSource
{
    /// <summary>
    /// Valeur DeclaredForeignKey de l'énumération.
    /// </summary>
    DeclaredForeignKey,
    /// <summary>
    /// Valeur SameColumnPrimaryKey de l'énumération.
    /// </summary>
    SameColumnPrimaryKey,
    /// <summary>
    /// Valeur SameColumnName de l'énumération.
    /// </summary>
    SameColumnName,
    /// <summary>
    /// Valeur TableNameColumnPattern de l'énumération.
    /// </summary>
    TableNameColumnPattern,
    /// <summary>
    /// Valeur CompositeTablePattern de l'énumération.
    /// </summary>
    CompositeTablePattern,
    /// <summary>
    /// Valeur CommentSimilarity de l'énumération.
    /// </summary>
    CommentSimilarity
}

/// <summary>
/// Représente InferredRelationship dans SQL Query Generator.
/// </summary>
public sealed record InferredRelationship
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
    /// Stocke la valeur interne Confidence.
    /// </summary>
    /// <value>Valeur de Confidence.</value>
    public required double Confidence { get; init; }
    /// <summary>
    /// Stocke la valeur interne Source.
    /// </summary>
    /// <value>Valeur de Source.</value>
    public required RelationshipSource Source { get; init; }
    /// <summary>
    /// Stocke la valeur interne Reason.
    /// </summary>
    /// <value>Valeur de Reason.</value>
    public required string Reason { get; init; }

    /// <summary>
    /// Obtient ou définit Key.
    /// </summary>
    /// <value>Valeur de Key.</value>
    public string Key => RelationshipKey.For(FromTable, FromColumn, ToTable, ToColumn);
    /// <summary>
    /// Obtient ou définit ReverseKey.
    /// </summary>
    /// <value>Valeur de ReverseKey.</value>
    public string ReverseKey => RelationshipKey.ReverseFor(FromTable, FromColumn, ToTable, ToColumn);

    /// <summary>
    /// Obtient ou définit DisplayName.
    /// </summary>
    /// <value>Valeur de DisplayName.</value>
    public string DisplayName => $"{FromTable}.{FromColumn} → {ToTable}.{ToColumn} ({Confidence:P0})";

    /// <summary>
    /// Exécute le traitement Connects.
    /// </summary>
    /// <param name="leftTable">Paramètre leftTable.</param>
    /// <param name="rightTable">Paramètre rightTable.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool Connects(string leftTable, string rightTable)
    {
        return (SqlNameNormalizer.EqualsName(FromTable, leftTable) && SqlNameNormalizer.EqualsName(ToTable, rightTable))
            || (SqlNameNormalizer.EqualsName(FromTable, rightTable) && SqlNameNormalizer.EqualsName(ToTable, leftTable));
    }
}
