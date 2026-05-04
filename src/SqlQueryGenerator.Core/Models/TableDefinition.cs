using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Models;

public sealed class TableDefinition
{
    public TableDefinition(string name, string? schemaName = null, string? comment = null)
    {
        Name = name.Trim();
        SchemaName = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();
        Comment = comment;
        Columns = new Collection<ColumnDefinition>();
    }

    public string Name { get; }
    public string? SchemaName { get; }
    public string? Comment { get; set; }
    public Collection<ColumnDefinition> Columns { get; }
    public string FullName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    public ColumnDefinition? FindColumn(string name)
    {
        var normalized = SqlNameNormalizer.Normalize(name);
        return Columns.FirstOrDefault(c => c.NormalizedName == normalized);
    }

    public IReadOnlyList<ColumnDefinition> PrimaryKeys => Columns.Where(c => c.IsPrimaryKey).ToArray();

    public override string ToString() => FullName;
}
