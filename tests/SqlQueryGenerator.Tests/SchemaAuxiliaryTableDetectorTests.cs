using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.Tests;

public sealed class SchemaAuxiliaryTableDetectorTests
{
    private readonly SchemaAuxiliaryTableDetector _detector = new();

    [Theory]
    [InlineData("CUSTOMER_BACKUP")]
    [InlineData("ORDERS_HIST")]
    [InlineData("TMP_IMPORT_CLIENT")]
    [InlineData("audit_log_2024")]
    [InlineData("APP.STAGING_ORDERS")]
    [InlineData("ZZ_PRODUCTS")]
    public void IsLikelyAuxiliaryTable_SuspiciousNames_ReturnsTrue(string tableName)
    {
        Assert.True(_detector.IsLikelyAuxiliaryTable(tableName));
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("ORDER_ITEMS")]
    [InlineData("CATALOG")]
    [InlineData("CUSTOMER_STATUS")]
    [InlineData("HISTOIRE_CLIENT")]
    public void IsLikelyAuxiliaryTable_NormalBusinessNames_ReturnsFalse(string tableName)
    {
        Assert.False(_detector.IsLikelyAuxiliaryTable(tableName));
    }

    [Fact]
    public void DetectBackupCandidates_UsesBaseTableAndTrailingDigits()
    {
        DatabaseSchema schema = BuildSchema(
            new TableDefinition("CUSTOMER"),
            new TableDefinition("CUSTOMER_20240101"),
            new TableDefinition("ORDERS"),
            new TableDefinition("ORDERS_SAVE"));

        IReadOnlyList<BackupTableCandidate> candidates = _detector.DetectBackupCandidates(schema);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate => candidate.TableName == "CUSTOMER_20240101" && candidate.BaseTableName == "CUSTOMER");
        Assert.Contains(candidates, candidate => candidate.TableName == "ORDERS_SAVE" && candidate.BaseTableName == "ORDERS" && candidate.DetectionReason.Contains("SAVE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DetectBackupCandidates_RequiresExistingBaseTable()
    {
        DatabaseSchema schema = BuildSchema(new TableDefinition("CUSTOMER_20240101"));

        IReadOnlyList<BackupTableCandidate> candidates = _detector.DetectBackupCandidates(schema);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ApplyImportSelection_ExcludesSelectedTablesAndMetadata()
    {
        DatabaseSchema schema = BuildSchema(
            new TableDefinition("CUSTOMER"),
            new TableDefinition("CUSTOMER_BACKUP_20240101"),
            new TableDefinition("ORDERS"));

        schema.Indexes.Add(new IndexDefinition("IX_CUSTOMER_BACKUP_ID", "CUSTOMER_BACKUP_20240101", false, ["ID"]));
        schema.DeclaredForeignKeys.Add(new DeclaredForeignKey
        {
            FromTable = "CUSTOMER_BACKUP_20240101",
            FromColumn = "ORDER_ID",
            ToTable = "ORDERS",
            ToColumn = "ID"
        });
        schema.Relationships.Add(new InferredRelationship
        {
            FromTable = "CUSTOMER_BACKUP_20240101",
            FromColumn = "ORDER_ID",
            ToTable = "ORDERS",
            ToColumn = "ID",
            Confidence = 0.82,
            Source = RelationshipSource.DeclaredForeignKey,
            Reason = "declared fk"
        });

        SchemaImportFilterResult result = _detector.ApplyImportSelection(schema, ["CUSTOMER_BACKUP_20240101"]);

        Assert.Single(result.ExcludedCandidates);
        Assert.DoesNotContain(result.Schema.Tables, table => table.FullName == "CUSTOMER_BACKUP_20240101");
        Assert.DoesNotContain(result.Schema.Indexes, index => index.Table == "CUSTOMER_BACKUP_20240101");
        Assert.DoesNotContain(result.Schema.DeclaredForeignKeys, fk => fk.FromTable == "CUSTOMER_BACKUP_20240101" || fk.ToTable == "CUSTOMER_BACKUP_20240101");
        Assert.DoesNotContain(result.Schema.Relationships, relationship => relationship.FromTable == "CUSTOMER_BACKUP_20240101" || relationship.ToTable == "CUSTOMER_BACKUP_20240101");
    }

    [Fact]
    public void ApplyImportSelection_KeepsViewsAndAddsWarningWhenExcludedTableIsReferenced()
    {
        DatabaseSchema schema = BuildSchema(
            new TableDefinition("CUSTOMER"),
            new TableDefinition("CUSTOMER_20240101"),
            new TableDefinition("ACTIVE_CUSTOMER", isView: true, viewSql: "SELECT * FROM CUSTOMER_20240101"));

        SchemaImportFilterResult result = _detector.ApplyImportSelection(schema, ["CUSTOMER_20240101"]);

        Assert.Contains(result.Schema.Tables, table => table.FullName == "ACTIVE_CUSTOMER" && table.IsView);
        Assert.Contains(result.Schema.Warnings, warning => warning.Contains("CUSTOMER_20240101", StringComparison.OrdinalIgnoreCase));
    }

    private static DatabaseSchema BuildSchema(params TableDefinition[] tables)
    {
        DatabaseSchema schema = new();
        foreach (TableDefinition table in tables)
        {
            table.Columns.Add(new ColumnDefinition
            {
                TableName = table.FullName,
                Name = "ID",
                IsPrimaryKey = true
            });
            table.Columns.Add(new ColumnDefinition
            {
                TableName = table.FullName,
                Name = "ORDER_ID"
            });
            schema.Tables.Add(table);
        }

        return schema;
    }
}
