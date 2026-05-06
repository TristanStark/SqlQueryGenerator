namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente SqlObjectDisplayName dans SQL Query Generator.
/// </summary>
public static class SqlObjectDisplayName
{
    /// <summary>
    /// Exécute le traitement Table.
    /// </summary>
    /// <param name="fullName">Paramètre fullName.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string Table(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return string.Empty;
        }

        string trimmed = fullName.Trim();
        int dot = trimmed.LastIndexOf('.');
        if (dot <= 0 || dot >= trimmed.Length - 1)
        {
            return trimmed;
        }

        // UI-only cleanup: schema.table stays intact in the query model, but the schema
        // prefix is hidden in common lists to avoid polluting the screen on enterprise DDLs.
        return trimmed[(dot + 1)..];
    }

    /// <summary>
    /// Exécute le traitement QualifiedColumn.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string QualifiedColumn(string? tableName, string? columnName)
    {
        string table = Table(tableName);
        return string.IsNullOrWhiteSpace(table) ? columnName ?? string.Empty : $"{table}.{columnName}";
    }
}
