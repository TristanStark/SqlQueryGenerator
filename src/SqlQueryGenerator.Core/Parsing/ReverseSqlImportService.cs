using SqlQueryGenerator.Core.Query;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Imports raw SQL into the existing query model while surfacing conservative diagnostics and coverage.
/// </summary>
public sealed class ReverseSqlImportService
{
    private readonly SqlSelectReverseParser _parser = new();

    /// <summary>
    /// Parses one read-only SELECT statement and returns the reconstructed query model.
    /// </summary>
    /// <param name="sql">Raw SQL to import.</param>
    /// <param name="sourceDialect">Selected source dialect profile.</param>
    /// <returns>Imported query plus warnings, diagnostics and coverage.</returns>
    public ReverseSqlImportResult Import(string sql, SourceSqlDialect sourceDialect = SourceSqlDialect.GenericSql)
    {
        SqlReversePreprocessResult preprocessed = SqlReversePreprocessor.Preprocess(sql);
        List<string> warnings = BuildWarnings(preprocessed.NormalizedSql, sourceDialect);
        List<ReverseSqlDiagnostic> diagnostics = [.. preprocessed.Diagnostics];
        diagnostics.AddRange(BuildWarningDiagnostics(warnings));

        try
        {
            QueryDefinition query = _parser.Parse(preprocessed.NormalizedSql, sourceDialect);
            return new ReverseSqlImportResult
            {
                Query = query,
                SourceDialect = sourceDialect,
                NormalizedSql = preprocessed.NormalizedSql,
                Warnings = warnings,
                Diagnostics = diagnostics,
                Coverage = BuildCoverageReport(preprocessed.NormalizedSql, query, warnings)
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            if (TryBuildPartialUnsupportedImportResult(preprocessed.NormalizedSql, sourceDialect, warnings, diagnostics, ex, out ReverseSqlImportResult partialResult))
            {
                return partialResult;
            }

            ReverseSqlDiagnostic diagnostic = BuildFailureDiagnostic(sql, preprocessed.NormalizedSql, ex);
            throw new ReverseSqlImportException(diagnostic.Message, diagnostic, ex);
        }
    }

    private static List<string> BuildWarnings(string sql, SourceSqlDialect sourceDialect)
    {
        List<string> warnings = [];
        if (Regex.IsMatch(sql, @"(?is)\bWITH\b"))
        {
            warnings.Add("Le SQL contient un CTE. L'import reste conservateur et peut simplifier certaines structures.");
        }

        if (Regex.IsMatch(sql, @"(?is)\(\s*SELECT\b"))
        {
            warnings.Add("Le SQL contient au moins une sous-requete. Les fragments complexes peuvent etre partiellement preserves seulement.");
        }

        if (Regex.IsMatch(sql, @"(?is)\b(UNION|INTERSECT|MINUS)\b"))
        {
            warnings.Add("Le SQL contient une operation d'ensemble. Elle est signalee mais n'est pas completement modelee.");
        }

        if (Regex.IsMatch(sql, @"(?is)\b(CONNECT\s+BY|START\s+WITH|MODEL)\b"))
        {
            warnings.Add("Le SQL contient des constructions Oracle avancees. Verifie le resultat importe avant edition.");
        }

        if (SqlSelectReverseParser.IsCognosPromptExpression(sql) || Regex.IsMatch(sql, @"(?is)#prompt\s*\("))
        {
            warnings.Add("Le SQL contient des prompts Cognos. Ils sont conserves comme parametres bruts.");
        }

        if (sourceDialect == SourceSqlDialect.Db2 && Regex.IsMatch(sql, @"(?is)\bFETCH\s+FIRST\s+\d+\s+ROWS\s+ONLY\b"))
        {
            warnings.Add("Profil DB2: FETCH FIRST a ete reconnu pendant l'import.");
        }

        if (sourceDialect == SourceSqlDialect.OracleLegacy && Regex.IsMatch(sql, @"(?is)\(\+\)|&\d+|&&?[A-Za-z_]\w*"))
        {
            warnings.Add("Profil Oracle Legacy: syntaxes (+) et/ou & ont ete preservees autant que possible.");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<ReverseSqlDiagnostic> BuildWarningDiagnostics(IEnumerable<string> warnings)
    {
        foreach (string warning in warnings)
        {
            yield return new ReverseSqlDiagnostic
            {
                Severity = ReverseSqlDiagnosticSeverity.Warning,
                Message = warning,
                SuggestedFix = "Relis le SQL genere et les clauses marquees comme partiellement prises en charge."
            };
        }
    }

    private static ReverseSqlCoverageReport BuildCoverageReport(string sql, QueryDefinition query, IReadOnlyList<string> warnings)
    {
        List<ReverseSqlClauseCoverage> clauses = [];
        string fromJoinFragment = ExtractClauseFragment(sql, "FROM");
        AddClauseCoverage(clauses, "SELECT", HasClause(sql, "SELECT"), query.SelectedColumns.Count + query.Aggregates.Count + query.CustomColumns.Count > 0, false, false, "Colonnes, agrégats ou expressions reconnus.");
        AddClauseCoverage(clauses, "FROM/JOIN", HasClause(sql, "FROM"), !string.IsNullOrWhiteSpace(query.BaseTable), HasLikelyPartialFromJoin(fromJoinFragment, query), false, "Table de base et jointures reconnues.");
        AddClauseCoverage(clauses, "WHERE", HasClause(sql, "WHERE"), query.Filters.Any(f => f.FieldKind == QueryFieldKind.Column || f.FieldKind == QueryFieldKind.CustomColumn), HasLikelyPartialWhere(sql, query), false, "Filtres WHERE reconstruits.");
        AddClauseCoverage(clauses, "GROUP BY", HasClause(sql, "GROUP BY"), query.GroupBy.Count > 0, HasClause(sql, "GROUP BY") && query.GroupBy.Count == 0, false, "Colonnes de regroupement reconnues.");
        AddClauseCoverage(clauses, "HAVING", HasClause(sql, "HAVING"), query.Filters.Any(f => f.FieldKind == QueryFieldKind.Aggregate), HasClause(sql, "HAVING") && !query.Filters.Any(f => f.FieldKind == QueryFieldKind.Aggregate), false, "Filtres HAVING reconstruits.");
        AddClauseCoverage(clauses, "ORDER BY", HasClause(sql, "ORDER BY"), query.OrderBy.Count > 0, HasClause(sql, "ORDER BY") && query.OrderBy.Count == 0, false, "Tri reconstruit.");
        AddClauseCoverage(clauses, "CTE", Regex.IsMatch(sql, @"(?is)\bWITH\b"), false, false, Regex.IsMatch(sql, @"(?is)\bWITH\b"), "CTE détecté mais non modélisé dans le constructeur.");
        AddClauseCoverage(clauses, "Subqueries", Regex.IsMatch(sql, @"(?is)\(\s*SELECT\b"), query.Filters.Any(f => f.ValueKind == FilterValueKind.Subquery), Regex.IsMatch(sql, @"(?is)\(\s*SELECT\b"), false, "Sous-requêtes détectées et partiellement préservées.");
        AddClauseCoverage(clauses, "Set operations", Regex.IsMatch(sql, @"(?is)\b(UNION|INTERSECT|MINUS)\b"), false, false, Regex.IsMatch(sql, @"(?is)\b(UNION|INTERSECT|MINUS)\b"), "Opérations d'ensemble détectées mais non modélisées.");
        AddClauseCoverage(clauses, "Vendor-specific", Regex.IsMatch(sql, @"(?is)\b(CONNECT\s+BY|MODEL|DECODE|NVL|TOP\s+\d+|ILIKE)\b"), false, true, false, "Syntaxe spécifique moteur détectée.");

        double[] scored = clauses
            .Where(c => c.Status != ReverseSqlCoverageStatus.NotPresent)
            .Select(c => c.Status switch
            {
                ReverseSqlCoverageStatus.FullyImported => 1.0,
                ReverseSqlCoverageStatus.PartiallyImported => 0.55,
                ReverseSqlCoverageStatus.Ignored => 0.35,
                ReverseSqlCoverageStatus.Unsupported => 0.15,
                _ => 0.40
            })
            .ToArray();

        return new ReverseSqlCoverageReport
        {
            Clauses = clauses,
            Confidence = scored.Length == 0 ? 0.0 : scored.Average(),
            RiskyFragments = warnings
        };
    }

    private static void AddClauseCoverage(
        ICollection<ReverseSqlClauseCoverage> clauses,
        string clause,
        bool present,
        bool imported,
        bool partial,
        bool unsupported,
        string message)
    {
        ReverseSqlCoverageStatus status = !present
            ? ReverseSqlCoverageStatus.NotPresent
            : unsupported
                ? ReverseSqlCoverageStatus.Unsupported
                : imported && !partial
                    ? ReverseSqlCoverageStatus.FullyImported
                    : partial
                        ? ReverseSqlCoverageStatus.PartiallyImported
                        : ReverseSqlCoverageStatus.Unknown;

        clauses.Add(new ReverseSqlClauseCoverage
        {
            Clause = clause,
            Status = status,
            Message = message
        });
    }

    private static bool HasClause(string sql, string clause) => Regex.IsMatch(sql, $@"(?is)\b{Regex.Escape(clause)}\b");

    private static bool ContainsAny(string sql, params string[] patterns) => patterns.Any(pattern => Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline));

    private static bool HasLikelyPartialFromJoin(string fromJoinFragment, QueryDefinition query)
    {
        if (string.IsNullOrWhiteSpace(fromJoinFragment))
        {
            return false;
        }

        bool hasJoinSyntax = Regex.IsMatch(fromJoinFragment, @"(?is)\bJOIN\b");
        bool hasImplicitMultipleTables = SplitTopLevelComma(fromJoinFragment).Count(segment => !string.IsNullOrWhiteSpace(segment)) > 1;
        return (hasJoinSyntax || hasImplicitMultipleTables) && query.Joins.Count == 0;
    }

    private static IEnumerable<string> SplitTopLevelComma(string text)
    {
        int start = 0;
        int depth = 0;
        bool inString = false;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '\'')
            {
                inString = !inString;
            }
            else if (!inString && current == '(')
            {
                depth++;
            }
            else if (!inString && current == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (!inString && depth == 0 && current == ',')
            {
                yield return text[start..index].Trim();
                start = index + 1;
            }
        }

        yield return text[start..].Trim();
    }

    private static bool HasLikelyPartialWhere(string sql, QueryDefinition query)
    {
        if (!HasClause(sql, "WHERE"))
        {
            return false;
        }

        string whereText = ExtractClauseFragment(sql, "WHERE");
        if (string.IsNullOrWhiteSpace(whereText))
        {
            return true;
        }

        if (Regex.IsMatch(whereText, @"(?is)\bOR\b"))
        {
            return true;
        }

        int predicateCount = Regex.Matches(whereText, @"(?is)\bAND\b").Count + 1;
        return query.Filters.Count == 0 || query.Filters.Count < predicateCount;
    }

    private static ReverseSqlDiagnostic BuildFailureDiagnostic(string originalSql, string normalizedSql, Exception exception)
    {
        string message = exception.Message;
        string? clause = null;
        string? fragment = null;
        int? startOffset = null;
        int? length = null;
        string? suggestedFix = null;

        if (message.Contains("WHERE clause is incomplete.", StringComparison.OrdinalIgnoreCase))
        {
            clause = "WHERE";
            fragment = ExtractClauseFragment(originalSql, "WHERE");
            startOffset = FindClauseOffset(originalSql, "WHERE");
            suggestedFix = "Ajoute une condition après WHERE ou supprime le mot-clé WHERE.";
        }
        else if (message.Contains("GROUP BY clause is incomplete.", StringComparison.OrdinalIgnoreCase))
        {
            clause = "GROUP BY";
            fragment = ExtractClauseFragment(originalSql, "GROUP BY");
            startOffset = FindClauseOffset(originalSql, "GROUP BY");
            suggestedFix = "Ajoute au moins une colonne après GROUP BY ou retire le mot-clé.";
        }
        else if (message.Contains("HAVING clause is incomplete.", StringComparison.OrdinalIgnoreCase))
        {
            clause = "HAVING";
            fragment = ExtractClauseFragment(originalSql, "HAVING");
            startOffset = FindClauseOffset(originalSql, "HAVING");
            suggestedFix = "Ajoute une condition d'agrégat après HAVING ou retire le mot-clé.";
        }
        else if (message.Contains("ORDER BY clause is incomplete.", StringComparison.OrdinalIgnoreCase))
        {
            clause = "ORDER BY";
            fragment = ExtractClauseFragment(originalSql, "ORDER BY");
            startOffset = FindClauseOffset(originalSql, "ORDER BY");
            suggestedFix = "Ajoute une colonne de tri après ORDER BY ou retire le mot-clé.";
        }
        else if (message.Contains("SELECT et FROM", StringComparison.OrdinalIgnoreCase) || message.Contains("SELECT and FROM", StringComparison.OrdinalIgnoreCase))
        {
            clause = "SELECT/FROM";
            fragment = originalSql.Trim();
            startOffset = 0;
            suggestedFix = "Vérifie que la requête contient bien un SELECT unique avec un FROM.";
        }
        else
        {
            clause = "Reverse SQL";
            fragment = normalizedSql.Trim();
            startOffset = 0;
            suggestedFix = "Simplifie la clause en cause ou retire la syntaxe non prise en charge avant de relancer l'import.";
        }

        if (startOffset is not null && fragment is not null)
        {
            length = Math.Max(Math.Min(fragment.Length, Math.Max(1, originalSql.Length - startOffset.Value)), 1);
        }

        (int? line, int? column) = startOffset is int offset
            ? ComputeLineAndColumn(originalSql, offset)
            : (null, null);

        return new ReverseSqlDiagnostic
        {
            Severity = ReverseSqlDiagnosticSeverity.Error,
            Clause = clause,
            Message = $"Reverse SQL failed{FormatLocation(line, column)}. {message}",
            Fragment = fragment,
            StartOffset = startOffset,
            Length = length,
            Line = line,
            Column = column,
            SuggestedFix = suggestedFix
        };
    }

    private bool TryBuildPartialUnsupportedImportResult(
        string normalizedSql,
        SourceSqlDialect sourceDialect,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ReverseSqlDiagnostic> diagnostics,
        Exception exception,
        out ReverseSqlImportResult result)
    {
        result = null!;
        if (exception is not InvalidOperationException invalidOperation
            || !invalidOperation.Message.Contains("UNION, INTERSECT et EXCEPT", StringComparison.OrdinalIgnoreCase)
            || !TryExtractFirstSupportedSelect(normalizedSql, out string supportedSql))
        {
            return false;
        }

        QueryDefinition partialQuery = _parser.Parse(supportedSql, sourceDialect);
        List<ReverseSqlDiagnostic> combinedDiagnostics = [.. diagnostics, BuildPartialImportDiagnostic(normalizedSql)];
        result = new ReverseSqlImportResult
        {
            Query = partialQuery,
            SourceDialect = sourceDialect,
            NormalizedSql = normalizedSql,
            Warnings = warnings,
            Diagnostics = combinedDiagnostics,
            Coverage = BuildCoverageReport(normalizedSql, partialQuery, warnings)
        };
        return true;
    }

    private static ReverseSqlDiagnostic BuildPartialImportDiagnostic(string normalizedSql)
    {
        return new ReverseSqlDiagnostic
        {
            Severity = ReverseSqlDiagnosticSeverity.Warning,
            Clause = "Set operations",
            Message = "Le SQL contient une operation d'ensemble. Seule la premiere branche SELECT a ete importee dans le constructeur.",
            Fragment = normalizedSql.Trim(),
            StartOffset = 0,
            Length = Math.Max(normalizedSql.Trim().Length, 1),
            Line = 1,
            Column = 1,
            SuggestedFix = "Simplifie la requete a un SELECT unique pour un import complet, ou recompose manuellement les branches restantes."
        };
    }

    private static bool TryExtractFirstSupportedSelect(string sql, out string supportedSql)
    {
        int splitIndex = FindFirstTopLevelSetOperationIndex(sql);
        if (splitIndex <= 0)
        {
            supportedSql = string.Empty;
            return false;
        }

        supportedSql = sql[..splitIndex].TrimEnd();
        return supportedSql.Length > 0;
    }

    private static int FindFirstTopLevelSetOperationIndex(string sql)
    {
        int unionIndex = FindTopLevelKeyword(sql, "UNION", 0);
        int intersectIndex = FindTopLevelKeyword(sql, "INTERSECT", 0);
        int exceptIndex = FindTopLevelKeyword(sql, "EXCEPT", 0);
        return new[] { unionIndex, intersectIndex, exceptIndex }
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static int FindTopLevelKeyword(string sql, string keyword, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int i = Math.Max(0, start); i <= sql.Length - keyword.Length; i++)
        {
            char current = sql[i];
            if (current == '\'')
            {
                inString = !inString;
            }
            else if (!inString && current == '(')
            {
                depth++;
            }
            else if (!inString && current == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (depth == 0 && !inString && string.Compare(sql, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                bool beforeOk = i == 0 || !char.IsLetterOrDigit(sql[i - 1]);
                bool afterOk = i + keyword.Length >= sql.Length || !char.IsLetterOrDigit(sql[i + keyword.Length]);
                if (beforeOk && afterOk)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string ExtractClauseFragment(string sql, string clause)
    {
        int start = FindClauseOffset(sql, clause);
        if (start < 0)
        {
            return string.Empty;
        }

        int contentStart = start + clause.Length;
        string[] nextClauses = ["WHERE", "GROUP BY", "HAVING", "ORDER BY", "FETCH FIRST", "LIMIT"];
        int end = sql.Length;
        foreach (string next in nextClauses.Where(next => !string.Equals(next, clause, StringComparison.OrdinalIgnoreCase)))
        {
            int nextIndex = FindClauseOffset(sql, next, contentStart);
            if (nextIndex >= 0 && nextIndex < end)
            {
                end = nextIndex;
            }
        }

        return sql[start..end].Trim();
    }

    private static int FindClauseOffset(string sql, string clause, int startIndex = 0)
    {
        Match match = Regex.Match(sql[startIndex..], $@"(?is)\b{Regex.Escape(clause)}\b");
        return match.Success ? startIndex + match.Index : -1;
    }

    private static (int? Line, int? Column) ComputeLineAndColumn(string sql, int offset)
    {
        if (offset < 0 || offset > sql.Length)
        {
            return (null, null);
        }

        int line = 1;
        int column = 1;
        for (int i = 0; i < offset; i++)
        {
            if (sql[i] == '\n')
            {
                line++;
                column = 1;
                continue;
            }

            if (sql[i] != '\r')
            {
                column++;
            }
        }

        return (line, column);
    }

    private static string FormatLocation(int? line, int? column)
    {
        return line is null || column is null
            ? string.Empty
            : $" near line {line}, column {column}";
    }
}
