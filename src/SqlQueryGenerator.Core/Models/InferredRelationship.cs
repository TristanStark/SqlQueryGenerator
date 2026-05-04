namespace SqlQueryGenerator.Core.Models;

public enum RelationshipSource
{
    DeclaredForeignKey,
    SameColumnPrimaryKey,
    SameColumnName,
    TableNameColumnPattern,
    CompositeTablePattern,
    CommentSimilarity
}

public sealed record InferredRelationship
{
    public required string FromTable { get; init; }
    public required string FromColumn { get; init; }
    public required string ToTable { get; init; }
    public required string ToColumn { get; init; }
    public required double Confidence { get; init; }
    public required RelationshipSource Source { get; init; }
    public required string Reason { get; init; }

    public string DisplayName => $"{FromTable}.{FromColumn} → {ToTable}.{ToColumn} ({Confidence:P0})";

    public bool Connects(string leftTable, string rightTable)
    {
        return (SqlNameNormalizer.EqualsName(FromTable, leftTable) && SqlNameNormalizer.EqualsName(ToTable, rightTable))
            || (SqlNameNormalizer.EqualsName(FromTable, rightTable) && SqlNameNormalizer.EqualsName(ToTable, leftTable));
    }
}
