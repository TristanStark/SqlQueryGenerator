using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Persistence;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

public sealed class MainViewModelWorkflowTests
{
    private const string SchemaSql = """
        CREATE TABLE CUSTOMER (
            ID INTEGER PRIMARY KEY,
            NAME TEXT
        );

        CREATE TABLE ORDERS (
            ORDER_ID INTEGER PRIMARY KEY,
            CUSTOMER_ID INTEGER NOT NULL,
            STATUS TEXT,
            CONSTRAINT FK_ORDERS_CUSTOMER FOREIGN KEY (CUSTOMER_ID) REFERENCES CUSTOMER(ID)
        );
        """;

    [Fact]
    public void JoinGraph_UpdatesWhenQueryUsesJoinedTable()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.BaseTable = "ORDERS";
        vm.SelectedColumns.Add(new SelectColumnRowViewModel
        {
            Table = "CUSTOMER",
            Column = "NAME"
        });

        Assert.Equal(2, vm.JoinGraphNodes.Count);
        Assert.Single(vm.JoinGraphEdges);
        Assert.Contains(vm.JoinGraphNodes, node => string.Equals(node.Table, "ORDERS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.JoinGraphNodes, node => string.Equals(node.Table, "CUSTOMER", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("2 table", vm.JoinGraphSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 jointure", vm.JoinGraphSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRelationshipCandidate_UpdatesCurrentJoinState_AndGeneratedSql()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        RelationshipItemViewModel relationship = FindCustomerOrdersRelationship(vm);
        vm.BaseTable = relationship.FromTable;

        Assert.False(relationship.IsUsed);
        Assert.Equal("Ajouter", relationship.UsageText);
        Assert.True(vm.AddRelationshipAsJoinCommand.CanExecute(relationship));

        vm.AddRelationshipAsJoinCommand.Execute(relationship);

        JoinRowViewModel join = Assert.Single(vm.Joins);
        Assert.True(relationship.IsUsed);
        Assert.Equal("Ajoutee", relationship.UsageText);
        Assert.False(vm.AddRelationshipAsJoinCommand.CanExecute(relationship));
        Assert.Contains("INNER JOIN", vm.GeneratedSql);
        Assert.Contains(relationship.ToTable, vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);

        join.JoinType = JoinType.Left;

        Assert.Contains("LEFT JOIN", vm.GeneratedSql);

        vm.RemoveJoinCommand.Execute(join);

        Assert.Empty(vm.Joins);
        Assert.False(relationship.IsUsed);
        Assert.Equal("Ajouter", relationship.UsageText);
        Assert.DoesNotContain(" JOIN ", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Undo_RestoresPreviousQueryAfterReverseImport()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.BaseTable = "CUSTOMER";
        vm.SelectedColumns.Add(new SelectColumnRowViewModel
        {
            Table = "CUSTOMER",
            Column = "NAME"
        });
        string baselineSql = vm.GeneratedSql;

        vm.RawSqlText = """
            SELECT
                CUSTOMER.ID,
                ORDERS.ORDER_ID
            FROM CUSTOMER
            JOIN ORDERS ON CUSTOMER.ID = ORDERS.CUSTOMER_ID
            WHERE ORDERS.STATUS = :status
            """;

        vm.ReverseEngineerRawSqlCommand.Execute(null);

        Assert.NotEqual(baselineSql, vm.GeneratedSql);
        Assert.NotEmpty(vm.Joins);
        Assert.True(vm.UndoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);

        Assert.Equal("CUSTOMER", vm.BaseTable);
        Assert.Empty(vm.Joins);
        SelectColumnRowViewModel selectedColumn = Assert.Single(vm.SelectedColumns);
        Assert.Equal("CUSTOMER", selectedColumn.Table);
        Assert.Equal("NAME", selectedColumn.Column);
        Assert.Equal(baselineSql, vm.GeneratedSql);
    }

    [Fact]
    public void Undo_RestoresPreviousQueryAfterLoadingSavedBuilderQuery()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.BaseTable = "CUSTOMER";
        vm.SelectedColumns.Add(new SelectColumnRowViewModel
        {
            Table = "CUSTOMER",
            Column = "NAME"
        });
        string baselineSql = vm.GeneratedSql;

        SavedQueryDefinition saved = new()
        {
            Kind = SavedQueryKind.Builder,
            Name = "orders_only",
            Query = new QueryDefinition
            {
                Name = "orders_only",
                BaseTable = "ORDERS",
                SelectedColumns =
                {
                    new ColumnReference { Table = "ORDERS", Column = "ORDER_ID" },
                    new ColumnReference { Table = "ORDERS", Column = "STATUS" }
                }
            }
        };

        vm.SelectedSavedQuery = new SavedQueryItemViewModel(saved);
        Assert.True(vm.LoadSelectedQueryCommand.CanExecute(null));

        vm.LoadSelectedQueryCommand.Execute(null);

        Assert.Equal("ORDERS", vm.BaseTable);
        Assert.Equal(2, vm.SelectedColumns.Count);
        Assert.True(vm.UndoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);

        Assert.Equal("CUSTOMER", vm.BaseTable);
        SelectColumnRowViewModel selectedColumn = Assert.Single(vm.SelectedColumns);
        Assert.Equal("CUSTOMER", selectedColumn.Table);
        Assert.Equal("NAME", selectedColumn.Column);
        Assert.Equal(baselineSql, vm.GeneratedSql);
    }

    [Fact]
    public void LoadSelectedQueryCommand_LoadsRawSqlPresetIntoEditor()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        SavedQueryDefinition saved = new()
        {
            Kind = SavedQueryKind.RawSql,
            Name = "raw_orders",
            Description = "Raw preset for reports",
            RawSql = """
                SELECT
                    ORDERS.ORDER_ID
                FROM ORDERS
                WHERE ORDERS.STATUS = :status
                """
        };

        vm.SelectedSavedQuery = new SavedQueryItemViewModel(saved);
        Assert.True(vm.LoadSelectedQueryCommand.CanExecute(null));

        vm.LoadSelectedQueryCommand.Execute(null);

        Assert.Equal(saved.Name, vm.QueryName);
        Assert.Equal(saved.Description, vm.QueryDescription);
        Assert.Contains("FROM ORDERS", vm.RawSqlText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM ORDERS", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Preset SQL brut chargé", vm.Warnings, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReverseImportFailure_UpdatesDiagnosticsAndRawSqlSelection()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.RawSqlText = """
            SELECT
                c.CUSTOMER_ID
            FROM CUSTOMER c
            WHERE
            ORDER BY c.CUSTOMER_ID
            """;

        vm.ReverseEngineerRawSqlCommand.Execute(null);

        Assert.Contains("WHERE", vm.ReverseSqlDiagnosticsReport, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete", vm.Warnings, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.RawSqlSelectionStart > 0);
        Assert.True(vm.RawSqlSelectionLength > 0);
    }

    private static MainViewModel CreateViewModelWithSchema()
    {
        MainViewModel vm = new();
        vm.LoadSchemaFromText(SchemaSql);
        return vm;
    }

    private static RelationshipItemViewModel FindCustomerOrdersRelationship(MainViewModel vm)
    {
        return Assert.Single(vm.Relationships, relationship =>
            string.Equals(relationship.FromTable, "ORDERS", StringComparison.OrdinalIgnoreCase)
            && string.Equals(relationship.FromColumn, "CUSTOMER_ID", StringComparison.OrdinalIgnoreCase)
            && string.Equals(relationship.ToTable, "CUSTOMER", StringComparison.OrdinalIgnoreCase)
            && string.Equals(relationship.ToColumn, "ID", StringComparison.OrdinalIgnoreCase));
    }
}
