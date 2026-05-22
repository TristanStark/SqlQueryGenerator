using SqlQueryGenerator.Core.Query;
using System.Text.Json.Serialization;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Représente SavedQueryDefinition dans SQL Query Generator.
/// </summary>
public sealed class SavedQueryDefinition
{
    /// <summary>
    /// Gets or sets whether the preset stores a visual builder query or a raw SQL SELECT.
    /// </summary>
    /// <value>Preset storage kind.</value>
    public SavedQueryKind Kind { get; set; } = SavedQueryKind.Builder;
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
    /// Gets or sets the raw read-only SELECT statement when <see cref="Kind"/> is <see cref="SavedQueryKind.RawSql"/>.
    /// </summary>
    /// <value>Raw SELECT SQL, without any DML/DDL intent.</value>
    public string? RawSql { get; set; }
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

/// <summary>
/// Describes the kind of preset stored in the local saved query library.
/// </summary>
public enum SavedQueryKind
{
    /// <summary>
    /// The preset was built with the visual query builder and contains a full <see cref="QueryDefinition"/>.
    /// </summary>
    Builder,

    /// <summary>
    /// The preset is a user-provided raw SELECT statement kept as text.
    /// </summary>
    RawSql
}
