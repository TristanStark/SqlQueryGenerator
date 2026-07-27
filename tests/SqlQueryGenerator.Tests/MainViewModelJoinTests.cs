using SqlQueryGenerator.App.ViewModels;

namespace SqlQueryGenerator.Tests;

public sealed class MainViewModelJoinTests
{
    [Fact]
    public void RemoveAutomaticJoin_DoesNotReenterObservableCollectionNotification()
    {
        const string schema = """
            CREATE TABLE CUSTOMER (CUSTOMER_ID INTEGER PRIMARY KEY, NAME TEXT);
            CREATE TABLE ORDERS (ORDER_ID INTEGER PRIMARY KEY, CUSTOMER_ID INTEGER, STATUS TEXT);
            """;

        MainViewModel viewModel = new();
        viewModel.LoadSchemaFromText(schema, "test-schema.sql");
        viewModel.BaseTable = "ORDERS";
        viewModel.SelectedColumns.Add(new SelectColumnRowViewModel
        {
            Table = "CUSTOMER",
            Column = "NAME"
        });

        JoinRowViewModel automaticJoin = Assert.Single(viewModel.Joins);

        Exception? exception = Record.Exception(() => viewModel.RemoveJoinCommand.Execute(automaticJoin));

        Assert.Null(exception);
        Assert.Single(viewModel.Joins);
    }
}
