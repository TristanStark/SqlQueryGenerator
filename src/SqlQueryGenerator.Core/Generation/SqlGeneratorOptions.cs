using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlGeneratorOptions dans SQL Query Generator.
/// </summary>
public sealed record SqlGeneratorOptions
{
    /// <summary>
    /// Stocke la valeur interne Dialect.
    /// </summary>
    /// <value>Valeur de Dialect.</value>
    public SqlDialect Dialect { get; init; } = SqlDialect.Generic;
    /// <summary>
    /// Stocke la valeur interne QuoteIdentifiers.
    /// </summary>
    /// <value>Valeur de QuoteIdentifiers.</value>
    public bool QuoteIdentifiers { get; init; }
    /// <summary>
    /// Stocke la valeur interne AutoGroupSelectedColumnsWhenAggregating.
    /// </summary>
    /// <value>Valeur de AutoGroupSelectedColumnsWhenAggregating.</value>
    public bool AutoGroupSelectedColumnsWhenAggregating { get; init; } = true;
    /// <summary>
    /// Stocke la valeur interne UseReadableAliases.
    /// </summary>
    /// <value>Valeur de UseReadableAliases.</value>
    public bool UseReadableAliases { get; init; } = true;
    /// <summary>
    /// Stocke la valeur interne EmitOptimizationComments.
    /// </summary>
    /// <value>Valeur de EmitOptimizationComments.</value>
    public bool EmitOptimizationComments { get; init; }
}
