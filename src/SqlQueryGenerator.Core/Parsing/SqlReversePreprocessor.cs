using SqlQueryGenerator.Core.Generation;
using System.Text;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Preprocesses raw SQL for reverse import by stripping comments while preserving string literals.
/// </summary>
public static class SqlReversePreprocessor
{
    /// <summary>
    /// Removes SQL comments, validates the cleaned statement and returns structured preprocessing diagnostics.
    /// </summary>
    /// <param name="sql">Raw SQL entered by the user.</param>
    /// <returns>Cleaned SQL plus diagnostics describing ignored fragments.</returns>
    public static SqlReversePreprocessResult Preprocess(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        string stripped = StripComments(sql, out bool removedLineComments, out bool removedBlockComments);
        string normalized = SqlSafety.NormalizeRawSelectQueryForReverse(stripped);
        List<ReverseSqlDiagnostic> diagnostics = [];

        if (removedLineComments || removedBlockComments)
        {
            List<string> removedKinds = [];
            if (removedLineComments)
            {
                removedKinds.Add("--");
            }

            if (removedBlockComments)
            {
                removedKinds.Add("/* ... */");
            }

            diagnostics.Add(new ReverseSqlDiagnostic
            {
                Severity = ReverseSqlDiagnosticSeverity.Info,
                Clause = "Comments",
                Message = $"Commentaires ignorés pendant la rétro-ingénierie: {string.Join(", ", removedKinds)}.",
                SuggestedFix = "Aucune action requise. Les commentaires sont ignorés pour le parsing seulement."
            });
        }

        return new SqlReversePreprocessResult
        {
            NormalizedSql = normalized,
            Diagnostics = diagnostics
        };
    }

    private static string StripComments(string sql, out bool removedLineComments, out bool removedBlockComments)
    {
        StringBuilder builder = new(sql.Length);
        removedLineComments = false;
        removedBlockComments = false;
        bool inString = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char current = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (current == '\'')
            {
                builder.Append(current);
                if (inString && next == '\'')
                {
                    builder.Append(next);
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString && current == '-' && next == '-')
            {
                removedLineComments = true;
                i += 2;
                while (i < sql.Length && sql[i] != '\r' && sql[i] != '\n')
                {
                    i++;
                }

                if (i < sql.Length)
                {
                    builder.Append(sql[i]);
                    if (sql[i] == '\r' && i + 1 < sql.Length && sql[i + 1] == '\n')
                    {
                        builder.Append(sql[i + 1]);
                        i++;
                    }
                }

                continue;
            }

            if (!inString && current == '/' && next == '*')
            {
                removedBlockComments = true;
                i += 2;
                while (i < sql.Length - 1 && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    if (sql[i] == '\r' || sql[i] == '\n')
                    {
                        builder.Append(sql[i]);
                        if (sql[i] == '\r' && i + 1 < sql.Length && sql[i + 1] == '\n')
                        {
                            builder.Append(sql[i + 1]);
                            i++;
                        }
                    }

                    i++;
                }

                i = Math.Min(i + 1, sql.Length - 1);
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}

/// <summary>
/// Result of SQL preprocessing before reverse import.
/// </summary>
public sealed class SqlReversePreprocessResult
{
    public string NormalizedSql { get; init; } = string.Empty;

    public IReadOnlyList<ReverseSqlDiagnostic> Diagnostics { get; init; } = Array.Empty<ReverseSqlDiagnostic>();
}
