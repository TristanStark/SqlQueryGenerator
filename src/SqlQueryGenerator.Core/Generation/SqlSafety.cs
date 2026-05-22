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
    /// Validates that a raw SQL preset is a single read-only SELECT statement that can safely be embedded or displayed.
    /// </summary>
    /// <param name="sql">Raw SQL entered or imported by the user.</param>
    public static void EnsureSelectQueryIsSafe(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("Requête SQL vide.");
        }

        string trimmed = sql.Trim();
        if (trimmed.Contains("--", StringComparison.Ordinal)
            || trimmed.Contains("/*", StringComparison.Ordinal)
            || trimmed.Contains("*/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Requête SQL refusée: les commentaires SQL sont interdits dans les presets SQL bruts.");
        }

        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Requête SQL refusée: seuls SELECT et WITH ... SELECT sont autorisés.");
        }

        string withoutTrailingSemicolon = trimmed.EndsWith(';') ? trimmed[..^1].TrimEnd() : trimmed;
        if (withoutTrailingSemicolon.Contains(';', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Requête SQL refusée: un preset SQL brut doit contenir une seule requête SELECT.");
        }

        if (DangerousSqlRegex().IsMatch(withoutTrailingSemicolon))
        {
            throw new InvalidOperationException("Requête SQL refusée: mots-clés DDL/DML interdits. Ce générateur ne produit que des SELECT.");
        }
    }

    /// <summary>
    /// Removes one optional trailing semicolon from a raw SELECT after it has been validated.
    /// </summary>
    /// <param name="sql">Raw SQL to normalize.</param>
    /// <returns>SQL text without a final semicolon and without surrounding whitespace.</returns>
    public static string NormalizeRawSelectQuery(string sql)
    {
        EnsureSelectQueryIsSafe(sql);
        string trimmed = sql.Trim();
        return trimmed.EndsWith(';') ? trimmed[..^1].TrimEnd() : trimmed;
    }

    /// <summary>
    /// Exécute le traitement DangerousSqlRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"\b(DELETE|UPDATE|INSERT|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|CALL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousSqlRegex();
}
