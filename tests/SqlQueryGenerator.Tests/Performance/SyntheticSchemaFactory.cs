using System.Text;

namespace SqlQueryGenerator.Tests.Performance;

internal static class SyntheticSchemaFactory
{
    public static string BuildLargeSchema(int tableCount, int columnsPerTable, int relationCount)
    {
        StringBuilder ddl = new();

        for (int tableIndex = 0; tableIndex < tableCount; tableIndex++)
        {
            ddl.AppendLine($"CREATE TABLE T_{tableIndex:0000} (");
            ddl.AppendLine("    ID INTEGER PRIMARY KEY,");

            for (int columnIndex = 0; columnIndex < columnsPerTable; columnIndex++)
            {
                string comma = columnIndex == columnsPerTable - 1 ? string.Empty : ",";
                ddl.AppendLine($"    CUSTOMER_ID_{columnIndex:000} INTEGER{comma}");
            }

            ddl.AppendLine(");");
            ddl.AppendLine();
        }

        int safeRelationCount = Math.Min(relationCount, tableCount - 1);
        for (int i = 1; i <= safeRelationCount; i++)
        {
            ddl.AppendLine($"CREATE INDEX IX_T_{i:0000}_CUSTOMER_ID ON T_{i:0000} (CUSTOMER_ID_000);");
        }

        return ddl.ToString();
    }
}
