using System.Text;

namespace SqlQueryGenerator.App.Services;

/// <summary>
/// Builds rich schema tooltips for table and column nodes shown in the schema explorer.
/// </summary>
public static class SchemaTooltipBuilder
{
    private const string MissingDocumentationText = "Aucune documentation métier disponible.";

    /// <summary>
    /// Builds a tooltip for a table or view node.
    /// </summary>
    /// <param name="tableName">Full SQL table name.</param>
    /// <param name="displayName">Display table name.</param>
    /// <param name="comment">Optional table comment or imported documentation.</param>
    /// <param name="totalColumnCount">Total number of columns on the table.</param>
    /// <param name="visibleColumnCount">Currently visible column count.</param>
    /// <param name="isView">Whether the object is a view.</param>
    /// <returns>Multiline tooltip text.</returns>
    public static string BuildTableTooltip(
        string tableName,
        string displayName,
        string? comment,
        int totalColumnCount,
        int visibleColumnCount,
        bool isView)
    {
        StringBuilder sb = new();

        sb.AppendLine(isView ? "Vue" : "Table");
        sb.AppendLine($"Nom SQL : {Safe(tableName)}");

        if (!string.Equals(tableName, displayName, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"Affichage : {Safe(displayName)}");
        }

        sb.AppendLine(visibleColumnCount == totalColumnCount
            ? $"Colonnes : {totalColumnCount:N0}"
            : $"Colonnes visibles : {visibleColumnCount:N0}/{totalColumnCount:N0}");

        AppendDocumentationBlock(sb, comment);

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds a tooltip for a column node.
    /// </summary>
    /// <param name="tableName">Full table name.</param>
    /// <param name="tableDisplayName">Display table name.</param>
    /// <param name="columnName">Column name.</param>
    /// <param name="dataType">SQL data type.</param>
    /// <param name="comment">Optional column comment or imported documentation.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="isDeclaredForeignKey">Whether the column is a declared FK.</param>
    /// <param name="foreignKeySummary">Declared or inferred FK summary.</param>
    /// <param name="indexSummary">Index summary.</param>
    /// <param name="isUniqueIndexed">Whether the column has a unique index.</param>
    /// <returns>Multiline tooltip text.</returns>
    public static string BuildColumnTooltip(
        string tableName,
        string tableDisplayName,
        string columnName,
        string? dataType,
        string? comment,
        bool isNullable,
        bool isPrimaryKey,
        bool isDeclaredForeignKey,
        string? foreignKeySummary,
        string? indexSummary,
        bool isUniqueIndexed)
    {
        StringBuilder sb = new();

        sb.AppendLine("Colonne");
        sb.AppendLine($"Nom SQL : {Safe(tableName)}.{Safe(columnName)}");

        if (!string.Equals(tableName, tableDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"Table affichée : {Safe(tableDisplayName)}");
        }

        sb.AppendLine($"Type : {Safe(dataType, fallback: "Type inconnu")}");
        sb.AppendLine($"Nullable : {(isNullable ? "Oui" : "Non")}");

        List<string> badges = [];
        if (isPrimaryKey)
        {
            badges.Add("PK");
        }

        if (isDeclaredForeignKey)
        {
            badges.Add("FK déclarée");
        }
        else if (!string.IsNullOrWhiteSpace(foreignKeySummary))
        {
            badges.Add("FK probable");
        }

        if (!string.IsNullOrWhiteSpace(indexSummary))
        {
            badges.Add(isUniqueIndexed ? "Index unique" : "Index");
        }

        if (badges.Count > 0)
        {
            sb.AppendLine($"Rôles : {string.Join(", ", badges)}");
        }

        if (!string.IsNullOrWhiteSpace(foreignKeySummary))
        {
            sb.AppendLine();
            sb.AppendLine("Relation :");
            sb.AppendLine(foreignKeySummary.Trim());
        }

        if (!string.IsNullOrWhiteSpace(indexSummary))
        {
            sb.AppendLine();
            sb.AppendLine("Index :");
            sb.AppendLine(indexSummary.Trim());
        }

        AppendDocumentationBlock(sb, comment);

        return sb.ToString().TrimEnd();
    }

    private static void AppendDocumentationBlock(StringBuilder sb, string? comment)
    {
        sb.AppendLine();
        sb.AppendLine("Documentation :");
        sb.AppendLine(string.IsNullOrWhiteSpace(comment)
            ? MissingDocumentationText
            : comment.Trim());
    }

    private static string Safe(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
