using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Covers SQL rewrite/import continuity and DDL export helpers introduced in v31.
/// </summary>
public sealed class RewriteAndDdlTests
{
    /// <summary>
    /// Ensures reverse-loaded SQL with aliases, implicit joins, filters, HAVING and ORDER BY stays editable and regenerates cleanly.
    /// </summary>
    [Fact]
    public void ReverseParser_ParseImplicitJoinWithAliases_PreservesEditableClauses()
    {
        const string schemaSql = @"
CREATE TABLE CUSTOMER (CUSTOMER_ID INTEGER PRIMARY KEY, NAME TEXT);
CREATE TABLE ORDERS (ORDER_ID INTEGER PRIMARY KEY, CUSTOMER_ID INTEGER, AMOUNT NUMBER, STATUS TEXT);
";
        const string sql = @"
SELECT c.NAME AS customer_name,
       SUM(o.AMOUNT) AS total
FROM CUSTOMER c, ORDERS o
WHERE c.CUSTOMER_ID = o.CUSTOMER_ID
  AND o.STATUS = :status
GROUP BY c.NAME
HAVING SUM(o.AMOUNT) > &1
ORDER BY total DESC";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(query, new SqlSchemaParser().Parse(schemaSql));

        Assert.Equal("CUSTOMER", query.BaseTable);
        Assert.Equal(2, query.TableAliases.Count);
        Assert.Single(query.Joins);
        Assert.Equal(2, query.Filters.Count);
        Assert.Contains(query.TableAliases, a => a.Table == "CUSTOMER" && a.Alias == "c");
        Assert.Contains(query.TableAliases, a => a.Table == "ORDERS" && a.Alias == "o");
        Assert.Contains("FROM CUSTOMER c", generated.Sql);
        Assert.Contains("INNER JOIN ORDERS o ON c.CUSTOMER_ID = o.CUSTOMER_ID", generated.Sql);
        Assert.Contains("WHERE o.STATUS = :status", generated.Sql);
        Assert.Contains("GROUP BY c.NAME", generated.Sql);
        Assert.Contains("HAVING SUM(o.AMOUNT) > &1", generated.Sql);
        Assert.Contains("ORDER BY total DESC", generated.Sql);
    }

    /// <summary>
    /// Ensures the rewrite service converts implicit joins and removes duplicated clauses conservatively.
    /// </summary>
    [Fact]
    public void RewriteService_RewriteImplicitJoinAndDuplicates_ModernizesSqlWithoutLosingAliases()
    {
        const string sql = @"
SELECT c.NAME, c.NAME, SUM(o.AMOUNT) AS total
FROM CUSTOMER c, ORDERS o
WHERE c.CUSTOMER_ID = o.CUSTOMER_ID
  AND o.STATUS = :status
  AND o.STATUS = :status
GROUP BY c.NAME, c.NAME
ORDER BY total DESC, total DESC";

        SqlRewriteResult result = new SqlRewriteSuggestionService().Rewrite(sql);

        Assert.Contains("ImplicitJoinConverted", result.AppliedTransformations);
        Assert.Contains("DuplicatePredicateRemoved", result.AppliedTransformations);
        Assert.Contains("FormattingImproved", result.AppliedTransformations);
        Assert.Contains("FROM CUSTOMER c", result.RewrittenSql);
        Assert.Contains("INNER JOIN ORDERS o ON c.CUSTOMER_ID = o.CUSTOMER_ID", result.RewrittenSql);
        Assert.Contains("WHERE o.STATUS = :status", result.RewrittenSql);
        Assert.DoesNotContain("GROUP BY c.NAME, c.NAME", result.RewrittenSql);
        Assert.Contains("GROUP BY c.NAME", result.RewrittenSql);
        Assert.DoesNotContain("ORDER BY total DESC, total DESC", result.RewrittenSql);
        Assert.Contains("ORDER BY total DESC", result.RewrittenSql);
        Assert.Equal(1, CountOccurrences(result.RewrittenSql, "o.STATUS = :status"));
    }

    /// <summary>
    /// Ensures import warnings are surfaced for advanced SQL structures that are only partially supported.
    /// </summary>
    [Fact]
    public void ReverseImportService_ImportWithCteAndSubquery_ProducesConservativeWarnings()
    {
        const string sql = @"
WITH recent_orders AS (
    SELECT CUSTOMER_ID
    FROM ORDERS
)
SELECT CUSTOMER_ID
FROM recent_orders";

        ReverseSqlImportResult result = new ReverseSqlImportService().Import(sql);

        Assert.Equal("recent_orders", result.Query.BaseTable);
        Assert.Contains(result.Warnings, warning => warning.Contains("CTE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, warning => warning.Contains("sous-requete", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures Oracle DDL export guidance uses DBMS_METADATA and the requested owner.
    /// </summary>
    [Fact]
    public void DdlCommandExportService_BuildOracleCommand_UsesDbmsMetadataAndOwner()
    {
        string command = new DdlCommandExportService().BuildCommand(DdlExportDialect.Oracle, "APP_OWNER");

        Assert.Contains("DBMS_METADATA.GET_DDL", command);
        Assert.Contains("FROM all_objects", command);
        Assert.Contains("WHERE owner = UPPER('APP_OWNER')", command);
        Assert.Contains("'TABLE', 'VIEW', 'MATERIALIZED VIEW', 'INDEX'", command);
    }

    /// <summary>
    /// Ensures SQLite DDL export guidance targets sqlite_master and defaults to the main database when empty.
    /// </summary>
    [Fact]
    public void DdlCommandExportService_BuildSqliteCommand_DefaultsToMainDatabase()
    {
        string command = new DdlCommandExportService().BuildCommand(DdlExportDialect.SQLite, string.Empty);

        Assert.Contains("FROM main.sqlite_master", command);
        Assert.Contains("WHERE type IN ('table', 'view', 'index', 'trigger')", command);
        Assert.Contains("name NOT LIKE 'sqlite_%'", command);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
