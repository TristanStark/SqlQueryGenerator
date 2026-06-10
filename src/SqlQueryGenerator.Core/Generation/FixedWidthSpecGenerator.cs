using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;
using System.Text;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Generates a Markdown fixed-width output specification from selected query columns.
/// </summary>
public sealed class FixedWidthSpecGenerator
{
    /// <summary>
    /// Generates a fixed-width output specification.
    /// </summary>
    /// <param name="query">Current query model.</param>
    /// <param name="schema">Loaded schema.</param>
    /// <param name="options">Optional generation options.</param>
    /// <returns>Generated fixed-width specification report.</returns>
    public FixedWidthSpecReport Generate(QueryDefinition query, DatabaseSchema schema, FixedWidthSpecOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);
        options ??= new FixedWidthSpecOptions();

        List<FixedWidthFieldSpec> fields = [];
        List<string> warnings = [];
        int cursor = 1;
        int order = 1;

        foreach (ColumnReference selected in query.SelectedColumns)
        {
            if (selected.Column.Trim() == "*")
            {
                warnings.Add($"Champ {selected.Key}: impossible de produire une spec fixed-width précise pour une projection wildcard.");
                fields.Add(FixedWidthFieldSpec.FromInvalid(order++, selected, "Wildcard non supporté"));
                continue;
            }

            ColumnDefinition? sourceColumn = schema.FindColumn(selected.Table, selected.Column);
            string dataType = sourceColumn?.DataType ?? "UNKNOWN";
            FixedWidthValueKind valueKind = DetectValueKind(dataType);
            string outputName = string.IsNullOrWhiteSpace(selected.Alias) ? selected.Column : selected.Alias!;

            int? length = selected.UseFixedLength && selected.FixedLength is > 0
                ? selected.FixedLength.Value
                : null;

            if (!selected.UseFixedLength)
            {
                warnings.Add($"Champ {selected.Key}: longueur fixe désactivée, position impossible à garantir.");
            }
            else if (selected.FixedLength is not > 0)
            {
                warnings.Add($"Champ {selected.Key}: longueur fixe activée mais longueur absente ou invalide.");
            }

            int? start = null;
            int? end = null;
            if (length is > 0)
            {
                start = cursor;
                end = cursor + length.Value - 1;
                cursor = end.Value + 1;
            }

            fields.Add(new FixedWidthFieldSpec
            {
                Order = order++,
                Source = selected.Key,
                OutputName = outputName,
                DataType = dataType,
                ValueKind = valueKind.ToString(),
                StartPosition = start,
                EndPosition = end,
                Length = length,
                NullAllowed = selected.NullAllowed,
                PaddingDirection = BuildPaddingDirection(valueKind),
                PaddingCharacter = BuildPaddingCharacter(valueKind),
                DefaultWhenNull = BuildDefaultWhenNull(valueKind),
                Warning = length is null ? "Longueur fixe manquante" : null
            });
        }

        int totalLength = fields.Where(field => field.Length is > 0).Sum(field => field.Length!.Value);
        string markdown = BuildMarkdown(query, options, fields, warnings, totalLength);

        return new FixedWidthSpecReport(markdown, totalLength, fields, warnings);
    }

    private static string BuildMarkdown(
        QueryDefinition query,
        FixedWidthSpecOptions options,
        IReadOnlyList<FixedWidthFieldSpec> fields,
        IReadOnlyList<string> warnings,
        int totalLength)
    {
        StringBuilder sb = new();
        DateTimeOffset generatedAt = options.GeneratedAtUtc ?? DateTimeOffset.UtcNow;

        sb.AppendLine("# Spécification fixed-width");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(options.ProfileName))
        {
            sb.AppendLine($"**Profil :** {EscapeMarkdown(options.ProfileName)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            sb.AppendLine($"**Requête :** {EscapeMarkdown(query.Name)}");
        }

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            sb.AppendLine($"**Description :** {EscapeMarkdown(options.Description)}");
        }

        sb.AppendLine($"**Généré le :** {generatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Longueur totale calculée :** {totalLength} caractère(s)");
        sb.AppendLine();

        if (warnings.Count > 0)
        {
            sb.AppendLine("## Avertissements");
            sb.AppendLine();

            foreach (string warning in warnings)
            {
                sb.AppendLine($"- {EscapeMarkdown(warning)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Champs");
        sb.AppendLine();
        sb.AppendLine("| # | Positions | Longueur | Nom sortie | Source | Type | NULL | Padding | Défaut NULL | Note |");
        sb.AppendLine("|---:|---|---:|---|---|---|---|---|---|---|");

        foreach (FixedWidthFieldSpec field in fields)
        {
            string positions = field.StartPosition is > 0 && field.EndPosition is > 0
                ? $"{field.StartPosition:000}-{field.EndPosition:000}"
                : "N/A";

            string length = field.Length is > 0 ? field.Length.Value.ToString() : "N/A";
            string nullPolicy = field.NullAllowed ? "Autorisé" : "Interdit";
            string padding = field.Length is > 0
                ? $"{field.PaddingDirection}, `{EscapeMarkdown(field.PaddingCharacter)}`"
                : "N/A";

            sb.Append("| ")
                .Append(field.Order)
                .Append(" | ")
                .Append(positions)
                .Append(" | ")
                .Append(length)
                .Append(" | ")
                .Append(EscapeMarkdown(field.OutputName))
                .Append(" | `")
                .Append(EscapeMarkdown(field.Source))
                .Append("` | ")
                .Append(EscapeMarkdown(field.DataType))
                .Append(" | ")
                .Append(nullPolicy)
                .Append(" | ")
                .Append(padding)
                .Append(" | `")
                .Append(EscapeMarkdown(field.DefaultWhenNull))
                .Append("` | ")
                .Append(EscapeMarkdown(field.Warning ?? string.Empty))
                .AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("## Règles de padding");
        sb.AppendLine();
        sb.AppendLine("- Les champs numériques sont alignés à gauche par `LPAD` / équivalent et complétés avec `0`.");
        sb.AppendLine("- Les champs texte/date sont alignés à droite par `RPAD` / équivalent et complétés avec des espaces.");
        sb.AppendLine("- Les champs dont `NULL` est interdit doivent être protégés par `NVL` ou `COALESCE` selon le dialecte SQL.");
        sb.AppendLine("- Les champs sans longueur fixe ne peuvent pas recevoir de positions garanties.");

        return sb.ToString();
    }

    private static string BuildPaddingDirection(FixedWidthValueKind kind)
    {
        return kind is FixedWidthValueKind.Integer or FixedWidthValueKind.Number or FixedWidthValueKind.Boolean
            ? "gauche"
            : "droite";
    }

    private static string BuildPaddingCharacter(FixedWidthValueKind kind)
    {
        return kind is FixedWidthValueKind.Integer or FixedWidthValueKind.Number or FixedWidthValueKind.Boolean
            ? "0"
            : "space";
    }

    private static string BuildDefaultWhenNull(FixedWidthValueKind kind)
    {
        return kind switch
        {
            FixedWidthValueKind.Integer => "0",
            FixedWidthValueKind.Number => "0",
            FixedWidthValueKind.Boolean => "0",
            FixedWidthValueKind.DateTime => "1900-01-01",
            _ => string.Empty
        };
    }

    private static FixedWidthValueKind DetectValueKind(string? dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return FixedWidthValueKind.Text;
        }

        string normalized = dataType.Trim().ToUpperInvariant();

        if (normalized.Contains("CHAR", StringComparison.Ordinal)
            || normalized.Contains("TEXT", StringComparison.Ordinal)
            || normalized.Contains("CLOB", StringComparison.Ordinal)
            || normalized.Contains("STRING", StringComparison.Ordinal))
        {
            return FixedWidthValueKind.Text;
        }

        if (normalized.Contains("INT", StringComparison.Ordinal))
        {
            return FixedWidthValueKind.Integer;
        }

        if (normalized.Contains("NUMBER", StringComparison.Ordinal)
            || normalized.Contains("NUMERIC", StringComparison.Ordinal)
            || normalized.Contains("DECIMAL", StringComparison.Ordinal)
            || normalized.Contains("FLOAT", StringComparison.Ordinal)
            || normalized.Contains("DOUBLE", StringComparison.Ordinal)
            || normalized.Contains("REAL", StringComparison.Ordinal))
        {
            return FixedWidthValueKind.Number;
        }

        if (normalized.Contains("DATE", StringComparison.Ordinal)
            || normalized.Contains("TIME", StringComparison.Ordinal))
        {
            return FixedWidthValueKind.DateTime;
        }

        if (normalized.Contains("BOOL", StringComparison.Ordinal) || normalized == "BIT")
        {
            return FixedWidthValueKind.Boolean;
        }

        return FixedWidthValueKind.Text;
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}

/// <summary>
/// Options used to generate a fixed-width specification.
/// </summary>
public sealed class FixedWidthSpecOptions
{
    /// <summary>
    /// Gets or sets the profile name displayed in the generated document.
    /// </summary>
    /// <value>Optional profile name.</value>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Gets or sets the profile description displayed in the generated document.
    /// </summary>
    /// <value>Optional profile description.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the generation timestamp.
    /// </summary>
    /// <value>Optional fixed timestamp, useful for tests.</value>
    public DateTimeOffset? GeneratedAtUtc { get; set; }
}

/// <summary>
/// Result of fixed-width specification generation.
/// </summary>
/// <param name="Markdown">Generated Markdown document.</param>
/// <param name="TotalLength">Total calculated fixed line length.</param>
/// <param name="Fields">Field specifications.</param>
/// <param name="Warnings">Generation warnings.</param>
public sealed record FixedWidthSpecReport(
    string Markdown,
    int TotalLength,
    IReadOnlyList<FixedWidthFieldSpec> Fields,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Fixed-width metadata for one output field.
/// </summary>
public sealed class FixedWidthFieldSpec
{
    /// <summary>
    /// Gets or sets the output order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the source column key.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output field name.
    /// </summary>
    public string OutputName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SQL data type.
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the broad value kind.
    /// </summary>
    public string ValueKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the one-based start position.
    /// </summary>
    public int? StartPosition { get; set; }

    /// <summary>
    /// Gets or sets the one-based end position.
    /// </summary>
    public int? EndPosition { get; set; }

    /// <summary>
    /// Gets or sets the target length.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Gets or sets whether NULL is allowed.
    /// </summary>
    public bool NullAllowed { get; set; }

    /// <summary>
    /// Gets or sets the padding direction.
    /// </summary>
    public string PaddingDirection { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the padding character.
    /// </summary>
    public string PaddingCharacter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default value used when NULL is forbidden.
    /// </summary>
    public string DefaultWhenNull { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional warning.
    /// </summary>
    public string? Warning { get; set; }

    /// <summary>
    /// Creates an invalid field spec placeholder.
    /// </summary>
    /// <param name="order">Output order.</param>
    /// <param name="column">Source column.</param>
    /// <param name="warning">Warning text.</param>
    /// <returns>Invalid field spec.</returns>
    public static FixedWidthFieldSpec FromInvalid(int order, ColumnReference column, string warning)
    {
        return new FixedWidthFieldSpec
        {
            Order = order,
            Source = column.Key,
            OutputName = string.IsNullOrWhiteSpace(column.Alias) ? column.Column : column.Alias!,
            DataType = "UNKNOWN",
            ValueKind = FixedWidthValueKind.Text.ToString(),
            NullAllowed = column.NullAllowed,
            Warning = warning
        };
    }
}

/// <summary>
/// Broad value kinds used by the fixed-width spec.
/// </summary>
public enum FixedWidthValueKind
{
    Text,
    Integer,
    Number,
    DateTime,
    Boolean
}
