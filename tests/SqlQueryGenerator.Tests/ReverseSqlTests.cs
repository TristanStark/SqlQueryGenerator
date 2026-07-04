using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Tests the raw SQL preset and reverse engineering features.
/// </summary>
public sealed class ReverseSqlTests
{
    /// <summary>
    /// Ensures that a raw SQL subquery preset can be embedded in an IN filter.
    /// </summary>
    [Fact]
    public void Generate_RawSqlSubqueryFilter_EmbedsValidatedSelect()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("CREATE TABLE pnj (id INTEGER, age INTEGER);");
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "id" });
        query.Filters.Add(new FilterCondition
        {
            Column = new ColumnReference { Table = "pnj", Column = "id" },
            Operator = "IN",
            ValueKind = FilterValueKind.Subquery,
            RawSubquerySql = "SELECT pnj_id FROM pnj_tags WHERE tag_id = :tag_id",
            SubqueryName = "pnj_par_tag"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("pnj.id IN (", result.Sql);
        Assert.Contains("SELECT pnj_id", result.Sql);
        Assert.Contains("WHERE tag_id = :tag_id", result.Sql);
    }

    /// <summary>
    /// Ensures that the reverse parser recognizes common SELECT clauses.
    /// </summary>
    [Fact]
    public void ReverseParser_ParseSimpleAggregateQuery_FillsBuilderModel()
    {
        const string sql = @"
SELECT pnj.age,
       COUNT(pnj.id) AS count_id
FROM pnj
WHERE pnj.age > :age_min
GROUP BY pnj.age
ORDER BY count_id DESC";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);

        Assert.Equal("pnj", query.BaseTable);
        Assert.Single(query.SelectedColumns);
        Assert.Single(query.Aggregates);
        Assert.Single(query.Filters);
        Assert.Single(query.GroupBy);
        Assert.Single(query.OrderBy);
        Assert.Equal(FilterValueKind.Parameter, query.Filters[0].ValueKind);
        Assert.Equal(":age_min", query.Parameters[0].Placeholder);
    }

    /// <summary>
    /// Ensures Oracle legacy (+) join syntax is reverse-loaded into explicit LEFT JOIN.
    /// </summary>
    [Fact]
    public void ReverseParser_ParseLegacyOracleOuterJoin_ConvertsToLeftJoin()
    {
        const string schemaSql = @"
CREATE TABLE pnj (id INTEGER PRIMARY KEY, job_id INTEGER, status TEXT);
CREATE TABLE jobs (id INTEGER PRIMARY KEY, label TEXT);
";
        const string sql = @"
SELECT pnj.id, jobs.label
FROM pnj, jobs
WHERE pnj.job_id = jobs.id(+)
  AND pnj.status = 'ACTIVE'";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(query, new SqlSchemaParser().Parse(schemaSql));

        Assert.Equal("pnj", query.BaseTable);
        Assert.Single(query.Joins);
        Assert.Equal(JoinType.Left, query.Joins[0].JoinType);
        Assert.Equal("pnj", query.Joins[0].FromTable);
        Assert.Equal("job_id", query.Joins[0].FromColumn);
        Assert.Equal("jobs", query.Joins[0].ToTable);
        Assert.Equal("id", query.Joins[0].ToColumn);
        Assert.Single(query.Filters);
        Assert.Contains("LEFT JOIN jobs ON pnj.job_id = jobs.id", generated.Sql);
        Assert.Contains("WHERE pnj.status = 'ACTIVE'", generated.Sql);
        Assert.DoesNotContain("(+)", generated.Sql);
    }

    /// <summary>
    /// Ensures reverse SQL preserves Oracle substitution parameters such as &1.
    /// </summary>
    [Fact]
    public void ReverseParser_ParseParameterAmpersand_IsDetectedAndRegenerated()
    {
        const string schemaSql = @"CREATE TABLE pnj (id INTEGER PRIMARY KEY, age INTEGER);";
        const string sql = @"
SELECT pnj.id
FROM pnj
WHERE pnj.age > &1";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(query, new SqlSchemaParser().Parse(schemaSql));

        Assert.Single(query.Parameters);
        Assert.Equal("&1", query.Parameters[0].Placeholder);
        Assert.Single(query.Filters);
        Assert.Equal(FilterValueKind.Parameter, query.Filters[0].ValueKind);
        Assert.Equal("&1", query.Filters[0].Value);
        Assert.Contains("WHERE pnj.age > &1", generated.Sql);
    }

    /// <summary>
    /// Ensures line comments and block comments are ignored during reverse SQL import.
    /// </summary>
    [Fact]
    public void ReverseImport_WithComments_IgnoresThemWithoutBreakingParsing()
    {
        const string sql = @"
-- Query used by old report
SELECT
    pnj.id, -- technical id
    pnj.name,
    pnj.note /* explanatory note */
FROM pnj
WHERE pnj.note LIKE '%-- not a real comment%'
ORDER BY pnj.name";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Equal("pnj", imported.Query.BaseTable);
        Assert.Equal(3, imported.Query.SelectedColumns.Count);
        Assert.Single(imported.Query.Filters);
        Assert.Single(imported.Query.OrderBy);
        Assert.Contains(imported.Diagnostics, diagnostic => diagnostic.Message.Contains("Commentaires ignorés", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures Cognos prompts are preserved as raw parameter expressions.
    /// </summary>
    [Fact]
    public void ReverseParser_CognosPrompt_IsDetectedAndRegenerated()
    {
        const string schemaSql = @"CREATE TABLE CUSTOMER (CUSTOMER_ID INTEGER PRIMARY KEY, NAME TEXT);";
        const string sql = @"
SELECT CUSTOMER.CUSTOMER_ID
FROM CUSTOMER
WHERE CUSTOMER.CUSTOMER_ID = #prompt(""Customer Id"", ""integer"")#";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql, SourceSqlDialect.GenericSql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(query, new SqlSchemaParser().Parse(schemaSql));

        Assert.Single(query.Parameters);
        Assert.Equal(QueryParameterSourceKind.CognosPrompt, query.Parameters[0].SourceKind);
        Assert.Equal("Customer Id", query.Parameters[0].Name);
        Assert.Equal("integer", query.Parameters[0].DeclaredType);
        Assert.Equal(@"#prompt(""Customer Id"", ""integer"")#", query.Parameters[0].Placeholder);
        Assert.Single(query.Filters);
        Assert.Equal(FilterValueKind.Parameter, query.Filters[0].ValueKind);
        Assert.Equal(@"#prompt(""Customer Id"", ""integer"")#", query.Filters[0].Value);
        Assert.Contains(@"WHERE CUSTOMER.CUSTOMER_ID = #prompt(""Customer Id"", ""integer"")#", generated.Sql);
    }

    /// <summary>
    /// Ensures TO_DATE-wrapped Cognos date prompts are reverse-loaded as parameter filters.
    /// </summary>
    [Fact]
    public void ReverseParser_CognosDatePrompt_IsDetectedAndRegenerated()
    {
        const string schemaSql = @"CREATE TABLE ORDERS (ORDER_ID INTEGER PRIMARY KEY, ORDER_DATE DATE);";
        const string sql = @"
SELECT ORDERS.ORDER_ID
FROM ORDERS
WHERE ORDERS.ORDER_DATE = TO_DATE(#prompt(""Order Date"", ""date"")#, 'dd/MM/YYYY')";

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql, SourceSqlDialect.CognosAnalytics);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(query, new SqlSchemaParser().Parse(schemaSql), new SqlGeneratorOptions { Dialect = SqlDialect.CognosAnalytics });

        Assert.Single(query.Parameters);
        Assert.Equal(QueryParameterSourceKind.CognosPrompt, query.Parameters[0].SourceKind);
        Assert.Equal("Order Date", query.Parameters[0].Name);
        Assert.Equal("date", query.Parameters[0].DeclaredType);
        Assert.Single(query.Filters);
        Assert.Equal(FilterValueKind.Parameter, query.Filters[0].ValueKind);
        Assert.Equal(@"#prompt(""Order Date"", ""date"")#", query.Filters[0].Value);
        Assert.Contains(@"WHERE ORDERS.ORDER_DATE = TO_DATE(#prompt(""Order Date"", ""date"")#, 'dd/MM/YYYY')", generated.Sql);
    }

    /// <summary>
    /// Ensures reverse import exposes clause-level coverage and dialect-specific warnings.
    /// </summary>
    [Fact]
    public void ReverseImport_Db2Profile_ReturnsCoverageAndFetchFirstWarning()
    {
        const string sql = @"
SELECT pnj.id
FROM pnj
WHERE pnj.age > :age_min
FETCH FIRST 5 ROWS ONLY";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.Db2);

        Assert.Equal(SourceSqlDialect.Db2, imported.SourceDialect);
        Assert.True(imported.Coverage.Confidence > 0.0);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "WHERE" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Warnings, warning => warning.Contains("FETCH FIRST", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures a fully supported query produces strong clause-level coverage.
    /// </summary>
    [Fact]
    public void ReverseImport_FullySupportedQuery_ReturnsFullyImportedCoverage()
    {
        const string sql = @"
SELECT pnj.age, COUNT(pnj.id) AS total
FROM pnj
WHERE pnj.age > :age_min
GROUP BY pnj.age
HAVING COUNT(pnj.id) > 1
ORDER BY total DESC";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "SELECT" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "FROM/JOIN" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "WHERE" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "GROUP BY" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "HAVING" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "ORDER BY" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.True(imported.Coverage.Confidence >= 0.80);
    }

    /// <summary>
    /// Ensures imported subqueries are marked as partially supported instead of silently treated as fully modeled.
    /// </summary>
    [Fact]
    public void ReverseImport_SubqueryFilter_ReturnsPartialSubqueryCoverage()
    {
        const string sql = @"
SELECT pnj.id
FROM pnj
WHERE pnj.id IN (
    SELECT tag.pnj_id
    FROM pnj_tags tag
    WHERE tag.tag_id = :tag_id
)";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Subqueries" && clause.Status == ReverseSqlCoverageStatus.PartiallyImported);
        Assert.Contains(imported.Query.Filters, filter => filter.ValueKind == FilterValueKind.Subquery);
        Assert.Contains(imported.Warnings, warning => warning.Contains("sous-requete", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures unsupported advanced constructs are surfaced explicitly in the coverage report.
    /// </summary>
    [Fact]
    public void ReverseImport_AdvancedSql_ReturnsUnsupportedCoverageEntries()
    {
        const string sql = @"
WITH recent_pnj AS (
    SELECT id, age
    FROM pnj
)
SELECT id
FROM recent_pnj
UNION
SELECT id
FROM archived_pnj";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "CTE" && clause.Status == ReverseSqlCoverageStatus.Unsupported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.Unsupported);
        Assert.Contains(imported.Warnings, warning => warning.Contains("CTE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(imported.Warnings, warning => warning.Contains("operation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures EXCEPT queries keep the first branch editable while surfacing set-operation diagnostics.
    /// </summary>
    [Fact]
    public void ReverseImport_ExceptQuery_PartiallyImportsFirstSelectAndFlagsSetOperation()
    {
        const string sql = @"
SELECT CUSTOMER.ID
FROM CUSTOMER
EXCEPT
SELECT ARCHIVED_CUSTOMER.ID
FROM ARCHIVED_CUSTOMER";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Equal("CUSTOMER", imported.Query.BaseTable);
        Assert.Single(imported.Query.SelectedColumns);
        Assert.Contains(imported.Warnings, warning => warning.Contains("operation d'ensemble", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(imported.Diagnostics, diagnostic => diagnostic.Clause == "Set operations" && diagnostic.Severity == ReverseSqlDiagnosticSeverity.Warning);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.Unsupported);
    }

    /// <summary>
    /// Ensures Oracle MINUS queries follow the same partial-import path as other set operations.
    /// </summary>
    [Fact]
    public void ReverseImport_MinusQuery_PartiallyImportsFirstSelectAndFlagsSetOperation()
    {
        const string sql = @"
SELECT pnj.id
FROM pnj
MINUS
SELECT archived_pnj.id
FROM archived_pnj";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.OracleLegacy);

        Assert.Equal("pnj", imported.Query.BaseTable);
        Assert.Single(imported.Query.SelectedColumns);
        Assert.Contains(imported.Warnings, warning => warning.Contains("operation d'ensemble", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(imported.Diagnostics, diagnostic => diagnostic.Clause == "Set operations" && diagnostic.Severity == ReverseSqlDiagnosticSeverity.Warning);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.Unsupported);
    }

    /// <summary>
    /// Ensures incomplete WHERE clauses produce a structured reverse SQL diagnostic.
    /// </summary>
    [Fact]
    public void ReverseImport_IncompleteWhere_ThrowsStructuredDiagnostic()
    {
        const string sql = @"
SELECT
    c.CUSTOMER_ID
FROM CUSTOMER c
WHERE
ORDER BY c.CUSTOMER_ID";

        ReverseSqlImportException ex = Assert.Throws<ReverseSqlImportException>(() =>
            new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql));

        Assert.Equal("WHERE", ex.Diagnostic.Clause);
        Assert.Equal(ReverseSqlDiagnosticSeverity.Error, ex.Diagnostic.Severity);
        Assert.NotNull(ex.Diagnostic.Line);
        Assert.NotNull(ex.Diagnostic.Column);
        Assert.Contains("WHERE clause is incomplete", ex.Diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures missing FROM produces a structured reverse SQL diagnostic instead of a generic failure.
    /// </summary>
    [Fact]
    public void ReverseImport_InvalidSelectWithoutFrom_ThrowsStructuredDiagnostic()
    {
        const string sql = @"
SELECT CUSTOMER_ID";

        ReverseSqlImportException ex = Assert.Throws<ReverseSqlImportException>(() =>
            new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql));

        Assert.Equal("SELECT/FROM", ex.Diagnostic.Clause);
        Assert.Equal(ReverseSqlDiagnosticSeverity.Error, ex.Diagnostic.Severity);
        Assert.Equal(1, ex.Diagnostic.Line);
        Assert.Equal(1, ex.Diagnostic.Column);
        Assert.Contains("SELECT", ex.Diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures Oracle legacy profile keeps existing compatibility messaging.
    /// </summary>
    [Fact]
    public void ReverseImport_OracleLegacyProfile_AddsLegacyCompatibilityWarning()
    {
        const string sql = @"
SELECT pnj.id
FROM pnj, jobs
WHERE pnj.job_id = jobs.id(+)
  AND pnj.age > &1";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.OracleLegacy);

        Assert.Equal(SourceSqlDialect.OracleLegacy, imported.SourceDialect);
        Assert.Contains(imported.Warnings, warning => warning.Contains("Oracle Legacy", StringComparison.OrdinalIgnoreCase));
    }
}
