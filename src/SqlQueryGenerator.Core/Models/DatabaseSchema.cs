using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

public sealed class DatabaseSchema
{
    public Collection<TableDefinition> Tables { get; } = [];
    public IEnumerable<TableDefinition> Views => Tables.Where(t => t.IsView);
    public IEnumerable<TableDefinition> PhysicalTables => Tables.Where(t => !t.IsView);
    public Collection<DeclaredForeignKey> DeclaredForeignKeys { get; } = [];
    public Collection<IndexDefinition> Indexes { get; } = [];
    public Collection<InferredRelationship> Relationships { get; } = [];
    public Collection<string> Warnings { get; } = [];

    public TableDefinition? FindTable(string tableName)
    {
        string normalized = SqlNameNormalizer.Normalize(tableName);
        return Tables.FirstOrDefault(t => SqlNameNormalizer.Normalize(t.Name) == normalized || SqlNameNormalizer.Normalize(t.FullName) == normalized);
    }

    public ColumnDefinition? FindColumn(string tableName, string columnName)
    {
        return FindTable(tableName)?.FindColumn(columnName);
    }

    public bool IsColumnIndexed(string tableName, string columnName)
    {
        return Indexes.Any(index => SqlNameNormalizer.EqualsName(index.Table, tableName) && index.ContainsColumn(columnName));
    }

    public bool IsColumnUniqueIndexed(string tableName, string columnName)
    {
        return Indexes.Any(index => index.IsUnique && index.IsSingleColumnOn(tableName, columnName));
    }

    public IReadOnlyList<IndexDefinition> FindIndexesForColumn(string tableName, string columnName)
    {
        return [.. Indexes
            .Where(index => SqlNameNormalizer.EqualsName(index.Table, tableName) && index.ContainsColumn(columnName))
            .OrderByDescending(index => index.IsUnique)
            .ThenBy(index => index.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
