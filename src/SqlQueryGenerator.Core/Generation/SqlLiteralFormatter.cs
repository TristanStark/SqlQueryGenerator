using System.Globalization;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlLiteralFormatter dans SQL Query Generator.
/// </summary>
public static partial class SqlLiteralFormatter
{
    /// <summary>
    /// Exécute le traitement FormatValue.
    /// </summary>
    /// <param name="raw">Paramètre raw.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string FormatValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "NULL";
        }

        string value = raw.Trim();
        if (value.StartsWith(':') || value.StartsWith('@') || value.StartsWith('?'))
        {
            return value;
        }

        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            || value.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToUpperInvariant();
        }

        if (NumberRegex().IsMatch(value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        if (value.StartsWith('(') && value.EndsWith(')'))
        {
            return value;
        }

        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    /// <summary>
    /// Exécute le traitement FormatRawList.
    /// </summary>
    /// <param name="raw">Paramètre raw.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string FormatRawList(string raw)
    {
        string trimmed = raw.Trim();
        if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
        {
            return trimmed;
        }

        string[] parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return $"({string.Join(", ", parts.Select(FormatValue))})";
    }

    /// <summary>
    /// Exécute le traitement NumberRegex.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    [GeneratedRegex(@"^-?\d+(?:\.\d+)?$")]
    private static partial Regex NumberRegex();
}
