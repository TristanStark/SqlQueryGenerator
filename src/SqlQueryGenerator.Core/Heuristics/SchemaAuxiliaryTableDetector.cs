using SqlQueryGenerator.Core.Models;
using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Detects auxiliary physical tables that are likely backups, history snapshots, staging copies, or temp imports.
/// </summary>
public sealed partial class SchemaAuxiliaryTableDetector
{
    private static readonly char[] CandidateSeparators = ['_', '-', '$'];

    private static readonly HashSet<string> SuspiciousTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bak",
        "bkp",
        "backup",
        "back",
        "save",
        "saved",
        "sauvegarde",
        "old",
        "hist",
        "history",
        "historique",
        "archive",
        "arch",
        "copy",
        "copie",
        "tmp",
        "temp",
        "staging",
        "stage",
        "audit",
        "log"
    };

    /// <summary>
    /// Returns whether the table name looks like an auxiliary imported object.
    /// </summary>
    /// <param name="tableName">Full or short table name.</param>
    /// <returns><c>true</c> when the name strongly suggests a backup/history/temp table.</returns>
    public bool IsLikelyAuxiliaryTable(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        string shortName = GetShortTableName(tableName);
        string normalized = SqlNameNormalizer.Normalize(shortName).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (AuxiliaryPrefixSuffixRegex().IsMatch(normalized))
        {
            return true;
        }

        string[] tokens = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token => SuspiciousTokens.Contains(token));
    }

    /// <summary>
    /// Detects conservative backup/archive/save tables that should be reviewed before import.
    /// </summary>
    /// <param name="schema">Parsed DDL schema.</param>
    /// <returns>Detected candidates ordered by table name.</returns>
    public IReadOnlyList<BackupTableCandidate> DetectBackupCandidates(DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        TableDefinition[] physicalTables = schema.PhysicalTables
            .OrderByDescending(table => GetShortTableName(table.FullName).Length)
            .ThenBy(table => table.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<BackupTableCandidate> candidates = [];
        foreach (TableDefinition candidate in physicalTables)
        {
            BackupTableCandidate? detected = BuildBackupCandidate(candidate, physicalTables);
            if (detected is not null)
            {
                candidates.Add(detected);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.TableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Applies the reviewed exclusion list to the parsed schema and safely drops metadata tied only to excluded tables.
    /// </summary>
    /// <param name="schema">Parsed schema before review filtering.</param>
    /// <param name="excludedTables">Reviewed table names to exclude from the final import.</param>
    /// <returns>Filtered schema plus detected/excluded candidate metadata.</returns>
    public SchemaImportFilterResult ApplyImportSelection(DatabaseSchema schema, IEnumerable<string>? excludedTables)
    {
        ArgumentNullException.ThrowIfNull(schema);

        BackupTableCandidate[] detectedCandidates = DetectBackupCandidates(schema).ToArray();
        HashSet<string> selectedExclusions = BuildNormalizedNameSet(excludedTables);
        BackupTableCandidate[] excludedCandidates = detectedCandidates
            .Where(candidate => selectedExclusions.Contains(SqlNameNormalizer.Normalize(candidate.TableName))
                || selectedExclusions.Contains(SqlNameNormalizer.Normalize(GetShortTableName(candidate.TableName))))
            .ToArray();
        HashSet<string> excludedCandidateNames = excludedCandidates
            .Select(candidate => candidate.TableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        BackupTableCandidate[] keptCandidates = detectedCandidates
            .Where(candidate => !excludedCandidateNames.Contains(candidate.TableName))
            .ToArray();

        DatabaseSchema filteredSchema = CloneFilteredSchema(schema, excludedCandidates);
        return new SchemaImportFilterResult
        {
            Schema = filteredSchema,
            DetectedCandidates = detectedCandidates,
            ExcludedCandidates = excludedCandidates,
            KeptCandidates = keptCandidates
        };
    }

    private static string GetShortTableName(string tableName)
    {
        int index = tableName.LastIndexOf('.');
        return index >= 0 ? tableName[(index + 1)..] : tableName;
    }

    private static BackupTableCandidate? BuildBackupCandidate(TableDefinition candidate, IReadOnlyList<TableDefinition> physicalTables)
    {
        foreach (TableDefinition baseTable in physicalTables)
        {
            if (ReferenceEquals(baseTable, candidate))
            {
                continue;
            }

            if (TryBuildDetectionReason(candidate.FullName, baseTable.FullName, out string reason))
            {
                return new BackupTableCandidate
                {
                    TableName = candidate.FullName,
                    BaseTableName = baseTable.FullName,
                    DetectionReason = reason
                };
            }
        }

        return null;
    }

    private static bool TryBuildDetectionReason(string candidateTableName, string baseTableName, out string reason)
    {
        string candidateShortName = NormalizeReviewName(GetShortTableName(candidateTableName));
        string baseShortName = NormalizeReviewName(GetShortTableName(baseTableName));
        if (string.IsNullOrWhiteSpace(candidateShortName)
            || string.IsNullOrWhiteSpace(baseShortName)
            || candidateShortName.Length <= baseShortName.Length
            || !candidateShortName.StartsWith(baseShortName, StringComparison.OrdinalIgnoreCase))
        {
            reason = string.Empty;
            return false;
        }

        string suffix = candidateShortName[baseShortName.Length..];
        if (suffix.Length < 2 || !CandidateSeparators.Contains(suffix[0]))
        {
            reason = string.Empty;
            return false;
        }

        string detail = suffix[1..].Trim(CandidateSeparators);
        if (string.IsNullOrWhiteSpace(detail) || !TrailingDigitsRegex().IsMatch(detail))
        {
            reason = string.Empty;
            return false;
        }

        string[] tokens = detail.Split(CandidateSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? suspiciousToken = tokens.FirstOrDefault(token => SuspiciousTokens.Contains(token));
        string baseLabel = GetShortTableName(baseTableName);
        reason = suspiciousToken is null
            ? $"commence par {baseLabel}_ et se termine par des chiffres"
            : $"mot-clé {suspiciousToken.ToUpperInvariant()} + suffixe numérique après la table de base {baseLabel}";
        return true;
    }

    private static DatabaseSchema CloneFilteredSchema(DatabaseSchema sourceSchema, IReadOnlyList<BackupTableCandidate> excludedCandidates)
    {
        HashSet<string> excludedNames = excludedCandidates
            .SelectMany(candidate => new[] { candidate.TableName, GetShortTableName(candidate.TableName) })
            .Select(SqlNameNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DatabaseSchema filteredSchema = new();
        HashSet<string> keptNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (TableDefinition table in sourceSchema.Tables)
        {
            if (!table.IsView && IsExcludedTable(table, excludedNames))
            {
                continue;
            }

            TableDefinition clone = new(table.Name, table.SchemaName, table.Comment, table.IsView, table.ViewSql);
            foreach (ColumnDefinition column in table.Columns)
            {
                clone.Columns.Add(column with { });
            }

            filteredSchema.Tables.Add(clone);
            keptNames.Add(SqlNameNormalizer.Normalize(clone.FullName));
            keptNames.Add(SqlNameNormalizer.Normalize(clone.Name));
        }

        foreach (DeclaredForeignKey declaredForeignKey in sourceSchema.DeclaredForeignKeys)
        {
            if (keptNames.Contains(SqlNameNormalizer.Normalize(declaredForeignKey.FromTable))
                && keptNames.Contains(SqlNameNormalizer.Normalize(declaredForeignKey.ToTable)))
            {
                filteredSchema.DeclaredForeignKeys.Add(declaredForeignKey with { });
            }
        }

        foreach (IndexDefinition index in sourceSchema.Indexes)
        {
            if (keptNames.Contains(SqlNameNormalizer.Normalize(index.Table)))
            {
                filteredSchema.Indexes.Add(new IndexDefinition(index.Name, index.Table, index.IsUnique, index.Columns));
            }
        }

        foreach (InferredRelationship relationship in sourceSchema.Relationships)
        {
            if (keptNames.Contains(SqlNameNormalizer.Normalize(relationship.FromTable))
                && keptNames.Contains(SqlNameNormalizer.Normalize(relationship.ToTable)))
            {
                filteredSchema.Relationships.Add(relationship with { });
            }
        }

        foreach (string warning in sourceSchema.Warnings)
        {
            filteredSchema.Warnings.Add(warning);
        }

        AddViewWarnings(filteredSchema, excludedCandidates);
        return filteredSchema;
    }

    private static bool IsExcludedTable(TableDefinition table, IReadOnlySet<string> excludedNames)
    {
        return excludedNames.Contains(SqlNameNormalizer.Normalize(table.FullName))
            || excludedNames.Contains(SqlNameNormalizer.Normalize(table.Name));
    }

    private static void AddViewWarnings(DatabaseSchema schema, IReadOnlyList<BackupTableCandidate> excludedCandidates)
    {
        if (excludedCandidates.Count == 0)
        {
            return;
        }

        HashSet<string> emittedWarnings = new(StringComparer.OrdinalIgnoreCase);
        foreach (TableDefinition view in schema.Views)
        {
            if (string.IsNullOrWhiteSpace(view.ViewSql))
            {
                continue;
            }

            foreach (BackupTableCandidate candidate in excludedCandidates)
            {
                if (!ViewSqlReferencesTable(view.ViewSql, candidate.TableName))
                {
                    continue;
                }

                string warning = $"La vue {view.FullName} référence potentiellement la table exclue {candidate.TableName}. Vérifie la requête importée.";
                if (emittedWarnings.Add(warning))
                {
                    schema.Warnings.Add(warning);
                }
            }
        }
    }

    private static bool ViewSqlReferencesTable(string viewSql, string tableName)
    {
        return ContainsIdentifier(viewSql, tableName)
            || ContainsIdentifier(viewSql, GetShortTableName(tableName));
    }

    private static bool ContainsIdentifier(string sql, string identifier)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        string pattern = $@"(?<![A-Z0-9_]){Regex.Escape(identifier)}(?![A-Z0-9_])";
        return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static HashSet<string> BuildNormalizedNameSet(IEnumerable<string>? names)
    {
        return names?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(SqlNameNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeReviewName(string tableName)
    {
        return SqlNameNormalizer.Normalize(tableName).Trim(CandidateSeparators);
    }

    [GeneratedRegex(@"^(TMP|TEMP|BAK|BKP|ZZ)_|_(BAK|BKP|BACKUP|OLD|HIST|HISTORY|HISTORIQUE|ARCH|ARCHIVE|TMP|TEMP|STAGING|STAGE|AUDIT|LOG)(_|$)")]
    private static partial Regex AuxiliaryPrefixSuffixRegex();

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingDigitsRegex();
}
