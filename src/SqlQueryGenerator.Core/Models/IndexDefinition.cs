using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

public sealed class IndexDefinition
{
    public IndexDefinition(string name, string table, bool isUnique, IEnumerable<string> columns)
    {
        Name = name.Trim();
        Table = table.Trim();
        IsUnique = isUnique;
        Columns = new Collection<string>([.. columns.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())]);
    }

    public string Name { get; }
    public string Table { get; }
    public bool IsUnique { get; }
    public Collection<string> Columns { get; }

    public bool ContainsColumn(string columnName)
    {
        string normalized = SqlNameNormalizer.Normalize(columnName);
        return Columns.Any(c => SqlNameNormalizer.Normalize(c) == normalized);
    }

    public bool IsSingleColumnOn(string tableName, string columnName)
    {
        return SqlNameNormalizer.EqualsName(Table, tableName)
            && Columns.Count == 1
            && ContainsColumn(columnName);
    }
}
