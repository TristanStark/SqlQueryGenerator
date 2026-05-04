namespace SqlQueryGenerator.Core.Query;

public sealed record ColumnReference
{
    public required string Table { get; init; }
    public required string Column { get; init; }
    public string? Alias { get; init; }

    public string Key => $"{Table}.{Column}";
}
