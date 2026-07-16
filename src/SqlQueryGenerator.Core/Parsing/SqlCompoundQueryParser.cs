using SqlQueryGenerator.Core.Generation;
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
