from __future__ import annotations

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    content = read(path)
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one occurrence, found {count}: {old[:100]!r}")
    write(path, content.replace(old, new, 1))


# Query model.
replace_once(
    "src/SqlQueryGenerator.Core/Query/QueryDefinition.cs",
    """    public int? LimitRows { get; set; }
}
""",
    """    public int? LimitRows { get; set; }

    /// <summary>
    /// Gets or sets whether the first branch of a compound query must remain parenthesized.
    /// </summary>
    /// <value><c>true</c> when the source SQL wrapped the first SELECT branch.</value>
    public bool FirstBranchParenthesized { get; set; }

    /// <summary>
    /// Gets the set operations appended after this query branch.
    /// </summary>
    /// <value>Ordered UNION, INTERSECT, EXCEPT or MINUS branches.</value>
    public Collection<SetOperationDefinition> SetOperations { get; set; } = [];

    /// <summary>
    /// Gets the ORDER BY items applying to the complete compound query.
    /// </summary>
    /// <value>Global sort expressions placed after all set-operation branches.</value>
    public Collection<OrderByItem> CompoundOrderBy { get; set; } = [];

    /// <summary>
    /// Gets or sets the row limit applying to the complete compound query.
    /// </summary>
    /// <value>Global LIMIT/FETCH FIRST value, or <c>null</c>.</value>
    public int? CompoundLimitRows { get; set; }
}

/// <summary>
/// Identifies a SQL set operator joining two SELECT query branches.
/// </summary>
public enum SetOperationKind
{
    /// <summary>SQL UNION.</summary>
    Union,

    /// <summary>SQL INTERSECT.</summary>
    Intersect,

    /// <summary>SQL EXCEPT.</summary>
    Except,

    /// <summary>Oracle SQL MINUS.</summary>
    Minus
}

/// <summary>
/// Represents one additional SELECT branch in a compound SQL query.
/// </summary>
public sealed record SetOperationDefinition
{
    /// <summary>Gets the operator placed before this branch.</summary>
    /// <value>Set operator joining the previous result with this branch.</value>
    public SetOperationKind Operator { get; init; } = SetOperationKind.Union;

    /// <summary>Gets whether the operator uses the ALL modifier.</summary>
    /// <value><c>true</c> for UNION ALL, INTERSECT ALL or EXCEPT ALL.</value>
    public bool All { get; init; }

    /// <summary>Gets whether this branch must be emitted between parentheses.</summary>
    /// <value><c>true</c> when parentheses preserve grouping or branch-local clauses.</value>
    public bool ParenthesizeQuery { get; init; }

    /// <summary>Gets the structured SELECT query for this branch.</summary>
    /// <value>Branch query definition.</value>
    public required QueryDefinition Query { get; init; }
}
""",
)

# Compound-query parser.
compound_parser = r'''using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Query;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Splits and reconstructs compound SELECT statements while delegating each individual branch
/// to <see cref="SqlSelectReverseParser"/>.
/// </summary>
internal static class SqlCompoundQueryParser
{
    private static readonly string[] SetKeywords = ["UNION", "INTERSECT", "EXCEPT", "MINUS"];

    /// <summary>
    /// Parses a raw SELECT or compound SELECT statement.
    /// </summary>
    /// <param name="sql">Raw SQL text.</param>
    /// <param name="sourceDialect">Source dialect profile.</param>
    /// <param name="branchParser">Parser used for individual SELECT branches.</param>
    /// <returns>Structured query definition, including every set-operation branch.</returns>
    internal static QueryDefinition Parse(
        string sql,
        SourceSqlDialect sourceDialect,
        SqlSelectReverseParser branchParser)
    {
        string candidate = sql.Trim();
        if (candidate.EndsWith(';'))
        {
            candidate = candidate[..^1].TrimEnd();
        }

        string unwrapped = StripBalancedOuterParentheses(candidate, out _);
        string normalized = SqlSafety.NormalizeRawSelectQueryForReverse(unwrapped);
        return ParseNormalized(normalized, sourceDialect, branchParser);
    }

    private static QueryDefinition ParseNormalized(
        string sql,
        SourceSqlDialect sourceDialect,
        SqlSelectReverseParser branchParser)
    {
        string normalized = sql.Trim();
        List<SetOperatorToken> operators = FindTopLevelSetOperators(normalized);
        if (operators.Count == 0)
        {
            return branchParser.ParseSingle(normalized, sourceDialect);
        }

        List<string> operands = SplitOperands(normalized, operators);
        ExtractGlobalCompoundTail(operands, out List<OrderByItem> compoundOrderBy, out int? compoundLimitRows);

        string firstSql = StripBalancedOuterParentheses(operands[0], out bool firstParenthesized);
        QueryDefinition root = ParseNormalized(firstSql, sourceDialect, branchParser);
        root.FirstBranchParenthesized = firstParenthesized;

        for (int index = 0; index < operators.Count; index++)
        {
            string branchSql = StripBalancedOuterParentheses(operands[index + 1], out bool parenthesized);
            if (string.IsNullOrWhiteSpace(branchSql))
            {
                throw new InvalidOperationException(
                    $"Reverse SQL impossible: la branche suivant {operators[index].DisplayText} est vide.");
            }

            QueryDefinition branch = ParseNormalized(branchSql, sourceDialect, branchParser);
            root.SetOperations.Add(new SetOperationDefinition
            {
                Operator = operators[index].Kind,
                All = operators[index].All,
                ParenthesizeQuery = parenthesized,
                Query = branch
            });
        }

        foreach (OrderByItem item in compoundOrderBy)
        {
            root.CompoundOrderBy.Add(item);
        }

        root.CompoundLimitRows = compoundLimitRows;
        MergeParameters(root);
        return root;
    }

    private static List<string> SplitOperands(string sql, IReadOnlyList<SetOperatorToken> operators)
    {
        List<string> operands = [];
        int start = 0;
        foreach (SetOperatorToken token in operators)
        {
            string operand = sql[start..token.Index].Trim();
            if (operand.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Reverse SQL impossible: une branche SELECT est manquante avant {token.DisplayText}.");
            }

            operands.Add(operand);
            start = token.Index + token.Length;
        }

        string last = sql[start..].Trim();
        if (last.Length == 0)
        {
            throw new InvalidOperationException(
                $"Reverse SQL impossible: la branche suivant {operators[^1].DisplayText} est vide.");
        }

        operands.Add(last);
        return operands;
    }

    private static List<SetOperatorToken> FindTopLevelSetOperators(string sql)
    {
        List<SetOperatorToken> result = [];
        ScannerState state = new();

        for (int index = 0; index < sql.Length; index++)
        {
            if (AdvanceQuotedState(sql, ref index, state))
            {
                continue;
            }

            char current = sql[index];
            if (current == '(')
            {
                state.Depth++;
                continue;
            }

            if (current == ')')
            {
                state.Depth = Math.Max(0, state.Depth - 1);
                continue;
            }

            if (state.Depth != 0)
            {
                continue;
            }

            foreach (string keyword in SetKeywords)
            {
                if (!MatchesKeyword(sql, index, keyword))
                {
                    continue;
                }

                int end = index + keyword.Length;
                int modifierStart = SkipWhitespace(sql, end);
                bool all = MatchesKeyword(sql, modifierStart, "ALL");
                bool distinct = !all && MatchesKeyword(sql, modifierStart, "DISTINCT");
                if (all)
                {
                    end = modifierStart + "ALL".Length;
                }
                else if (distinct)
                {
                    end = modifierStart + "DISTINCT".Length;
                }

                result.Add(new SetOperatorToken(
                    index,
                    end - index,
                    ParseKind(keyword),
                    all,
                    sql[index..end].Trim()));
                index = end - 1;
                break;
            }
        }

        return result;
    }

    private static void ExtractGlobalCompoundTail(
        IList<string> operands,
        out List<OrderByItem> compoundOrderBy,
        out int? compoundLimitRows)
    {
        compoundOrderBy = [];
        compoundLimitRows = null;

        string lastOperand = operands[^1];
        int orderIndex = FindTopLevelKeyword(lastOperand, "ORDER BY", 0);
        int limitIndex = FindTopLevelKeyword(lastOperand, "LIMIT", 0);
        int fetchFirstIndex = FindTopLevelKeyword(lastOperand, "FETCH FIRST", 0);
        int fetchNextIndex = FindTopLevelKeyword(lastOperand, "FETCH NEXT", 0);
        int tailIndex = FirstPositive(orderIndex, limitIndex, fetchFirstIndex, fetchNextIndex);
        if (tailIndex < 0)
        {
            return;
        }

        string branchSql = lastOperand[..tailIndex].TrimEnd();
        if (branchSql.Length == 0)
        {
            throw new InvalidOperationException("Reverse SQL impossible: la dernière branche SELECT est vide.");
        }

        string tailSql = lastOperand[tailIndex..].Trim();
        operands[^1] = branchSql;

        if (orderIndex >= 0)
        {
            int relativeOrder = orderIndex - tailIndex;
            int relativeLimit = limitIndex >= 0 ? limitIndex - tailIndex : -1;
            int relativeFetchFirst = fetchFirstIndex >= 0 ? fetchFirstIndex - tailIndex : -1;
            int relativeFetchNext = fetchNextIndex >= 0 ? fetchNextIndex - tailIndex : -1;
            int orderEnd = FirstPositive(relativeLimit, relativeFetchFirst, relativeFetchNext, tailSql.Length);
            string orderText = tailSql[(relativeOrder + "ORDER BY".Length)..orderEnd].Trim();
            ParseCompoundOrderBy(orderText, compoundOrderBy);
        }

        Match limitMatch = Regex.Match(tailSql, @"(?is)\bLIMIT\s+(\d+)\b");
        Match fetchMatch = Regex.Match(
            tailSql,
            @"(?is)\bFETCH\s+(?:FIRST|NEXT)\s+(\d+)\s+ROWS?\s+ONLY\b");
        Match chosen = limitMatch.Success ? limitMatch : fetchMatch;
        if (chosen.Success)
        {
            compoundLimitRows = int.Parse(chosen.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    private static void ParseCompoundOrderBy(string text, ICollection<OrderByItem> target)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("ORDER BY clause is incomplete.");
        }

        foreach (string rawItem in SplitTopLevelComma(text))
        {
            Match match = Regex.Match(rawItem.Trim(), @"(?is)^(.+?)(?:\s+(ASC|DESC))?$");
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                continue;
            }

            target.Add(new OrderByItem
            {
                FieldKind = QueryFieldKind.CustomColumn,
                FieldAlias = match.Groups[1].Value.Trim(),
                Direction = match.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Descending
                    : SortDirection.Ascending
            });
        }
    }

    private static IEnumerable<string> SplitTopLevelComma(string text)
    {
        ScannerState state = new();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (AdvanceQuotedState(text, ref index, state))
            {
                continue;
            }

            char current = text[index];
            if (current == '(')
            {
                state.Depth++;
            }
            else if (current == ')')
            {
                state.Depth = Math.Max(0, state.Depth - 1);
            }
            else if (current == ',' && state.Depth == 0)
            {
                yield return text[start..index].Trim();
                start = index + 1;
            }
        }

        yield return text[start..].Trim();
    }

    private static string StripBalancedOuterParentheses(string sql, out bool parenthesized)
    {
        string result = sql.Trim();
        parenthesized = false;
        while (IsWrappedBySingleParenthesisPair(result))
        {
            parenthesized = true;
            result = result[1..^1].Trim();
        }

        return result;
    }

    private static bool IsWrappedBySingleParenthesisPair(string text)
    {
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
        {
            return false;
        }

        ScannerState state = new();
        for (int index = 0; index < text.Length; index++)
        {
            if (AdvanceQuotedState(text, ref index, state))
            {
                continue;
            }

            if (text[index] == '(')
            {
                state.Depth++;
            }
            else if (text[index] == ')')
            {
                state.Depth--;
                if (state.Depth == 0 && index != text.Length - 1)
                {
                    return false;
                }

                if (state.Depth < 0)
                {
                    return false;
                }
            }
        }

        return state.Depth == 0;
    }

    private static int FindTopLevelKeyword(string sql, string keyword, int start)
    {
        ScannerState state = new();
        for (int index = Math.Max(0, start); index <= sql.Length - keyword.Length; index++)
        {
            if (AdvanceQuotedState(sql, ref index, state))
            {
                continue;
            }

            char current = sql[index];
            if (current == '(')
            {
                state.Depth++;
                continue;
            }

            if (current == ')')
            {
                state.Depth = Math.Max(0, state.Depth - 1);
                continue;
            }

            if (state.Depth == 0 && MatchesKeyword(sql, index, keyword))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool MatchesKeyword(string sql, int index, string keyword)
    {
        if (index < 0 || index + keyword.Length > sql.Length
            || string.Compare(sql, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        bool beforeOk = index == 0 || !IsIdentifierCharacter(sql[index - 1]);
        bool afterOk = index + keyword.Length >= sql.Length
            || !IsIdentifierCharacter(sql[index + keyword.Length]);
        return beforeOk && afterOk;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '$' or '#';
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int FirstPositive(params int[] values)
    {
        return values.Where(value => value >= 0).DefaultIfEmpty(-1).Min();
    }

    private static SetOperationKind ParseKind(string keyword)
    {
        return keyword.ToUpperInvariant() switch
        {
            "UNION" => SetOperationKind.Union,
            "INTERSECT" => SetOperationKind.Intersect,
            "EXCEPT" => SetOperationKind.Except,
            "MINUS" => SetOperationKind.Minus,
            _ => throw new ArgumentOutOfRangeException(nameof(keyword), keyword, null)
        };
    }

    private static void MergeParameters(QueryDefinition root)
    {
        HashSet<string> seen = root.Parameters
            .Select(parameter => parameter.Placeholder)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (SetOperationDefinition operation in root.SetOperations)
        {
            foreach (QueryParameterDefinition parameter in operation.Query.Parameters)
            {
                if (seen.Add(parameter.Placeholder))
                {
                    root.Parameters.Add(parameter);
                }
            }
        }
    }

    private static bool AdvanceQuotedState(string text, ref int index, ScannerState state)
    {
        char current = text[index];

        if (state.InSingleQuote)
        {
            if (current == '\'' && index + 1 < text.Length && text[index + 1] == '\'')
            {
                index++;
            }
            else if (current == '\'')
            {
                state.InSingleQuote = false;
            }

            return true;
        }

        if (state.InDoubleQuote)
        {
            if (current == '"' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index++;
            }
            else if (current == '"')
            {
                state.InDoubleQuote = false;
            }

            return true;
        }

        if (state.InBacktick)
        {
            if (current == '`')
            {
                state.InBacktick = false;
            }

            return true;
        }

        if (state.InBracket)
        {
            if (current == ']')
            {
                state.InBracket = false;
            }

            return true;
        }

        switch (current)
        {
            case '\'':
                state.InSingleQuote = true;
                return true;
            case '"':
                state.InDoubleQuote = true;
                return true;
            case '`':
                state.InBacktick = true;
                return true;
            case '[':
                state.InBracket = true;
                return true;
            default:
                return false;
        }
    }

    private sealed class ScannerState
    {
        internal int Depth { get; set; }

        internal bool InSingleQuote { get; set; }

        internal bool InDoubleQuote { get; set; }

        internal bool InBacktick { get; set; }

        internal bool InBracket { get; set; }
    }

    private sealed record SetOperatorToken(
        int Index,
        int Length,
        SetOperationKind Kind,
        bool All,
        string DisplayText);
}
'''
write("src/SqlQueryGenerator.Core/Parsing/SqlCompoundQueryParser.cs", compound_parser)

replace_once(
    "src/SqlQueryGenerator.Core/Parsing/SqlSelectReverseParser.cs",
    """    public QueryDefinition Parse(string sql, SourceSqlDialect sourceDialect = SourceSqlDialect.GenericSql)
    {
        string normalized = SqlSafety.NormalizeRawSelectQueryForReverse(sql);
""",
    """    public QueryDefinition Parse(string sql, SourceSqlDialect sourceDialect = SourceSqlDialect.GenericSql)
    {
        return SqlCompoundQueryParser.Parse(sql, sourceDialect, this);
    }

    /// <summary>
    /// Parses one SELECT branch after compound set operators have been removed.
    /// </summary>
    /// <param name="sql">Single SELECT branch.</param>
    /// <param name="sourceDialect">Selected source dialect profile.</param>
    /// <returns>Structured branch query.</returns>
    internal QueryDefinition ParseSingle(string sql, SourceSqlDialect sourceDialect = SourceSqlDialect.GenericSql)
    {
        string normalized = SqlSafety.NormalizeRawSelectQueryForReverse(sql);
""",
)

# Reverse import diagnostics and coverage.
replace_once(
    "src/SqlQueryGenerator.Core/Parsing/ReverseSqlImportService.cs",
    """        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            if (TryBuildPartialUnsupportedImportResult(preprocessed.NormalizedSql, sourceDialect, warnings, diagnostics, ex, out ReverseSqlImportResult partialResult))
            {
                return partialResult;
            }

            ReverseSqlDiagnostic diagnostic = BuildFailureDiagnostic(sql, preprocessed.NormalizedSql, ex);
""",
    """        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ReverseSqlDiagnostic diagnostic = BuildFailureDiagnostic(sql, preprocessed.NormalizedSql, ex);
""",
)
replace_once(
    "src/SqlQueryGenerator.Core/Parsing/ReverseSqlImportService.cs",
    """        if (ContainsSetOperation(sql))
        {
            warnings.Add("Le SQL contient une operation d'ensemble. Elle est signalee mais n'est pas completement modelee.");
        }

""",
    "",
)
replace_once(
    "src/SqlQueryGenerator.Core/Parsing/ReverseSqlImportService.cs",
    """        AddClauseCoverage(clauses, "Set operations", ContainsSetOperation(sql), false, false, ContainsSetOperation(sql), "Opérations d'ensemble détectées mais non modélisées.");
""",
    """        AddClauseCoverage(
            clauses,
            "Set operations",
            ContainsSetOperation(sql),
            query.SetOperations.Count > 0,
            false,
            false,
            "Toutes les branches SELECT et leurs opérateurs d'ensemble ont été reconstruits.");
""",
)

# Deep cloning.
replace_once(
    "src/SqlQueryGenerator.Core/Query/QueryDefinitionCloner.cs",
    """            BaseTable = source.BaseTable,
            Distinct = source.Distinct,
            LimitRows = source.LimitRows
""",
    """            BaseTable = source.BaseTable,
            Distinct = source.Distinct,
            WithClauseSql = source.WithClauseSql,
            LimitRows = source.LimitRows,
            FirstBranchParenthesized = source.FirstBranchParenthesized,
            CompoundLimitRows = source.CompoundLimitRows
""",
)
replace_once(
    "src/SqlQueryGenerator.Core/Query/QueryDefinitionCloner.cs",
    """        foreach (string key in source.DisabledAutoJoinKeys)
        {
            clone.DisabledAutoJoinKeys.Add(key);
        }

        return clone;
""",
    """        foreach (string key in source.DisabledAutoJoinKeys)
        {
            clone.DisabledAutoJoinKeys.Add(key);
        }

        foreach (OrderByItem orderBy in source.CompoundOrderBy)
        {
            clone.CompoundOrderBy.Add(new OrderByItem
            {
                Column = orderBy.Column is null ? null : Clone(orderBy.Column),
                FieldKind = orderBy.FieldKind,
                FieldAlias = orderBy.FieldAlias,
                Direction = orderBy.Direction
            });
        }

        foreach (SetOperationDefinition operation in source.SetOperations)
        {
            clone.SetOperations.Add(new SetOperationDefinition
            {
                Operator = operation.Operator,
                All = operation.All,
                ParenthesizeQuery = operation.ParenthesizeQuery,
                Query = Clone(operation.Query)
            });
        }

        return clone;
""",
)

# SQL generation.
replace_once(
    "src/SqlQueryGenerator.Core/Generation/SqlQueryGeneratorEngine.cs",
    """        options ??= new SqlGeneratorOptions();
        IReadOnlyDictionary<string, string> tableAliases = BuildTableAliasLookup(query);
""",
    """        options ??= new SqlGeneratorOptions();
        if (query.SetOperations.Count > 0)
        {
            return GenerateCompoundQuery(query, schema, options);
        }

        IReadOnlyDictionary<string, string> tableAliases = BuildTableAliasLookup(query);
""",
)

compound_generator_methods = r'''
    /// <summary>
    /// Generates a compound SELECT query containing UNION, INTERSECT, EXCEPT or MINUS branches.
    /// </summary>
    private SqlGenerationResult GenerateCompoundQuery(
        QueryDefinition query,
        DatabaseSchema schema,
        SqlGeneratorOptions options)
    {
        List<string> warnings = [];
        QueryDefinition firstBranch = CloneWithoutCompoundTail(query);
        string? withClause = firstBranch.WithClauseSql;
        firstBranch.WithClauseSql = null;

        SqlGenerationResult firstResult = Generate(firstBranch, schema, options);
        warnings.AddRange(firstResult.Warnings);

        StringBuilder sql = new();
        if (!string.IsNullOrWhiteSpace(withClause))
        {
            sql.AppendLine(withClause.Trim());
        }

        AppendCompoundBranch(sql, firstResult.Sql, query.FirstBranchParenthesized);
        int expectedProjectionCount = GetProjectionCount(firstBranch);

        foreach (SetOperationDefinition operation in query.SetOperations)
        {
            SqlGenerationResult branchResult = Generate(operation.Query, schema, options);
            warnings.AddRange(branchResult.Warnings);

            int projectionCount = GetProjectionCount(operation.Query);
            string operatorSql = SetOperatorSql(operation, options.Dialect, warnings);
            if (projectionCount != expectedProjectionCount)
            {
                warnings.Add(
                    $"La branche {operatorSql} expose {projectionCount} colonne(s), "
                    + $"contre {expectedProjectionCount} pour la première branche.");
            }

            sql.AppendLine();
            sql.AppendLine(operatorSql);
            AppendCompoundBranch(sql, branchResult.Sql, operation.ParenthesizeQuery);
        }

        AppendCompoundOrderBy(sql, query, options, warnings);
        AppendCompoundLimit(sql, query.CompoundLimitRows, options);

        return new SqlGenerationResult
        {
            Sql = sql.ToString().TrimEnd() + Environment.NewLine,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            JoinPlan = firstResult.JoinPlan
        };
    }

    private static QueryDefinition CloneWithoutCompoundTail(QueryDefinition source)
    {
        QueryDefinition clone = QueryDefinitionCloner.Clone(source);
        clone.SetOperations.Clear();
        clone.CompoundOrderBy.Clear();
        clone.CompoundLimitRows = null;
        clone.FirstBranchParenthesized = false;
        return clone;
    }

    private static void AppendCompoundBranch(StringBuilder target, string branchSql, bool parenthesized)
    {
        string normalized = branchSql.Trim();
        if (!parenthesized)
        {
            target.Append(normalized);
            return;
        }

        target.AppendLine("(");
        foreach (string line in normalized.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            target.Append("    ").AppendLine(line);
        }

        target.Append(')');
    }

    private static string SetOperatorSql(
        SetOperationDefinition operation,
        SqlDialect dialect,
        ICollection<string> warnings)
    {
        string keyword = operation.Operator switch
        {
            SetOperationKind.Union => "UNION",
            SetOperationKind.Intersect => "INTERSECT",
            SetOperationKind.Except when dialect == SqlDialect.Oracle => "MINUS",
            SetOperationKind.Except => "EXCEPT",
            SetOperationKind.Minus when dialect is SqlDialect.SQLite or SqlDialect.CognosAnalytics => "EXCEPT",
            SetOperationKind.Minus => "MINUS",
            _ => throw new ArgumentOutOfRangeException(nameof(operation.Operator))
        };

        if (operation.Operator == SetOperationKind.Except && dialect == SqlDialect.Oracle)
        {
            warnings.Add("EXCEPT a été rendu en MINUS pour le dialecte Oracle.");
        }
        else if (operation.Operator == SetOperationKind.Minus
                 && dialect is SqlDialect.SQLite or SqlDialect.CognosAnalytics)
        {
            warnings.Add("MINUS a été rendu en EXCEPT pour le dialecte de sortie sélectionné.");
        }

        return operation.All ? keyword + " ALL" : keyword;
    }

    private static void AppendCompoundOrderBy(
        StringBuilder sql,
        QueryDefinition query,
        SqlGeneratorOptions options,
        ICollection<string> warnings)
    {
        if (query.CompoundOrderBy.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, string> aliases = BuildTableAliasLookup(query);
        string[] items = query.CompoundOrderBy
            .Select(item =>
            {
                string expression = item.Column is not null
                    ? ColumnSql(item.Column, options, includeAlias: false, aliases)
                    : item.FieldAlias?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(expression))
                {
                    warnings.Add("Expression ORDER BY globale vide ignorée.");
                    return string.Empty;
                }

                SqlSafety.EnsureSelectExpressionIsSafe(expression);
                return $"{expression} {(item.Direction == SortDirection.Descending ? "DESC" : "ASC")}";
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        if (items.Length > 0)
        {
            sql.AppendLine();
            sql.Append("ORDER BY ").Append(string.Join(", ", items));
        }
    }

    private static void AppendCompoundLimit(
        StringBuilder sql,
        int? limitRows,
        SqlGeneratorOptions options)
    {
        if (limitRows is not > 0)
        {
            return;
        }

        sql.AppendLine();
        if (options.Dialect == SqlDialect.Oracle)
        {
            sql.Append($"FETCH FIRST {limitRows.Value} ROWS ONLY");
        }
        else
        {
            sql.Append($"LIMIT {limitRows.Value}");
        }
    }

    private static int GetProjectionCount(QueryDefinition query)
    {
        int count = query.SelectedColumns.Count + query.Aggregates.Count + query.CustomColumns.Count;
        return count == 0 ? 1 : count;
    }

'''
replace_once(
    "src/SqlQueryGenerator.Core/Generation/SqlQueryGeneratorEngine.cs",
    """    /// <summary>
    /// Exécute le traitement BuildJoinOnClause.
""",
    compound_generator_methods + """    /// <summary>
    /// Exécute le traitement BuildJoinOnClause.
""",
)

# Validation.
replace_once(
    "src/SqlQueryGenerator.Core/Validation/QueryValidator.cs",
    """        foreach (FilterCondition? subqueryFilter in query.Filters.Where(f => f.ValueKind == FilterValueKind.Subquery))
        {
""",
    """        int expectedProjectionCount = GetProjectionCount(query);
        int branchIndex = 2;
        foreach (SetOperationDefinition operation in query.SetOperations)
        {
            int branchProjectionCount = GetProjectionCount(operation.Query);
            if (branchProjectionCount != expectedProjectionCount)
            {
                errors.Add(
                    $"La branche SELECT {branchIndex} ({operation.Operator}) doit exposer {expectedProjectionCount} colonne(s), "
                    + $"mais elle en expose {branchProjectionCount}.");
            }

            foreach (string branchError in Validate(operation.Query, schema))
            {
                errors.Add($"Branche SELECT {branchIndex} ({operation.Operator}): {branchError}");
            }

            branchIndex++;
        }

        foreach (FilterCondition? subqueryFilter in query.Filters.Where(f => f.ValueKind == FilterValueKind.Subquery))
        {
""",
)
replace_once(
    "src/SqlQueryGenerator.Core/Validation/QueryValidator.cs",
    """    private static IEnumerable<ColumnReference> EnumerateColumns(QueryDefinition query)
    {
""",
    """    private static int GetProjectionCount(QueryDefinition query)
    {
        int count = query.SelectedColumns.Count + query.Aggregates.Count + query.CustomColumns.Count;
        return count == 0 ? 1 : count;
    }

    private static IEnumerable<ColumnReference> EnumerateColumns(QueryDefinition query)
    {
""",
)

# Rewrite all branches.
replace_once(
    "src/SqlQueryGenerator.Core/Generation/SqlRewriteSuggestionService.cs",
    """        if (LooksLikeImplicitJoinSql(sql) && rewritten.Joins.Count > 0)
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
""",
    """        QueryDefinition[] branches = EnumerateBranches(rewritten).ToArray();
        if (LooksLikeImplicitJoinSql(sql) && branches.Any(branch => branch.Joins.Count > 0))
        {
            transformations.Add("ImplicitJoinConverted");
        }

        int duplicateFilterCount = branches.Sum(RemoveDuplicateFilters);
        if (duplicateFilterCount > 0)
        {
            transformations.Add("DuplicatePredicateRemoved");
        }

        foreach (QueryDefinition branch in branches)
        {
            RemoveDuplicateSelectedColumns(branch);
            RemoveDuplicateGroupBy(branch);
            RemoveDuplicateOrderBy(branch);
        }

        transformations.Add("FormattingImproved");
""",
)
replace_once(
    "src/SqlQueryGenerator.Core/Generation/SqlRewriteSuggestionService.cs",
    """    private static bool LooksLikeImplicitJoinSql(string sql)
    {
""",
    """    private static IEnumerable<QueryDefinition> EnumerateBranches(QueryDefinition query)
    {
        yield return query;
        foreach (SetOperationDefinition operation in query.SetOperations)
        {
            foreach (QueryDefinition branch in EnumerateBranches(operation.Query))
            {
                yield return branch;
            }
        }
    }

    private static bool LooksLikeImplicitJoinSql(string sql)
    {
""",
)

print("Core compound-query patch applied.")
