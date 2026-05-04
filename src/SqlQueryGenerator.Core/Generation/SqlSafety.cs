using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

public static partial class SqlSafety
{
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

    [GeneratedRegex(@"\b(DELETE|UPDATE|INSERT|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|CALL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousSqlRegex();
}
