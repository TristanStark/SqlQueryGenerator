using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

public sealed record SqlGeneratorOptions
{
    public SqlDialect Dialect { get; init; } = SqlDialect.Generic;
    public bool QuoteIdentifiers { get; init; }
    public bool AutoGroupSelectedColumnsWhenAggregating { get; init; } = true;
    public bool UseReadableAliases { get; init; } = true;
    public bool EmitOptimizationComments { get; init; }
}
