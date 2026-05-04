namespace SqlQueryGenerator.Core.Models;

public static class RelationshipKey
{
    public static string For(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return string.Join("|", Normalize(fromTable), Normalize(fromColumn), Normalize(toTable), Normalize(toColumn));
    }

    public static string ReverseFor(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return For(toTable, toColumn, fromTable, fromColumn);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }
}
