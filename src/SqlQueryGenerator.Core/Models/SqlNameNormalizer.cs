using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Models;

/// <summary>
/// Représente SqlNameNormalizer dans SQL Query Generator.
/// </summary>
public static partial class SqlNameNormalizer
{
    /// <summary>
    /// Exécute le traitement Normalize.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string value = name.Trim().Trim('`', '"', '[', ']');
        value = MultiWhitespaceRegex().Replace(value, "_");
        return value.ToUpperInvariant();
    }

    /// <summary>
    /// Exécute le traitement EqualsName.
    /// </summary>
    /// <param name="left">Paramètre left.</param>
    /// <param name="right">Paramètre right.</param>
    /// <returns>Résultat du traitement.</returns>
    public static bool EqualsName(string? left, string? right) => Normalize(left) == Normalize(right);

    /// <summary>
    /// Exécute le traitement StripDecorations.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string StripDecorations(string name)
    {
        return Normalize(name)
            .Replace("_IDEN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_ID", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("IDEN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("ID", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("PK_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("FK_", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement MultiWhitespaceRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
