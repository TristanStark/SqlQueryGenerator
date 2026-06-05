using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Rewrites imported SQL into a cleaner canonical form using conservative transformations only.
/// </summary>
public sealed class SqlRewriteSuggestionService
{
    private readonly ReverseSqlImportService _importService = new();
    private readonly SqlQueryGeneratorEngine _generator = new();
    private readonly SqlComparisonService _comparisonService = new();

    /// <summary>
    /// Rewrites one raw SQL statement into a cleaner SQL string.
    /// </summary>
    /// <param name="sql">Raw SQL to modernize.</param>
    /// <param name="options">Optional SQL generation options.</param>
    /// <returns>Rewritten SQL plus warnings and applied transformations.</returns>
    public SqlRewriteResult Rewrite(string sql, SqlGeneratorOptions? options = null, SourceSqlDialect sourceDialect = SourceSqlDialect.GenericSql)
    {
        ReverseSqlImportResult imported = _importService.Import(sql, sourceDialect);
        QueryDefinition rewritten = QueryDefinitionCloner.Clone(imported.Query);
        List<string> transformations = [];
        List<string> warnings = [.. imported.Warnings];

        if (sql.Contains("(+)", StringComparison.Ordinal))
        {
            transformations.Add("LegacyOuterJoinConverted");
        }

        if (LooksLikeImplicitJoinSql(sql) && rewritten.Joins.Count > 0)
        {
            transformations.Add("ImplicitJoinConverted");
        }

        int duplicateFilterCount = RemoveDuplicateFilters(rewritten);
        if (duplicateFilterCount > 0)
        {
            transformations.Add("DuplicatePredicateRemoved");
        }

        RemoveDuplicateSelectedColumns(rewritten);
        RemoveDuplicateGroupBy(rewritten);
        RemoveDuplicateOrderBy(rewritten);
        transformations.Add("FormattingImproved");

        SqlGenerationResult generated = _generator.Generate(rewritten, new DatabaseSchema(), options);
        warnings.AddRange(generated.Warnings);

        return new SqlRewriteResult
        {
            RewrittenSql = generated.Sql,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            AppliedTransformations = transformations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Comparison = _comparisonService.Compare(sql, generated.Sql)
        };
    }
    private static bool LooksLikeImplicitJoinSql(string sql)
    {
        return sql.Contains(',', StringComparison.Ordinal)
            && Regex.IsMatch(sql, @"(?is)\bFROM\b.+?,.+?\bWHERE\b")
            && !Regex.IsMatch(sql, @"(?is)\bJOIN\b");
    }

    private static int RemoveDuplicateFilters(QueryDefinition query)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<FilterCondition> unique = [];
        int removed = 0;

        foreach (FilterCondition filter in query.Filters)
        {
            string key = string.Join("|",
                filter.FieldKind,
                filter.Column?.Table ?? string.Empty,
                filter.Column?.Column ?? string.Empty,
                filter.FieldAlias ?? string.Empty,
                filter.Operator,
                filter.Value ?? string.Empty,
                filter.SecondValue ?? string.Empty,
                filter.ValueKind,
                filter.SubqueryName ?? string.Empty,
                filter.RawSubquerySql ?? string.Empty,
                filter.Connector);

            if (!seen.Add(key))
            {
                removed++;
                continue;
            }

            unique.Add(filter);
        }

        if (removed > 0)
        {
            query.Filters.Clear();
            foreach (FilterCondition filter in unique)
            {
                query.Filters.Add(filter);
            }
        }

        return removed;
    }

    private static void RemoveDuplicateSelectedColumns(QueryDefinition query)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        ColumnReference[] unique = query.SelectedColumns
            .Where(c => seen.Add($"{c.Table}|{c.Column}|{c.Alias ?? string.Empty}"))
            .ToArray();

        if (unique.Length == query.SelectedColumns.Count)
        {
            return;
        }

        query.SelectedColumns.Clear();
        foreach (ColumnReference column in unique)
        {
            query.SelectedColumns.Add(column);
        }
    }

    private static void RemoveDuplicateGroupBy(QueryDefinition query)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        ColumnReference[] unique = query.GroupBy
            .Where(c => seen.Add($"{c.Table}|{c.Column}"))
            .ToArray();

        if (unique.Length == query.GroupBy.Count)
        {
            return;
        }

        query.GroupBy.Clear();
        foreach (ColumnReference column in unique)
        {
            query.GroupBy.Add(column);
        }
    }

    private static void RemoveDuplicateOrderBy(QueryDefinition query)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        OrderByItem[] unique = query.OrderBy
            .Where(o => seen.Add($"{o.Column?.Table ?? string.Empty}|{o.Column?.Column ?? string.Empty}|{o.FieldKind}|{o.FieldAlias ?? string.Empty}|{o.Direction}"))
            .ToArray();

        if (unique.Length == query.OrderBy.Count)
        {
            return;
        }

        query.OrderBy.Clear();
        foreach (OrderByItem item in unique)
        {
            query.OrderBy.Add(item);
        }
    }
}

/// <summary>
/// Represents the outcome of one SQL rewrite operation.
/// </summary>
public sealed class SqlRewriteResult
{
    /// <summary>
    /// Gets or sets the rewritten SQL.
    /// </summary>
    /// <value>Canonical SQL generated from the imported model.</value>
    public string RewrittenSql { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets rewrite warnings.
    /// </summary>
    /// <value>Warnings about partial support or conservative skips.</value>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the applied transformation names.
    /// </summary>
    /// <value>List of conservative transformations applied during rewrite.</value>
    public IReadOnlyList<string> AppliedTransformations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the line comparison between the source SQL and the rewritten SQL.
    /// </summary>
    /// <value>Side-by-side comparison aligned by line.</value>
    public SqlComparisonReport Comparison { get; init; } = new();
}
