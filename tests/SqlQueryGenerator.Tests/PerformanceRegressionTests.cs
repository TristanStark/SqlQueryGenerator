using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using System.Diagnostics;
using System.Text;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Guards core schema parsing/inference performance and deterministic behavior.
/// </summary>
public sealed class PerformanceRegressionTests
{
    /// <summary>
    /// Verifies that parsing + relationship inference on a large synthetic schema
    /// stays within a practical bound and keeps expected key relationships.
    /// </summary>
    [Fact]
    public void Parse_LargeSchema_CompletesWithinGuardrail_AndFindsExpectedRelationships()
    {
        const int tableCount = 220;
        string sql = BuildLargeSchema(tableCount);
        SqlSchemaParser parser = new();

        Stopwatch stopwatch = Stopwatch.StartNew();
        DatabaseSchema schema = parser.Parse(sql);
        stopwatch.Stop();

        Assert.Equal(tableCount * 2, schema.Tables.Count);
        Assert.Contains(schema.Relationships, r => r.FromTable == "orders_001" && r.FromColumn == "customer_001_id" && r.ToTable == "customers_001" && r.ToColumn == "id");
        Assert.Contains(schema.Relationships, r => r.FromTable == $"orders_{tableCount:000}" && r.FromColumn == $"customer_{tableCount:000}_id" && r.ToTable == $"customers_{tableCount:000}" && r.ToColumn == "id");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8), $"Large schema parse+infer took {stopwatch.Elapsed.TotalMilliseconds:F0} ms, expected < 8000 ms.");
    }

    /// <summary>
    /// Verifies deterministic output ordering/content despite parallel candidate generation.
    /// </summary>
    [Fact]
    public void Parse_SameSchemaTwice_ProducesStableRelationshipSetAndOrder()
    {
        const int tableCount = 160;
        string sql = BuildLargeSchema(tableCount);
        SqlSchemaParser parser = new();

        DatabaseSchema first = parser.Parse(sql);
        DatabaseSchema second = parser.Parse(sql);

        string[] firstKeys = first.Relationships
            .Select(r => $"{r.FromTable}.{r.FromColumn}->{r.ToTable}.{r.ToColumn}|{r.Source}|{r.Confidence:F4}")
            .ToArray();
        string[] secondKeys = second.Relationships
            .Select(r => $"{r.FromTable}.{r.FromColumn}->{r.ToTable}.{r.ToColumn}|{r.Source}|{r.Confidence:F4}")
            .ToArray();

        Assert.Equal(firstKeys.Length, secondKeys.Length);
        Assert.Equal(firstKeys, secondKeys);
    }

    private static string BuildLargeSchema(int pairCount)
    {
        StringBuilder sb = new();
        for (int i = 1; i <= pairCount; i++)
        {
            string suffix = i.ToString("000");
            sb.AppendLine($"CREATE TABLE customers_{suffix} (");
            sb.AppendLine("    id INTEGER PRIMARY KEY,");
            sb.AppendLine("    code TEXT");
            sb.AppendLine(");");
            sb.AppendLine();

            sb.AppendLine($"CREATE TABLE orders_{suffix} (");
            sb.AppendLine("    id INTEGER PRIMARY KEY,");
            sb.AppendLine($"    customer_{suffix}_id INTEGER,");
            sb.AppendLine("    amount INTEGER,");
            sb.AppendLine("    status TEXT");
            sb.AppendLine(");");
            sb.AppendLine();

            sb.AppendLine($"CREATE INDEX idx_orders_{suffix}_customer ON orders_{suffix}(customer_{suffix}_id);");
            sb.AppendLine($"CREATE UNIQUE INDEX ux_customers_{suffix}_id ON customers_{suffix}(id);");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
