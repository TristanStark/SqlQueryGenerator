using SqlQueryGenerator.Core.Parsing;

namespace SqlQueryGenerator.Tests;

public sealed class InferenceTests
{
    [Fact]
    public void Infer_SameColumnAsPrimaryKey_FindsRelationship()
    {
        const string sql = @"
CREATE TABLE CUSTOMER (
    CUSTOMER_ID NUMBER PRIMARY KEY,
    NAME VARCHAR2(100)
);
CREATE TABLE ORDERS (
    ORDER_ID NUMBER PRIMARY KEY,
    CUSTOMER_ID NUMBER,
    AMOUNT NUMBER
);
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "ORDERS" && r.FromColumn == "CUSTOMER_ID" && r.ToTable == "CUSTOMER" && r.ToColumn == "CUSTOMER_ID");
    }

    [Fact]
    public void Infer_TableNamePattern_FindsOrdIdenRelationship()
    {
        const string sql = @"
CREATE TABLE ORD (
    ORD_IDEN NUMBER PRIMARY KEY
);
CREATE TABLE MVTO (
    MVTO_IDEN NUMBER PRIMARY KEY,
    ORD_IDEN NUMBER
);
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "MVTO" && r.FromColumn == "ORD_IDEN" && r.ToTable == "ORD" && r.ToColumn == "ORD_IDEN");
    }

    [Fact]
    public void Infer_SingularForeignKeyColumn_ToPluralTableId_WinsOverGenericId()
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

        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "jobs" && r.ToColumn == "id");
        Assert.DoesNotContain(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "id" && r.ToTable == "jobs" && r.ToColumn == "id");
    }

    [Fact]
    public void Infer_GroupId_ToCompoundPluralTableId()
    {
        const string sql = @"
CREATE TABLE jobs (
    id INTEGER,
    group_id INTEGER,
    name TEXT
);
CREATE TABLE jobs_groups (
    id INTEGER,
    name TEXT
);
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "jobs" && r.FromColumn == "group_id" && r.ToTable == "jobs_groups" && r.ToColumn == "id");
    }


    [Fact]
    public void Infer_SourceColumnStem_ToSourcePrefixedPluralLookupTable()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    nom TEXT,
    job_id INTEGER
);
CREATE TABLE pnj_jobs (
    id INTEGER,
    name TEXT
);
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
        Assert.DoesNotContain(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
    }


    [Fact]
    public void Infer_SourcePrefixedLookup_Outranks_GenericPluralLookup_WhenBothExist()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    job_id INTEGER
);
CREATE TABLE jobs (
    id INTEGER,
    name TEXT
);
CREATE TABLE pnj_jobs (
    id INTEGER,
    name TEXT
);
";
        var schema = new SqlSchemaParser().Parse(sql);

        var pnjJobs = schema.Relationships.Single(r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
        var jobs = schema.Relationships.Single(r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "jobs" && r.ToColumn == "id");
        Assert.True(pnjJobs.Confidence > jobs.Confidence);
    }

}
