using System.Collections.ObjectModel;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Defines a reusable output profile that can be applied to selected query columns.
/// </summary>
public sealed class OutputProfileDefinition
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    /// <value>User-facing profile name.</value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    /// <value>Business or technical description.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the query name from which the profile was created.
    /// </summary>
    /// <value>Optional source query name.</value>
    public string? SourceQueryName { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation date.
    /// </summary>
    /// <value>Creation timestamp.</value>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the UTC update date.
    /// </summary>
    /// <value>Last update timestamp.</value>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets output field definitions in profile order.
    /// </summary>
    /// <value>Output field profile rows.</value>
    public Collection<OutputProfileFieldDefinition> Fields { get; } = [];
}

/// <summary>
/// Defines output properties for one selected column.
/// </summary>
public sealed class OutputProfileFieldDefinition
{
    /// <summary>
    /// Gets or sets the field order inside the output profile.
    /// </summary>
    /// <value>One-based output order.</value>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the source table.
    /// </summary>
    /// <value>Source table name.</value>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source column.
    /// </summary>
    /// <value>Source column name.</value>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional output alias.
    /// </summary>
    /// <value>Output alias.</value>
    public string? Alias { get; set; }

    /// <summary>
    /// Gets or sets whether NULL is allowed for this output field.
    /// </summary>
    /// <value><c>true</c> when NULL is allowed.</value>
    public bool NullAllowed { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this output field must have a fixed length.
    /// </summary>
    /// <value><c>true</c> when fixed-width formatting is enabled.</value>
    public bool UseFixedLength { get; set; }

    /// <summary>
    /// Gets or sets the fixed field length.
    /// </summary>
    /// <value>Target length in characters.</value>
    public int? FixedLength { get; set; }

    /// <summary>
    /// Gets the source key.
    /// </summary>
    /// <value>Key formatted as <c>table.column</c>.</value>
    public string Key => $"{Table}.{Column}";

    /// <summary>
    /// Gets the effective output name.
    /// </summary>
    /// <value>Alias when present, otherwise column name.</value>
    public string OutputName => string.IsNullOrWhiteSpace(Alias) ? Column : Alias!;

    /// <summary>
    /// Checks whether this profile field targets the supplied selected column.
    /// </summary>
    /// <param name="table">Selected column table.</param>
    /// <param name="column">Selected column name.</param>
    /// <param name="alias">Selected column alias.</param>
    /// <returns><c>true</c> when the field matches.</returns>
    public bool Matches(string table, string column, string? alias)
    {
        if (!string.Equals(Table, table, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Column, column, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Alias))
        {
            return true;
        }

        return string.Equals(Alias, alias, StringComparison.OrdinalIgnoreCase);
    }
}
