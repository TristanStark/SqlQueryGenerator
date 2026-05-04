using SqlQueryGenerator.Core.Parsing;

namespace SqlQueryGenerator.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Parse_OracleCommentsAndPrimaryKeys_ExtractsTablesColumnsAndComments()
    {
        const string sql = @"
CREATE TABLE ORD (
    ORD_IDEN NUMBER(10) NOT NULL,
    CUSTOMER_ID NUMBER(10),
    STATUS VARCHAR2(20),
    CONSTRAINT PK_ORD PRIMARY KEY (ORD_IDEN)
);
COMMENT ON TABLE ORD IS 'Orders table';
COMMENT ON COLUMN ORD.CUSTOMER_ID IS 'Customer identifier';
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Single(schema.Tables);
        var table = schema.Tables[0];
        Assert.Equal("ORD", table.Name);
        Assert.Equal("Orders table", table.Comment);
        Assert.True(table.FindColumn("ORD_IDEN")!.IsPrimaryKey);
        Assert.Equal("Customer identifier", table.FindColumn("CUSTOMER_ID")!.Comment);
    }

    [Fact]
    public void Parse_InlineComments_ExtractsColumnComments()
    {
        const string sql = @"
CREATE TABLE customer (
    id INTEGER PRIMARY KEY, -- technical id
    name TEXT COMMENT 'display name'
);
";
        var schema = new SqlSchemaParser().Parse(sql);
        var table = schema.FindTable("customer")!;

        Assert.Equal("technical id", table.FindColumn("id")!.Comment);
        Assert.Equal("display name", table.FindColumn("name")!.Comment);
    }

    [Fact]
    public void Parse_InlineCommentAfterComma_DoesNotAssignCommentToNextColumn()
    {
        const string sql = @"
CREATE TABLE customer (
    id INTEGER PRIMARY KEY, -- technical id
    name TEXT -- display name
);
";
        var schema = new SqlSchemaParser().Parse(sql);
        var table = schema.FindTable("customer")!;

        Assert.Equal("technical id", table.FindColumn("id")!.Comment);
        Assert.Equal("display name", table.FindColumn("name")!.Comment);
    }

    [Fact]
    public void Parse_CreateIndex_ExtractsIndexedColumns()
    {
        const string sql = @"
CREATE TABLE pnj (
    id INTEGER PRIMARY KEY,
    job_id INTEGER,
    nom TEXT
);
CREATE INDEX idx_pnj_job ON pnj(job_id);
CREATE UNIQUE INDEX ux_pnj_nom ON pnj(nom);
";
        var schema = new SqlSchemaParser().Parse(sql);

        Assert.Equal(2, schema.Indexes.Count);
        Assert.True(schema.IsColumnIndexed("pnj", "job_id"));
        Assert.True(schema.IsColumnUniqueIndexed("pnj", "nom"));
        Assert.False(schema.IsColumnUniqueIndexed("pnj", "job_id"));
    }

}
