using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Représente InferenceTests dans SQL Query Generator.
/// </summary>
public sealed class InferenceTests
{
    /// <summary>
    /// Exécute le traitement Infer SameColumnAsPrimaryKey FindsRelationship.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "ORDERS" && r.FromColumn == "CUSTOMER_ID" && r.ToTable == "CUSTOMER" && r.ToColumn == "CUSTOMER_ID");
    }

    /// <summary>
    /// Exécute le traitement Infer TableNamePattern FindsOrdIdenRelationship.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "MVTO" && r.FromColumn == "ORD_IDEN" && r.ToTable == "ORD" && r.ToColumn == "ORD_IDEN");
    }

    /// <summary>
    /// Exécute le traitement Infer SingularForeignKeyColumn ToPluralTableId WinsOverGenericId.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "jobs" && r.ToColumn == "id");
        Assert.DoesNotContain(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "id" && r.ToTable == "jobs" && r.ToColumn == "id");
    }

    /// <summary>
    /// Exécute le traitement Infer GroupId ToCompoundPluralTableId.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "jobs" && r.FromColumn == "group_id" && r.ToTable == "jobs_groups" && r.ToColumn == "id");
    }


    /// <summary>
    /// Exécute le traitement Infer SourceColumnStem ToSourcePrefixedPluralLookupTable.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
        Assert.DoesNotContain(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
    }


    /// <summary>
    /// Exécute le traitement Infer SourcePrefixedLookup Outranks GenericPluralLookup WhenBothExist.
    /// </summary>
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
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        InferredRelationship pnjJobs = schema.Relationships.Single(r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "pnj_jobs" && r.ToColumn == "id");
        InferredRelationship jobs = schema.Relationships.Single(r => r.FromTable == "pnj" && r.FromColumn == "job_id" && r.ToTable == "jobs" && r.ToColumn == "id");
        Assert.True(pnjJobs.Confidence > jobs.Confidence);
    }


    /// <summary>
    /// Exécute le traitement Infer JunctionTable FindsBothSides.
    /// </summary>
    [Fact]
    public void Infer_JunctionTable_FindsBothSides()
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

        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj_item" && r.FromColumn == "pnj_id" && r.ToTable == "pnj" && r.ToColumn == "id");
        Assert.Contains(schema.Relationships, r => r.FromTable == "pnj_item" && r.FromColumn == "item_id" && r.ToTable == "items" && r.ToColumn == "id");
        Assert.DoesNotContain(schema.Relationships, r => r.FromTable == "pnj" && r.FromColumn == "id" && r.ToTable == "items" && r.ToColumn == "id");
    }

    /// <summary>
    /// Exécute le traitement Infer IndexedForeignKey GetsHigherConfidenceThanUnindexedAlternative.
    /// </summary>
    [Fact]
    public void Infer_IndexedForeignKey_GetsHigherConfidenceThanUnindexedAlternative()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER,
    item_id INTEGER,
    item_code INTEGER
);
CREATE TABLE items (
    id INTEGER,
    item_code INTEGER
);
CREATE INDEX idx_pnj_item_id ON pnj(item_id);
CREATE UNIQUE INDEX ux_items_id ON items(id);
";
        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);

        InferredRelationship indexed = schema.Relationships.Single(r => r.FromTable == "pnj" && r.FromColumn == "item_id" && r.ToTable == "items" && r.ToColumn == "id");
        Assert.Contains("Signal index", indexed.Reason);
        Assert.True(indexed.Confidence >= 0.98);
    }

}
