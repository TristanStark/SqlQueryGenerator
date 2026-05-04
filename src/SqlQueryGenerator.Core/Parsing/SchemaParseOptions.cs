namespace SqlQueryGenerator.Core.Parsing;

public sealed record SchemaParseOptions
{
    public int MaxInputCharacters { get; init; } = 15_000_000;
    public bool InferRelationships { get; init; } = true;
    public bool KeepCaseSensitiveIdentifiers { get; init; }
}
