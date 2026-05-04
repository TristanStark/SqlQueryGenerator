using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Models;

public static partial class SqlNameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var value = name.Trim().Trim('`', '"', '[', ']');
        value = MultiWhitespaceRegex().Replace(value, "_");
        return value.ToUpperInvariant();
    }

    public static bool EqualsName(string? left, string? right) => Normalize(left) == Normalize(right);

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

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
