using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;
using SqlQueryGenerator.Core.Validation;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Covers reverse engineering, generation and validation of compound SELECT queries.
/// </summary>
public sealed class CompoundQueryTests
{
    [Fact]
    public void ReverseParser_UnionAllChain_ModelsAndRegeneratesEveryBranch()
    {
        const string sql = """
            SELECT active_customer.id AS customer_id
            FROM active_customer
            WHERE active_customer.enabled = 1
            UNION ALL
            SELECT archived_customer.id AS customer_id
            FROM archived_customer
            WHERE archived_customer.deleted_at IS NOT NULL
            INTERSECT
            SELECT allowed_customer.id AS customer_id
            FROM allowed_customer
            ORDER BY customer_id DESC
            LIMIT 25
            """;

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(
            query,
            new DatabaseSchema(),
            new SqlGeneratorOptions { Dialect = SqlDialect.SQLite });

        Assert.Equal(2, query.SetOperations.Count);
        Assert.Equal(SetOperationKind.Union, query.SetOperations[0].Operator);
        Assert.True(query.SetOperations[0].All);
        Assert.Equal(SetOperationKind.Intersect, query.SetOperations[1].Operator);
        Assert.Single(query.CompoundOrderBy);
        Assert.Equal(25, query.CompoundLimitRows);
        Assert.Contains("UNION ALL", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM archived_customer", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INTERSECT", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM allowed_customer", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY customer_id DESC", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 25", generated.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReverseParser_ParenthesizedNestedSetOperation_PreservesGrouping()
    {
        const string sql = """
            SELECT current_item.id
            FROM current_item
            UNION
            (
                SELECT archived_item.id
                FROM archived_item
                EXCEPT
                SELECT blocked_item.id
                FROM blocked_item
            )
            """;

        QueryDefinition query = new SqlSelectReverseParser().Parse(sql);
        SetOperationDefinition union = Assert.Single(query.SetOperations);
        Assert.True(union.ParenthesizeQuery);
        SetOperationDefinition exceptOperation = Assert.Single(union.Query.SetOperations);
        Assert.Equal(SetOperationKind.Except, exceptOperation.Operator);

        string generated = new SqlQueryGeneratorEngine()
            .Generate(query, new DatabaseSchema(), new SqlGeneratorOptions { Dialect = SqlDialect.Generic })
            .Sql;

        Assert.Contains("UNION", generated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXCEPT", generated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(" + Environment.NewLine, generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseImport_SetOperations_AreReportedAsFullyImported()
    {
        const string sql = """
            SELECT CUSTOMER.ID
            FROM CUSTOMER
            EXCEPT
            SELECT ARCHIVED_CUSTOMER.ID
            FROM ARCHIVED_CUSTOMER
            """;

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql);

        Assert.Single(imported.Query.SetOperations);
        Assert.Contains(
            imported.Coverage.Clauses,
            clause => clause.Clause == "Set operations"
                && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.DoesNotContain(
            imported.Diagnostics,
            diagnostic => diagnostic.Clause == "Set operations"
                && diagnostic.Severity == ReverseSqlDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Generate_Minus_UsesOracleMinusAndSQLiteExcept()
    {
        QueryDefinition query = new() { BaseTable = "CURRENT_ITEM" };
        query.SelectedColumns.Add(new ColumnReference { Table = "CURRENT_ITEM", Column = "ID" });
        QueryDefinition archived = new() { BaseTable = "ARCHIVED_ITEM" };
        archived.SelectedColumns.Add(new ColumnReference { Table = "ARCHIVED_ITEM", Column = "ID" });
        query.SetOperations.Add(new SetOperationDefinition
        {
            Operator = SetOperationKind.Minus,
            Query = archived
        });

        string oracle = new SqlQueryGeneratorEngine()
            .Generate(query, new DatabaseSchema(), new SqlGeneratorOptions { Dialect = SqlDialect.Oracle })
            .Sql;
        SqlGenerationResult sqlite = new SqlQueryGeneratorEngine()
            .Generate(query, new DatabaseSchema(), new SqlGeneratorOptions { Dialect = SqlDialect.SQLite });

        Assert.Contains("MINUS", oracle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXCEPT", sqlite.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(sqlite.Warnings, warning => warning.Contains("MINUS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_CompoundProjectionMismatch_ReturnsExplicitError()
    {
        DatabaseSchema schema = new SqlSchemaParser().Parse("""
            CREATE TABLE A (ID INTEGER, NAME TEXT);
            CREATE TABLE B (ID INTEGER);
            """);
        QueryDefinition query = new() { BaseTable = "A" };
        query.SelectedColumns.Add(new ColumnReference { Table = "A", Column = "ID" });
        query.SelectedColumns.Add(new ColumnReference { Table = "A", Column = "NAME" });

        QueryDefinition branch = new() { BaseTable = "B" };
        branch.SelectedColumns.Add(new ColumnReference { Table = "B", Column = "ID" });
        query.SetOperations.Add(new SetOperationDefinition
        {
            Operator = SetOperationKind.Union,
            Query = branch
        });

        IReadOnlyList<string> errors = new QueryValidator().Validate(query, schema);

        Assert.Contains(
            errors,
            error => error.Contains("branche SELECT 2", StringComparison.OrdinalIgnoreCase)
                && error.Contains("2 colonne", StringComparison.OrdinalIgnoreCase));
    }
}
