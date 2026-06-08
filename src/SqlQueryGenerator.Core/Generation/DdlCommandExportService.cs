using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Generates helper SQL commands that users can run to extract schema DDL from Oracle or SQLite.
/// </summary>
public sealed class DdlCommandExportService
{
    /// <summary>
    /// Builds the SQL command used to export DDL for the requested source engine.
    /// </summary>
    /// <param name="dialect">Source engine for the DDL extraction query.</param>
    /// <param name="schemaOrDatabaseName">Schema owner for Oracle or attached database name for SQLite.</param>
    /// <returns>SQL command text.</returns>
    public string BuildCommand(DdlExportDialect dialect, string? schemaOrDatabaseName)
    {
        return dialect switch
        {
            DdlExportDialect.Oracle => BuildOracleCommand(schemaOrDatabaseName),
            _ => BuildSqliteCommand(schemaOrDatabaseName)
        };
    }

    private static string BuildOracleCommand(string? schemaName)
    {
        string escaped = EscapeSqlLiteral(string.IsNullOrWhiteSpace(schemaName) ? "PUBLIC" : schemaName.Trim());
        return
$@"SELECT DBMS_METADATA.GET_DDL(object_type, object_name, owner) AS ddl
FROM all_objects
WHERE owner = UPPER('{escaped}')
  AND object_type IN ('TABLE', 'VIEW', 'MATERIALIZED VIEW', 'INDEX')
ORDER BY object_type, object_name;";
    }

    private static string BuildSqliteCommand(string? databaseName)
    {
        string db = string.IsNullOrWhiteSpace(databaseName) ? "main" : databaseName.Trim();
        return
$@"SELECT sql
FROM {db}.sqlite_master
WHERE type IN ('table', 'view', 'index', 'trigger')
  AND name NOT LIKE 'sqlite_%'
  AND sql IS NOT NULL
ORDER BY
  CASE type
    WHEN 'table' THEN 1
    WHEN 'view' THEN 2
    WHEN 'index' THEN 3
    ELSE 4
  END,
  name;";
    }

    /// <summary>
    /// Builds a CREATE MATERIALIZED VIEW statement around the current generated SELECT query.
    /// </summary>
    /// <param name="viewName">Materialized view name to create.</param>
    /// <param name="selectSql">Generated SELECT SQL.</param>
    /// <param name="dialect">Target SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether identifiers should be quoted.</param>
    /// <returns>CREATE MATERIALIZED VIEW SQL.</returns>
    public string BuildCreateMaterializedViewCommand(
        string viewName,
        string selectSql,
        SqlDialect dialect,
        bool quoteIdentifiers)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("Nom de vue matérialisée vide.", nameof(viewName));
        }

        if (string.IsNullOrWhiteSpace(selectSql))
        {
            throw new ArgumentException("Requête SELECT vide.", nameof(selectSql));
        }

        string normalizedSelect = selectSql.Trim().TrimEnd(';');
        if (!normalizedSelect.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !normalizedSelect.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Une vue matérialisée doit être générée à partir d'une requête SELECT ou WITH valide.");
        }

        string quotedName = SqlIdentifierQuoter.Quote(viewName.Trim(), dialect, quoteIdentifiers);

        return dialect switch
        {
            SqlDialect.SQLite => throw new InvalidOperationException("SQLite ne supporte pas les vues matérialisées natives."),
            _ =>
    $@"CREATE MATERIALIZED VIEW {quotedName} AS
{normalizedSelect};"
        };
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

/// <summary>
/// Represents the source engine targeted by a DDL extraction helper command.
/// </summary>
public enum DdlExportDialect
{
    /// <summary>
    /// Oracle DDL extraction using DBMS_METADATA.
    /// </summary>
    Oracle,

    /// <summary>
    /// SQLite DDL extraction using sqlite_master.
    /// </summary>
    SQLite
}
