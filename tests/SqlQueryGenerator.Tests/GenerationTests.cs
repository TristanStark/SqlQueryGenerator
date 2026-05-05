using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "ORDERS" };
        query.SelectedColumns.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME", Alias = "client" });
        query.Filters.Add(new FilterCondition { Column = new ColumnReference { Table = "ORDERS", Column = "STATUS" }, Operator = "=", Value = "PAID" });
        query.GroupBy.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME" });
        query.Aggregates.Add(new AggregateSelection { Function = AggregateFunction.Sum, Column = new ColumnReference { Table = "ORDERS", Column = "AMOUNT" }, Alias = "total" });
        query.OrderBy.Add(new OrderByItem { Column = new ColumnReference { Table = "CUSTOMER", Column = "NAME" }, Direction = SortDirection.Ascending });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { Dialect = SqlDialect.SQLite });

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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "T" };
        query.CustomColumns.Add(new CustomColumnSelection
        {
            Alias = "label",
            CaseColumn = new ColumnReference { Table = "T", Column = "A" },
            CaseOperator = "=",
            CaseCompareValue = "X",
            CaseThenValue = "Z",
            CaseElseValue = "Y"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("CASE WHEN T.A = 'X' THEN 'Z' ELSE 'Y' END AS label", result.Sql);
    }


    [Fact]
    public void Generate_QueryDistinct_EmitsSelectDistinct()
    {
        const string sql = @"CREATE TABLE T (A TEXT, B INTEGER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "T", Distinct = true };
        query.SelectedColumns.Add(new ColumnReference { Table = "T", Column = "A" });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.StartsWith("SELECT DISTINCT T.A", result.Sql);
    }

    [Fact]
    public void Generate_AggregateDistinct_EmitsDistinctInsideAggregate()
    {
        const string sql = @"CREATE TABLE T (A TEXT, B INTEGER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "T" };
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "T", Column = "A" },
            Distinct = true,
            Alias = "nb_a"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("COUNT(DISTINCT T.A) AS nb_a", result.Sql);
    }

    [Fact]
    public void Generate_ConditionalCount_EmitsCaseWhenCountWithoutSubquery()
    {
        const string sql = @"CREATE TABLE PAYMENTS (PAYMENT_ID INTEGER PRIMARY KEY, PERSON_ID INTEGER, MODE_REGLEMENT TEXT, AMOUNT NUMBER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "PAYMENTS" };
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

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("COUNT(CASE WHEN PAYMENTS.MODE_REGLEMENT = 'CB' THEN PAYMENTS.PAYMENT_ID END) AS nb_paiements_cb", result.Sql);
        Assert.Contains("GROUP BY PAYMENTS.PERSON_ID", result.Sql);
        Assert.DoesNotContain("SELECT * FROM (", result.Sql);
    }

    [Fact]
    public void Generate_ConditionalSum_EmitsCaseWhenSumWithoutSubquery()
    {
        const string sql = @"CREATE TABLE PAYMENTS (PAYMENT_ID INTEGER PRIMARY KEY, PERSON_ID INTEGER, STATUS TEXT, AMOUNT NUMBER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "PAYMENTS" };
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

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "jobs", Column = "name" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "jobs", Column = "id" },
            Alias = "id_agg"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("INNER JOIN jobs ON pnj.job_id = jobs.id", result.Sql);
        Assert.DoesNotContain("pnj.id = jobs.id", result.Sql);
    }


    [Fact]
    public void Generate_AutoJoin_UsesJunctionTableBetweenBaseAndSelectedTable()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    nom TEXT
);
CREATE TABLE items (
    id INTEGER,
    name TEXT
);
CREATE TABLE pnj_item (
    pnj_id INTEGER,
    item_id INTEGER
);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "items", Column = "name" });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("INNER JOIN pnj_item ON pnj.id = pnj_item.pnj_id", result.Sql);
        Assert.Contains("INNER JOIN items ON pnj_item.item_id = items.id", result.Sql);
        Assert.DoesNotContain("pnj.id = items.id", result.Sql);
    }


    [Fact]
    public void Generate_AutoJoin_PrefersExactJunctionTableOverSpecificFocusTable()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    job_id INTEGER,
    genre TEXT
);
CREATE TABLE items (
    id INTEGER,
    name TEXT
);
CREATE TABLE pnj_item (
    pnj_id INTEGER,
    item_id INTEGER
);
CREATE TABLE pnj_jobs_items_focus (
    pnj_id INTEGER,
    item_id INTEGER,
    focus_score REAL
);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "items", Column = "name" });
        query.GroupBy.Add(new ColumnReference { Table = "items", Column = "name" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "pnj", Column = "genre" },
            Alias = "count_genre"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("INNER JOIN pnj_item ON pnj.id = pnj_item.pnj_id", result.Sql);
        Assert.Contains("INNER JOIN items ON pnj_item.item_id = items.id", result.Sql);
        Assert.DoesNotContain("pnj_jobs_items_focus", result.Sql);
    }


    [Fact]
    public void Generate_AutoJoin_RejectsLongDetourWhenDirectJunctionExists()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    job_id INTEGER,
    genre TEXT
);
CREATE TABLE items (
    id INTEGER,
    base_item_code INTEGER,
    name TEXT
);
CREATE TABLE pnj_item (
    pnj_id INTEGER,
    item_id INTEGER
);
CREATE TABLE pnj_lastname_lineage (
    id INTEGER,
    pnj_id INTEGER
);
CREATE TABLE quests_pnjs (
    quest_id INTEGER,
    pnj_id INTEGER
);
CREATE TABLE pnj_jobs_items_focus (
    id INTEGER,
    pnj_id INTEGER,
    item_id INTEGER,
    base_item_code INTEGER
);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "items", Column = "name" });
        query.GroupBy.Add(new ColumnReference { Table = "items", Column = "name" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "pnj", Column = "genre" },
            Alias = "count_genre"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("INNER JOIN pnj_item ON pnj.id = pnj_item.pnj_id", result.Sql);
        Assert.Contains("INNER JOIN items ON pnj_item.item_id = items.id", result.Sql);
        Assert.DoesNotContain("pnj_lastname_lineage", result.Sql);
        Assert.DoesNotContain("quests_pnjs", result.Sql);
        Assert.DoesNotContain("pnj_jobs_items_focus", result.Sql);
        Assert.DoesNotContain("pnj_item.item_id = items.base_item_code", result.Sql);
    }

    [Fact]
    public void Generate_DisabledAutoJoin_DoesNotUseThatDetectedRelationship()
    {
        const string sql = @"
CREATE TABLE pnj (id INTEGER);
CREATE TABLE items (id INTEGER, name TEXT);
CREATE TABLE pnj_item (pnj_id INTEGER, item_id INTEGER);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "items", Column = "name" });
        query.DisabledAutoJoinKeys.Add(RelationshipKey.For("pnj", "id", "pnj_item", "pnj_id"));
        query.DisabledAutoJoinKeys.Add(RelationshipKey.ReverseFor("pnj", "id", "pnj_item", "pnj_id"));

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.DoesNotContain("JOIN pnj_item", result.Sql);
        Assert.Contains("Aucune jointure fiable", string.Join("\n", result.Warnings));
    }

    [Fact]
    public void Generate_FilterOnAggregate_EmitsHavingWithoutSubquery()
    {
        const string sql = @"CREATE TABLE pnj (id INTEGER, race_id INTEGER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "race_id" });
        query.GroupBy.Add(new ColumnReference { Table = "pnj", Column = "race_id" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "pnj", Column = "id" },
            Alias = "count_id"
        });
        query.Filters.Add(new FilterCondition
        {
            FieldKind = QueryFieldKind.Aggregate,
            FieldAlias = "count_id",
            Operator = ">",
            Value = "10"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("COUNT(pnj.id) AS count_id", result.Sql);
        Assert.Contains("GROUP BY pnj.race_id", result.Sql);
        Assert.Contains("HAVING COUNT(pnj.id) > 10", result.Sql);
        Assert.DoesNotContain("WHERE COUNT", result.Sql);
        Assert.DoesNotContain("SELECT * FROM (", result.Sql);
    }

    [Fact]
    public void Generate_OrderByAggregateAlias_UsesAlias()
    {
        const string sql = @"CREATE TABLE pnj (id INTEGER, race_id INTEGER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "race_id" });
        query.Aggregates.Add(new AggregateSelection
        {
            Function = AggregateFunction.Count,
            Column = new ColumnReference { Table = "pnj", Column = "id" },
            Alias = "count_id"
        });
        query.OrderBy.Add(new OrderByItem
        {
            FieldKind = QueryFieldKind.Aggregate,
            FieldAlias = "count_id",
            Direction = SortDirection.Descending
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema, new SqlGeneratorOptions { AutoGroupSelectedColumnsWhenAggregating = true });

        Assert.Contains("ORDER BY count_id DESC", result.Sql);
    }

    [Fact]
    public void Generate_FilterAndOrderOnCustomColumn_UsesExpressionForWhereAndAliasForOrder()
    {
        const string sql = @"CREATE TABLE pnj (id INTEGER, race_id INTEGER);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.CustomColumns.Add(new CustomColumnSelection
        {
            Alias = "race_label",
            CaseColumn = new ColumnReference { Table = "pnj", Column = "race_id" },
            CaseOperator = "=",
            CaseCompareValue = "1",
            CaseThenValue = "Humain",
            CaseElseValue = "Autre"
        });
        query.Filters.Add(new FilterCondition
        {
            FieldKind = QueryFieldKind.CustomColumn,
            FieldAlias = "race_label",
            Operator = "=",
            Value = "Humain"
        });
        query.OrderBy.Add(new OrderByItem
        {
            FieldKind = QueryFieldKind.CustomColumn,
            FieldAlias = "race_label",
            Direction = SortDirection.Ascending
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("CASE WHEN pnj.race_id = 1 THEN 'Humain' ELSE 'Autre' END AS race_label", result.Sql);
        Assert.Contains("WHERE CASE WHEN pnj.race_id = 1 THEN 'Humain' ELSE 'Autre' END = 'Humain'", result.Sql);
        Assert.Contains("ORDER BY race_label ASC", result.Sql);
    }


    [Fact]
    public void Generate_AutoJoin_PrefersDirectDetectedLookupOverBridgeDetour()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER PRIMARY KEY,
    age INTEGER,
    nom TEXT,
    prenom TEXT,
    race_id INTEGER
);
CREATE TABLE pnj_race_descriptions (
    id INTEGER PRIMARY KEY,
    legal_majority INTEGER,
    name TEXT
);
CREATE TABLE seances_pnjs (
    id INTEGER PRIMARY KEY,
    pnj_id INTEGER,
    seance_id INTEGER
);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "age" });
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "id" });
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "nom" });
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj", Column = "prenom" });
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj_race_descriptions", Column = "legal_majority" });
        query.Filters.Add(new FilterCondition
        {
            Column = new ColumnReference { Table = "pnj", Column = "age" },
            Operator = ">",
            ValueKind = FilterValueKind.RawSql,
            Value = "pnj_race_descriptions.legal_majority"
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("INNER JOIN pnj_race_descriptions ON pnj.race_id = pnj_race_descriptions.id", result.Sql);
        Assert.DoesNotContain("seances_pnjs", result.Sql);
        Assert.DoesNotContain("Aucune jointure fiable", string.Join("\n", result.Warnings));
    }

    [Fact]
    public void Generate_AutoJoin_StillUsesDirectLookupWhenUnrelatedBridgeIsDisabled()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER PRIMARY KEY,
    race_id INTEGER
);
CREATE TABLE pnj_race_descriptions (
    id INTEGER PRIMARY KEY,
    legal_majority INTEGER
);
CREATE TABLE seances_pnjs (
    id INTEGER PRIMARY KEY,
    pnj_id INTEGER
);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "pnj" };
        query.SelectedColumns.Add(new ColumnReference { Table = "pnj_race_descriptions", Column = "legal_majority" });
        query.DisabledAutoJoinKeys.Add(RelationshipKey.For("pnj", "id", "seances_pnjs", "pnj_id"));
        query.DisabledAutoJoinKeys.Add(RelationshipKey.ReverseFor("pnj", "id", "seances_pnjs", "pnj_id"));

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("INNER JOIN pnj_race_descriptions ON pnj.race_id = pnj_race_descriptions.id", result.Sql);
        Assert.DoesNotContain("seances_pnjs", result.Sql);
        Assert.DoesNotContain("Aucune jointure fiable", string.Join("\n", result.Warnings));
    }

}

// v21 regression tests are intentionally kept simple and focused on core behavior.
public sealed class GenerationV21Tests
{
    [Fact]
    public void Generate_FilterWithSubquery_EmbedsSavedQueryAsRightSide()
    {
        const string sql = @"
CREATE TABLE ACTIONS (ACTI_IDEN INTEGER PRIMARY KEY, LABEL TEXT);
CREATE TABLE PAYMENTS (ID INTEGER PRIMARY KEY, ACTI_IDEN INTEGER, AMOUNT NUMBER);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition sub = new() { Name = "actions_by_label", BaseTable = "ACTIONS" };
        sub.SelectedColumns.Add(new ColumnReference { Table = "ACTIONS", Column = "ACTI_IDEN" });
        sub.Filters.Add(new FilterCondition
        {
            Column = new ColumnReference { Table = "ACTIONS", Column = "LABEL" },
            Operator = "=",
            ValueKind = FilterValueKind.Parameter,
            Value = "label"
        });

        QueryDefinition main = new() { BaseTable = "PAYMENTS" };
        main.SelectedColumns.Add(new ColumnReference { Table = "PAYMENTS", Column = "ID" });
        main.Filters.Add(new FilterCondition
        {
            Column = new ColumnReference { Table = "PAYMENTS", Column = "ACTI_IDEN" },
            Operator = "IN",
            ValueKind = FilterValueKind.Subquery,
            SubqueryName = "actions_by_label",
            Subquery = sub
        });

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(main, schema);

        Assert.Contains("PAYMENTS.ACTI_IDEN IN (", result.Sql);
        Assert.Contains("SELECT ACTIONS.ACTI_IDEN", result.Sql);
        Assert.Contains("WHERE ACTIONS.LABEL = :label", result.Sql);
    }
}

public sealed class GenerationV24Tests
{
    [Fact]
    public void Generate_ManualCompositeJoin_EmitsAndPredicates()
    {
        const string sql = @"
CREATE TABLE A (acti_iden INTEGER, emet_iden INTEGER, soaa_date TEXT, amount NUMBER);
CREATE TABLE B (acti_iden INTEGER, emet_iden INTEGER, soaa_date TEXT, label TEXT);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "A" };
        query.SelectedColumns.Add(new ColumnReference { Table = "B", Column = "label" });
        JoinDefinition join = new()
        {
            FromTable = "A",
            FromColumn = "acti_iden",
            ToTable = "B",
            ToColumn = "acti_iden"
        };
        join.AdditionalColumnPairs.Add(new JoinColumnPair { FromColumn = "emet_iden", ToColumn = "emet_iden", Enabled = true });
        join.AdditionalColumnPairs.Add(new JoinColumnPair { FromColumn = "soaa_date", ToColumn = "soaa_date", Enabled = true });
        query.Joins.Add(join);

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("INNER JOIN B ON A.acti_iden = B.acti_iden AND A.emet_iden = B.emet_iden AND A.soaa_date = B.soaa_date", result.Sql);
    }

    [Fact]
    public void Generate_ManualCompositeJoin_IgnoresDisabledPair()
    {
        const string sql = @"
CREATE TABLE A (acti_iden INTEGER, emet_iden INTEGER, soaa_date TEXT);
CREATE TABLE B (acti_iden INTEGER, emet_iden INTEGER, soaa_date TEXT);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        QueryDefinition query = new() { BaseTable = "A" };
        query.SelectedColumns.Add(new ColumnReference { Table = "B", Column = "soaa_date" });
        JoinDefinition join = new()
        {
            FromTable = "A",
            FromColumn = "acti_iden",
            ToTable = "B",
            ToColumn = "acti_iden"
        };
        join.AdditionalColumnPairs.Add(new JoinColumnPair { FromColumn = "emet_iden", ToColumn = "emet_iden", Enabled = false });
        query.Joins.Add(join);

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        Assert.Contains("INNER JOIN B ON A.acti_iden = B.acti_iden", result.Sql);
        Assert.DoesNotContain("A.emet_iden = B.emet_iden", result.Sql);
    }
}
