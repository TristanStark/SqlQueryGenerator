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
}
