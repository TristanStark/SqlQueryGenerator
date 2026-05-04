using System.Text.RegularExpressions;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

public static partial class SqlIdentifierQuoter
{
    public static string Quote(string identifier, SqlDialect dialect, bool quoteIdentifiers)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifiant SQL vide.", nameof(identifier));
        }

        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('.', parts.Select(part => QuoteSingle(part, dialect, quoteIdentifiers)));
    }

    private static string QuoteSingle(string identifier, SqlDialect dialect, bool quoteIdentifiers)
    {
        var clean = identifier.Trim().Trim('`', '"', '[', ']');
        if (!SafeIdentifierRegex().IsMatch(clean))
        {
            throw new InvalidOperationException($"Identifiant SQL dangereux ou invalide: {identifier}");
        }

        if (!quoteIdentifiers)
        {
            return clean;
        }

        return dialect == SqlDialect.SQLite ? $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$#]*$")]
    private static partial Regex SafeIdentifierRegex();
}
