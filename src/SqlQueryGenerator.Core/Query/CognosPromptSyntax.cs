using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Helpers for Cognos Analytics prompt macros preserved during reverse import and SQL generation.
/// </summary>
public static partial class CognosPromptSyntax
{
    /// <summary>
    /// Builds a canonical Cognos prompt macro.
    /// </summary>
    /// <param name="name">Prompt name.</param>
    /// <param name="declaredType">Prompt type such as string, integer or date.</param>
    /// <returns>Prompt macro string.</returns>
    public static string BuildPromptExpression(string name, string? declaredType)
    {
        string safeName = EscapePromptValue(string.IsNullOrWhiteSpace(name) ? "param" : name.Trim());
        string safeType = EscapePromptValue(NormalizePromptType(declaredType));
        return $"#prompt(\"{safeName}\", \"{safeType}\")#";
    }

    /// <summary>
    /// Builds the Cognos date prompt wrapped in TO_DATE.
    /// </summary>
    /// <param name="name">Prompt name.</param>
    /// <returns>Date prompt SQL fragment.</returns>
    public static string BuildDatePromptExpression(string name)
    {
        return BuildDatePromptExpressionFromPrompt(BuildPromptExpression(name, "date"));
    }

    /// <summary>
    /// Wraps one bare Cognos prompt macro in TO_DATE.
    /// </summary>
    /// <param name="promptExpression">Bare prompt macro.</param>
    /// <returns>Date prompt SQL fragment.</returns>
    public static string BuildDatePromptExpressionFromPrompt(string promptExpression)
    {
        return $"TO_DATE({promptExpression}, 'dd/MM/YYYY')";
    }

    /// <summary>
    /// Detects whether a fragment is a bare Cognos prompt macro.
    /// </summary>
    /// <param name="value">Raw SQL fragment.</param>
    /// <returns><c>true</c> when the fragment matches a Cognos prompt.</returns>
    public static bool IsPromptExpression(string? value)
    {
        return TryParsePromptExpression(value, out _, out _);
    }

    /// <summary>
    /// Parses a bare Cognos prompt macro.
    /// </summary>
    /// <param name="value">Raw SQL fragment.</param>
    /// <param name="name">Parsed prompt name.</param>
    /// <param name="declaredType">Parsed prompt type.</param>
    /// <returns><c>true</c> when parsing succeeded.</returns>
    public static bool TryParsePromptExpression(string? value, out string name, out string declaredType)
    {
        name = string.Empty;
        declaredType = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Match match = PromptRegex().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        name = match.Groups["name"].Value.Trim();
        declaredType = NormalizePromptType(match.Groups["type"].Value);
        return true;
    }

    /// <summary>
    /// Detects either a bare Cognos prompt or its TO_DATE-wrapped variant and returns the bare prompt macro.
    /// </summary>
    /// <param name="value">Raw SQL fragment.</param>
    /// <param name="promptExpression">Canonical bare prompt macro.</param>
    /// <param name="wrappedAsDate">Whether the original SQL wrapped it in TO_DATE.</param>
    /// <returns><c>true</c> when a Cognos prompt was found.</returns>
    public static bool TryExtractPromptExpression(string? value, out string promptExpression, out bool wrappedAsDate)
    {
        promptExpression = string.Empty;
        wrappedAsDate = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (TryParsePromptExpression(trimmed, out string name, out string declaredType))
        {
            promptExpression = BuildPromptExpression(name, declaredType);
            return true;
        }

        Match wrapped = DateWrappedPromptRegex().Match(trimmed);
        if (!wrapped.Success)
        {
            return false;
        }

        string promptCandidate = wrapped.Groups["prompt"].Value.Trim();
        if (TryParsePromptExpression(promptCandidate, out name, out declaredType))
        {
            promptExpression = BuildPromptExpression(name, declaredType);
            wrappedAsDate = true;
            return true;
        }

        if (!promptCandidate.EndsWith("#", StringComparison.Ordinal)
            && TryParsePromptExpression(promptCandidate + "#", out name, out declaredType))
        {
            promptExpression = BuildPromptExpression(name, declaredType);
            wrappedAsDate = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a Cognos prompt type with a conservative default.
    /// </summary>
    /// <param name="declaredType">Raw declared type.</param>
    /// <returns>Normalized type string.</returns>
    public static string NormalizePromptType(string? declaredType)
    {
        return string.IsNullOrWhiteSpace(declaredType) ? "string" : declaredType.Trim();
    }

    private static string EscapePromptValue(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^#prompt\s*\(\s*(?<quote>['""])(?<name>.*?)\k<quote>\s*,\s*(?<typeQuote>['""])(?<type>.*?)\k<typeQuote>\s*\)\s*#?$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PromptRegex();

    [GeneratedRegex(@"^TO_DATE\s*\(\s*(?<prompt>#prompt\s*\(.+?\)\s*#?)\s*,\s*(?<formatQuote>['""])(?<format>dd/MM/YYYY)\k<formatQuote>\s*\)\s*#?$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DateWrappedPromptRegex();
}
