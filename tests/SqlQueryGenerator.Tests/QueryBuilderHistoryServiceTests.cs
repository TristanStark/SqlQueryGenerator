using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

public sealed class QueryBuilderHistoryServiceTests
{
    [Fact]
    public void Track_ChangedState_EnablesUndoAndReturnsPreviousSnapshot()
    {
        QueryBuilderHistoryService history = new();
        history.Reset(BuildState("CUSTOMER"));

        history.Track(BuildState("ORDERS"));

        Assert.True(history.CanUndo);
        QueryBuilderHistoryState restored = history.Undo();
        Assert.Equal("CUSTOMER", restored.Query.BaseTable);
        Assert.True(history.CanRedo);
    }

    [Fact]
    public void Track_SameState_DoesNotCreateUndoEntry()
    {
        QueryBuilderHistoryState state = BuildState("CUSTOMER");
        QueryBuilderHistoryService history = new();
        history.Reset(state);

        history.Track(state.Clone());

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_ThenTrack_ClearsRedo()
    {
        QueryBuilderHistoryService history = new();
        history.Reset(BuildState("CUSTOMER"));
        history.Track(BuildState("ORDERS"));

        QueryBuilderHistoryState undone = history.Undo();
        Assert.Equal("CUSTOMER", undone.Query.BaseTable);

        history.Track(BuildState("PRODUCTS"));

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clone_ProducesIndependentQueryCopy()
    {
        QueryBuilderHistoryState state = BuildState("CUSTOMER");
        QueryBuilderHistoryState clone = state.Clone();

        clone.Query.SelectedColumns.Add(new ColumnReference { Table = "CUSTOMER", Column = "NAME" });

        Assert.Single(state.Query.SelectedColumns);
        Assert.Equal(2, clone.Query.SelectedColumns.Count);
    }

    [Fact]
    public void Track_MoreThanMaxDepth_TrimsOldestUndoStates()
    {
        QueryBuilderHistoryService history = new(maxDepth: 2);
        history.Reset(BuildState("A"));
        history.Track(BuildState("B"));
        history.Track(BuildState("C"));
        history.Track(BuildState("D"));

        QueryBuilderHistoryState firstUndo = history.Undo();
        QueryBuilderHistoryState secondUndo = history.Undo();

        Assert.Equal("C", firstUndo.Query.BaseTable);
        Assert.Equal("B", secondUndo.Query.BaseTable);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Undo_RestoresRawSqlDialectAliasesAndMetadata()
    {
        QueryBuilderHistoryService history = new();
        QueryBuilderHistoryState original = BuildDetailedState("CUSTOMER", "cust");
        QueryBuilderHistoryState loaded = BuildDetailedState("ORDERS", "ord");

        history.Reset(original);
        history.Track(loaded);

        QueryBuilderHistoryState restored = history.Undo();

        Assert.Equal("CUSTOMER", restored.Query.BaseTable);
        Assert.Equal("snapshot_CUSTOMER", restored.Query.Name);
        Assert.Equal("history_CUSTOMER", restored.Query.Description);
        Assert.Equal("SELECT ID FROM CUSTOMER", restored.RawSqlText);
        Assert.Equal(SourceSqlDialect.Db2, restored.SourceSqlDialect);
        Assert.Equal("cust", restored.Query.TableAliases.Single().Alias);
        Assert.Equal("CUSTOMER", restored.Query.TableAliases.Single().Table);
        Assert.Single(restored.Query.Joins);
        Assert.Equal("CUSTOMER", restored.Query.Joins[0].FromTable);
        Assert.Equal("ORDERS", restored.Query.Joins[0].ToTable);
    }

    private static QueryBuilderHistoryState BuildState(string baseTable)
    {
        QueryDefinition query = new()
        {
            Name = "snapshot",
            Description = "history",
            BaseTable = baseTable,
            Distinct = true,
            LimitRows = 50
        };
        query.SelectedColumns.Add(new ColumnReference { Table = baseTable, Column = "ID" });

        return new QueryBuilderHistoryState
        {
            Query = query,
            Dialect = SqlDialect.SQLite,
            QuoteIdentifiers = false,
            AutoGroupSelectedColumns = true,
            RawSqlText = $"SELECT ID FROM {baseTable}",
            SourceSqlDialect = SourceSqlDialect.GenericSql
        };
    }

    private static QueryBuilderHistoryState BuildDetailedState(string baseTable, string alias)
    {
        QueryDefinition query = new()
        {
            Name = $"snapshot_{baseTable}",
            Description = $"history_{baseTable}",
            BaseTable = baseTable,
            Distinct = true,
            LimitRows = 25
        };
        query.SelectedColumns.Add(new ColumnReference { Table = baseTable, Column = "ID" });
        query.TableAliases.Add(new TableAliasDefinition { Table = baseTable, Alias = alias });
        query.Joins.Add(new JoinDefinition
        {
            FromTable = baseTable,
            FromColumn = "ID",
            ToTable = "ORDERS",
            ToColumn = "CUSTOMER_ID",
            JoinType = JoinType.Left
        });

        return new QueryBuilderHistoryState
        {
            Query = query,
            Dialect = SqlDialect.SQLite,
            QuoteIdentifiers = false,
            AutoGroupSelectedColumns = true,
            RawSqlText = $"SELECT ID FROM {baseTable}",
            SourceSqlDialect = SourceSqlDialect.Db2
        };
    }
}
