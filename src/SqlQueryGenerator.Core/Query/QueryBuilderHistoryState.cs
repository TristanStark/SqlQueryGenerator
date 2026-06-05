using SqlQueryGenerator.Core.Parsing;
using System.Text.Json;

namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Immutable query-builder state captured for undo/redo history.
/// </summary>
public sealed class QueryBuilderHistoryState
{
    /// <summary>
    /// Gets or sets the structural query model.
    /// </summary>
    public QueryDefinition Query { get; init; } = new();

    /// <summary>
    /// Gets or sets the target SQL dialect.
    /// </summary>
    public SqlDialect Dialect { get; init; }

    /// <summary>
    /// Gets or sets whether generated identifiers are quoted.
    /// </summary>
    public bool QuoteIdentifiers { get; init; }

    /// <summary>
    /// Gets or sets whether selected columns are auto-grouped during aggregation.
    /// </summary>
    public bool AutoGroupSelectedColumns { get; init; }

    /// <summary>
    /// Gets or sets the raw SQL editor content associated with the current session.
    /// </summary>
    public string RawSqlText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source SQL dialect profile used by reverse/rewrite features.
    /// </summary>
    public SourceSqlDialect SourceSqlDialect { get; init; }

    /// <summary>
    /// Gets a deterministic signature used to compare snapshots.
    /// </summary>
    public string Signature => JsonSerializer.Serialize(new
    {
        Query,
        Dialect,
        QuoteIdentifiers,
        AutoGroupSelectedColumns,
        RawSqlText,
        SourceSqlDialect
    });

    /// <summary>
    /// Creates a deep clone of this state.
    /// </summary>
    /// <returns>Independent state copy.</returns>
    public QueryBuilderHistoryState Clone()
    {
        return new QueryBuilderHistoryState
        {
            Query = QueryDefinitionCloner.Clone(Query),
            Dialect = Dialect,
            QuoteIdentifiers = QuoteIdentifiers,
            AutoGroupSelectedColumns = AutoGroupSelectedColumns,
            RawSqlText = RawSqlText,
            SourceSqlDialect = SourceSqlDialect
        };
    }
}
