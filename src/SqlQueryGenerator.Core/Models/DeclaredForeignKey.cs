namespace SqlQueryGenerator.Core.Models;

public sealed record DeclaredForeignKey
{
    public required string FromTable { get; init; }
    public required string FromColumn { get; init; }
    public required string ToTable { get; init; }
    public required string ToColumn { get; init; }
    public string? ConstraintName { get; init; }
}
