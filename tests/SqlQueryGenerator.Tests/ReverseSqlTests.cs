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

        Assert.Matches(@"pnj\.id\s+IN\s*\(\s*SELECT\s+pnj_id\s+FROM\s+pnj_tags", result.Sql);
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
}
