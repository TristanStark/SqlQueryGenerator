using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Covers heuristic performance analysis warnings introduced in the index-aware v2 pass.
/// </summary>
public sealed class PerformanceAnalyzerTests
{
    /// <summary>
    /// Ensures SELECT * and leading-wildcard LIKE are flagged, along with missing row limiting on broad queries.
    /// </summary>
    [Fact]
    public void Analyze_SelectStarLeadingWildcardAndNoLimit_EmitsWarnings()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE CUSTOMER (
                ID INTEGER PRIMARY KEY,
                NAME TEXT,
                CITY TEXT
            );
            CREATE INDEX IX_CUSTOMER_NAME ON CUSTOMER(NAME);
            """);

        QueryDefinition query = new() { BaseTable = "CUSTOMER" };
        query.SelectedColumns.Add(new ColumnReference { Table = "CUSTOMER", Column = "*" });
        query.Filters.Add(new FilterCondition
        {
            Column = new ColumnReference { Table = "CUSTOMER", Column = "NAME" },
            Operator = "LIKE",
            Value = "%dupont%",
            ValueKind = FilterValueKind.Literal
        });

        QueryPerformanceReport report = new QueryPerformanceAnalyzer().Analyze(query, schema);

        Assert.Contains(report.Hints, hint => hint.Message.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Hints, hint => hint.Message.Contains("wildcard en tête", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Hints, hint => hint.Message.Contains("sans LIMIT/FETCH/TOP", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures filters on computed expressions are called out as index-unfriendly.
    /// </summary>
    [Fact]
    public void Analyze_CustomExpressionFilter_EmitsFunctionWarning()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE CUSTOMER (
                ID INTEGER PRIMARY KEY,
                NAME TEXT
            );
            """);

        QueryDefinition query = new() { BaseTable = "CUSTOMER" };
        query.Filters.Add(new FilterCondition
        {
            FieldKind = QueryFieldKind.CustomColumn,
            FieldAlias = "UPPER(CUSTOMER.NAME)",
            Operator = "LIKE",
            Value = "DUPONT%",
            ValueKind = FilterValueKind.Literal
        });

        QueryPerformanceReport report = new QueryPerformanceAnalyzer().Analyze(query, schema);

        Assert.Contains(report.Hints, hint => hint.Message.Contains("fonctions appliquées", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures non-indexed ORDER BY and wide GROUP BY clauses are highlighted.
    /// </summary>
    [Fact]
    public void Analyze_GroupByManyColumnsAndOrderByNonIndexed_EmitsWarnings()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE SALES (
                ID INTEGER PRIMARY KEY,
                CUSTOMER_ID INTEGER,
                CITY TEXT,
                COUNTRY TEXT,
                CHANNEL TEXT,
                STATUS TEXT,
                CREATED_AT TEXT
            );
            CREATE INDEX IX_SALES_CUSTOMER ON SALES(CUSTOMER_ID);
            """);

        QueryDefinition query = new() { BaseTable = "SALES" };
        query.GroupBy.Add(new ColumnReference { Table = "SALES", Column = "CUSTOMER_ID" });
        query.GroupBy.Add(new ColumnReference { Table = "SALES", Column = "CITY" });
        query.GroupBy.Add(new ColumnReference { Table = "SALES", Column = "COUNTRY" });
        query.GroupBy.Add(new ColumnReference { Table = "SALES", Column = "CHANNEL" });
        query.OrderBy.Add(new OrderByItem
        {
            Column = new ColumnReference { Table = "SALES", Column = "CREATED_AT" },
            Direction = SortDirection.Descending
        });

        QueryPerformanceReport report = new QueryPerformanceAnalyzer().Analyze(query, schema);

        Assert.Contains(report.Hints, hint => hint.Message.Contains("GROUP BY sur 4 colonnes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Hints, hint => hint.Message.Contains("ORDER BY SALES.CREATED_AT", StringComparison.OrdinalIgnoreCase) && hint.Severity == QueryPerformanceSeverity.Warning);
    }

    /// <summary>
    /// Ensures using multiple tables without any join emits a critical hint.
    /// </summary>
    [Fact]
    public void Analyze_MultipleTablesWithoutJoin_EmitsCriticalWarning()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE CUSTOMER (
                ID INTEGER PRIMARY KEY,
                NAME TEXT
            );
            CREATE TABLE ORDERS (
                ID INTEGER PRIMARY KEY,
                CUSTOMER_ID INTEGER
            );
            """);

        QueryDefinition query = new() { BaseTable = "ORDERS" };
        query.SelectedColumns.Add(new ColumnReference { Table = "ORDERS", Column = "ID" });
        query.SelectedColumns.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME" });

        QueryPerformanceReport report = new QueryPerformanceAnalyzer().Analyze(query, schema);

        Assert.Contains(report.Hints, hint => hint.Severity == QueryPerformanceSeverity.Critical && hint.Message.Contains("sans jointure explicite", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures weak join keys are highlighted and the formatted report stays grouped by severity.
    /// </summary>
    [Fact]
    public void Analyze_NonUniqueJoin_EmitsCardinalityWarning_AndGroupedText()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE SALES (
                ID INTEGER PRIMARY KEY,
                STATUS TEXT,
                CHANNEL TEXT
            );
            CREATE TABLE SALES_TARGET (
                ID INTEGER PRIMARY KEY,
                STATUS TEXT,
                CHANNEL TEXT
            );
            """);

        QueryDefinition query = new() { BaseTable = "SALES" };
        query.SelectedColumns.Add(new ColumnReference { Table = "SALES", Column = "ID" });
        query.SelectedColumns.Add(new ColumnReference { Table = "SALES_TARGET", Column = "ID" });
        query.Joins.Add(new JoinDefinition
        {
            FromTable = "SALES",
            FromColumn = "STATUS",
            ToTable = "SALES_TARGET",
            ToColumn = "CHANNEL",
            JoinType = JoinType.Inner
        });

        QueryPerformanceReport report = new QueryPerformanceAnalyzer().Analyze(query, schema);
        string rendered = report.ToString();

        Assert.Contains(report.Hints, hint => hint.Message.Contains("aucune extrémité PK/unique", StringComparison.OrdinalIgnoreCase) && hint.Severity == QueryPerformanceSeverity.Warning);
        Assert.StartsWith("[Warning]", rendered, StringComparison.Ordinal);
        Assert.Contains("- Jointure SALES.STATUS -> SALES_TARGET.CHANNEL", rendered, StringComparison.OrdinalIgnoreCase);
    }
}
