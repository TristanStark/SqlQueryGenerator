using SqlQueryGenerator.Core.Query;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlIdentifierQuoter dans SQL Query Generator.
/// </summary>
public static partial class SqlIdentifierQuoter
{
    /// <summary>
    /// Exécute le traitement Quote.
    /// </summary>
    /// <param name="identifier">Paramètre identifier.</param>
    /// <param name="dialect">Paramètre dialect.</param>
    /// <param name="quoteIdentifiers">Paramètre quoteIdentifiers.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string Quote(string identifier, SqlDialect dialect, bool quoteIdentifiers)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifiant SQL vide.", nameof(identifier));
        }

        string[] parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('.', parts.Select(part => QuoteSingle(part, dialect, quoteIdentifiers)));
    }

    /// <summary>
    /// Exécute le traitement QuoteSingle.
    /// </summary>
    /// <param name="identifier">Paramètre identifier.</param>
    /// <param name="dialect">Paramètre dialect.</param>
    /// <param name="quoteIdentifiers">Paramètre quoteIdentifiers.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string QuoteSingle(string identifier, SqlDialect dialect, bool quoteIdentifiers)
    {
        string clean = identifier.Trim().Trim('`', '"', '[', ']');
        if (string.IsNullOrWhiteSpace(clean))
        {
            throw new InvalidOperationException($"Identifiant SQL dangereux ou invalide: {identifier}");
        }

        if (SafeIdentifierRegex().IsMatch(clean))
        {
            return quoteIdentifiers ? QuoteDelimited(clean, dialect) : clean;
        }

        // v25: aliases such as "Âge moyen" or "Total payé" must be legal.
        // They are emitted as delimited identifiers even when global identifier quoting is off.
        if (RelaxedDelimitedIdentifierRegex().IsMatch(clean) && !ContainsSqlStructuralToken(clean))
        {
            return QuoteDelimited(clean, dialect);
        }

        throw new InvalidOperationException($"Identifiant SQL dangereux ou invalide: {identifier}");
    }

    /// <summary>
    /// Exécute le traitement QuoteDelimited.
    /// </summary>
    /// <param name="clean">Paramètre clean.</param>
    /// <param name="dialect">Paramètre dialect.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string QuoteDelimited(string clean, SqlDialect dialect)
    {
        return dialect == SqlDialect.SQLite
            ? $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>
    /// Exécute le traitement ContainsSqlStructuralToken.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool ContainsSqlStructuralToken(string value)
    {
        return value.Contains(';')
            || value.Contains("--", StringComparison.Ordinal)
            || value.Contains("/*", StringComparison.Ordinal)
            || value.Contains("*/", StringComparison.Ordinal)
            || value.Contains('(')
            || value.Contains(')')
            || value.Contains(',')
            || value.Contains('\'');
    }

    /// <summary>
    /// Exécute le traitement SafeIdentifierRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$#]*$")]
    private static partial Regex SafeIdentifierRegex();

    /// <summary>
    /// Exécute le traitement RelaxedDelimitedIdentifierRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_$# \-]{0,127}$")]
    private static partial Regex RelaxedDelimitedIdentifierRegex();
}
