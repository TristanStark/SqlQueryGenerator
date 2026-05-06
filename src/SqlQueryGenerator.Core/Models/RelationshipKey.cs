namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente RelationshipKey dans SQL Query Generator.
/// </summary>
public static class RelationshipKey
{
    /// <summary>
    /// Exécute le traitement For.
    /// </summary>
    /// <param name="fromTable">Paramètre fromTable.</param>
    /// <param name="fromColumn">Paramètre fromColumn.</param>
    /// <param name="toTable">Paramètre toTable.</param>
    /// <param name="toColumn">Paramètre toColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string For(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return string.Join("|", Normalize(fromTable), Normalize(fromColumn), Normalize(toTable), Normalize(toColumn));
    }

    /// <summary>
    /// Exécute le traitement ReverseFor.
    /// </summary>
    /// <param name="fromTable">Paramètre fromTable.</param>
    /// <param name="fromColumn">Paramètre fromColumn.</param>
    /// <param name="toTable">Paramètre toTable.</param>
    /// <param name="toColumn">Paramètre toColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string ReverseFor(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return For(toTable, toColumn, fromTable, fromColumn);
    }

    /// <summary>
    /// Exécute le traitement Normalize.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }
}
