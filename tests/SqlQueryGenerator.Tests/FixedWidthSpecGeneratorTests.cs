using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

public sealed class FixedWidthSpecGeneratorTests
{
    [Fact]
    public void Generate_FixedWidthColumns_ComputesPositionsAndTotalLength()
    {
        const string ddl = @"
CREATE TABLE CUSTOMER (
    CUSTOMER_ID INTEGER,
    NAME VARCHAR2(100),
    BIRTH_DATE DATE
);
";

        DatabaseSchema schema = new SqlSchemaParser().Parse(ddl);

        QueryDefinition query = new()
        {
            Name = "export_clients",
            BaseTable = "CUSTOMER"
        };

        query.SelectedColumns.Add(new ColumnReference
        {
            Table = "CUSTOMER",
            Column = "CUSTOMER_ID",
            Alias = "client_id",
            NullAllowed = false,
            UseFixedLength = true,
            FixedLength = 6
        });

        query.SelectedColumns.Add(new ColumnReference
        {
            Table = "CUSTOMER",
            Column = "NAME",
            Alias = "nom_client",
            NullAllowed = false,
            UseFixedLength = true,
            FixedLength = 30
        });

        FixedWidthSpecReport report = new FixedWidthSpecGenerator().Generate(
            query,
            schema,
            new FixedWidthSpecOptions
            {
                ProfileName = "profil_clients",
                Description = "Export clients",
                GeneratedAtUtc = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero)
            });

        Assert.Equal(36, report.TotalLength);
        Assert.Equal(2, report.Fields.Count);
        Assert.Empty(report.Warnings);

        Assert.Equal(1, report.Fields[0].StartPosition);
        Assert.Equal(6, report.Fields[0].EndPosition);
        Assert.Equal("gauche", report.Fields[0].PaddingDirection);
        Assert.Equal("0", report.Fields[0].PaddingCharacter);

        Assert.Equal(7, report.Fields[1].StartPosition);
        Assert.Equal(36, report.Fields[1].EndPosition);
        Assert.Equal("droite", report.Fields[1].PaddingDirection);
        Assert.Equal("space", report.Fields[1].PaddingCharacter);

        Assert.Contains("001-006", report.Markdown);
        Assert.Contains("007-036", report.Markdown);
        Assert.Contains("profil_clients", report.Markdown);
    }

    [Fact]
    public void Generate_MissingFixedLength_ReportsWarning()
    {
        const string ddl = @"CREATE TABLE CUSTOMER (NAME VARCHAR2(100));";
        DatabaseSchema schema = new SqlSchemaParser().Parse(ddl);

        QueryDefinition query = new()
        {
            BaseTable = "CUSTOMER"
        };

        query.SelectedColumns.Add(new ColumnReference
        {
            Table = "CUSTOMER",
            Column = "NAME",
            UseFixedLength = false
        });

        FixedWidthSpecReport report = new FixedWidthSpecGenerator().Generate(query, schema);

        Assert.Single(report.Warnings);
        Assert.Contains("longueur fixe désactivée", report.Warnings[0]);
        Assert.Equal(0, report.TotalLength);
        Assert.Contains("N/A", report.Markdown);
    }

    [Fact]
    public void Generate_WildcardSelection_ReportsWarning()
    {
        const string ddl = @"CREATE TABLE CUSTOMER (ID INTEGER, NAME TEXT);";
        DatabaseSchema schema = new SqlSchemaParser().Parse(ddl);

        QueryDefinition query = new()
        {
            BaseTable = "CUSTOMER"
        };

        query.SelectedColumns.Add(new ColumnReference
        {
            Table = "CUSTOMER",
            Column = "*",
            UseFixedLength = true,
            FixedLength = 10
        });

        FixedWidthSpecReport report = new FixedWidthSpecGenerator().Generate(query, schema);

        Assert.Single(report.Warnings);
        Assert.Contains("Wildcard", report.Warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Wildcard non supporté", report.Markdown);
    }
}
