using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Represents the result of importing raw SQL into the internal query model.
/// </summary>
public sealed class ReverseSqlImportResult
{
    /// <summary>
    /// Gets or sets the imported query model.
    /// </summary>
    /// <value>Parsed query definition.</value>
    public QueryDefinition Query { get; init; } = new();

    /// <summary>
    /// Gets or sets warnings collected during import.
    /// </summary>
    /// <value>Conservative warnings about partial support or risky constructs.</value>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
