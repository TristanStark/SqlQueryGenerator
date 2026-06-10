using SqlQueryGenerator.Core.Export;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Covers Markdown generation for query documentation exports.
/// </summary>
public sealed class QueryDocumentationMarkdownExporterTests
{
    [Fact]
    public void GenerateMarkdown_IncludesCoreQueryDocumentationSections()
    {
        QueryDocumentationMarkdownExporter exporter = new();
        QueryDocumentationExportContext context = new()
        {
            QueryName = "monthly_sales",
            Description = "Documents monthly sales by customer.",
            GeneratedAt = new DateTimeOffset(2026, 6, 10, 21, 30, 0, TimeSpan.FromHours(2)),
            Dialect = "Oracle",
            BaseTable = "SALES.ORDERS",
            Distinct = true,
            LimitRows = 100,
            Purpose = "Returns customer sales totals.",
            GeneratedSql = "SELECT customer_id, SUM(amount) AS total_amount\nFROM SALES.ORDERS\nGROUP BY customer_id",
            SelectedColumns = new[]
            {
                new QueryDocumentationColumn("SALES.ORDERS", "CUSTOMER_ID", "customer_id")
            },
            Joins = new[]
            {
                new QueryDocumentationJoin(
                    "Inner",
                    "SALES.ORDERS",
                    "CUSTOMER_ID",
                    "CRM.CUSTOMERS",
                    "ID",
                    new[] { new QueryDocumentationJoinPair("TENANT_ID", "TENANT_ID") })
            },
            Filters = new[]
            {
                new QueryDocumentationFilter("And", "SALES.ORDERS.STATUS", "=", "Literal", "PAID")
            },
            GroupByColumns = new[] { "SALES.ORDERS.CUSTOMER_ID" },
            Aggregates = new[]
            {
                new QueryDocumentationAggregate("Sum", "SALES.ORDERS", "AMOUNT", "total_amount", false, "SALES.ORDERS.STATUS = PAID")
            },
            Sorting = new[]
            {
                new QueryDocumentationOrder("total_amount", "Descending")
            },
            CalculatedColumns = new[]
            {
                new QueryDocumentationCalculatedColumn("bucket", "CASE WHEN amount > 100 THEN 'large' ELSE 'small' END")
            },
            Parameters = new[]
            {
                new QueryDocumentationParameter("p_year", "number", true, false, "2026", "Fiscal year")
            },
            Warnings = "No LIMIT configured.",
            PerformanceNotes = "Index ORDERS_CUSTOMER_ID may help."
        };

        string markdown = exporter.GenerateMarkdown(context);

        Assert.Contains("# Query: monthly\\_sales", markdown);
        Assert.Contains("## Summary", markdown);
        Assert.Contains("| Generated at | 2026-06-10 21:30:00 +02:00 |", markdown);
        Assert.Contains("## Generated SQL", markdown);
        Assert.Contains("```sql", markdown);
        Assert.Contains("SUM(amount) AS total_amount", markdown);
        Assert.Contains("## Selected columns", markdown);
        Assert.Contains("| SALES.ORDERS | CUSTOMER\\_ID | customer\\_id |", markdown);
        Assert.Contains("## Joins", markdown);
        Assert.Contains("TENANT\\_ID = TENANT\\_ID", markdown);
        Assert.Contains("## Filters", markdown);
        Assert.Contains("## Grouping", markdown);
        Assert.Contains("## Aggregates", markdown);
        Assert.Contains("## Sorting", markdown);
        Assert.Contains("## Calculated columns", markdown);
        Assert.Contains("## Parameters", markdown);
        Assert.Contains("## Warnings", markdown);
        Assert.Contains("No LIMIT configured.", markdown);
        Assert.Contains("## Performance notes", markdown);
        Assert.Contains("Index ORDERS\\_CUSTOMER\\_ID may help.", markdown);
    }

    [Fact]
    public void GenerateMarkdown_EscapesTablePipesAndUsesLongerFenceWhenSqlContainsBackticks()
    {
        QueryDocumentationMarkdownExporter exporter = new();
        QueryDocumentationExportContext context = new()
        {
            QueryName = "pipe_test",
            GeneratedSql = "SELECT '```' AS fence_marker",
            SelectedColumns = new[]
            {
                new QueryDocumentationColumn("A|B", "C|D", "alias|value")
            }
        };

        string markdown = exporter.GenerateMarkdown(context);

        Assert.Contains("````sql", markdown);
        Assert.Contains("| A\\|B | C\\|D | alias\\|value |", markdown);
    }
}
