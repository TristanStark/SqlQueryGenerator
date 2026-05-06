using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlSafety dans SQL Query Generator.
/// </summary>
public static partial class SqlSafety
{
    /// <summary>
    /// Exécute le traitement EnsureSelectExpressionIsSafe.
    /// </summary>
    /// <param name="expression">Paramètre expression.</param>
    public static void EnsureSelectExpressionIsSafe(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException("Expression SQL vide.");
        }

        if (expression.Contains(';', StringComparison.Ordinal)
            || expression.Contains("--", StringComparison.Ordinal)
            || expression.Contains("/*", StringComparison.Ordinal)
            || expression.Contains("*/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expression SQL refusée: les points-virgules et commentaires SQL sont interdits dans les colonnes personnalisées.");
        }

        if (DangerousSqlRegex().IsMatch(expression))
        {
            throw new InvalidOperationException("Expression SQL refusée: mots-clés DDL/DML interdits. Ce générateur ne produit que des SELECT.");
        }
    }

    /// <summary>
    /// Exécute le traitement DangerousSqlRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"\b(DELETE|UPDATE|INSERT|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|CALL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousSqlRegex();
}
