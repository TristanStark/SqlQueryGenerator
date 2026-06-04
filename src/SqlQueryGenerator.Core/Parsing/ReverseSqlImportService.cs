using SqlQueryGenerator.Core.Generation;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Imports raw SQL into the existing query model while surfacing conservative warnings.
/// </summary>
public sealed class ReverseSqlImportService
{
    private readonly SqlSelectReverseParser _parser = new();

    /// <summary>
    /// Parses one read-only SELECT statement and returns the reconstructed query model.
    /// </summary>
    /// <param name="sql">Raw SQL to import.</param>
    /// <returns>Imported query plus warnings.</returns>
    public ReverseSqlImportResult Import(string sql)
    {
        string normalized = SqlSafety.NormalizeRawSelectQuery(sql);
        List<string> warnings = BuildWarnings(normalized);

        return new ReverseSqlImportResult
        {
            Query = _parser.Parse(normalized),
            Warnings = warnings
        };
    }

    private static List<string> BuildWarnings(string sql)
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

        if (Regex.IsMatch(sql, @"(?is)\b(UNION|INTERSECT|MINUS|CONNECT\s+BY|START\s+WITH|MODEL)\b"))
        {
            warnings.Add("Le SQL contient des constructions avancees ou specifiques a un moteur. Verifie le resultat importe avant edition.");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
