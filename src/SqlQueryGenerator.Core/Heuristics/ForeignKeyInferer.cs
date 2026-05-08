using SqlQueryGenerator.Core.Models;
using System.Collections.Concurrent;

namespace SqlQueryGenerator.Core.Heuristics;

/// <summary>
/// Représente ForeignKeyInferer dans SQL Query Generator.
/// </summary>
public sealed class ForeignKeyInferer
{
    /// <summary>
    /// Stocke la valeur interne SameColumnWeakGroupLimit.
    /// </summary>
    /// <value>Valeur de SameColumnWeakGroupLimit.</value>
    private const int SameColumnWeakGroupLimit = 48;
    /// <summary>
    /// Stocke la valeur interne ParallelInferenceThreshold.
    /// </summary>
    /// <value>Valeur de ParallelInferenceThreshold.</value>
    private const int ParallelInferenceThreshold = 512;

    /// <summary>
    /// Exécute le traitement Infer.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <returns>Résultat du traitement.</returns>
    public IReadOnlyList<InferredRelationship> Infer(DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        InferenceContext context = InferenceContext.Build(schema);
        List<InferredRelationship> relationships = [];
        Dictionary<string, int> relationshipIndexByKey = new(StringComparer.OrdinalIgnoreCase);
        ParallelOptions parallelOptions = CreateParallelOptions(context);

        void Add(InferredRelationship relationship)
        {
            if (relationship.FromTable.Equals(relationship.ToTable, StringComparison.OrdinalIgnoreCase)
                && relationship.FromColumn.Equals(relationship.ToColumn, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            InferredRelationship adjusted = ApplyIndexBias(context, relationship);
            string key = RelationshipKey.For(adjusted.FromTable, adjusted.FromColumn, adjusted.ToTable, adjusted.ToColumn);
            if (!relationshipIndexByKey.TryGetValue(key, out int existingIndex))
            {
                relationshipIndexByKey[key] = relationships.Count;
                relationships.Add(adjusted);
                return;
            }

            InferredRelationship existing = relationships[existingIndex];
            if (adjusted.Confidence > existing.Confidence || (Math.Abs(adjusted.Confidence - existing.Confidence) < 0.0001 && IsDeterministicallyPreferred(adjusted, existing)))
            {
                relationships[existingIndex] = adjusted;
            }
        }

        // Declared FKs are cheap and already authoritative: keep them serial and deterministic.
        AddDeclaredForeignKeys(schema, Add);

        // Heuristic discovery is CPU-heavy on large schemas. Generate candidates concurrently,
        // then merge serially so de-duplication and final ordering remain deterministic.
        ConcurrentBag<InferredRelationship> candidates = [];
        AddSameColumnRelationships(context, candidates.Add, parallelOptions);
        AddTableNameColumnPatternRelationships(context, candidates.Add, parallelOptions);
        AddCompositeTablePatternRelationships(context, candidates.Add, parallelOptions);
        AddCommentRelationships(context, candidates.Add, parallelOptions);

        foreach (InferredRelationship? candidate in candidates
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.FromColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ToTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ToColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Source.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            Add(candidate);
        }

        return relationships
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.FromColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ToTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ToColumn, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Exécute le traitement CreateParallelOptions.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <returns>Résultat du traitement.</returns>
    private static ParallelOptions CreateParallelOptions(InferenceContext context)
    {
        int estimatedWorkItems = context.IdentifierColumns.Count + context.ColumnsByNormalizedName.Count;
        int degree = estimatedWorkItems < ParallelInferenceThreshold
            ? 1
            : Math.Max(1, Environment.ProcessorCount - 1);

        return new ParallelOptions
        {
            MaxDegreeOfParallelism = degree
        };
    }

    /// <summary>
    /// Exécute le traitement IsDeterministicallyPreferred.
    /// </summary>
    /// <param name="candidate">Paramètre candidate.</param>
    /// <param name="existing">Paramètre existing.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsDeterministicallyPreferred(InferredRelationship candidate, InferredRelationship existing)
    {
        (string, string ToTable, string ToColumn, string Reason) candidateTuple = (candidate.Source.ToString(), candidate.ToTable, candidate.ToColumn, candidate.Reason);
        (string, string ToTable, string ToColumn, string Reason) existingTuple = (existing.Source.ToString(), existing.ToTable, existing.ToColumn, existing.Reason);
        return StringComparer.OrdinalIgnoreCase.Compare(candidateTuple.ToString(), existingTuple.ToString()) < 0;
    }

    /// <summary>
    /// Exécute le traitement AddDeclaredForeignKeys.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="add">Paramètre add.</param>
    private static void AddDeclaredForeignKeys(DatabaseSchema schema, Action<InferredRelationship> add)
    {
        foreach (DeclaredForeignKey fk in schema.DeclaredForeignKeys)
        {
            add(new InferredRelationship
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
    }

    /// <summary>
    /// Exécute le traitement AddSameColumnRelationships.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <param name="add">Paramètre add.</param>
    /// <param name="parallelOptions">Paramètre parallelOptions.</param>
    private static void AddSameColumnRelationships(InferenceContext context, Action<InferredRelationship> add, ParallelOptions parallelOptions)
    {
        Parallel.ForEach(context.ColumnsByNormalizedName.Values, parallelOptions, group =>
        {
            if (group.Count < 2)
            {
                return;
            }

            string normalizedName = group[0].NormalizedName;
            if (IsGenericIdentifier(normalizedName))
            {
                return;
            }

            // Strong case: specific same column name where target is PK/unique.
            // This is now O(group^2) for small same-name groups instead of O(total_columns^2).
            ColumnInfo[] strongTargets = [.. group.Where(c => c.TargetUnique)];
            if (strongTargets.Length > 0)
            {
                foreach (ColumnInfo? source in group.Where(c => !c.Column.IsPrimaryKey))
                {
                    foreach (ColumnInfo? target in strongTargets)
                    {
                        if (ReferenceEquals(source.Table, target.Table))
                        {
                            continue;
                        }

                        add(new InferredRelationship
                        {
                            FromTable = source.Table.FullName,
                            FromColumn = source.Column.Name,
                            ToTable = target.Table.FullName,
                            ToColumn = target.Column.Name,
                            Confidence = target.Column.IsPrimaryKey ? 0.86 : 0.82,
                            Source = RelationshipSource.SameColumnPrimaryKey,
                            Reason = $"Même nom de colonne spécifique ({source.Column.Name}) et colonne cible marquée comme PK/unique."
                        });
                    }
                }
            }

            // Weak case: same FK-ish non-PK column on two tables. This can explode for common names,
            // so we only keep it for reasonably small groups or when indexes give a credible signal.
            if (group.Count > SameColumnWeakGroupLimit && group.Count(c => c.Indexed) < 2)
            {
                return;
            }

            ColumnInfo[] weakCandidates = [.. group.Where(c => !c.Column.IsPrimaryKey && c.LooksLikeIdentifier)];

            for (int i = 0; i < weakCandidates.Length; i++)
            {
                ColumnInfo source = weakCandidates[i];
                for (int j = 0; j < weakCandidates.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    ColumnInfo target = weakCandidates[j];
                    if (ReferenceEquals(source.Table, target.Table))
                    {
                        continue;
                    }

                    if (group.Count > SameColumnWeakGroupLimit && (!source.Indexed || !target.Indexed))
                    {
                        continue;
                    }

                    add(new InferredRelationship
                    {
                        FromTable = source.Table.FullName,
                        FromColumn = source.Column.Name,
                        ToTable = target.Table.FullName,
                        ToColumn = target.Column.Name,
                        Confidence = 0.52,
                        Source = RelationshipSource.SameColumnName,
                        Reason = $"Même nom de colonne identifiant spécifique ({source.Column.Name}) dans deux tables non-PK. À valider si plusieurs relations candidates existent."
                    });
                }
            }
        });
    }

    /// <summary>
    /// Exécute le traitement AddTableNameColumnPatternRelationships.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <param name="add">Paramètre add.</param>
    /// <param name="parallelOptions">Paramètre parallelOptions.</param>
    private static void AddTableNameColumnPatternRelationships(InferenceContext context, Action<InferredRelationship> add, ParallelOptions parallelOptions)
    {
        Parallel.ForEach(context.IdentifierColumns, parallelOptions, source =>
        {
            string sourceStem = source.IdentifierStem;
            if (string.IsNullOrWhiteSpace(sourceStem) || IsGenericIdentifier(sourceStem))
            {
                return;
            }

            foreach (TableInfo targetTable in context.FindTablesByNameVariant(sourceStem))
            {
                if (ReferenceEquals(source.Table, targetTable))
                {
                    continue;
                }

                foreach (ColumnInfo targetColumn in targetTable.ReferenceTargetColumns)
                {
                    double score = TableColumnPatternScoreFast(source, targetTable, targetColumn);
                    if (score <= 0)
                    {
                        continue;
                    }

                    add(new InferredRelationship
                    {
                        FromTable = source.Table.FullName,
                        FromColumn = source.Column.Name,
                        ToTable = targetTable.FullName,
                        ToColumn = targetColumn.Column.Name,
                        Confidence = score,
                        Source = RelationshipSource.TableNameColumnPattern,
                        Reason = $"La colonne {source.Column.Name} ressemble à une référence vers {targetTable.Table.Name}.{targetColumn.Column.Name}."
                    });
                }
            }
        });
    }

    /// <summary>
    /// Exécute le traitement AddCompositeTablePatternRelationships.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <param name="add">Paramètre add.</param>
    /// <param name="parallelOptions">Paramètre parallelOptions.</param>
    private static void AddCompositeTablePatternRelationships(InferenceContext context, Action<InferredRelationship> add, ParallelOptions parallelOptions)
    {
        Parallel.ForEach(context.IdentifierColumns, parallelOptions, source =>
        {
            string sourceStem = source.IdentifierStem;
            if (string.IsNullOrWhiteSpace(sourceStem) || IsGenericIdentifier(sourceStem))
            {
                return;
            }

            IEnumerable<TableInfo> candidateTables = context.FindTablesByTokenOrVariant(sourceStem);
            foreach (TableInfo targetTable in candidateTables)
            {
                if (ReferenceEquals(source.Table, targetTable))
                {
                    continue;
                }

                if (!targetTable.HasAnyNameVariant(source.Table.NameVariants)
                    && !targetTable.HasAnyToken(source.Table.NameVariants))
                {
                    continue;
                }

                if (!targetTable.HasAnyNameVariant(source.StemVariants)
                    && !targetTable.HasAnyToken(source.StemVariants))
                {
                    continue;
                }

                foreach (ColumnInfo targetColumn in targetTable.ReferenceTargetColumns)
                {
                    double score = CompositeTablePatternScoreFast(source, targetTable, targetColumn);
                    if (score <= 0)
                    {
                        continue;
                    }

                    add(new InferredRelationship
                    {
                        FromTable = source.Table.FullName,
                        FromColumn = source.Column.Name,
                        ToTable = targetTable.FullName,
                        ToColumn = targetColumn.Column.Name,
                        Confidence = score,
                        Source = RelationshipSource.CompositeTablePattern,
                        Reason = $"La table {targetTable.Table.Name} ressemble à une table spécialisée/de liaison pour {source.Table.Table.Name} et {source.Column.Name}."
                    });
                }
            }
        });
    }

    /// <summary>
    /// Exécute le traitement AddCommentRelationships.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <param name="add">Paramètre add.</param>
    /// <param name="parallelOptions">Paramètre parallelOptions.</param>
    private static void AddCommentRelationships(InferenceContext context, Action<InferredRelationship> add, ParallelOptions parallelOptions)
    {
        Parallel.ForEach(context.IdentifierColumns.Where(c => !string.IsNullOrWhiteSpace(c.Column.Comment)), parallelOptions, source =>
        {
            string normalizedComment = SqlNameNormalizer.Normalize(source.Column.Comment);
            if (normalizedComment.Length == 0)
            {
                return;
            }

            foreach (TableInfo targetTable in context.Tables)
            {
                if (ReferenceEquals(source.Table, targetTable))
                {
                    continue;
                }

                bool tableMentioned = targetTable.NameVariants.Any(v => normalizedComment.Contains(v, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(targetTable.StrippedName) && normalizedComment.Contains(targetTable.StrippedName, StringComparison.OrdinalIgnoreCase));
                if (!tableMentioned)
                {
                    continue;
                }

                foreach (ColumnInfo targetColumn in targetTable.ReferenceTargetColumns)
                {
                    double score = CommentSimilarityFromNormalized(normalizedComment, targetTable, targetColumn);
                    if (score < 0.70)
                    {
                        continue;
                    }

                    add(new InferredRelationship
                    {
                        FromTable = source.Table.FullName,
                        FromColumn = source.Column.Name,
                        ToTable = targetTable.FullName,
                        ToColumn = targetColumn.Column.Name,
                        Confidence = Math.Min(0.80, score),
                        Source = RelationshipSource.CommentSimilarity,
                        Reason = $"Le commentaire de {source.Table.Table.Name}.{source.Column.Name} mentionne probablement {targetTable.Table.Name}."
                    });
                }
            }
        });
    }

    /// <summary>
    /// Exécute le traitement ApplyIndexBias.
    /// </summary>
    /// <param name="context">Paramètre context.</param>
    /// <param name="relationship">Paramètre relationship.</param>
    /// <returns>Résultat du traitement.</returns>
    private static InferredRelationship ApplyIndexBias(InferenceContext context, InferredRelationship relationship)
    {
        if (relationship.Source == RelationshipSource.DeclaredForeignKey)
        {
            return relationship;
        }

        ColumnInfo? source = context.FindColumn(relationship.FromTable, relationship.FromColumn);
        ColumnInfo? target = context.FindColumn(relationship.ToTable, relationship.ToColumn);
        if (source is null || target is null)
        {
            return relationship;
        }

        double bonus = 0.0;
        List<string> reasons = [];

        if (source.Indexed && source.LooksLikeIdentifier)
        {
            bonus += 0.06;
            reasons.Add("colonne source indexée");
        }

        if (target.TargetUnique)
        {
            bonus += 0.06;
            reasons.Add("cible PK/unique");
        }
        else if (target.Indexed)
        {
            bonus += 0.03;
            reasons.Add("colonne cible indexée");
        }

        if (source.Indexed && (target.Indexed || target.TargetUnique))
        {
            bonus += 0.02;
        }

        if (!source.Indexed && !target.Indexed && !target.TargetUnique && relationship.Source == RelationshipSource.SameColumnName)
        {
            bonus -= 0.05;
        }

        if (Math.Abs(bonus) < 0.0001)
        {
            return relationship;
        }

        double confidence = Math.Clamp(relationship.Confidence + bonus, 0.0, 0.995);
        string reason = reasons.Count == 0
            ? relationship.Reason
            : relationship.Reason + " Signal index: " + string.Join(", ", reasons) + ".";

        return relationship with
        {
            Confidence = confidence,
            Reason = reason
        };
    }

    /// <summary>
    /// Exécute le traitement TableColumnPatternScoreFast.
    /// </summary>
    /// <param name="source">Paramètre source.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="targetColumn">Paramètre targetColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double TableColumnPatternScoreFast(ColumnInfo source, TableInfo targetTable, ColumnInfo targetColumn)
    {
        if (!source.LooksLikeIdentifier || !targetColumn.IsReferenceTargetColumn)
        {
            return 0.0;
        }

        foreach (NameVariant variant in targetTable.NameVariantsWithWeight)
        {
            if (!source.IdentifierCandidates.Contains(variant.Name) && !source.CompactIdentifierCandidates.Contains(variant.CompactName))
            {
                continue;
            }

            double baseScore = targetColumn.Column.IsPrimaryKey ? 0.97 : 0.92;
            return Math.Min(0.99, baseScore * variant.Weight);
        }

        if (source.NormalizedName == targetColumn.NormalizedName && !IsGenericIdentifier(source.NormalizedName))
        {
            return targetColumn.Column.IsPrimaryKey ? 0.84 : 0.60;
        }

        return 0.0;
    }

    /// <summary>
    /// Exécute le traitement CompositeTablePatternScoreFast.
    /// </summary>
    /// <param name="source">Paramètre source.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="targetColumn">Paramètre targetColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double CompositeTablePatternScoreFast(ColumnInfo source, TableInfo targetTable, ColumnInfo targetColumn)
    {
        if (string.IsNullOrWhiteSpace(source.IdentifierStem)
            || IsGenericIdentifier(source.IdentifierStem)
            || !targetColumn.IsReferenceTargetColumn)
        {
            return 0.0;
        }

        bool startsWithSource = source.Table.NameVariants.Any(v => targetTable.NormalizedName.Equals(v, StringComparison.OrdinalIgnoreCase)
            || targetTable.NormalizedName.StartsWith(v + "_", StringComparison.OrdinalIgnoreCase));
        bool containsSource = targetTable.HasAnyToken(source.Table.NameVariants);
        if (!startsWithSource && !containsSource)
        {
            return 0.0;
        }

        bool containsStemToken = targetTable.HasAnyToken(source.StemVariants);
        bool containsStemTail = source.StemVariants.Any(v => targetTable.NormalizedName.EndsWith("_" + v, StringComparison.OrdinalIgnoreCase)
            || targetTable.SingularNormalizedName.EndsWith("_" + Singularize(v), StringComparison.OrdinalIgnoreCase));

        if (!containsStemToken && !containsStemTail)
        {
            return 0.0;
        }

        double baseScore = targetColumn.Column.IsPrimaryKey ? 0.985 : 0.955;
        return startsWithSource ? baseScore : baseScore - 0.03;
    }

    /// <summary>
    /// Exécute le traitement CommentSimilarityFromNormalized.
    /// </summary>
    /// <param name="normalizedComment">Paramètre normalizedComment.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="targetColumn">Paramètre targetColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double CommentSimilarityFromNormalized(string normalizedComment, TableInfo targetTable, ColumnInfo targetColumn)
    {
        double score = 0.0;

        if (normalizedComment.Contains(targetTable.NormalizedName, StringComparison.OrdinalIgnoreCase)) score += 0.55;
        if (!string.IsNullOrWhiteSpace(targetTable.StrippedName) && normalizedComment.Contains(targetTable.StrippedName, StringComparison.OrdinalIgnoreCase)) score += 0.45;
        if (normalizedComment.Contains(targetColumn.NormalizedName, StringComparison.OrdinalIgnoreCase)) score += 0.20;
        if (normalizedComment.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) || normalizedComment.Contains("REF", StringComparison.OrdinalIgnoreCase)) score += 0.15;
        if (normalizedComment.Contains("IDENTIFIANT", StringComparison.OrdinalIgnoreCase) || normalizedComment.Contains("ID", StringComparison.OrdinalIgnoreCase)) score += 0.10;

        return Math.Min(score, 1.0);
    }

    /// <summary>
    /// Exécute le traitement LooksLikeIdentifier.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool LooksLikeIdentifier(string columnName)
    {
        string n = SqlNameNormalizer.Normalize(columnName);
        return IsGenericIdentifier(n)
            || n.EndsWith("_ID", StringComparison.Ordinal)
            || n.EndsWith("_IDEN", StringComparison.Ordinal)
            || n.EndsWith("_IDENT", StringComparison.Ordinal)
            || n.EndsWith("_CODE", StringComparison.Ordinal)
            || n.StartsWith("ID_", StringComparison.Ordinal)
            || n.StartsWith("IDEN_", StringComparison.Ordinal)
            || n.StartsWith("FK_", StringComparison.Ordinal);
    }

    /// <summary>
    /// Exécute le traitement IsGenericIdentifier.
    /// </summary>
    /// <param name="normalizedColumnName">Paramètre normalizedColumnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsGenericIdentifier(string normalizedColumnName)
    {
        return normalizedColumnName is "ID" or "IDEN" or "IDENT";
    }

    /// <summary>
    /// Exécute le traitement IsReferenceTargetColumn.
    /// </summary>
    /// <param name="normalizedTargetColumn">Paramètre normalizedTargetColumn.</param>
    /// <param name="normalizedTargetTable">Paramètre normalizedTargetTable.</param>
    /// <param name="targetIsPk">Paramètre targetIsPk.</param>
    /// <returns>Résultat du traitement.</returns>
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

        foreach ((string Name, double Weight) variant in GetTableNameVariants(normalizedTargetTable))
        {
            string v = variant.Name;
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

    /// <summary>
    /// Exécute le traitement GetTableNameVariants.
    /// </summary>
    /// <param name="Name">Paramètre Name.</param>
    /// <param name="normalizedTableName">Paramètre normalizedTableName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IEnumerable<(string Name, double Weight)> GetTableNameVariants(string normalizedTableName)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void Add(List<(string Name, double Weight)> list, string? value, double weight)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = SqlNameNormalizer.Normalize(value).Trim('_');
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                return;
            }

            list.Add((normalized, weight));
        }

        List<(string Name, double Weight)> result = [];
        string table = SqlNameNormalizer.Normalize(normalizedTableName);
        Add(result, table, 1.00);
        Add(result, Singularize(table), 0.99);

        foreach (string withoutPrefix in RemoveCommonTablePrefixes(table))
        {
            Add(result, withoutPrefix, 0.96);
            Add(result, Singularize(withoutPrefix), 0.95);
        }

        string[] tokens = table.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1)
        {
            string withoutFirst = string.Join('_', tokens.Skip(1));
            Add(result, withoutFirst, 0.91);
            Add(result, Singularize(withoutFirst), 0.90);

            string last = tokens[^1];
            Add(result, last, 0.88);
            Add(result, Singularize(last), 0.87);
        }

        return result;
    }

    /// <summary>
    /// Exécute le traitement RemoveCommonTablePrefixes.
    /// </summary>
    /// <param name="normalizedTableName">Paramètre normalizedTableName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IEnumerable<string> RemoveCommonTablePrefixes(string normalizedTableName)
    {
        string[] prefixes = new[] { "T_", "TB_", "TBL_", "REF_", "DIM_", "D_", "R_" };
        foreach (string? prefix in prefixes)
        {
            if (normalizedTableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && normalizedTableName.Length > prefix.Length)
            {
                yield return normalizedTableName[prefix.Length..];
            }
        }
    }

    /// <summary>
    /// Exécute le traitement Singularize.
    /// </summary>
    /// <param name="normalizedName">Paramètre normalizedName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string Singularize(string normalizedName)
    {
        IEnumerable<string> parts = normalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SingularizeToken);
        return string.Join('_', parts);
    }

    /// <summary>
    /// Exécute le traitement SingularizeToken.
    /// </summary>
    /// <param name="token">Paramètre token.</param>
    /// <returns>Résultat du traitement.</returns>
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

    /// <summary>
    /// Exécute le traitement ExtractIdentifierStem.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string ExtractIdentifierStem(string columnName)
    {
        string normalized = SqlNameNormalizer.Normalize(columnName).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        string[] prefixes = new[] { "FK_", "ID_", "IDEN_", "IDENT_" };
        foreach (string? prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && normalized.Length > prefix.Length)
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        string[] suffixes = new[] { "_IDEN", "_IDENT", "_ID", "_CODE" };
        foreach (string? suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && normalized.Length > suffix.Length)
            {
                normalized = normalized[..^suffix.Length];
                break;
            }
        }

        return Singularize(normalized.Trim('_'));
    }

    /// <summary>
    /// Représente InferenceContext dans SQL Query Generator.
    /// </summary>
    private sealed class InferenceContext
    {
        /// <summary>
        /// Stocke la valeur interne  columnsByQualifiedName.
        /// </summary>
        /// <value>Valeur de _columnsByQualifiedName.</value>
        private readonly Dictionary<string, ColumnInfo> _columnsByQualifiedName;
        /// <summary>
        /// Stocke la valeur interne  tablesByNameVariant.
        /// </summary>
        /// <value>Valeur de _tablesByNameVariant.</value>
        private readonly Dictionary<string, IReadOnlyList<TableInfo>> _tablesByNameVariant;
        /// <summary>
        /// Stocke la valeur interne  tablesByToken.
        /// </summary>
        /// <value>Valeur de _tablesByToken.</value>
        private readonly Dictionary<string, IReadOnlyList<TableInfo>> _tablesByToken;

        /// <summary>
        /// Initialise une nouvelle instance de InferenceContext.
        /// </summary>
        private InferenceContext(
            IReadOnlyList<TableInfo> tables,
            IReadOnlyList<ColumnInfo> identifierColumns,
            Dictionary<string, List<ColumnInfo>> columnsByNormalizedName,
            Dictionary<string, ColumnInfo> columnsByQualifiedName,
            Dictionary<string, IReadOnlyList<TableInfo>> tablesByNameVariant,
            Dictionary<string, IReadOnlyList<TableInfo>> tablesByToken)
        {
            Tables = tables;
            IdentifierColumns = identifierColumns;
            ColumnsByNormalizedName = columnsByNormalizedName;
            _columnsByQualifiedName = columnsByQualifiedName;
            _tablesByNameVariant = tablesByNameVariant;
            _tablesByToken = tablesByToken;
        }

        /// <summary>
        /// Stocke la valeur interne Tables.
        /// </summary>
        /// <value>Valeur de Tables.</value>
        public IReadOnlyList<TableInfo> Tables { get; }
        /// <summary>
        /// Stocke la valeur interne IdentifierColumns.
        /// </summary>
        /// <value>Valeur de IdentifierColumns.</value>
        public IReadOnlyList<ColumnInfo> IdentifierColumns { get; }
        /// <summary>
        /// Stocke la valeur interne ColumnsByNormalizedName.
        /// </summary>
        /// <value>Valeur de ColumnsByNormalizedName.</value>
        public Dictionary<string, List<ColumnInfo>> ColumnsByNormalizedName { get; }

        /// <summary>
        /// Exécute le traitement Build.
        /// </summary>
        /// <param name="schema">Paramètre schema.</param>
        /// <returns>Résultat du traitement.</returns>
        public static InferenceContext Build(DatabaseSchema schema)
        {
            HashSet<string> indexedColumns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> uniqueColumns = new(StringComparer.OrdinalIgnoreCase);

            foreach (IndexDefinition index in schema.Indexes)
            {
                foreach (string column in index.Columns)
                {
                    indexedColumns.Add(QualifiedColumnKey(index.Table, column));
                }

                if (index.IsUnique && index.Columns.Count == 1)
                {
                    uniqueColumns.Add(QualifiedColumnKey(index.Table, index.Columns[0]));
                }
            }

            List<TableInfo> tables = new(schema.Tables.Count);
            foreach (TableDefinition table in schema.Tables)
            {
                tables.Add(new TableInfo(table));
            }

            Dictionary<string, TableInfo> tableByAnyName = new(StringComparer.OrdinalIgnoreCase);
            foreach (TableInfo table in tables)
            {
                tableByAnyName[SqlNameNormalizer.Normalize(table.Table.Name)] = table;
                tableByAnyName[SqlNameNormalizer.Normalize(table.Table.FullName)] = table;
            }

            // Index DDL often references the unqualified table name while columns use FullName.
            // Normalize the index flags onto the resolved TableInfo before building ColumnInfo.
            HashSet<string> resolvedIndexedColumns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> resolvedUniqueColumns = new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in indexedColumns)
            {
                int separator = key.IndexOf('|', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                string tableName = key[..separator];
                string columnName = key[(separator + 1)..];
                if (tableByAnyName.TryGetValue(tableName, out TableInfo? table))
                {
                    resolvedIndexedColumns.Add(QualifiedColumnKey(table.FullName, columnName));
                    resolvedIndexedColumns.Add(QualifiedColumnKey(table.Table.Name, columnName));
                }
            }

            foreach (string key in uniqueColumns)
            {
                int separator = key.IndexOf('|', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                string tableName = key[..separator];
                string columnName = key[(separator + 1)..];
                if (tableByAnyName.TryGetValue(tableName, out TableInfo? table))
                {
                    resolvedUniqueColumns.Add(QualifiedColumnKey(table.FullName, columnName));
                    resolvedUniqueColumns.Add(QualifiedColumnKey(table.Table.Name, columnName));
                }
            }

            Dictionary<string, List<ColumnInfo>> columnsByNormalizedName = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ColumnInfo> columnsByQualifiedName = new(StringComparer.OrdinalIgnoreCase);
            List<ColumnInfo> identifierColumns = [];

            foreach (TableInfo table in tables)
            {
                table.BuildColumns(resolvedIndexedColumns, resolvedUniqueColumns);
                foreach (ColumnInfo column in table.Columns)
                {
                    if (!columnsByNormalizedName.TryGetValue(column.NormalizedName, out List<ColumnInfo>? group))
                    {
                        group = [];
                        columnsByNormalizedName[column.NormalizedName] = group;
                    }
                    group.Add(column);

                    columnsByQualifiedName[QualifiedColumnKey(table.FullName, column.Column.Name)] = column;
                    columnsByQualifiedName[QualifiedColumnKey(table.Table.Name, column.Column.Name)] = column;

                    if (column.LooksLikeIdentifier)
                    {
                        identifierColumns.Add(column);
                    }
                }
            }

            Dictionary<string, IReadOnlyList<TableInfo>> tablesByNameVariant = BuildTableLookup(tables, t => t.NameVariants);
            Dictionary<string, IReadOnlyList<TableInfo>> tablesByToken = BuildTableLookup(tables, t => t.Tokens.Concat(t.SingularTokens));

            return new InferenceContext(
                tables,
                identifierColumns,
                columnsByNormalizedName,
                columnsByQualifiedName,
                tablesByNameVariant,
                tablesByToken);
        }

        /// <summary>
        /// Exécute le traitement FindColumn.
        /// </summary>
        /// <param name="table">Paramètre table.</param>
        /// <param name="column">Paramètre column.</param>
        /// <returns>Résultat du traitement.</returns>
        public ColumnInfo? FindColumn(string table, string column)
        {
            return _columnsByQualifiedName.TryGetValue(QualifiedColumnKey(table, column), out ColumnInfo? found)
                ? found
                : null;
        }

        /// <summary>
        /// Exécute le traitement FindTablesByNameVariant.
        /// </summary>
        /// <param name="stem">Paramètre stem.</param>
        /// <returns>Résultat du traitement.</returns>
        public IEnumerable<TableInfo> FindTablesByNameVariant(string stem)
        {
            IEnumerable<string> variants = GetTableNameVariants(stem)
                .Select(v => v.Name)
                .Append(stem)
                .Append(Singularize(stem))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return variants.SelectMany(v => _tablesByNameVariant.TryGetValue(v, out IReadOnlyList<TableInfo>? tables) ? tables : Array.Empty<TableInfo>())
                .Distinct();
        }

        /// <summary>
        /// Exécute le traitement FindTablesByTokenOrVariant.
        /// </summary>
        /// <param name="stem">Paramètre stem.</param>
        /// <returns>Résultat du traitement.</returns>
        public IEnumerable<TableInfo> FindTablesByTokenOrVariant(string stem)
        {
            string[] variants = [.. GetTableNameVariants(stem)
                .Select(v => v.Name)
                .Append(stem)
                .Append(Singularize(stem))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            return variants
                .SelectMany(v => (_tablesByToken.TryGetValue(v, out IReadOnlyList<TableInfo>? tokenTables) ? tokenTables : Array.Empty<TableInfo>())
                    .Concat(_tablesByNameVariant.TryGetValue(v, out IReadOnlyList<TableInfo>? variantTables) ? variantTables : Array.Empty<TableInfo>()))
                .Distinct();
        }

        /// <summary>
        /// Exécute le traitement BuildTableLookup.
        /// </summary>
        /// <param name="tables">Paramètre tables.</param>
        /// <param name="keySelector">Paramètre keySelector.</param>
        /// <returns>Résultat du traitement.</returns>
        private static Dictionary<string, IReadOnlyList<TableInfo>> BuildTableLookup(IEnumerable<TableInfo> tables, Func<TableInfo, IEnumerable<string>> keySelector)
        {
            Dictionary<string, List<TableInfo>> lookup = new(StringComparer.OrdinalIgnoreCase);
            foreach (TableInfo table in tables)
            {
                foreach (string? key in keySelector(table).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!lookup.TryGetValue(key, out List<TableInfo>? list))
                    {
                        list = [];
                        lookup[key] = list;
                    }
                    list.Add(table);
                }
            }

            return lookup.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TableInfo>)pair.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Représente TableInfo dans SQL Query Generator.
    /// </summary>
    private sealed class TableInfo
    {
        /// <summary>
        /// Initialise une nouvelle instance de TableInfo.
        /// </summary>
        /// <param name="table">Paramètre table.</param>
        public TableInfo(TableDefinition table)
        {
            Table = table;
            FullName = table.FullName;
            NormalizedName = SqlNameNormalizer.Normalize(table.Name);
            SingularNormalizedName = Singularize(NormalizedName);
            StrippedName = SqlNameNormalizer.StripDecorations(table.Name);
            NameVariantsWithWeight = GetTableNameVariants(NormalizedName)
                .Select(v => new NameVariant(v.Name, v.Weight, v.Name.Replace("_", string.Empty, StringComparison.Ordinal)))
                .ToArray();
            NameVariants = NameVariantsWithWeight.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Tokens = NormalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            SingularTokens = Tokens.Select(SingularizeToken).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            TokenSet = Tokens.Concat(SingularTokens).ToHashSet(StringComparer.OrdinalIgnoreCase);
            NameVariantSet = NameVariants.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stocke la valeur interne Table.
        /// </summary>
        /// <value>Valeur de Table.</value>
        public TableDefinition Table { get; }
        /// <summary>
        /// Stocke la valeur interne FullName.
        /// </summary>
        /// <value>Valeur de FullName.</value>
        public string FullName { get; }
        /// <summary>
        /// Stocke la valeur interne NormalizedName.
        /// </summary>
        /// <value>Valeur de NormalizedName.</value>
        public string NormalizedName { get; }
        /// <summary>
        /// Stocke la valeur interne SingularNormalizedName.
        /// </summary>
        /// <value>Valeur de SingularNormalizedName.</value>
        public string SingularNormalizedName { get; }
        /// <summary>
        /// Stocke la valeur interne StrippedName.
        /// </summary>
        /// <value>Valeur de StrippedName.</value>
        public string StrippedName { get; }
        /// <summary>
        /// Stocke la valeur interne NameVariantsWithWeight.
        /// </summary>
        /// <value>Valeur de NameVariantsWithWeight.</value>
        public IReadOnlyList<NameVariant> NameVariantsWithWeight { get; }
        /// <summary>
        /// Stocke la valeur interne NameVariants.
        /// </summary>
        /// <value>Valeur de NameVariants.</value>
        public IReadOnlyList<string> NameVariants { get; }
        /// <summary>
        /// Stocke la valeur interne Tokens.
        /// </summary>
        /// <value>Valeur de Tokens.</value>
        public IReadOnlyList<string> Tokens { get; }
        /// <summary>
        /// Stocke la valeur interne SingularTokens.
        /// </summary>
        /// <value>Valeur de SingularTokens.</value>
        public IReadOnlyList<string> SingularTokens { get; }
        /// <summary>
        /// Stocke la valeur interne TokenSet.
        /// </summary>
        /// <value>Valeur de TokenSet.</value>
        public HashSet<string> TokenSet { get; }
        /// <summary>
        /// Stocke la valeur interne NameVariantSet.
        /// </summary>
        /// <value>Valeur de NameVariantSet.</value>
        public HashSet<string> NameVariantSet { get; }
        /// <summary>
        /// Stocke la valeur interne Columns.
        /// </summary>
        /// <value>Valeur de Columns.</value>
        public IReadOnlyList<ColumnInfo> Columns { get; private set; } = Array.Empty<ColumnInfo>();
        /// <summary>
        /// Stocke la valeur interne ReferenceTargetColumns.
        /// </summary>
        /// <value>Valeur de ReferenceTargetColumns.</value>
        public IReadOnlyList<ColumnInfo> ReferenceTargetColumns { get; private set; } = Array.Empty<ColumnInfo>();

        /// <summary>
        /// Exécute le traitement BuildColumns.
        /// </summary>
        /// <param name="indexedColumns">Paramètre indexedColumns.</param>
        /// <param name="uniqueColumns">Paramètre uniqueColumns.</param>
        public void BuildColumns(HashSet<string> indexedColumns, HashSet<string> uniqueColumns)
        {
            ColumnInfo[] columns = [.. Table.Columns.Select(c => new ColumnInfo(this, c, indexedColumns.Contains(QualifiedColumnKey(FullName, c.Name)) || indexedColumns.Contains(QualifiedColumnKey(Table.Name, c.Name)), uniqueColumns.Contains(QualifiedColumnKey(FullName, c.Name)) || uniqueColumns.Contains(QualifiedColumnKey(Table.Name, c.Name))))];

            Columns = columns;
            ReferenceTargetColumns = columns.Where(c => c.IsReferenceTargetColumn).ToArray();
        }

        /// <summary>
        /// Exécute le traitement HasAnyNameVariant.
        /// </summary>
        /// <param name="variants">Paramètre variants.</param>
        /// <returns>Résultat du traitement.</returns>
        public bool HasAnyNameVariant(IEnumerable<string> variants) => variants.Any(v => NameVariantSet.Contains(v));
        /// <summary>
        /// Exécute le traitement HasAnyToken.
        /// </summary>
        /// <param name="variants">Paramètre variants.</param>
        /// <returns>Résultat du traitement.</returns>
        public bool HasAnyToken(IEnumerable<string> variants) => variants.Any(v => TokenSet.Contains(v));
    }

    /// <summary>
    /// Représente ColumnInfo dans SQL Query Generator.
    /// </summary>
    private sealed class ColumnInfo
    {
        /// <summary>
        /// Initialise une nouvelle instance de ColumnInfo.
        /// </summary>
        /// <param name="table">Paramètre table.</param>
        /// <param name="column">Paramètre column.</param>
        /// <param name="indexed">Paramètre indexed.</param>
        /// <param name="uniqueIndexed">Paramètre uniqueIndexed.</param>
        public ColumnInfo(TableInfo table, ColumnDefinition column, bool indexed, bool uniqueIndexed)
        {
            Table = table;
            Column = column;
            Indexed = indexed;
            UniqueIndexed = uniqueIndexed;
            TargetUnique = column.IsPrimaryKey || uniqueIndexed;
            NormalizedName = SqlNameNormalizer.Normalize(column.Name);
            LooksLikeIdentifier = ForeignKeyInferer.LooksLikeIdentifier(column.Name);
            IdentifierStem = ExtractIdentifierStem(column.Name);
            StemVariants = GetTableNameVariants(IdentifierStem)
                .Select(v => v.Name)
                .Append(IdentifierStem)
                .Append(Singularize(IdentifierStem))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            IdentifierCandidates = BuildIdentifierCandidates(NormalizedName);
            CompactIdentifierCandidates = IdentifierCandidates.Select(c => c.Replace("_", string.Empty, StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            IsReferenceTargetColumn = ForeignKeyInferer.IsReferenceTargetColumn(NormalizedName, table.NormalizedName, column.IsPrimaryKey || uniqueIndexed);
        }

        /// <summary>
        /// Stocke la valeur interne Table.
        /// </summary>
        /// <value>Valeur de Table.</value>
        public TableInfo Table { get; }
        /// <summary>
        /// Stocke la valeur interne Column.
        /// </summary>
        /// <value>Valeur de Column.</value>
        public ColumnDefinition Column { get; }
        /// <summary>
        /// Stocke la valeur interne NormalizedName.
        /// </summary>
        /// <value>Valeur de NormalizedName.</value>
        public string NormalizedName { get; }
        /// <summary>
        /// Stocke la valeur interne Indexed.
        /// </summary>
        /// <value>Valeur de Indexed.</value>
        public bool Indexed { get; }
        /// <summary>
        /// Stocke la valeur interne UniqueIndexed.
        /// </summary>
        /// <value>Valeur de UniqueIndexed.</value>
        public bool UniqueIndexed { get; }
        /// <summary>
        /// Stocke la valeur interne TargetUnique.
        /// </summary>
        /// <value>Valeur de TargetUnique.</value>
        public bool TargetUnique { get; }
        /// <summary>
        /// Stocke la valeur interne LooksLikeIdentifier.
        /// </summary>
        /// <value>Valeur de LooksLikeIdentifier.</value>
        public bool LooksLikeIdentifier { get; }
        /// <summary>
        /// Stocke la valeur interne IdentifierStem.
        /// </summary>
        /// <value>Valeur de IdentifierStem.</value>
        public string IdentifierStem { get; }
        /// <summary>
        /// Stocke la valeur interne StemVariants.
        /// </summary>
        /// <value>Valeur de StemVariants.</value>
        public IReadOnlyList<string> StemVariants { get; }
        /// <summary>
        /// Stocke la valeur interne IdentifierCandidates.
        /// </summary>
        /// <value>Valeur de IdentifierCandidates.</value>
        public HashSet<string> IdentifierCandidates { get; }
        /// <summary>
        /// Stocke la valeur interne CompactIdentifierCandidates.
        /// </summary>
        /// <value>Valeur de CompactIdentifierCandidates.</value>
        public HashSet<string> CompactIdentifierCandidates { get; }
        /// <summary>
        /// Stocke la valeur interne IsReferenceTargetColumn.
        /// </summary>
        /// <value>Valeur de IsReferenceTargetColumn.</value>
        public bool IsReferenceTargetColumn { get; }

        /// <summary>
        /// Exécute le traitement BuildIdentifierCandidates.
        /// </summary>
        /// <param name="sourceColumn">Paramètre sourceColumn.</param>
        /// <returns>Résultat du traitement.</returns>
        private static HashSet<string> BuildIdentifierCandidates(string sourceColumn)
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string? suffix in new[] { "_ID", "_IDEN", "_IDENT", "_CODE" })
            {
                if (sourceColumn.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && sourceColumn.Length > suffix.Length)
                {
                    result.Add(sourceColumn[..^suffix.Length]);
                }
            }

            foreach (string? prefix in new[] { "ID_", "IDEN_", "FK_" })
            {
                if (sourceColumn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && sourceColumn.Length > prefix.Length)
                {
                    string rest = sourceColumn[prefix.Length..];
                    if (rest.EndsWith("_ID", StringComparison.OrdinalIgnoreCase) && rest.Length > 3)
                    {
                        rest = rest[..^3];
                    }
                    result.Add(rest);
                }
            }

            string stem = ExtractIdentifierStem(sourceColumn);
            if (!string.IsNullOrWhiteSpace(stem))
            {
                result.Add(stem);
                result.Add(Singularize(stem));
            }

            return result;
        }
    }

    /// <summary>
    /// Représente NameVariant dans SQL Query Generator.
    /// </summary>
    private sealed record NameVariant(string Name, double Weight, string CompactName);

    /// <summary>
    /// Exécute le traitement QualifiedColumnKey.
    /// </summary>
    /// <param name="table">Paramètre table.</param>
    /// <param name="column">Paramètre column.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string QualifiedColumnKey(string table, string column)
    {
        return SqlNameNormalizer.Normalize(table) + "|" + SqlNameNormalizer.Normalize(column);
    }
}
