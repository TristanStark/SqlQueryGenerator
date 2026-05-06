using SqlQueryGenerator.Core.Query;
using System.Text.Json.Serialization;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Représente SavedQueryDefinition dans SQL Query Generator.
/// </summary>
public sealed class SavedQueryDefinition
{
    /// <summary>
    /// Stocke la valeur interne Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Stocke la valeur interne Description.
    /// </summary>
    /// <value>Valeur de Description.</value>
    public string? Description { get; set; }
    /// <summary>
    /// Stocke la valeur interne CreatedAtUtc.
    /// </summary>
    /// <value>Valeur de CreatedAtUtc.</value>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Stocke la valeur interne UpdatedAtUtc.
    /// </summary>
    /// <value>Valeur de UpdatedAtUtc.</value>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Stocke la valeur interne Query.
    /// </summary>
    /// <value>Valeur de Query.</value>
    public QueryDefinition Query { get; set; } = new();
    /// <summary>
    /// Stocke la valeur interne LastGeneratedSql.
    /// </summary>
    /// <value>Valeur de LastGeneratedSql.</value>
    public string? LastGeneratedSql { get; set; }

    /// <summary>
    /// Obtient ou définit Parameters.
    /// </summary>
    /// <value>Valeur de Parameters.</value>
    [JsonIgnore]
    public IReadOnlyList<QueryParameterDefinition> Parameters => Query.Parameters.ToArray();
}
