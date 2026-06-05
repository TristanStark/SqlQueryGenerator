namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Builds a conservative line-based comparison between two SQL texts.
/// </summary>
public sealed class SqlComparisonService
{
    /// <summary>
    /// Compares two SQL texts line by line.
    /// </summary>
    /// <param name="sourceSql">Original SQL.</param>
    /// <param name="targetSql">Result SQL.</param>
    /// <returns>Comparison report with aligned source/result lines.</returns>
    public SqlComparisonReport Compare(string? sourceSql, string? targetSql, SqlComparisonOptions? options = null)
    {
        options ??= new SqlComparisonOptions();
        string[] sourceLines = SplitLines(sourceSql);
        string[] targetLines = SplitLines(targetSql);
        string[] comparableSourceLines = sourceLines.Select(line => NormalizeComparableLine(line, options)).ToArray();
        string[] comparableTargetLines = targetLines.Select(line => NormalizeComparableLine(line, options)).ToArray();
        List<SqlComparisonLine> rawLines = BuildRawDiff(sourceLines, comparableSourceLines, targetLines, comparableTargetLines);
        List<SqlComparisonLine> mergedLines = MergeAdjacentEdits(rawLines);

        return new SqlComparisonReport
        {
            SourceLineCount = sourceLines.Length,
            TargetLineCount = targetLines.Length,
            Lines = mergedLines,
            Options = options
        };
    }

    private static string[] SplitLines(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return Array.Empty<string>();
        }

        return sql
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();
    }

    private static List<SqlComparisonLine> BuildRawDiff(string[] sourceLines, string[] comparableSourceLines, string[] targetLines, string[] comparableTargetLines)
    {
        int[,] lcs = BuildLongestCommonSubsequenceMatrix(comparableSourceLines, comparableTargetLines);
        List<SqlComparisonLine> result = [];

        int sourceIndex = 0;
        int targetIndex = 0;
        while (sourceIndex < sourceLines.Length && targetIndex < targetLines.Length)
        {
            if (string.Equals(comparableSourceLines[sourceIndex], comparableTargetLines[targetIndex], StringComparison.Ordinal))
            {
                result.Add(new SqlComparisonLine(
                    SqlComparisonKind.Unchanged,
                    sourceIndex + 1,
                    sourceLines[sourceIndex],
                    targetIndex + 1,
                    targetLines[targetIndex]));
                sourceIndex++;
                targetIndex++;
            }
            else if (lcs[sourceIndex + 1, targetIndex] >= lcs[sourceIndex, targetIndex + 1])
            {
                result.Add(new SqlComparisonLine(
                    SqlComparisonKind.Removed,
                    sourceIndex + 1,
                    sourceLines[sourceIndex],
                    null,
                    string.Empty));
                sourceIndex++;
            }
            else
            {
                result.Add(new SqlComparisonLine(
                    SqlComparisonKind.Added,
                    null,
                    string.Empty,
                    targetIndex + 1,
                    targetLines[targetIndex]));
                targetIndex++;
            }
        }

        while (sourceIndex < sourceLines.Length)
        {
            result.Add(new SqlComparisonLine(
                SqlComparisonKind.Removed,
                sourceIndex + 1,
                sourceLines[sourceIndex],
                null,
                string.Empty));
            sourceIndex++;
        }

        while (targetIndex < targetLines.Length)
        {
            result.Add(new SqlComparisonLine(
                SqlComparisonKind.Added,
                null,
                string.Empty,
                targetIndex + 1,
                targetLines[targetIndex]));
            targetIndex++;
        }

        return result;
    }

    private static int[,] BuildLongestCommonSubsequenceMatrix(string[] sourceLines, string[] targetLines)
    {
        int[,] lcs = new int[sourceLines.Length + 1, targetLines.Length + 1];
        for (int sourceIndex = sourceLines.Length - 1; sourceIndex >= 0; sourceIndex--)
        {
            for (int targetIndex = targetLines.Length - 1; targetIndex >= 0; targetIndex--)
            {
                lcs[sourceIndex, targetIndex] = string.Equals(sourceLines[sourceIndex], targetLines[targetIndex], StringComparison.Ordinal)
                    ? 1 + lcs[sourceIndex + 1, targetIndex + 1]
                    : Math.Max(lcs[sourceIndex + 1, targetIndex], lcs[sourceIndex, targetIndex + 1]);
            }
        }

        return lcs;
    }

    private static string NormalizeComparableLine(string line, SqlComparisonOptions options)
    {
        string comparable = line;
        if (options.IgnoreWhitespaceChanges)
        {
            comparable = string.Join(" ", comparable.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        if (options.IgnoreCaseChanges)
        {
            comparable = comparable.ToUpperInvariant();
        }

        return comparable;
    }

    private static List<SqlComparisonLine> MergeAdjacentEdits(IReadOnlyList<SqlComparisonLine> rawLines)
    {
        List<SqlComparisonLine> result = [];
        int index = 0;
        while (index < rawLines.Count)
        {
            if (rawLines[index].Kind is SqlComparisonKind.Unchanged)
            {
                result.Add(rawLines[index]);
                index++;
                continue;
            }

            List<SqlComparisonLine> removed = [];
            List<SqlComparisonLine> added = [];
            while (index < rawLines.Count && rawLines[index].Kind is not SqlComparisonKind.Unchanged)
            {
                if (rawLines[index].Kind == SqlComparisonKind.Removed)
                {
                    removed.Add(rawLines[index]);
                }
                else
                {
                    added.Add(rawLines[index]);
                }

                index++;
            }

            int pairedCount = Math.Min(removed.Count, added.Count);
            for (int pairIndex = 0; pairIndex < pairedCount; pairIndex++)
            {
                SqlComparisonLine removedLine = removed[pairIndex];
                SqlComparisonLine addedLine = added[pairIndex];
                result.Add(new SqlComparisonLine(
                    SqlComparisonKind.Modified,
                    removedLine.SourceLineNumber,
                    removedLine.SourceText,
                    addedLine.TargetLineNumber,
                    addedLine.TargetText));
            }

            for (int pairIndex = pairedCount; pairIndex < removed.Count; pairIndex++)
            {
                result.Add(removed[pairIndex]);
            }

            for (int pairIndex = pairedCount; pairIndex < added.Count; pairIndex++)
            {
                result.Add(added[pairIndex]);
            }
        }

        return result;
    }
}

/// <summary>
/// One aligned line in a SQL comparison report.
/// </summary>
/// <param name="Kind">Comparison status for this row.</param>
/// <param name="SourceLineNumber">Line number in the original SQL, if any.</param>
/// <param name="SourceText">Original SQL text.</param>
/// <param name="TargetLineNumber">Line number in the result SQL, if any.</param>
/// <param name="TargetText">Result SQL text.</param>
public sealed record SqlComparisonLine(
    SqlComparisonKind Kind,
    int? SourceLineNumber,
    string SourceText,
    int? TargetLineNumber,
    string TargetText)
{
    /// <summary>
    /// Gets a short label used in the comparison grid.
    /// </summary>
    public string KindLabel => Kind switch
    {
        SqlComparisonKind.Unchanged => "=",
        SqlComparisonKind.Modified => "~",
        SqlComparisonKind.Added => "+",
        SqlComparisonKind.Removed => "-",
        _ => string.Empty
    };
}

/// <summary>
/// Summary of a SQL line comparison.
/// </summary>
public sealed class SqlComparisonReport
{
    /// <summary>
    /// Gets or sets the aligned comparison lines.
    /// </summary>
    public IReadOnlyList<SqlComparisonLine> Lines { get; init; } = Array.Empty<SqlComparisonLine>();

    /// <summary>
    /// Gets the options used to compute this comparison.
    /// </summary>
    public SqlComparisonOptions Options { get; init; } = new();

    /// <summary>
    /// Gets or sets the number of lines in the original SQL.
    /// </summary>
    public int SourceLineCount { get; init; }

    /// <summary>
    /// Gets or sets the number of lines in the result SQL.
    /// </summary>
    public int TargetLineCount { get; init; }

    /// <summary>
    /// Gets the number of unchanged rows.
    /// </summary>
    public int UnchangedCount => Lines.Count(line => line.Kind == SqlComparisonKind.Unchanged);

    /// <summary>
    /// Gets the number of modified rows.
    /// </summary>
    public int ModifiedCount => Lines.Count(line => line.Kind == SqlComparisonKind.Modified);

    /// <summary>
    /// Gets the number of added rows.
    /// </summary>
    public int AddedCount => Lines.Count(line => line.Kind == SqlComparisonKind.Added);

    /// <summary>
    /// Gets the number of removed rows.
    /// </summary>
    public int RemovedCount => Lines.Count(line => line.Kind == SqlComparisonKind.Removed);

    /// <summary>
    /// Gets whether the two SQL texts differ.
    /// </summary>
    public bool HasDifferences => ModifiedCount > 0 || AddedCount > 0 || RemovedCount > 0;

    /// <summary>
    /// Formats a concise human-readable summary.
    /// </summary>
    /// <param name="sourceLabel">Label of the original SQL.</param>
    /// <param name="targetLabel">Label of the result SQL.</param>
    /// <returns>Comparison summary.</returns>
    public string FormatSummary(string sourceLabel, string targetLabel)
    {
        string optionsSuffix = Options.IsDefault ? string.Empty : $" (options: {Options.FormatLabel()})";
        if (!HasDifferences)
        {
            return $"{sourceLabel} et {targetLabel} sont identiques sur {UnchangedCount} ligne(s){optionsSuffix}.";
        }

        return $"{sourceLabel} -> {targetLabel}: {ModifiedCount} ligne(s) modifiee(s), {AddedCount} ajoutee(s), {RemovedCount} supprimee(s), {UnchangedCount} inchangee(s){optionsSuffix}.";
    }
}

/// <summary>
/// Options controlling SQL comparison normalization.
/// </summary>
public sealed class SqlComparisonOptions
{
    /// <summary>
    /// Gets or sets whether whitespace-only line differences should be ignored.
    /// </summary>
    public bool IgnoreWhitespaceChanges { get; init; }

    /// <summary>
    /// Gets or sets whether case-only line differences should be ignored.
    /// </summary>
    public bool IgnoreCaseChanges { get; init; }

    /// <summary>
    /// Gets whether the strict default comparison is used.
    /// </summary>
    public bool IsDefault => !IgnoreWhitespaceChanges && !IgnoreCaseChanges;

    /// <summary>
    /// Formats a short label for summaries.
    /// </summary>
    public string FormatLabel()
    {
        List<string> labels = [];
        if (IgnoreWhitespaceChanges)
        {
            labels.Add("ignore espaces");
        }

        if (IgnoreCaseChanges)
        {
            labels.Add("ignore casse");
        }

        return labels.Count == 0 ? "strict" : string.Join(", ", labels);
    }
}

/// <summary>
/// Change kinds used by SQL comparisons.
/// </summary>
public enum SqlComparisonKind
{
    Unchanged,
    Modified,
    Added,
    Removed
}
