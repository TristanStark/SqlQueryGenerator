using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

public sealed class DatabaseSchema
{
    public Collection<TableDefinition> Tables { get; } = new();
    public Collection<DeclaredForeignKey> DeclaredForeignKeys { get; } = new();
    public Collection<InferredRelationship> Relationships { get; } = new();
    public Collection<string> Warnings { get; } = new();

    public TableDefinition? FindTable(string tableName)
    {
        var normalized = SqlNameNormalizer.Normalize(tableName);
        return Tables.FirstOrDefault(t => SqlNameNormalizer.Normalize(t.Name) == normalized || SqlNameNormalizer.Normalize(t.FullName) == normalized);
    }

    public ColumnDefinition? FindColumn(string tableName, string columnName)
    {
        return FindTable(tableName)?.FindColumn(columnName);
    }
}
