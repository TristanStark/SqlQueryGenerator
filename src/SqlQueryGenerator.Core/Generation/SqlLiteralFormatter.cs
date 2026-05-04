using System.Globalization;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

public static partial class SqlLiteralFormatter
{
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

        return value.StartsWith('(') && value.EndsWith(')') ? value : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

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

    [GeneratedRegex(@"^-?\d+(?:\.\d+)?$")]
    private static partial Regex NumberRegex();
}
