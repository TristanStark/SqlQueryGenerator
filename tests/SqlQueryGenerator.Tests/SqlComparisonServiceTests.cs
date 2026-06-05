using SqlQueryGenerator.Core.Generation;

namespace SqlQueryGenerator.Tests;

public sealed class SqlComparisonServiceTests
{
    [Fact]
    public void Compare_IdenticalSql_ReportsNoDifferences()
    {
        const string sql = """
SELECT customer_id,
       amount
FROM orders
WHERE status = :status
""";

        SqlComparisonReport report = new SqlComparisonService().Compare(sql, sql);

        Assert.False(report.HasDifferences);
        Assert.Equal(4, report.UnchangedCount);
        Assert.Equal("source et cible sont identiques sur 4 ligne(s).", report.FormatSummary("source", "cible"));
    }

    [Fact]
    public void Compare_RewriteStyleChange_ProducesModifiedRows()
    {
        const string sourceSql = """
SELECT c.NAME
FROM CUSTOMER c, ORDERS o
WHERE c.CUSTOMER_ID = o.CUSTOMER_ID
""";

        const string targetSql = """
SELECT c.NAME
FROM CUSTOMER c
INNER JOIN ORDERS o ON c.CUSTOMER_ID = o.CUSTOMER_ID
""";

        SqlComparisonReport report = new SqlComparisonService().Compare(sourceSql, targetSql);

        Assert.True(report.HasDifferences);
        Assert.Equal(2, report.ModifiedCount);
        Assert.Equal(0, report.AddedCount);
        Assert.Contains(report.Lines, line => line.Kind == SqlComparisonKind.Modified && line.SourceText == "FROM CUSTOMER c, ORDERS o" && line.TargetText == "FROM CUSTOMER c");
        Assert.Contains(report.Lines, line => line.Kind == SqlComparisonKind.Modified && line.TargetText.StartsWith("INNER JOIN ORDERS", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_AddedClause_ProducesAddedRow()
    {
        const string sourceSql = """
SELECT customer_id
FROM orders
""";

        const string targetSql = """
SELECT customer_id
FROM orders
ORDER BY customer_id
""";

        SqlComparisonReport report = new SqlComparisonService().Compare(sourceSql, targetSql);

        Assert.True(report.HasDifferences);
        Assert.Equal(1, report.AddedCount);
        Assert.Contains(report.Lines, line => line.Kind == SqlComparisonKind.Added && line.TargetText == "ORDER BY customer_id");
    }

    [Fact]
    public void Compare_IgnoreWhitespaceChanges_TreatsFormattingOnlyDiffAsUnchanged()
    {
        const string sourceSql = """
SELECT customer_id,   amount
FROM   orders
""";

        const string targetSql = """
SELECT customer_id, amount
FROM orders
""";

        SqlComparisonReport report = new SqlComparisonService().Compare(sourceSql, targetSql, new SqlComparisonOptions
        {
            IgnoreWhitespaceChanges = true
        });

        Assert.False(report.HasDifferences);
        Assert.Equal(2, report.UnchangedCount);
        Assert.Contains("ignore espaces", report.FormatSummary("source", "cible"), StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_IgnoreCaseChanges_TreatsCaseOnlyDiffAsUnchanged()
    {
        const string sourceSql = """
select customer_id
from orders
""";

        const string targetSql = """
SELECT CUSTOMER_ID
FROM ORDERS
""";

        SqlComparisonReport report = new SqlComparisonService().Compare(sourceSql, targetSql, new SqlComparisonOptions
        {
            IgnoreCaseChanges = true
        });

        Assert.False(report.HasDifferences);
        Assert.Equal(2, report.UnchangedCount);
        Assert.Contains("ignore casse", report.FormatSummary("source", "cible"), StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteService_Rewrite_PopulatesComparisonReport()
    {
        const string sql = """
SELECT c.NAME, c.NAME
FROM CUSTOMER c, ORDERS o
WHERE c.CUSTOMER_ID = o.CUSTOMER_ID
  AND o.STATUS = :status
  AND o.STATUS = :status
""";

        SqlRewriteResult result = new SqlRewriteSuggestionService().Rewrite(sql);

        Assert.True(result.Comparison.HasDifferences);
        Assert.NotEmpty(result.Comparison.Lines);
        Assert.Contains(result.Comparison.Lines, line => line.Kind is SqlComparisonKind.Modified or SqlComparisonKind.Added);
    }
}
