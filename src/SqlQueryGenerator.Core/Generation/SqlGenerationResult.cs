namespace SqlQueryGenerator.Core.Generation;

public sealed record SqlGenerationResult
{
    public required string Sql { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
