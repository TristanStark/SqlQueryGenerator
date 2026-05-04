using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

public sealed class PurposeTests
{
    [Fact]
    public void Describe_CountBaseRowsGroupedByRelatedName_UsesBusinessIntentInsteadOfRawCountColumn()
    {
        const string sql = @"
CREATE TABLE pnj (id INTEGER, genre TEXT);
CREATE TABLE items (id INTEGER, name TEXT);
";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "items", Column = "name" });
        query.GroupBy.Add(new ColumnReference { Table = "items", Column = "name" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "pnj", Column = "genre" },
            Alias = "count_genre"
        });

        var purpose = new QueryPurposeDescriber().Describe(query, schema);

        Assert.Contains("nombre de pnj par item", purpose);
        Assert.DoesNotContain("nombre de genre", purpose);
    }
}
