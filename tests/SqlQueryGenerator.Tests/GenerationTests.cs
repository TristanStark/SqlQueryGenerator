using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

public sealed class GenerationTests
{
    [Fact]
    public void Generate_SelectWithJoinFilterGroupAggregateAndOrder_ProducesFlatSql()
    {
        const string sql = @"
CREATE TABLE CUSTOMER (CUSTOMER_ID INTEGER PRIMARY KEY, NAME TEXT);
CREATE TABLE ORDERS (ORDER_ID INTEGER PRIMARY KEY, CUSTOMER_ID INTEGER, AMOUNT NUMBER, STATUS TEXT);
";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "ORDERS" };
        query.SelectedColumns.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME", Alias = "client" });
        query.Filters.Add(new FilterCondition { Column = new ColumnReference { Table = "ORDERS", Column = "STATUS" }, Operator = "=", Value = "PAID" });
        query.GroupBy.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME" });
        query.Aggregates.Add(new AggregateSelection { Function = AggregateFunction.Sum, Column = new ColumnReference { Table = "ORDERS", Column = "AMOUNT" }, Alias = "total" });
        query.OrderBy.Add(new OrderByItem { Column = new ColumnReference { Table = "CUSTOMER", Column = "NAME" }, Direction = SortDirection.Ascending });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { Dialect = SqlDialect.SQLite });

        Assert.Contains("SELECT CUSTOMER.NAME AS client", result.Sql);
        Assert.Contains("INNER JOIN CUSTOMER ON ORDERS.CUSTOMER_ID = CUSTOMER.CUSTOMER_ID", result.Sql);
        Assert.Contains("WHERE ORDERS.STATUS = 'PAID'", result.Sql);
        Assert.Contains("SUM(ORDERS.AMOUNT) AS total", result.Sql);
        Assert.Contains("GROUP BY CUSTOMER.NAME", result.Sql);
        Assert.DoesNotContain("SELECT * FROM (", result.Sql);
    }

    [Fact]
    public void Generate_CustomCaseColumn_ProducesCaseWhen()
    {
        const string sql = @"CREATE TABLE T (A TEXT, B INTEGER);";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "T" };
        query.CustomColumns.Add(new CustomColumnSelection
        {
            Alias = "label",
            CaseColumn = new ColumnReference { Table = "T", Column = "A" },
            CaseOperator = "=",
            CaseCompareValue = "X",
            CaseThenValue = "Z",
            CaseElseValue = "Y"
        });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("CASE WHEN T.A = 'X' THEN 'Z' ELSE 'Y' END AS label", result.Sql);
    }


    [Fact]
    public void Generate_QueryDistinct_EmitsSelectDistinct()
    {
        const string sql = @"CREATE TABLE T (A TEXT, B INTEGER);";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "T", Distinct = true };
        query.SelectedColumns.Add(new ColumnReference { Table = "T", Column = "A" });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.StartsWith("SELECT DISTINCT T.A", result.Sql);
    }

    [Fact]
    public void Generate_AggregateDistinct_EmitsDistinctInsideAggregate()
    {
        const string sql = @"CREATE TABLE T (A TEXT, B INTEGER);";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "T" };
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "T", Column = "A" },
            Distinct = true,
            Alias = "nb_a"
        });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("COUNT(DISTINCT T.A) AS nb_a", result.Sql);
    }

    [Fact]
    public void Generate_ConditionalCount_EmitsCaseWhenCountWithoutSubquery()
    {
        const string sql = @"CREATE TABLE PAYMENTS (PAYMENT_ID INTEGER PRIMARY KEY, PERSON_ID INTEGER, MODE_REGLEMENT TEXT, AMOUNT NUMBER);";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "PAYMENTS" };
        query.GroupBy.Add(new ColumnReference { Table = "PAYMENTS", Column = "PERSON_ID" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "PAYMENTS", Column = "PAYMENT_ID" },
            Alias = "nb_paiements_cb",
            ConditionColumn = new ColumnReference { Table = "PAYMENTS", Column = "MODE_REGLEMENT" },
            ConditionOperator = "=",
            ConditionValue = "CB"
        });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("COUNT(CASE WHEN PAYMENTS.MODE_REGLEMENT = 'CB' THEN PAYMENTS.PAYMENT_ID END) AS nb_paiements_cb", result.Sql);
        Assert.Contains("GROUP BY PAYMENTS.PERSON_ID", result.Sql);
        Assert.DoesNotContain("SELECT * FROM (", result.Sql);
    }

    [Fact]
    public void Generate_ConditionalSum_EmitsCaseWhenSumWithoutSubquery()
    {
        const string sql = @"CREATE TABLE PAYMENTS (PAYMENT_ID INTEGER PRIMARY KEY, PERSON_ID INTEGER, STATUS TEXT, AMOUNT NUMBER);";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "PAYMENTS" };
        query.SelectedColumns.Add(new ColumnReference { Table = "PAYMENTS", Column = "PERSON_ID" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Sum,
            Column = new ColumnReference { Table = "PAYMENTS", Column = "AMOUNT" },
            Alias = "total_paye",
            ConditionColumn = new ColumnReference { Table = "PAYMENTS", Column = "STATUS" },
            ConditionOperator = "=",
            ConditionValue = "PAID"
        });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("SUM(CASE WHEN PAYMENTS.STATUS = 'PAID' THEN PAYMENTS.AMOUNT ELSE 0 END) AS total_paye", result.Sql);
        Assert.Contains("GROUP BY PAYMENTS.PERSON_ID", result.Sql);
        Assert.DoesNotContain("SELECT * FROM (", result.Sql);
    }



    [Fact]
    public void Generate_AutoJoin_PrefersSpecificForeignKeyOverGenericId()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    nom TEXT,
    job_id INTEGER
);
CREATE TABLE jobs (
    id INTEGER,
    name TEXT
);
";
        var schema = new SqlSchemaParser().Parse(sql);
        var query = new QueryDefinition { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "jobs", Column = "name" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "jobs", Column = "id" },
            Alias = "id_agg"
        });

        var result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("INNER JOIN jobs ON pnj.job_id = jobs.id", result.Sql);
        Assert.DoesNotContain("pnj.id = jobs.id", result.Sql);
    }

}
