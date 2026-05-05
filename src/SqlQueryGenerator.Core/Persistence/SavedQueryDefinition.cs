using System.Text.Json.Serialization;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Persistence;

public sealed class SavedQueryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public QueryDefinition Query { get; set; } = new();
    public string? LastGeneratedSql { get; set; }

    [JsonIgnore]
    public IReadOnlyList<QueryParameterDefinition> Parameters => Query.Parameters.ToArray();
}
