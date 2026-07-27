using System.IO;

namespace SqlQueryGenerator.App.Services;

/// <summary>
/// Supported file types that can be dropped on the main window.
/// </summary>
public enum DroppedFileKind
{
    Unsupported,
    RawSql,
    SqlSchema,
    SavedQuery
}

/// <summary>
/// Classifies files dropped on the application without coupling the logic to WPF.
/// </summary>
public static class DroppedFileClassifier
{
    private const int MaxSqlInspectionCharacters = 128 * 1024;

    /// <summary>
    /// Returns whether the path has an extension supported by the main window.
    /// </summary>
    public static bool IsSupportedPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return filePath.EndsWith(".sqlqg.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Classifies one existing dropped file.
    /// </summary>
    public static DroppedFileKind Classify(string filePath)
    {
        if (!IsSupportedPath(filePath))
        {
            return DroppedFileKind.Unsupported;
        }

        if (filePath.EndsWith(".sqlqg.json", StringComparison.OrdinalIgnoreCase))
        {
            return DroppedFileKind.SavedQuery;
        }

        try
        {
            using StreamReader reader = new(filePath);
            char[] buffer = new char[MaxSqlInspectionCharacters];
            int read = reader.ReadBlock(buffer, 0, buffer.Length);
            return ClassifySqlText(new string(buffer, 0, read));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DroppedFileKind.Unsupported;
        }
    }

    /// <summary>
    /// Distinguishes schema DDL from a regular SQL query.
    /// </summary>
    public static DroppedFileKind ClassifySqlText(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return DroppedFileKind.RawSql;
        }

        string normalized = sql.ToUpperInvariant();
        bool containsSchemaDdl = normalized.Contains("CREATE TABLE", StringComparison.Ordinal)
            || normalized.Contains("CREATE VIEW", StringComparison.Ordinal)
            || normalized.Contains("CREATE MATERIALIZED VIEW", StringComparison.Ordinal)
            || normalized.Contains("ALTER TABLE", StringComparison.Ordinal)
            || normalized.Contains("COMMENT ON TABLE", StringComparison.Ordinal)
            || normalized.Contains("COMMENT ON COLUMN", StringComparison.Ordinal);

        return containsSchemaDdl ? DroppedFileKind.SqlSchema : DroppedFileKind.RawSql;
    }
}
