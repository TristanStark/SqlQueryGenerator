from __future__ import annotations

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    content = read(path)
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one occurrence, found {count}: {old[:100]!r}")
    write(path, content.replace(old, new, 1))


def replace_regex_once(path: str, pattern: str, replacement: str) -> None:
    content = read(path)
    updated, count = re.subn(pattern, replacement, content, count=1, flags=re.MULTILINE | re.DOTALL)
    if count != 1:
        raise RuntimeError(f"{path}: regex expected one occurrence, found {count}: {pattern[:100]!r}")
    write(path, updated)


def remove_if_exists(path: str) -> None:
    target = ROOT / path
    if target.exists():
        target.unlink()


# MainViewModel keeps all imported set-operation branches while the first branch is edited.
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """    private readonly Dictionary<string, string> _tableAliases = new(StringComparer.OrdinalIgnoreCase);
    private DdlExportDialect _ddlExportDialect = DdlExportDialect.SQLite;
""",
    """    private readonly Dictionary<string, string> _tableAliases = new(StringComparer.OrdinalIgnoreCase);
    private QueryDefinition? _compoundQueryTemplate;
    private DdlExportDialect _ddlExportDialect = DdlExportDialect.SQLite;
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """        foreach (RelationshipItemViewModel? disabled in Relationships.Where(r => !r.IsEnabled))
        {
            query.DisabledAutoJoinKeys.Add(disabled.Key);
            query.DisabledAutoJoinKeys.Add(disabled.ReverseKey);
        }

        return query;
""",
    """        foreach (RelationshipItemViewModel? disabled in Relationships.Where(r => !r.IsEnabled))
        {
            query.DisabledAutoJoinKeys.Add(disabled.Key);
            query.DisabledAutoJoinKeys.Add(disabled.ReverseKey);
        }

        ApplyCompoundQueryTemplate(query);
        return query;
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """    /// <summary>
    /// Exécute le traitement AddImplicitParametersFromFilters.
""",
    """    /// <summary>
    /// Restores the non-visible branches and global clauses of the last imported compound query.
    /// </summary>
    /// <param name="query">Current first branch rebuilt from the visual controls.</param>
    private void ApplyCompoundQueryTemplate(QueryDefinition query)
    {
        if (_compoundQueryTemplate is null || _compoundQueryTemplate.SetOperations.Count == 0)
        {
            return;
        }

        query.FirstBranchParenthesized = _compoundQueryTemplate.FirstBranchParenthesized;
        query.CompoundLimitRows = _compoundQueryTemplate.CompoundLimitRows;

        foreach (OrderByItem orderBy in _compoundQueryTemplate.CompoundOrderBy)
        {
            query.CompoundOrderBy.Add(new OrderByItem
            {
                Column = orderBy.Column,
                FieldKind = orderBy.FieldKind,
                FieldAlias = orderBy.FieldAlias,
                Direction = orderBy.Direction
            });
        }

        foreach (SetOperationDefinition operation in _compoundQueryTemplate.SetOperations)
        {
            query.SetOperations.Add(new SetOperationDefinition
            {
                Operator = operation.Operator,
                All = operation.All,
                ParenthesizeQuery = operation.ParenthesizeQuery,
                Query = QueryDefinitionCloner.Clone(operation.Query)
            });
        }
    }

    /// <summary>
    /// Exécute le traitement AddImplicitParametersFromFilters.
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """    private void LoadQueryDefinition(QueryDefinition query, string? name, string? description)
    {
        _suppressAutoGenerate = true;
""",
    """    private void LoadQueryDefinition(QueryDefinition query, string? name, string? description)
    {
        _compoundQueryTemplate = query.SetOperations.Count == 0
            ? null
            : QueryDefinitionCloner.Clone(query);
        _suppressAutoGenerate = true;
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """    private void ClearQuery()
    {
        _suppressAutoGenerate = true;
""",
    """    private void ClearQuery()
    {
        _compoundQueryTemplate = null;
        _suppressAutoGenerate = true;
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """            Status = "Reverse SQL terminé: les clauses reconnues ont été replacées dans le constructeur visuel.";
""",
    """            Status = query.SetOperations.Count == 0
                ? "Reverse SQL terminé: les clauses reconnues ont été replacées dans le constructeur visuel."
                : $"Reverse SQL terminé: {CountCompoundBranches(query)} branches SELECT ont été chargées et seront régénérées ensemble.";
""",
)
replace_once(
    "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs",
    """    private static string BuildReverseSqlFeedbackText(ReverseSqlImportResult imported, string headline)
    {
""",
    """    private static int CountCompoundBranches(QueryDefinition query)
    {
        return 1 + query.SetOperations.Sum(operation => CountCompoundBranches(operation.Query));
    }

    private static string BuildReverseSqlFeedbackText(ReverseSqlImportResult imported, string headline)
    {
""",
)

# Core regression tests.
compound_tests = r'''using SqlQueryGenerator.Core.Generation;
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
'''
write("tests/SqlQueryGenerator.Tests/CompoundQueryTests.cs", compound_tests)

# Replace old partial-support tests with full-support expectations.
replace_regex_once(
    "tests/SqlQueryGenerator.Tests/ReverseSqlTests.cs",
    r'''    /// <summary>
    /// Ensures unsupported advanced constructs are surfaced explicitly in the coverage report\.
    /// </summary>
    \[Fact\]
    public void ReverseImport_AdvancedSql_ReturnsUnsupportedCoverageEntries\(\)
    \{.*?
    \}
''',
    '''    /// <summary>
    /// Ensures CTE preservation and set-operation reconstruction are reported independently.
    /// </summary>
    [Fact]
    public void ReverseImport_AdvancedSql_ImportsSetOperationsWhileKeepingCteDiagnostic()
    {
        const string sql = @"\nWITH recent_pnj AS (\n    SELECT id, age\n    FROM pnj\n)\nSELECT id\nFROM recent_pnj\nUNION\nSELECT id\nFROM archived_pnj";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);

        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "CTE" && clause.Status == ReverseSqlCoverageStatus.Unsupported);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
        Assert.Single(imported.Query.SetOperations);
        Assert.Contains(imported.Warnings, warning => warning.Contains("CTE", StringComparison.OrdinalIgnoreCase));
    }
''',
)
replace_regex_once(
    "tests/SqlQueryGenerator.Tests/ReverseSqlTests.cs",
    r'''    /// <summary>
    /// Ensures EXCEPT queries keep the first branch editable while surfacing set-operation diagnostics\.
    /// </summary>
    \[Fact\]
    public void ReverseImport_ExceptQuery_PartiallyImportsFirstSelectAndFlagsSetOperation\(\)
    \{.*?
    \}
''',
    '''    /// <summary>
    /// Ensures EXCEPT queries import and regenerate every SELECT branch.
    /// </summary>
    [Fact]
    public void ReverseImport_ExceptQuery_ImportsAndRegeneratesEverySelect()
    {
        const string sql = @"\nSELECT CUSTOMER.ID\nFROM CUSTOMER\nEXCEPT\nSELECT ARCHIVED_CUSTOMER.ID\nFROM ARCHIVED_CUSTOMER";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.GenericSql);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(
            imported.Query,
            new DatabaseSchema(),
            new SqlGeneratorOptions { Dialect = SqlDialect.Generic });

        Assert.Equal("CUSTOMER", imported.Query.BaseTable);
        SetOperationDefinition operation = Assert.Single(imported.Query.SetOperations);
        Assert.Equal(SetOperationKind.Except, operation.Operator);
        Assert.Equal("ARCHIVED_CUSTOMER", operation.Query.BaseTable);
        Assert.Contains("EXCEPT", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM ARCHIVED_CUSTOMER", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
    }
''',
)
replace_regex_once(
    "tests/SqlQueryGenerator.Tests/ReverseSqlTests.cs",
    r'''    /// <summary>
    /// Ensures Oracle MINUS queries follow the same partial-import path as other set operations\.
    /// </summary>
    \[Fact\]
    public void ReverseImport_MinusQuery_PartiallyImportsFirstSelectAndFlagsSetOperation\(\)
    \{.*?
    \}
''',
    '''    /// <summary>
    /// Ensures Oracle MINUS queries import and regenerate every SELECT branch.
    /// </summary>
    [Fact]
    public void ReverseImport_MinusQuery_ImportsAndRegeneratesEverySelect()
    {
        const string sql = @"\nSELECT pnj.id\nFROM pnj\nMINUS\nSELECT archived_pnj.id\nFROM archived_pnj";

        ReverseSqlImportResult imported = new ReverseSqlImportService().Import(sql, SourceSqlDialect.OracleLegacy);
        SqlGenerationResult generated = new SqlQueryGeneratorEngine().Generate(
            imported.Query,
            new DatabaseSchema(),
            new SqlGeneratorOptions { Dialect = SqlDialect.Oracle });

        Assert.Equal("pnj", imported.Query.BaseTable);
        SetOperationDefinition operation = Assert.Single(imported.Query.SetOperations);
        Assert.Equal(SetOperationKind.Minus, operation.Operator);
        Assert.Equal("archived_pnj", operation.Query.BaseTable);
        Assert.Contains("MINUS", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM archived_pnj", generated.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(imported.Coverage.Clauses, clause => clause.Clause == "Set operations" && clause.Status == ReverseSqlCoverageStatus.FullyImported);
    }
''',
)

# App-level workflow regression: branches survive an edit in the visible first SELECT.
replace_once(
    "tests/SqlQueryGenerator.Tests/MainViewModelWorkflowTests.cs",
    """    [Fact]
    public void ReverseImportFailure_UpdatesDiagnosticsAndRawSqlSelection()
""",
    """    [Fact]
    public void ReverseImport_CompoundQuery_RemainsCompleteAfterEditingFirstBranch()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.RawSqlText = @"
            SELECT CUSTOMER.ID
            FROM CUSTOMER
            UNION ALL
            SELECT ORDERS.CUSTOMER_ID
            FROM ORDERS
            WHERE ORDERS.STATUS = :status
            ORDER BY ID
            ";

        vm.ReverseEngineerRawSqlCommand.Execute(null);

        Assert.Contains("UNION ALL", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM ORDERS", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 branches SELECT", vm.Status, StringComparison.OrdinalIgnoreCase);

        SelectColumnRowViewModel firstColumn = Assert.Single(vm.SelectedColumns);
        firstColumn.Alias = "CUSTOMER_KEY";

        Assert.Contains("CUSTOMER.ID AS CUSTOMER_KEY", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNION ALL", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM ORDERS", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReverseImportFailure_UpdatesDiagnosticsAndRawSqlSelection()
""",
)

# The old corpus fixtures are now valid inputs rather than expected failures.
for fixture in ("unsupported_union", "unsupported_except", "unsupported_minus"):
    remove_if_exists(f"tests/SqlQueryGenerator.Tests/Fixtures/ReverseSql/{fixture}.expected-error.txt")

# Documentation.
replace_once(
    "docs/V28_RAW_SQL_PRESETS_AND_REVERSE.md",
    """- `ORDER BY`.

## Limites assumées
""",
    """- `ORDER BY` ;
- requêtes composées avec `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT` et `MINUS`, y compris les chaînes de plusieurs branches, les regroupements parenthésés et le tri/limit global.

Lorsqu'une requête composée est chargée dans le constructeur, la première branche reste affichée dans les contrôles visuels et les branches suivantes sont conservées dans le modèle. Toute régénération, sauvegarde, restauration d'historique ou réécriture réémet l'ensemble des branches.

## Limites assumées
""",
)

changelog_path = "CHANGELOG.md"
changelog = read(changelog_path)
entry = "- Added full reverse-engineering and regeneration support for compound SELECT queries using `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT`, and Oracle `MINUS`, including nested parenthesized branches and global `ORDER BY`/row limits."
if entry not in changelog:
    marker = "## Unreleased"
    if marker not in changelog:
        raise RuntimeError("CHANGELOG.md: Unreleased marker not found")
    changelog = changelog.replace(marker, marker + "\n\n" + entry, 1)
    write(changelog_path, changelog)

print("App, tests and documentation compound-query patch applied.")
