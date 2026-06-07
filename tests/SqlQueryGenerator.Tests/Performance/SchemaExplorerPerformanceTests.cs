using SqlQueryGenerator.App.Services;
using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using System.Diagnostics;
using Xunit;

namespace SqlQueryGenerator.Tests.Performance;

public sealed class SchemaExplorerPerformanceTests
{
    private const int TableCount = 650;
    private const int ColumnsPerTable = 90;
    private const int MaxSearchResults = 750;

    [Trait("Category", "Performance")]
    [Fact]
    public void LargeSchema_ParseBuildAndSearch_StaysWithinPerformanceBudget()
    {
        string ddl = SyntheticSchemaFactory.BuildLargeSchema(
            tableCount: TableCount,
            columnsPerTable: ColumnsPerTable,
            relationCount: 1_200);

        ForceFullGc();
        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        Stopwatch stopwatch = Stopwatch.StartNew();

        DatabaseSchema schema = new SqlSchemaParser().Parse(ddl);

        SchemaExplorerIndex index = SchemaExplorerIndex.Build(
            schema,
            auxiliaryTableDetector: new SchemaAuxiliaryTableDetector(),
            pinnedTableNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            hideAuxiliaryTables: false,
            foreignKeySummaries: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            indexSummaries: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            uniqueIndexColumns: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        SchemaExplorerSearchResult narrowSearch = index.Search("CUSTOMER_ID", MaxSearchResults);
        SchemaExplorerSearchResult broadSearch = index.Search("ID", MaxSearchResults);

        stopwatch.Stop();

        ForceFullGc();
        long memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

        long retainedBytes = Math.Max(0, memoryAfter - memoryBefore);
        long allocatedBytes = Math.Max(0, allocatedAfter - allocatedBefore);

        Assert.Equal(TableCount, schema.PhysicalTables.Count());
        Assert.True(
            index.AllColumns.Count >= TableCount * ColumnsPerTable,
            $"Expected at least {TableCount * ColumnsPerTable:N0} indexed columns, got {index.AllColumns.Count:N0}.");

        Assert.True(
            narrowSearch.MatchedColumnCount > 0,
            "The narrow search should match at least one column.");

        Assert.True(
            broadSearch.IsTruncated,
            "The broad search should be truncated to protect UI responsiveness and memory.");

        Assert.True(
            broadSearch.DisplayedColumnCount <= MaxSearchResults,
            $"The broad search displayed too many columns: {broadSearch.DisplayedColumnCount:N0}.");

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Large schema parse/build/search took {stopwatch.Elapsed}.");

        Assert.True(
            retainedBytes < 350L * 1024 * 1024,
            $"Large schema retained too much memory: {retainedBytes / 1024 / 1024} MB.");

        Assert.True(
            allocatedBytes < 900L * 1024 * 1024,
            $"Large schema allocated too much memory: {allocatedBytes / 1024 / 1024} MB.");
    }

    private static void ForceFullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
