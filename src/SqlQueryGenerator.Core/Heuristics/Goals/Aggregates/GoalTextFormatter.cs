using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;

/// <summary>
/// Formats aggregate heuristic labels and sentences for the French WPF UI.
/// </summary>
public sealed class GoalTextFormatter
{
    /// <summary>
    /// Regular expression used to split snake-case, kebab-case, and dotted SQL identifiers.
    /// </summary>
    private static readonly Regex IdentifierSeparatorRegex = new("[_.\\-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Regular expression used to reduce repeated whitespace in generated labels.
    /// </summary>
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the best user-facing label for an aggregate projection.
    /// </summary>
    /// <param name="projection">The aggregate projection to format.</param>
    /// <returns>A stable French label for the aggregate metric.</returns>
    public string FormatAggregateLabel(AggregateProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        string baseLabel = FirstNonBlank(
            projection.DisplayName,
            projection.Alias,
            projection.SourceColumnComment,
            projection.SourceColumn,
            ExtractArgumentFromSql(projection.ExpressionSql),
            projection.ExpressionSql,
            "valeur");

        string cleanLabel = CleanLabel(baseLabel);

        return projection.Function switch
        {
            AggregateFunction.Count when projection.ExpressionSql.Contains("*", StringComparison.Ordinal) => "nombre de lignes",
            AggregateFunction.Count => $"nombre de {cleanLabel}",
            AggregateFunction.CountDistinct => $"nombre de {cleanLabel} distincts",
            AggregateFunction.Sum => $"total de {cleanLabel}",
            AggregateFunction.Average => $"moyenne de {cleanLabel}",
            AggregateFunction.Minimum => $"minimum de {cleanLabel}",
            AggregateFunction.Maximum => $"maximum de {cleanLabel}",
            AggregateFunction.Median => $"médiane de {cleanLabel}",
            AggregateFunction.StandardDeviation => $"écart-type de {cleanLabel}",
            AggregateFunction.Variance => $"variance de {cleanLabel}",
            AggregateFunction.Custom => CleanLabel(projection.Alias ?? projection.ExpressionSql),
            _ => cleanLabel
        };
    }

    /// <summary>
    /// Returns the best user-facing label for a grouping projection.
    /// </summary>
    /// <param name="projection">The grouping projection to format.</param>
    /// <returns>A stable French label for the grouping dimension.</returns>
    public string FormatGroupingLabel(GroupingProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        string label = FirstNonBlank(
            projection.DisplayName,
            projection.SourceColumnComment,
            projection.SourceColumn,
            projection.ExpressionSql,
            "groupe");

        return CleanLabel(label);
    }

    /// <summary>
    /// Returns the best user-facing label for the main entity of a query.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <returns>A French label representing the root entity or table.</returns>
    public string FormatRootEntity(AggregateQuerySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        TableUsageSummary? rootTable = snapshot.Tables.FirstOrDefault(table => table.IsRootTable)
            ?? snapshot.Tables.FirstOrDefault();

        if (rootTable is null)
        {
            return "résultats";
        }

        string label = FirstNonBlank(rootTable.DisplayName, rootTable.TableComment, rootTable.TableName, "résultats");
        return CleanLabel(RemoveSchemaPrefix(label));
    }

    /// <summary>
    /// Joins labels into a readable French list.
    /// </summary>
    /// <param name="labels">The labels to join.</param>
    /// <param name="maxLabels">The maximum number of labels to display before adding an overflow suffix.</param>
    /// <returns>A French list such as "A", "A et B", or "A, B et 2 autres".</returns>
    public string JoinLabels(IEnumerable<string> labels, int maxLabels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        List<string> distinctLabels = [.. labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(CleanLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (distinctLabels.Count == 0)
        {
            return "valeur";
        }

        if (distinctLabels.Count > maxLabels)
        {
            int remainingCount = distinctLabels.Count - maxLabels;
            distinctLabels = [.. distinctLabels.Take(maxLabels)];
            distinctLabels.Add($"{remainingCount} autre{(remainingCount > 1 ? "s" : string.Empty)}");
        }

        if (distinctLabels.Count == 1)
        {
            return distinctLabels[0];
        }

        return string.Join(", ", distinctLabels.Take(distinctLabels.Count - 1)) + " et " + distinctLabels[^1];
    }

    /// <summary>
    /// Converts an arbitrary SQL name into a readable label while preserving explicit accents and spaces.
    /// </summary>
    /// <param name="value">The raw label, alias, identifier, or expression.</param>
    /// <returns>A cleaned label intended for display.</returns>
    public string CleanLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string withoutQuotes = value.Trim().Trim('"', '[', ']', '`');
        string withoutSchema = RemoveSchemaPrefix(withoutQuotes);
        string separated = IdentifierSeparatorRegex.Replace(withoutSchema, " ");
        string compact = WhitespaceRegex.Replace(separated, " ").Trim();

        return compact.Length == 0 ? value.Trim() : compact;
    }

    /// <summary>
    /// Removes a schema prefix from an identifier such as SCHEMA.TABLE or SCHEMA.COLUMN.
    /// </summary>
    /// <param name="value">The identifier or label to normalize.</param>
    /// <returns>The identifier without its schema prefix when a dotted prefix exists.</returns>
    public string RemoveSchemaPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[^1] : value.Trim();
    }

    /// <summary>
    /// Returns the first non-blank string in a candidate list.
    /// </summary>
    /// <param name="candidates">The candidate strings to inspect.</param>
    /// <returns>The first useful string, or an empty string when all candidates are blank.</returns>
    private static string FirstNonBlank(params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts a readable aggregate argument from a SQL expression.
    /// </summary>
    /// <param name="expressionSql">The aggregate expression to inspect.</param>
    /// <returns>The detected aggregate argument, or the original expression when no argument can be parsed.</returns>
    private static string ExtractArgumentFromSql(string expressionSql)
    {
        if (string.IsNullOrWhiteSpace(expressionSql))
        {
            return string.Empty;
        }

        int openIndex = expressionSql.IndexOf('(', StringComparison.Ordinal);
        int closeIndex = expressionSql.LastIndexOf(')');

        if (openIndex < 0 || closeIndex <= openIndex)
        {
            return expressionSql;
        }

        string argument = expressionSql[(openIndex + 1)..closeIndex].Trim();
        return argument.StartsWith("DISTINCT ", StringComparison.OrdinalIgnoreCase)
            ? argument[9..].Trim()
            : argument;
    }
}
