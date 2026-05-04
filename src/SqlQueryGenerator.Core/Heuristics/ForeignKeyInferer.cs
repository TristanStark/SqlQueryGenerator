using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.Core.Heuristics;

public sealed class ForeignKeyInferer
{
    public IReadOnlyList<InferredRelationship> Infer(DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var relationships = new List<InferredRelationship>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(InferredRelationship relationship)
        {
            if (relationship.FromTable.Equals(relationship.ToTable, StringComparison.OrdinalIgnoreCase)
                && relationship.FromColumn.Equals(relationship.ToColumn, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var key = $"{relationship.FromTable}.{relationship.FromColumn}->{relationship.ToTable}.{relationship.ToColumn}";
            if (seen.Add(key))
            {
                relationships.Add(relationship);
                return;
            }

            var existingIndex = relationships.FindIndex(existing =>
                existing.FromTable.Equals(relationship.FromTable, StringComparison.OrdinalIgnoreCase)
                && existing.FromColumn.Equals(relationship.FromColumn, StringComparison.OrdinalIgnoreCase)
                && existing.ToTable.Equals(relationship.ToTable, StringComparison.OrdinalIgnoreCase)
                && existing.ToColumn.Equals(relationship.ToColumn, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0 && relationship.Confidence > relationships[existingIndex].Confidence)
            {
                relationships[existingIndex] = relationship;
            }
        }

        foreach (var fk in schema.DeclaredForeignKeys)
        {
            Add(new InferredRelationship
            {
                FromTable = fk.FromTable,
                FromColumn = fk.FromColumn,
                ToTable = fk.ToTable,
                ToColumn = fk.ToColumn,
                Confidence = 1.0,
                Source = RelationshipSource.DeclaredForeignKey,
                Reason = string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? "Clé étrangère déclarée dans le schéma."
                    : $"Clé étrangère déclarée: {fk.ConstraintName}."
            });
        }

        foreach (var sourceTable in schema.Tables)
        {
            foreach (var sourceColumn in sourceTable.Columns)
            {
                var sourceName = SqlNameNormalizer.Normalize(sourceColumn.Name);

                foreach (var targetTable in schema.Tables)
                {
                    if (ReferenceEquals(sourceTable, targetTable))
                    {
                        continue;
                    }

                    foreach (var targetColumn in targetTable.Columns)
                    {
                        var targetName = SqlNameNormalizer.Normalize(targetColumn.Name);

                        // Do not infer pnj.id = jobs.id. A bare ID/IDEN/IDENT is almost always
                        // the local primary key of each table, not a relationship between tables.
                        // Real FK-like same-name columns such as ORD_IDEN are still accepted below.
                        if (sourceName == targetName && !IsGenericIdentifier(sourceName))
                        {
                            if (targetColumn.IsPrimaryKey && !sourceColumn.IsPrimaryKey)
                            {
                                Add(new InferredRelationship
                                {
                                    FromTable = sourceTable.FullName,
                                    FromColumn = sourceColumn.Name,
                                    ToTable = targetTable.FullName,
                                    ToColumn = targetColumn.Name,
                                    Confidence = 0.86,
                                    Source = RelationshipSource.SameColumnPrimaryKey,
                                    Reason = $"Même nom de colonne spécifique ({sourceColumn.Name}) et colonne cible marquée comme PK."
                                });
                            }
                            else if (!sourceColumn.IsPrimaryKey && !targetColumn.IsPrimaryKey && LooksLikeIdentifier(sourceColumn.Name) && LooksLikeIdentifier(targetColumn.Name))
                            {
                                Add(new InferredRelationship
                                {
                                    FromTable = sourceTable.FullName,
                                    FromColumn = sourceColumn.Name,
                                    ToTable = targetTable.FullName,
                                    ToColumn = targetColumn.Name,
                                    Confidence = 0.52,
                                    Source = RelationshipSource.SameColumnName,
                                    Reason = $"Même nom de colonne identifiant spécifique ({sourceColumn.Name}) dans deux tables non-PK. À valider si plusieurs relations candidates existent."
                                });
                            }
                        }

                        var tablePatternScore = TableColumnPatternScore(sourceColumn.Name, targetTable.Name, targetColumn.Name, targetColumn.IsPrimaryKey);
                        if (tablePatternScore > 0)
                        {
                            Add(new InferredRelationship
                            {
                                FromTable = sourceTable.FullName,
                                FromColumn = sourceColumn.Name,
                                ToTable = targetTable.FullName,
                                ToColumn = targetColumn.Name,
                                Confidence = tablePatternScore,
                                Source = RelationshipSource.TableNameColumnPattern,
                                Reason = $"La colonne {sourceColumn.Name} ressemble à une référence vers {targetTable.Name}.{targetColumn.Name}."
                            });
                        }

                        var compositeTableScore = CompositeTablePatternScore(sourceTable.Name, sourceColumn.Name, targetTable.Name, targetColumn.Name, targetColumn.IsPrimaryKey);
                        if (compositeTableScore > 0)
                        {
                            Add(new InferredRelationship
                            {
                                FromTable = sourceTable.FullName,
                                FromColumn = sourceColumn.Name,
                                ToTable = targetTable.FullName,
                                ToColumn = targetColumn.Name,
                                Confidence = compositeTableScore,
                                Source = RelationshipSource.CompositeTablePattern,
                                Reason = $"La table {targetTable.Name} ressemble à une table spécialisée/de liaison pour {sourceTable.Name} et {sourceColumn.Name}."
                            });
                        }

                        var commentScore = CommentSimilarity(sourceColumn.Comment, targetTable.Name, targetColumn.Name);
                        if (commentScore >= 0.70 && LooksLikeIdentifier(sourceColumn.Name))
                        {
                            Add(new InferredRelationship
                            {
                                FromTable = sourceTable.FullName,
                                FromColumn = sourceColumn.Name,
                                ToTable = targetTable.FullName,
                                ToColumn = targetColumn.Name,
                                Confidence = Math.Min(0.80, commentScore),
                                Source = RelationshipSource.CommentSimilarity,
                                Reason = $"Le commentaire de {sourceTable.Name}.{sourceColumn.Name} mentionne probablement {targetTable.Name}."
                            });
                        }
                    }
                }
            }
        }

        return relationships
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.FromColumn, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool LooksLikeIdentifier(string columnName)
    {
        var n = SqlNameNormalizer.Normalize(columnName);
        return IsGenericIdentifier(n)
            || n.EndsWith("_ID", StringComparison.Ordinal)
            || n.EndsWith("_IDEN", StringComparison.Ordinal)
            || n.EndsWith("_IDENT", StringComparison.Ordinal)
            || n.EndsWith("_CODE", StringComparison.Ordinal)
            || n.StartsWith("ID_", StringComparison.Ordinal)
            || n.StartsWith("IDEN_", StringComparison.Ordinal)
            || n.StartsWith("FK_", StringComparison.Ordinal);
    }

    private static bool IsGenericIdentifier(string normalizedColumnName)
    {
        return normalizedColumnName is "ID" or "IDEN" or "IDENT";
    }

    private static bool IsReferenceTargetColumn(string normalizedTargetColumn, string normalizedTargetTable, bool targetIsPk)
    {
        if (targetIsPk)
        {
            return true;
        }

        if (IsGenericIdentifier(normalizedTargetColumn))
        {
            return true;
        }

        foreach (var variant in GetTableNameVariants(normalizedTargetTable))
        {
            var v = variant.Name;
            if (normalizedTargetColumn == $"{v}_ID"
                || normalizedTargetColumn == $"{v}_IDEN"
                || normalizedTargetColumn == $"{v}_IDENT"
                || normalizedTargetColumn == $"{v}_CODE")
            {
                return true;
            }
        }

        return false;
    }

    private static double TableColumnPatternScore(string sourceColumn, string targetTable, string targetColumn, bool targetIsPk)
    {
        var src = SqlNameNormalizer.Normalize(sourceColumn);
        var target = SqlNameNormalizer.Normalize(targetColumn);
        var table = SqlNameNormalizer.Normalize(targetTable);

        if (!LooksLikeIdentifier(src) || !IsReferenceTargetColumn(target, table, targetIsPk))
        {
            return 0.0;
        }

        foreach (var variant in GetTableNameVariants(table).OrderByDescending(v => v.Weight))
        {
            var name = variant.Name;
            var compactName = name.Replace("_", string.Empty, StringComparison.Ordinal);
            var compactSrc = src.Replace("_", string.Empty, StringComparison.Ordinal);

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $"{name}_ID",
                $"{name}_IDEN",
                $"{name}_IDENT",
                $"{name}_CODE",
                $"ID_{name}",
                $"IDEN_{name}",
                $"FK_{name}",
                $"FK_{name}_ID",
                $"{compactName}ID",
                $"{compactName}IDEN",
                $"{compactName}IDENT",
                $"{compactName}CODE"
            };

            if (!candidates.Contains(src) && !candidates.Contains(compactSrc))
            {
                continue;
            }

            // Specific source-column patterns such as PNJ.JOB_ID -> JOBS.ID must outrank
            // weak same-name relations. Plural/singular exact table variants get the highest
            // score; suffix variants such as GROUP_ID -> JOBS_GROUPS.ID are a little weaker.
            var baseScore = targetIsPk ? 0.97 : 0.92;
            return Math.Min(0.99, baseScore * variant.Weight);
        }

        // Case for schemas where the referenced PK is also named JOB_ID instead of ID.
        if (src == target && !IsGenericIdentifier(src))
        {
            return targetIsPk ? 0.84 : 0.60;
        }

        return 0.0;
    }

    private static IEnumerable<(string Name, double Weight)> GetTableNameVariants(string normalizedTableName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(List<(string Name, double Weight)> list, string? value, double weight)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = SqlNameNormalizer.Normalize(value).Trim('_');
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                return;
            }

            list.Add((normalized, weight));
        }

        var result = new List<(string Name, double Weight)>();
        var table = SqlNameNormalizer.Normalize(normalizedTableName);
        Add(result, table, 1.00);
        Add(result, Singularize(table), 0.99);

        foreach (var withoutPrefix in RemoveCommonTablePrefixes(table))
        {
            Add(result, withoutPrefix, 0.96);
            Add(result, Singularize(withoutPrefix), 0.95);
        }

        var tokens = table.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1)
        {
            var withoutFirst = string.Join('_', tokens.Skip(1));
            Add(result, withoutFirst, 0.91);
            Add(result, Singularize(withoutFirst), 0.90);

            var last = tokens[^1];
            Add(result, last, 0.88);
            Add(result, Singularize(last), 0.87);
        }

        return result;
    }

    private static IEnumerable<string> RemoveCommonTablePrefixes(string normalizedTableName)
    {
        var prefixes = new[] { "T_", "TB_", "TBL_", "REF_", "DIM_", "D_", "R_" };
        foreach (var prefix in prefixes)
        {
            if (normalizedTableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && normalizedTableName.Length > prefix.Length)
            {
                yield return normalizedTableName[prefix.Length..];
            }
        }
    }

    private static string Singularize(string normalizedName)
    {
        var parts = normalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SingularizeToken);
        return string.Join('_', parts);
    }

    private static string SingularizeToken(string token)
    {
        if (token.Length <= 3)
        {
            return token;
        }

        if (token.EndsWith("IES", StringComparison.Ordinal) && token.Length > 4)
        {
            return token[..^3] + "Y";
        }

        if ((token.EndsWith("S", StringComparison.Ordinal) || token.EndsWith("X", StringComparison.Ordinal)) && token.Length > 3)
        {
            return token[..^1];
        }

        return token;
    }

    private static double CompositeTablePatternScore(string sourceTable, string sourceColumn, string targetTable, string targetColumn, bool targetIsPk)
    {
        var sourceColumnStem = ExtractIdentifierStem(sourceColumn);
        if (string.IsNullOrWhiteSpace(sourceColumnStem) || IsGenericIdentifier(sourceColumnStem))
        {
            return 0.0;
        }

        var normalizedTargetTable = SqlNameNormalizer.Normalize(targetTable);
        var normalizedTargetColumn = SqlNameNormalizer.Normalize(targetColumn);
        if (!IsReferenceTargetColumn(normalizedTargetColumn, normalizedTargetTable, targetIsPk))
        {
            return 0.0;
        }

        var sourceTableVariants = GetTableNameVariants(SqlNameNormalizer.Normalize(sourceTable)).Select(v => v.Name).ToArray();
        var stemVariants = GetTableNameVariants(sourceColumnStem).Select(v => v.Name).Append(sourceColumnStem).Append(Singularize(sourceColumnStem)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var targetTokens = normalizedTargetTable.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var singularTargetTokens = targetTokens.Select(SingularizeToken).ToArray();

        foreach (var sourceVariant in sourceTableVariants)
        {
            if (string.IsNullOrWhiteSpace(sourceVariant))
            {
                continue;
            }

            var startsWithSource = normalizedTargetTable.Equals(sourceVariant, StringComparison.OrdinalIgnoreCase)
                || normalizedTargetTable.StartsWith(sourceVariant + "_", StringComparison.OrdinalIgnoreCase);
            var containsSourceToken = targetTokens.Any(t => SameNameRelaxed(t, sourceVariant))
                || singularTargetTokens.Any(t => SameNameRelaxed(t, sourceVariant));

            if (!startsWithSource && !containsSourceToken)
            {
                continue;
            }

            foreach (var stemVariant in stemVariants)
            {
                if (string.IsNullOrWhiteSpace(stemVariant))
                {
                    continue;
                }

                var containsStemToken = targetTokens.Any(t => SameNameRelaxed(t, stemVariant))
                    || singularTargetTokens.Any(t => SameNameRelaxed(t, stemVariant));
                var containsStemTail = normalizedTargetTable.EndsWith("_" + stemVariant, StringComparison.OrdinalIgnoreCase)
                    || Singularize(normalizedTargetTable).EndsWith("_" + Singularize(stemVariant), StringComparison.OrdinalIgnoreCase);

                if (containsStemToken || containsStemTail)
                {
                    // Example: PNJ.JOB_ID -> PNJ_JOBS.ID.
                    // The target table starts with/contains the source table name and also contains
                    // the referenced concept, with singular/plural normalization.
                    var baseScore = targetIsPk ? 0.985 : 0.955;
                    return startsWithSource ? baseScore : baseScore - 0.03;
                }
            }
        }

        return 0.0;
    }

    private static string ExtractIdentifierStem(string columnName)
    {
        var normalized = SqlNameNormalizer.Normalize(columnName).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var prefixes = new[] { "FK_", "ID_", "IDEN_", "IDENT_" };
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && normalized.Length > prefix.Length)
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        var suffixes = new[] { "_IDEN", "_IDENT", "_ID", "_CODE" };
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && normalized.Length > suffix.Length)
            {
                normalized = normalized[..^suffix.Length];
                break;
            }
        }

        return Singularize(normalized.Trim('_'));
    }

    private static bool SameNameRelaxed(string left, string right)
    {
        var a = Singularize(SqlNameNormalizer.Normalize(left));
        var b = Singularize(SqlNameNormalizer.Normalize(right));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    private static double CommentSimilarity(string? sourceComment, string targetTable, string targetColumn)
    {
        if (string.IsNullOrWhiteSpace(sourceComment))
        {
            return 0.0;
        }

        var comment = SqlNameNormalizer.Normalize(sourceComment);
        var table = SqlNameNormalizer.Normalize(targetTable);
        var column = SqlNameNormalizer.Normalize(targetColumn);
        var strippedTable = SqlNameNormalizer.StripDecorations(targetTable);
        var score = 0.0;

        if (comment.Contains(table, StringComparison.OrdinalIgnoreCase)) score += 0.55;
        if (!string.IsNullOrWhiteSpace(strippedTable) && comment.Contains(strippedTable, StringComparison.OrdinalIgnoreCase)) score += 0.45;
        if (comment.Contains(column, StringComparison.OrdinalIgnoreCase)) score += 0.20;
        if (comment.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) || comment.Contains("REF", StringComparison.OrdinalIgnoreCase)) score += 0.15;
        if (comment.Contains("IDENTIFIANT", StringComparison.OrdinalIgnoreCase) || comment.Contains("ID", StringComparison.OrdinalIgnoreCase)) score += 0.10;

        return Math.Min(score, 1.0);
    }
}
