using SqlQueryGenerator.App.Services;

namespace SqlQueryGenerator.Tests;

public sealed class DroppedFileClassifierTests
{
    [Theory]
    [InlineData("query.sql", true)]
    [InlineData("backup.sqlqg.json", true)]
    [InlineData("backup.json", false)]
    [InlineData("notes.txt", false)]
    public void IsSupportedPath_RecognizesApplicationFiles(string path, bool expected)
    {
        Assert.Equal(expected, DroppedFileClassifier.IsSupportedPath(path));
    }

    [Fact]
    public void ClassifySqlText_DetectsSchemaDdl()
    {
        const string sql = "CREATE TABLE CUSTOMER (CUSTOMER_ID INTEGER PRIMARY KEY);";

        Assert.Equal(DroppedFileKind.SqlSchema, DroppedFileClassifier.ClassifySqlText(sql));
    }

    [Fact]
    public void ClassifySqlText_TreatsSelectAsRawSql()
    {
        const string sql = "SELECT CUSTOMER_ID FROM CUSTOMER;";

        Assert.Equal(DroppedFileKind.RawSql, DroppedFileClassifier.ClassifySqlText(sql));
    }
}
