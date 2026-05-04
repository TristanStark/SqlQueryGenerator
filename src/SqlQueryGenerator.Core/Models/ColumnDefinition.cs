namespace SqlQueryGenerator.Core.Models;

public sealed record ColumnDefinition
{
    public required string TableName { get; init; }
    public required string Name { get; init; }
    public string? DataType { get; init; }
    public string? Comment { get; init; }
    public bool IsNullable { get; init; } = true;
    public bool IsPrimaryKey { get; init; }
    public bool IsDeclaredForeignKey { get; init; }
    public string NormalizedName => SqlNameNormalizer.Normalize(Name);
    public string QualifiedName => $"{TableName}.{Name}";

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Comment) ? QualifiedName : $"{QualifiedName} — {Comment}";
    }
}
