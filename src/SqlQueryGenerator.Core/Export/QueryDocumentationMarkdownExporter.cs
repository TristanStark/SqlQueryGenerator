using System.Globalization;
using System.Text;

namespace SqlQueryGenerator.Core.Export;

/// <summary>
/// Generates readable Markdown documentation for a SQL query produced by the builder or reverse-import workflow.
/// </summary>
public sealed class QueryDocumentationMarkdownExporter
{
    /// <summary>
    /// Generates a complete Markdown document for the supplied query documentation context.
    /// </summary>
    /// <param name="context">Current query documentation data.</param>
    /// <returns>A Markdown document containing SQL, metadata, builder sections, warnings and performance notes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
    public string GenerateMarkdown(QueryDocumentationExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        StringBuilder builder = new();
        string queryName = string.IsNullOrWhiteSpace(context.QueryName) ? "query" : context.QueryName.Trim();

        builder.Append("# Query: ").AppendLine(EscapeHeading(queryName));
        builder.AppendLine();

        AppendSummary(builder, context);
        AppendGeneratedSql(builder, context.GeneratedSql);
        AppendSelectedColumns(builder, context.SelectedColumns);
        AppendJoins(builder, context.Joins);
        AppendFilters(builder, context.Filters);
        AppendSimpleListSection(builder, "Grouping", context.GroupByColumns);
        AppendAggregates(builder, context.Aggregates);
        AppendSorting(builder, context.Sorting);
        AppendCalculatedColumns(builder, context.CalculatedColumns);
        AppendParameters(builder, context.Parameters);
        AppendTextSection(builder, "Warnings", context.Warnings);
        AppendTextSection(builder, "Performance notes", context.PerformanceNotes);

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>
    /// Appends the high-level summary section and metadata table.
    /// </summary>
    private static void AppendSummary(StringBuilder builder, QueryDocumentationExportContext context)
    {
        AppendSectionTitle(builder, "Summary");

        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            builder.AppendLine(EscapeParagraph(context.Description.Trim()));
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.Purpose))
        {
            builder.Append("**Purpose:** ").AppendLine(EscapeParagraph(context.Purpose.Trim()));
            builder.AppendLine();
        }

        builder.AppendLine("| Property | Value |");
        builder.AppendLine("| --- | --- |");
        AppendTableRow(builder, "Generated at", context.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        AppendTableRow(builder, "Dialect", EmptyFallback(context.Dialect));
        AppendTableRow(builder, "Base table", EmptyFallback(context.BaseTable));
        AppendTableRow(builder, "DISTINCT", context.Distinct ? "Yes" : "No");
        AppendTableRow(builder, "Limit", context.LimitRows?.ToString(CultureInfo.InvariantCulture) ?? "None");
        builder.AppendLine();
    }

    /// <summary>
    /// Appends the generated SQL section as a fenced code block.
    /// </summary>
    private static void AppendGeneratedSql(StringBuilder builder, string generatedSql)
    {
        AppendSectionTitle(builder, "Generated SQL");
        AppendFencedCodeBlock(builder, "sql", generatedSql);
    }

    /// <summary>
    /// Appends the SELECT column table when columns are present.
    /// </summary>
    private static void AppendSelectedColumns(StringBuilder builder, IReadOnlyList<QueryDocumentationColumn> columns)
    {
        if (columns.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Selected columns");
        builder.AppendLine("| Table | Column | Alias |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (QueryDocumentationColumn column in columns)
        {
            AppendTableRow(builder, column.Table, column.Column, column.Alias);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends join definitions and enabled additional join pairs.
    /// </summary>
    private static void AppendJoins(StringBuilder builder, IReadOnlyList<QueryDocumentationJoin> joins)
    {
        if (joins.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Joins");
        builder.AppendLine("| Type | From | To | Additional pairs |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (QueryDocumentationJoin join in joins)
        {
            string from = Qualified(join.FromTable, join.FromColumn);
            string to = Qualified(join.ToTable, join.ToColumn);
            string additionalPairs = join.AdditionalPairs.Count == 0
                ? string.Empty
                : string.Join("<br>", join.AdditionalPairs.Select(pair => $"{pair.FromColumn} = {pair.ToColumn}"));

            AppendTableRow(builder, join.JoinType, from, to, additionalPairs);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends WHERE or HAVING filter documentation rows.
    /// </summary>
    private static void AppendFilters(StringBuilder builder, IReadOnlyList<QueryDocumentationFilter> filters)
    {
        if (filters.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Filters");
        builder.AppendLine("| Connector | Field | Operator | Value kind | Value |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (QueryDocumentationFilter filter in filters)
        {
            AppendTableRow(builder, filter.Connector, filter.Field, filter.Operator, filter.ValueKind, filter.Value);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends aggregate expressions and their optional conditions.
    /// </summary>
    private static void AppendAggregates(StringBuilder builder, IReadOnlyList<QueryDocumentationAggregate> aggregates)
    {
        if (aggregates.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Aggregates");
        builder.AppendLine("| Function | Column | Alias | DISTINCT | Condition |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (QueryDocumentationAggregate aggregate in aggregates)
        {
            AppendTableRow(
                builder,
                aggregate.Function,
                Qualified(aggregate.Table, aggregate.Column),
                aggregate.Alias,
                aggregate.Distinct ? "Yes" : "No",
                aggregate.Condition);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends ORDER BY documentation rows.
    /// </summary>
    private static void AppendSorting(StringBuilder builder, IReadOnlyList<QueryDocumentationOrder> sorting)
    {
        if (sorting.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Sorting");
        builder.AppendLine("| Field | Direction |");
        builder.AppendLine("| --- | --- |");

        foreach (QueryDocumentationOrder order in sorting)
        {
            AppendTableRow(builder, order.Field, order.Direction);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends calculated column aliases and expressions.
    /// </summary>
    private static void AppendCalculatedColumns(StringBuilder builder, IReadOnlyList<QueryDocumentationCalculatedColumn> calculatedColumns)
    {
        if (calculatedColumns.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Calculated columns");
        builder.AppendLine("| Alias | Expression |");
        builder.AppendLine("| --- | --- |");

        foreach (QueryDocumentationCalculatedColumn column in calculatedColumns)
        {
            AppendTableRow(builder, column.Alias, column.Expression);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends query parameter metadata rows.
    /// </summary>
    private static void AppendParameters(StringBuilder builder, IReadOnlyList<QueryDocumentationParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, "Parameters");
        builder.AppendLine("| Name | Type | Required | Cognos prompt | Default | Description |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (QueryDocumentationParameter parameter in parameters)
        {
            AppendTableRow(
                builder,
                parameter.Name,
                parameter.DeclaredType,
                parameter.Required ? "Yes" : "No",
                parameter.UseCognosPrompt ? "Yes" : "No",
                parameter.DefaultValue,
                parameter.Description);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends a bullet-list section for simple string values.
    /// </summary>
    private static void AppendSimpleListSection(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        AppendSectionTitle(builder, title);

        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            builder.Append("- ").AppendLine(EscapeParagraph(value.Trim()));
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends a multiline textual section as bullet points.
    /// </summary>
    private static void AppendTextSection(StringBuilder builder, string title, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        AppendSectionTitle(builder, title);

        foreach (string line in NormalizeLines(text))
        {
            builder.Append("- ").AppendLine(EscapeParagraph(line));
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Normalizes multiline text into trimmed non-empty lines.
    /// </summary>
    private static IEnumerable<string> NormalizeLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    /// <summary>
    /// Appends a level-two Markdown section title.
    /// </summary>
    private static void AppendSectionTitle(StringBuilder builder, string title)
    {
        builder.Append("## ").AppendLine(title);
        builder.AppendLine();
    }

    /// <summary>
    /// Appends a fenced Markdown code block using a safe fence length.
    /// </summary>
    private static void AppendFencedCodeBlock(StringBuilder builder, string language, string code)
    {
        string safeCode = code ?? string.Empty;
        string fence = BuildFence(safeCode);

        builder.Append(fence).AppendLine(language);
        builder.AppendLine(safeCode.TrimEnd());
        builder.AppendLine(fence);
        builder.AppendLine();
    }

    /// <summary>
    /// Builds a Markdown fence longer than any backtick sequence in the SQL body.
    /// </summary>
    private static string BuildFence(string code)
    {
        int longestFence = 0;
        int currentFence = 0;

        foreach (char character in code)
        {
            if (character == '`')
            {
                currentFence++;
                longestFence = Math.Max(longestFence, currentFence);
            }
            else
            {
                currentFence = 0;
            }
        }

        return new string('`', Math.Max(3, longestFence + 1));
    }

    /// <summary>
    /// Appends one escaped Markdown table row.
    /// </summary>
    private static void AppendTableRow(StringBuilder builder, params string?[] values)
    {
        builder.Append('|');

        foreach (string? value in values)
        {
            builder.Append(' ').Append(EscapeTableCell(value)).Append(' ').Append('|');
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Escapes a value for safe use inside a Markdown table cell.
    /// </summary>
    private static string EscapeTableCell(string? value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        return EscapeParagraph(normalized)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }

    /// <summary>
    /// Escapes a value for safe use inside a Markdown heading.
    /// </summary>
    private static string EscapeHeading(string value)
    {
        return EscapeParagraph(value).Replace("#", "\\#", StringComparison.Ordinal);
    }

    /// <summary>
    /// Escapes lightweight Markdown formatting characters in plain text.
    /// </summary>
    private static string EscapeParagraph(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns an em dash when a display value is empty.
    /// </summary>
    private static string EmptyFallback(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }

    /// <summary>
    /// Builds a readable qualified column name from table and column parts.
    /// </summary>
    private static string Qualified(string table, string column)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            return column;
        }

        if (string.IsNullOrWhiteSpace(column))
        {
            return table;
        }

        return $"{table}.{column}";
    }
}
