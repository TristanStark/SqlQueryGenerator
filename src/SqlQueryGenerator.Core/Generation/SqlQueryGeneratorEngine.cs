using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;
using System.Text;

namespace SqlQueryGenerator.Core.Generation;

/// <summary>
/// Représente SqlQueryGeneratorEngine dans SQL Query Generator.
/// </summary>
public sealed class SqlQueryGeneratorEngine
{
    /// <summary>
    /// Exécute le traitement Generate.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
    public SqlGenerationResult Generate(QueryDefinition query, DatabaseSchema schema, SqlGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);
        options ??= new SqlGeneratorOptions();

        List<string> warnings = [];
        string? baseTable = ResolveBaseTable(query, warnings);
        if (baseTable is null)
        {
            return new SqlGenerationResult { Sql = "-- Sélectionne au moins une colonne ou une table de départ.", Warnings = warnings };
        }

        HashSet<string> usedTables = CollectUsedTables(query);
        usedTables.Add(baseTable);
        IReadOnlyList<JoinDefinition> joins = BuildJoinPlan(query, schema, baseTable, usedTables, warnings);

        List<string> selectItems = BuildSelectItems(query, options, warnings);
        if (selectItems.Count == 0)
        {
            selectItems.Add("*");
            warnings.Add("Aucune colonne sélectionnée: SELECT * généré par défaut.");
        }

        StringBuilder sb = new();
        if (options.EmitOptimizationComments)
        {
            sb.AppendLine("/* Requête générée sans sous-requête, avec jointures explicites lorsque possible. */");
        }

        sb.Append("SELECT ");
        if (query.Distinct)
        {
            sb.Append("DISTINCT ");
        }
        sb.AppendLine(string.Join(",\n       ", selectItems));
        sb.Append("FROM ").AppendLine(Q(baseTable, options));

        foreach (JoinDefinition join in joins)
        {
            sb.Append(join.JoinType == JoinType.Left ? "LEFT JOIN " : "INNER JOIN ");
            sb.Append(Q(join.ToTable, options));
            sb.Append(" ON ");
            sb.Append(BuildJoinOnClause(join, options));
            if (join.AutoInferred && options.EmitOptimizationComments)
            {
                sb.Append(" /* jointure inférée */");
            }
            sb.AppendLine();
        }

        List<FilterCondition> whereFilters = query.Filters.Where(f => f.FieldKind != QueryFieldKind.Aggregate).ToList();
        List<(string Predicate, LogicalConnector Connector)> where = BuildFilterPredicates(query, whereFilters, schema, options, warnings);
        AppendPredicateBlock(sb, "WHERE", where);

        List<string> groupBy = BuildGroupBy(query, options);
        if (query.Aggregates.Count > 0 && options.AutoGroupSelectedColumnsWhenAggregating)
        {
            foreach (ColumnReference selected in query.SelectedColumns)
            {
                string expr = ColumnSql(selected, options, includeAlias: false);
                if (!groupBy.Contains(expr, StringComparer.OrdinalIgnoreCase))
                {
                    groupBy.Add(expr);
                }
            }
        }

        if (groupBy.Count > 0)
        {
            sb.Append("GROUP BY ").AppendLine(string.Join(", ", groupBy));
        }

        List<FilterCondition> havingFilters = query.Filters.Where(f => f.FieldKind == QueryFieldKind.Aggregate).ToList();
        List<(string Predicate, LogicalConnector Connector)> having = BuildFilterPredicates(query, havingFilters, schema, options, warnings);
        AppendPredicateBlock(sb, "HAVING", having);

        if (query.OrderBy.Count > 0)
        {
            string[] orderItems = query.OrderBy
                .Select(o =>
                {
                    string expression = BuildOrderExpression(query, o, options, warnings);
                    return string.IsNullOrWhiteSpace(expression)
                        ? string.Empty
                        : $"{expression} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}";
                })
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToArray();
            if (orderItems.Length > 0)
            {
                sb.Append("ORDER BY ").AppendLine(string.Join(", ", orderItems));
            }
        }

        if (query.LimitRows is > 0)
        {
            if (options.Dialect == SqlDialect.Oracle)
            {
                sb.AppendLine($"FETCH FIRST {query.LimitRows.Value} ROWS ONLY");
            }
            else
            {
                sb.AppendLine($"LIMIT {query.LimitRows.Value}");
            }
        }

        return new SqlGenerationResult
        {
            Sql = sb.ToString().TrimEnd() + Environment.NewLine,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Exécute le traitement BuildJoinOnClause.
    /// </summary>
    /// <param name="join">Paramètre join.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildJoinOnClause(JoinDefinition join, SqlGeneratorOptions options)
    {
        List<string> predicates =
        [
            $"{Q(join.FromTable, options)}.{Q(join.FromColumn, options)} = {Q(join.ToTable, options)}.{Q(join.ToColumn, options)}"
        ];

        foreach (JoinColumnPair? pair in join.AdditionalColumnPairs.Where(p => p.Enabled
                     && !string.IsNullOrWhiteSpace(p.FromColumn)
                     && !string.IsNullOrWhiteSpace(p.ToColumn)))
        {
            string predicate = $"{Q(join.FromTable, options)}.{Q(pair.FromColumn, options)} = {Q(join.ToTable, options)}.{Q(pair.ToColumn, options)}";
            if (!predicates.Contains(predicate, StringComparer.OrdinalIgnoreCase))
            {
                predicates.Add(predicate);
            }
        }

        return string.Join(" AND ", predicates);
    }

    /// <summary>
    /// Exécute le traitement ResolveBaseTable.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? ResolveBaseTable(QueryDefinition query, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(query.BaseTable))
        {
            return query.BaseTable.Trim();
        }

        string? first = query.SelectedColumns.FirstOrDefault()?.Table
            ?? query.Filters.FirstOrDefault(f => f.Column is not null)?.Column?.Table
            ?? query.GroupBy.FirstOrDefault()?.Table
            ?? query.OrderBy.FirstOrDefault(o => o.Column is not null)?.Column?.Table
            ?? query.Aggregates.FirstOrDefault(a => a.Column is not null)?.Column?.Table
            ?? query.Aggregates.FirstOrDefault(a => a.ConditionColumn is not null)?.ConditionColumn?.Table
            ?? query.CustomColumns.FirstOrDefault(c => c.CaseColumn is not null)?.CaseColumn?.Table;

        if (!string.IsNullOrWhiteSpace(first))
        {
            warnings.Add($"Table de départ non renseignée: {first} utilisée automatiquement.");
        }

        return first;
    }

    /// <summary>
    /// Exécute le traitement CollectUsedTables.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <returns>Résultat du traitement.</returns>
    private static HashSet<string> CollectUsedTables(QueryDefinition query)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (ColumnReference c in query.SelectedColumns) result.Add(c.Table);
        foreach (FilterCondition? f in query.Filters.Where(f => f.Column is not null)) result.Add(f.Column!.Table);
        foreach (ColumnReference g in query.GroupBy) result.Add(g.Table);
        foreach (OrderByItem? o in query.OrderBy.Where(o => o.Column is not null)) result.Add(o.Column!.Table);
        foreach (AggregateSelection? a in query.Aggregates.Where(a => a.Column is not null)) result.Add(a.Column!.Table);
        foreach (AggregateSelection? a in query.Aggregates.Where(a => a.ConditionColumn is not null)) result.Add(a.ConditionColumn!.Table);
        foreach (CustomColumnSelection? c in query.CustomColumns.Where(c => c.CaseColumn is not null)) result.Add(c.CaseColumn!.Table);
        return result;
    }

    /// <summary>
    /// Exécute le traitement BuildJoinPlan.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="baseTable">Paramètre baseTable.</param>
    /// <param name="usedTables">Paramètre usedTables.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IReadOnlyList<JoinDefinition> BuildJoinPlan(QueryDefinition query, DatabaseSchema schema, string baseTable, HashSet<string> usedTables, List<string> warnings)
    {
        List<JoinDefinition> joins = [];
        HashSet<string> connected = new(StringComparer.OrdinalIgnoreCase) { baseTable };
        HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);

        void AddJoin(JoinDefinition join, bool autoInferred)
        {
            string key = JoinKey(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
            if (!emitted.Add(key))
            {
                return;
            }

            JoinDefinition preparedJoin = autoInferred
                ? AddCompositePairsFromRelationships(schema, join, query.DisabledAutoJoinKeys)
                : join;
            joins.Add(preparedJoin with { AutoInferred = autoInferred });
            connected.Add(join.FromTable);
            connected.Add(join.ToTable);
        }

        foreach (JoinDefinition explicitJoin in query.Joins)
        {
            AddJoin(explicitJoin, autoInferred: false);
        }

        HashSet<string> remaining = usedTables.Where(t => !connected.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int safety = 0;
        while (remaining.Count > 0 && safety++ < 256)
        {
            List<JoinDefinition> bestPath = FindBestJoinPath(schema, connected, remaining, query.DisabledAutoJoinKeys);
            if (bestPath.Count == 0)
            {
                foreach (string? table in remaining.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"Aucune jointure fiable trouvée depuis {baseTable} vers {table}. La table n'a pas été jointe automatiquement.");
                }
                break;
            }

            foreach (JoinDefinition join in bestPath)
            {
                AddJoin(join, autoInferred: true);
            }

            remaining = usedTables.Where(t => !connected.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return joins;
    }

    /// <summary>
    /// Exécute le traitement AddCompositePairsFromRelationships.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="join">Paramètre join.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static JoinDefinition AddCompositePairsFromRelationships(DatabaseSchema schema, JoinDefinition join, IEnumerable<string> disabledAutoJoinKeys)
    {
        JoinColumnPair[] compatible = schema.Relationships
            .Where(r => !IsRelationshipDisabled(r, disabledAutoJoinKeys))
            .Select(r =>
            {
                if (SqlNameNormalizer.EqualsName(r.FromTable, join.FromTable)
                    && SqlNameNormalizer.EqualsName(r.ToTable, join.ToTable))
                {
                    return new JoinColumnPair { FromColumn = r.FromColumn, ToColumn = r.ToColumn, Enabled = true };
                }

                if (SqlNameNormalizer.EqualsName(r.FromTable, join.ToTable)
                    && SqlNameNormalizer.EqualsName(r.ToTable, join.FromTable))
                {
                    return new JoinColumnPair { FromColumn = r.ToColumn, ToColumn = r.FromColumn, Enabled = true };
                }

                return null;
            })
            .Where(p => p is not null)
            .Cast<JoinColumnPair>()
            .Where(p => !SqlNameNormalizer.EqualsName(p.FromColumn, join.FromColumn)
                && !SqlNameNormalizer.EqualsName(p.ToColumn, join.ToColumn)
                && LooksLikeCompositeCompanion(p.FromColumn, p.ToColumn))
            .Take(8)
            .ToArray();

        if (compatible.Length == 0)
        {
            return join;
        }

        JoinDefinition result = join with { AdditionalColumnPairs = [] };
        foreach (JoinColumnPair pair in join.AdditionalColumnPairs)
        {
            result.AdditionalColumnPairs.Add(pair);
        }

        foreach (JoinColumnPair? pair in compatible)
        {
            if (result.AdditionalColumnPairs.Any(p => SqlNameNormalizer.EqualsName(p.FromColumn, pair.FromColumn)
                    && SqlNameNormalizer.EqualsName(p.ToColumn, pair.ToColumn)))
            {
                continue;
            }

            result.AdditionalColumnPairs.Add(pair);
        }

        return result;
    }

    /// <summary>
    /// Exécute le traitement LooksLikeCompositeCompanion.
    /// </summary>
    /// <param name="fromColumn">Paramètre fromColumn.</param>
    /// <param name="toColumn">Paramètre toColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool LooksLikeCompositeCompanion(string fromColumn, string toColumn)
    {
        string left = SqlNameNormalizer.Normalize(fromColumn);
        string right = SqlNameNormalizer.Normalize(toColumn);
        if (left == right)
        {
            return true;
        }

        static string StripCommonSuffix(string value)
        {
            foreach (string? suffix in new[] { "_iden", "_id", "_code", "_date", "_dt" })
            {
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return value[..^suffix.Length];
                }
            }
            return value;
        }

        string l = StripCommonSuffix(left);
        string r = StripCommonSuffix(right);
        return l == r
            || left.EndsWith("_date", StringComparison.OrdinalIgnoreCase) && right.EndsWith("_date", StringComparison.OrdinalIgnoreCase)
            || left.EndsWith("_iden", StringComparison.OrdinalIgnoreCase) && right.EndsWith("_iden", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement FindBestJoinPath.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="connected">Paramètre connected.</param>
    /// <param name="remaining">Paramètre remaining.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<JoinDefinition> FindBestJoinPath(DatabaseSchema schema, HashSet<string> connected, HashSet<string> remaining, IEnumerable<string> disabledAutoJoinKeys)
    {
        List<JoinDefinition> best = [];
        double bestScore = double.NegativeInfinity;

        // Highest-priority rule: if a reliable direct relationship exists between an
        // already-connected table and the requested table, use it before considering any
        // bridge/junction table. This fixes cases such as:
        //   pnj.race_id -> pnj_race_descriptions.id
        // where a generic graph search might otherwise route through seances_pnjs because
        // both table names contain the token "pnj".
        foreach (string start in connected)
        {
            foreach (string target in remaining)
            {
                List<JoinDefinition> directPath = TryFindBestDirectRelationshipPath(schema, start, target, disabledAutoJoinKeys);
                if (directPath.Count > 0)
                {
                    double score = ScoreJoinPath(schema, directPath, start, target) + 10.0;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = directPath;
                    }
                }
            }
        }

        if (best.Count > 0)
        {
            return best;
        }

        // Second-class rule: if a clean junction table exists between a connected table and
        // the requested target table, prefer that short business path. This prevents
        // graph-search detours such as PNJ -> lineage -> quests -> pnj_item -> items.
        foreach (string start in connected)
        {
            foreach (string target in remaining)
            {
                List<JoinDefinition> bridgePath = TryFindBestDirectJunctionPath(schema, start, target, disabledAutoJoinKeys);
                if (bridgePath.Count > 0)
                {
                    double score = ScoreJoinPath(schema, bridgePath, start, target) + 5.0;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = bridgePath;
                    }
                }
            }
        }

        if (best.Count > 0)
        {
            return best;
        }

        foreach (string start in connected)
        {
            foreach (string target in remaining)
            {
                foreach (List<JoinDefinition> path in FindCandidatePaths(schema, start, target, maxDepth: 2, disabledAutoJoinKeys))
                {
                    if (path.Count == 0)
                    {
                        continue;
                    }

                    double score = ScoreJoinPath(schema, path, start, target);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = path;
                    }
                }
            }
        }

        // Below this threshold we would rather warn than generate nonsense SQL. Users can
        // still add the join manually from the UI.
        return bestScore >= 0.70 ? best : [];
    }


    /// <summary>
    /// Exécute le traitement TryFindBestDirectRelationshipPath.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<JoinDefinition> TryFindBestDirectRelationshipPath(DatabaseSchema schema, string startTable, string targetTable, IEnumerable<string> disabledAutoJoinKeys)
    {
        var candidates = schema.Relationships
            .Where(r => !IsRelationshipDisabled(r, disabledAutoJoinKeys))
            .Where(r => r.Connects(startTable, targetTable))
            .Select(r => new
            {
                Relationship = r,
                Join = OrientRelationshipAsJoin(r, startTable, targetTable),
                Score = DirectRelationshipPlannerScore(schema, r, startTable, targetTable)
            })
            .Where(x => x.Join is not null)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Relationship.Confidence)
            .ThenBy(x => x.Relationship.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Relationship.FromColumn, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var best = candidates.FirstOrDefault();
        if (best?.Join is null || best.Score < 0.70)
        {
            return [];
        }

        return [best.Join];
    }

    /// <summary>
    /// Exécute le traitement OrientRelationshipAsJoin.
    /// </summary>
    /// <param name="relationship">Paramètre relationship.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static JoinDefinition? OrientRelationshipAsJoin(InferredRelationship relationship, string startTable, string targetTable)
    {
        if (SqlNameNormalizer.EqualsName(relationship.FromTable, startTable) && SqlNameNormalizer.EqualsName(relationship.ToTable, targetTable))
        {
            return new JoinDefinition
            {
                FromTable = relationship.FromTable,
                FromColumn = relationship.FromColumn,
                ToTable = relationship.ToTable,
                ToColumn = relationship.ToColumn,
                JoinType = JoinType.Inner,
                AutoInferred = true
            };
        }

        if (SqlNameNormalizer.EqualsName(relationship.ToTable, startTable) && SqlNameNormalizer.EqualsName(relationship.FromTable, targetTable))
        {
            return new JoinDefinition
            {
                FromTable = relationship.ToTable,
                FromColumn = relationship.ToColumn,
                ToTable = relationship.FromTable,
                ToColumn = relationship.FromColumn,
                JoinType = JoinType.Inner,
                AutoInferred = true
            };
        }

        return null;
    }

    /// <summary>
    /// Exécute le traitement DirectRelationshipPlannerScore.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="relationship">Paramètre relationship.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double DirectRelationshipPlannerScore(DatabaseSchema schema, InferredRelationship relationship, string startTable, string targetTable)
    {
        JoinDefinition? join = OrientRelationshipAsJoin(relationship, startTable, targetTable);
        if (join is null)
        {
            return double.NegativeInfinity;
        }

        double score = relationship.Confidence;
        score += relationship.Source switch
        {
            RelationshipSource.DeclaredForeignKey => 0.55,
            RelationshipSource.TableNameColumnPattern => 0.40,
            RelationshipSource.CompositeTablePattern => 0.30,
            RelationshipSource.SameColumnPrimaryKey => 0.05,
            RelationshipSource.SameColumnName => -0.75,
            RelationshipSource.CommentSimilarity => -0.20,
            _ => 0.0
        };

        ColumnDefinition? fromCol = schema.FindColumn(join.FromTable, join.FromColumn);
        ColumnDefinition? toCol = schema.FindColumn(join.ToTable, join.ToColumn);
        string fromName = SqlNameNormalizer.Normalize(join.FromColumn);
        string toName = SqlNameNormalizer.Normalize(join.ToColumn);

        if (IsForeignKeyLike(fromName) && (toCol?.IsPrimaryKey == true || schema.IsColumnUniqueIndexed(join.ToTable, join.ToColumn) || IsGenericIdentifier(toName)))
        {
            score += 0.55;
        }

        if (fromCol?.IsPrimaryKey == true && IsForeignKeyLike(toName))
        {
            score += 0.25;
        }

        if (schema.IsColumnIndexed(join.FromTable, join.FromColumn))
        {
            score += 0.10;
        }

        if (schema.IsColumnUniqueIndexed(join.ToTable, join.ToColumn) || toCol?.IsPrimaryKey == true)
        {
            score += 0.15;
        }

        // Direct same-name edges such as id=id are frequently false positives unless the
        // relationship has already been declared or strongly supported by a PK/FK pattern.
        if (relationship.Source == RelationshipSource.SameColumnName || relationship.Source == RelationshipSource.SameColumnPrimaryKey)
        {
            if (!IsForeignKeyLike(fromName) && !IsForeignKeyLike(toName))
            {
                score -= 0.40;
            }
        }

        return score;
    }

    /// <summary>
    /// Exécute le traitement TryFindBestDirectJunctionPath.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<JoinDefinition> TryFindBestDirectJunctionPath(DatabaseSchema schema, string startTable, string targetTable, IEnumerable<string> disabledAutoJoinKeys)
    {
        TableDefinition? start = schema.FindTable(startTable);
        TableDefinition? target = schema.FindTable(targetTable);
        if (start is null || target is null)
        {
            return [];
        }

        double bestScore = double.NegativeInfinity;
        List<JoinDefinition> best = [];
        foreach (TableDefinition bridge in schema.Tables)
        {
            if (SqlNameNormalizer.EqualsName(bridge.FullName, startTable)
                || SqlNameNormalizer.EqualsName(bridge.FullName, targetTable))
            {
                continue;
            }

            double bridgeScore = ScoreJunctionTableCandidate(bridge, start, target);
            if (bridgeScore <= 0)
            {
                continue;
            }

            ColumnDefinition? startBridgeColumn = FindBridgeColumnForTable(bridge, start);
            ColumnDefinition? targetBridgeColumn = FindBridgeColumnForTable(bridge, target);
            if (startBridgeColumn is null || targetBridgeColumn is null)
            {
                continue;
            }

            ColumnDefinition? startKey = FindBestKeyColumnForBridge(start, startBridgeColumn);
            ColumnDefinition? targetKey = FindBestKeyColumnForBridge(target, targetBridgeColumn);
            if (startKey is null || targetKey is null)
            {
                continue;
            }

            if (SqlNameNormalizer.EqualsName(startBridgeColumn.Name, targetBridgeColumn.Name))
            {
                continue;
            }

            double score = bridgeScore;
            score += KeyColumnScore(startKey, start) + KeyColumnScore(targetKey, target);
            score += BridgeColumnScore(startBridgeColumn, start) + BridgeColumnScore(targetBridgeColumn, target);
            score -= Math.Max(0, bridge.Columns.Count - 3) * 0.02;

            List<JoinDefinition> candidate =
            [
                new()
                {
                    FromTable = start.FullName,
                    FromColumn = startKey.Name,
                    ToTable = bridge.FullName,
                    ToColumn = startBridgeColumn.Name,
                    JoinType = JoinType.Inner,
                    AutoInferred = true
                },
                new()
                {
                    FromTable = bridge.FullName,
                    FromColumn = targetBridgeColumn.Name,
                    ToTable = target.FullName,
                    ToColumn = targetKey.Name,
                    JoinType = JoinType.Inner,
                    AutoInferred = true
                }
            ];

            if (candidate.Any(join => IsJoinDisabled(join, disabledAutoJoinKeys)))
            {
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= 1.25 ? best : [];
    }

    /// <summary>
    /// Exécute le traitement ScoreJunctionTableCandidate.
    /// </summary>
    /// <param name="bridge">Paramètre bridge.</param>
    /// <param name="left">Paramètre left.</param>
    /// <param name="right">Paramètre right.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double ScoreJunctionTableCandidate(TableDefinition bridge, TableDefinition left, TableDefinition right)
    {
        string leftStem = TableStem(left.FullName);
        string rightStem = TableStem(right.FullName);
        string bridgeNorm = Singularize(SqlNameNormalizer.Normalize(bridge.FullName));
        string bridgeNameOnly = Singularize(SqlNameNormalizer.Normalize(bridge.Name));
        string[] tokens = bridgeNameOnly.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        double score = 0.0;
        string exactA = $"{leftStem}_{rightStem}";
        string exactB = $"{rightStem}_{leftStem}";
        if (bridgeNameOnly.Equals(exactA, StringComparison.OrdinalIgnoreCase)
            || bridgeNameOnly.Equals(exactB, StringComparison.OrdinalIgnoreCase)
            || bridgeNorm.EndsWith("_" + exactA, StringComparison.OrdinalIgnoreCase)
            || bridgeNorm.EndsWith("_" + exactB, StringComparison.OrdinalIgnoreCase))
        {
            score += 2.50;
        }
        else
        {
            bool containsLeft = tokens.Any(t => SameNameRelaxed(t, leftStem));
            bool containsRight = tokens.Any(t => SameNameRelaxed(t, rightStem));
            if (!containsLeft || !containsRight)
            {
                return 0.0;
            }

            score += 1.10;
            int extraTokens = tokens.Count(t => !SameNameRelaxed(t, leftStem) && !SameNameRelaxed(t, rightStem));
            score -= Math.Min(0.95, extraTokens * 0.18);
        }

        if (FindBridgeColumnForTable(bridge, left) is not null)
        {
            score += 0.55;
        }

        if (FindBridgeColumnForTable(bridge, right) is not null)
        {
            score += 0.55;
        }

        if (bridge.Columns.Count <= 4)
        {
            score += 0.25;
        }

        string[] suspiciousTokens = new[] { "focus", "tmp", "temp", "staging", "archive", "history", "historique", "log", "audit", "backup", "old", "lineage", "quest", "quests" };
        if (tokens.Any(t => suspiciousTokens.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            score -= 1.20;
        }

        return score;
    }

    /// <summary>
    /// Exécute le traitement FindBridgeColumnForTable.
    /// </summary>
    /// <param name="bridge">Paramètre bridge.</param>
    /// <param name="table">Paramètre table.</param>
    /// <returns>Résultat du traitement.</returns>
    private static ColumnDefinition? FindBridgeColumnForTable(TableDefinition bridge, TableDefinition table)
    {
        string[] stems = TableNameStems(table.FullName).Concat(TableNameStems(table.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string? stem in stems)
        {
            ColumnDefinition? direct = bridge.Columns.FirstOrDefault(c => IsColumnForTable(c.Name, stem));
            if (direct is not null)
            {
                return direct;
            }
        }

        return bridge.Columns
            .Where(c => IsForeignKeyLike(SqlNameNormalizer.Normalize(c.Name)))
            .FirstOrDefault(c => stems.Any(stem => Singularize(SqlNameNormalizer.Normalize(c.Name)).Contains(stem + "_", StringComparison.OrdinalIgnoreCase)
                || Singularize(SqlNameNormalizer.Normalize(c.Name)).StartsWith(stem, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Exécute le traitement FindBestKeyColumnForBridge.
    /// </summary>
    /// <param name="table">Paramètre table.</param>
    /// <param name="bridgeColumn">Paramètre bridgeColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static ColumnDefinition? FindBestKeyColumnForBridge(TableDefinition table, ColumnDefinition bridgeColumn)
    {
        string tableStem = TableStem(table.FullName);
        string bridgeNorm = Singularize(SqlNameNormalizer.Normalize(bridgeColumn.Name));

        var candidates = table.Columns
            .Select(c => new { Column = c, Score = CandidateKeyScore(c, tableStem, bridgeNorm) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Column.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.FirstOrDefault()?.Column;
    }

    /// <summary>
    /// Exécute le traitement CandidateKeyScore.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="tableStem">Paramètre tableStem.</param>
    /// <param name="bridgeColumnNorm">Paramètre bridgeColumnNorm.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double CandidateKeyScore(ColumnDefinition column, string tableStem, string bridgeColumnNorm)
    {
        string col = Singularize(SqlNameNormalizer.Normalize(column.Name));
        double score = 0.0;
        if (column.IsPrimaryKey)
        {
            score += 3.0;
        }

        if (IsGenericIdentifier(col))
        {
            score += 2.5;
        }

        if (col == bridgeColumnNorm)
        {
            score += 2.1;
        }

        if (col == $"{tableStem}_id" || col == $"{tableStem}_iden" || col == $"{tableStem}_ident" || col == $"{tableStem}_code")
        {
            score += 1.9;
        }

        if (col == "code" || col == $"base_{tableStem}_code")
        {
            score += 1.0;
        }

        return score;
    }

    /// <summary>
    /// Exécute le traitement KeyColumnScore.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="table">Paramètre table.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double KeyColumnScore(ColumnDefinition column, TableDefinition table)
    {
        string col = Singularize(SqlNameNormalizer.Normalize(column.Name));
        double score = 0.0;
        if (column.IsPrimaryKey) score += 0.45;
        if (IsGenericIdentifier(col)) score += 0.35;
        if (col == $"{TableStem(table.FullName)}_id") score += 0.15;
        return score;
    }

    /// <summary>
    /// Exécute le traitement BridgeColumnScore.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double BridgeColumnScore(ColumnDefinition column, TableDefinition targetTable)
    {
        IEnumerable<string> stems = TableNameStems(targetTable.FullName).Concat(TableNameStems(targetTable.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
        return stems.Any(stem => IsColumnForTable(column.Name, stem)) ? 0.35 : 0.0;
    }

    /// <summary>
    /// Exécute le traitement FindCandidatePaths.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <param name="maxDepth">Paramètre maxDepth.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IEnumerable<List<JoinDefinition>> FindCandidatePaths(DatabaseSchema schema, string startTable, string targetTable, int maxDepth, IEnumerable<string> disabledAutoJoinKeys)
    {
        List<List<JoinDefinition>> results = [];
        Queue<(string Table, List<JoinDefinition> Path, HashSet<string> Visited)> queue = new();
        queue.Enqueue((startTable, new List<JoinDefinition>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startTable }));

        IReadOnlyDictionary<string, InferredRelationship[]> adjacency = BuildRelationshipAdjacency(schema, disabledAutoJoinKeys);

        while (queue.Count > 0 && results.Count < 128)
        {
            (string Table, List<JoinDefinition> Path, HashSet<string> Visited) current = queue.Dequeue();
            if (current.Path.Count >= maxDepth)
            {
                continue;
            }

            if (!adjacency.TryGetValue(SqlNameNormalizer.Normalize(current.Table), out InferredRelationship[]? edges))
            {
                continue;
            }

            foreach (InferredRelationship edge in edges)
            {
                string nextTable = SqlNameNormalizer.EqualsName(edge.FromTable, current.Table) ? edge.ToTable : edge.FromTable;
                if (current.Visited.Contains(nextTable) && !SqlNameNormalizer.EqualsName(nextTable, targetTable))
                {
                    continue;
                }

                JoinDefinition oriented = SqlNameNormalizer.EqualsName(edge.FromTable, current.Table)
                    ? new JoinDefinition
                    {
                        FromTable = edge.FromTable,
                        FromColumn = edge.FromColumn,
                        ToTable = edge.ToTable,
                        ToColumn = edge.ToColumn,
                        JoinType = JoinType.Inner,
                        AutoInferred = true
                    }
                    : new JoinDefinition
                    {
                        FromTable = edge.ToTable,
                        FromColumn = edge.ToColumn,
                        ToTable = edge.FromTable,
                        ToColumn = edge.FromColumn,
                        JoinType = JoinType.Inner,
                        AutoInferred = true
                    };

                if (IsJoinDisabled(oriented, disabledAutoJoinKeys) || IsBadFkToFkHop(schema, oriented, targetTable))
                {
                    continue;
                }

                List<JoinDefinition> nextPath = current.Path.Concat(new[] { oriented }).ToList();
                if (SqlNameNormalizer.EqualsName(nextTable, targetTable))
                {
                    results.Add(nextPath);
                    continue;
                }

                HashSet<string> visited = new(current.Visited, StringComparer.OrdinalIgnoreCase) { nextTable };
                queue.Enqueue((nextTable, nextPath, visited));
            }
        }

        return results;
    }

    /// <summary>
    /// Exécute le traitement BuildRelationshipAdjacency.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IReadOnlyDictionary<string, InferredRelationship[]> BuildRelationshipAdjacency(DatabaseSchema schema, IEnumerable<string> disabledAutoJoinKeys)
    {
        ISet<string> disabled = disabledAutoJoinKeys as ISet<string> ?? disabledAutoJoinKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<InferredRelationship>> dict = new(StringComparer.OrdinalIgnoreCase);

        foreach (InferredRelationship relationship in schema.Relationships)
        {
            if (disabled.Contains(relationship.Key) || disabled.Contains(relationship.ReverseKey))
            {
                continue;
            }

            Add(relationship.FromTable, relationship);
            Add(relationship.ToTable, relationship);
        }

        return dict.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderByDescending(r => r.Confidence)
                .Take(96)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

        void Add(string table, InferredRelationship relationship)
        {
            string key = SqlNameNormalizer.Normalize(table);
            if (!dict.TryGetValue(key, out List<InferredRelationship>? list))
            {
                list = [];
                dict[key] = list;
            }

            list.Add(relationship);
        }
    }

    /// <summary>
    /// Exécute le traitement IsBadFkToFkHop.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="join">Paramètre join.</param>
    /// <param name="finalTargetTable">Paramètre finalTargetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsBadFkToFkHop(DatabaseSchema schema, JoinDefinition join, string finalTargetTable)
    {
        ColumnDefinition? fromCol = schema.FindColumn(join.FromTable, join.FromColumn);
        ColumnDefinition? toCol = schema.FindColumn(join.ToTable, join.ToColumn);
        string fromName = SqlNameNormalizer.Normalize(join.FromColumn);
        string toName = SqlNameNormalizer.Normalize(join.ToColumn);
        bool bothFkLike = IsForeignKeyLike(fromName) && IsForeignKeyLike(toName);
        if (!bothFkLike)
        {
            return false;
        }

        bool touchesFinalTarget = SqlNameNormalizer.EqualsName(join.FromTable, finalTargetTable) || SqlNameNormalizer.EqualsName(join.ToTable, finalTargetTable);
        if (touchesFinalTarget)
        {
            return false;
        }

        return fromCol?.IsPrimaryKey != true && toCol?.IsPrimaryKey != true;
    }

    /// <summary>
    /// Exécute le traitement ScoreJoinPath.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="path">Paramètre path.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double ScoreJoinPath(DatabaseSchema schema, IReadOnlyList<JoinDefinition> path, string startTable, string targetTable)
    {
        double score = path.Sum(j => RelationshipScore(schema, j));

        // A shorter path is usually easier to understand and cheaper.
        score -= Math.Max(0, path.Count - 1) * 0.45;

        if (path.Count == 2)
        {
            string bridge = path[0].ToTable;
            if (SqlNameNormalizer.EqualsName(bridge, path[1].FromTable))
            {
                score += JunctionBridgeScore(schema, startTable, bridge, targetTable);
            }
        }

        // Very long semantic detours are almost always wrong in this UI. The user can add
        // them manually if needed.
        score -= Math.Max(0, path.Count - 2) * 2.00;
        return score;
    }

    /// <summary>
    /// Exécute le traitement RelationshipScore.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="join">Paramètre join.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double RelationshipScore(DatabaseSchema schema, JoinDefinition join)
    {
        InferredRelationship? relationship = FindRelationship(schema, join);
        double score = relationship?.Confidence ?? 0.45;

        if (relationship is not null)
        {
            score += relationship.Source switch
            {
                RelationshipSource.DeclaredForeignKey => 0.35,
                RelationshipSource.TableNameColumnPattern => 0.18,
                RelationshipSource.CompositeTablePattern => 0.08,
                RelationshipSource.SameColumnPrimaryKey => 0.02,
                RelationshipSource.SameColumnName => -0.55,
                RelationshipSource.CommentSimilarity => -0.12,
                _ => 0.0
            };
        }

        ColumnDefinition? fromCol = schema.FindColumn(join.FromTable, join.FromColumn);
        ColumnDefinition? toCol = schema.FindColumn(join.ToTable, join.ToColumn);
        string fromName = SqlNameNormalizer.Normalize(join.FromColumn);
        string toName = SqlNameNormalizer.Normalize(join.ToColumn);

        // Bad smell: local generic ID matched to a non-PK business/code column.
        // Example observed: pnj_jobs_items_focus.id = items.base_item_code.
        if (IsGenericIdentifier(fromName) && toCol?.IsPrimaryKey != true && !IsGenericIdentifier(toName))
        {
            score -= 0.90;
        }

        if (IsGenericIdentifier(toName) && fromCol?.IsPrimaryKey != true && !IsGenericIdentifier(fromName))
        {
            score -= 0.65;
        }

        if (IsForeignKeyLike(fromName) && IsForeignKeyLike(toName) && fromCol?.IsPrimaryKey != true && toCol?.IsPrimaryKey != true)
        {
            score -= 0.80;
        }

        if ((fromCol?.IsPrimaryKey == true && IsForeignKeyLike(toName)) || (toCol?.IsPrimaryKey == true && IsForeignKeyLike(fromName)))
        {
            score += 0.15;
        }

        if (schema.IsColumnIndexed(join.FromTable, join.FromColumn))
        {
            score += IsForeignKeyLike(fromName) ? 0.08 : 0.03;
        }

        if (schema.IsColumnUniqueIndexed(join.ToTable, join.ToColumn) || toCol?.IsPrimaryKey == true)
        {
            score += 0.09;
        }
        else if (schema.IsColumnIndexed(join.ToTable, join.ToColumn))
        {
            score += 0.04;
        }

        return score;
    }

    /// <summary>
    /// Exécute le traitement FindRelationship.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="join">Paramètre join.</param>
    /// <returns>Résultat du traitement.</returns>
    private static InferredRelationship? FindRelationship(DatabaseSchema schema, JoinDefinition join)
    {
        return schema.Relationships.FirstOrDefault(r =>
            SqlNameNormalizer.EqualsName(r.FromTable, join.FromTable)
            && SqlNameNormalizer.EqualsName(r.FromColumn, join.FromColumn)
            && SqlNameNormalizer.EqualsName(r.ToTable, join.ToTable)
            && SqlNameNormalizer.EqualsName(r.ToColumn, join.ToColumn))
            ?? schema.Relationships.FirstOrDefault(r =>
                SqlNameNormalizer.EqualsName(r.FromTable, join.ToTable)
                && SqlNameNormalizer.EqualsName(r.FromColumn, join.ToColumn)
                && SqlNameNormalizer.EqualsName(r.ToTable, join.FromTable)
                && SqlNameNormalizer.EqualsName(r.ToColumn, join.FromColumn));
    }

    /// <summary>
    /// Exécute le traitement RelationshipConfidence.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="join">Paramètre join.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double RelationshipConfidence(DatabaseSchema schema, JoinDefinition join)
    {
        return FindRelationship(schema, join)?.Confidence ?? 0.50;
    }

    /// <summary>
    /// Exécute le traitement JunctionBridgeScore.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="startTable">Paramètre startTable.</param>
    /// <param name="bridgeTable">Paramètre bridgeTable.</param>
    /// <param name="targetTable">Paramètre targetTable.</param>
    /// <returns>Résultat du traitement.</returns>
    private static double JunctionBridgeScore(DatabaseSchema schema, string startTable, string bridgeTable, string targetTable)
    {
        TableDefinition? bridge = schema.FindTable(bridgeTable);
        if (bridge is null)
        {
            return 0.0;
        }

        string startStem = TableStem(startTable);
        string targetStem = TableStem(targetTable);
        string bridgeNorm = SqlNameNormalizer.Normalize(bridgeTable);
        string bridgeSingular = Singularize(bridgeNorm);
        string[] tokens = bridgeSingular.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();

        double score = 0.0;
        string exactA = $"{startStem}_{targetStem}";
        string exactB = $"{targetStem}_{startStem}";
        if (bridgeSingular.Equals(exactA, StringComparison.OrdinalIgnoreCase) || bridgeSingular.Equals(exactB, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.25;
        }
        else
        {
            bool containsStart = tokens.Any(t => SameNameRelaxed(t, startStem));
            bool containsTarget = tokens.Any(t => SameNameRelaxed(t, targetStem));
            if (containsStart && containsTarget)
            {
                score += 0.55;
                int extraTokens = tokens.Count(t => !SameNameRelaxed(t, startStem) && !SameNameRelaxed(t, targetStem));
                score -= Math.Min(0.60, extraTokens * 0.12);
            }
        }

        if (bridge.Columns.Any(c => IsColumnForTable(c.Name, startStem)))
        {
            score += 0.45;
        }

        if (bridge.Columns.Any(c => IsColumnForTable(c.Name, targetStem)))
        {
            score += 0.45;
        }

        if (bridge.Columns.Count <= 4)
        {
            score += 0.15;
        }

        string[] suspiciousTokens = new[] { "focus", "tmp", "temp", "staging", "archive", "history", "historique", "log", "audit", "backup", "old", "lineage", "quest", "quests" };
        if (tokens.Any(t => suspiciousTokens.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            score -= 0.95;
        }

        return score;
    }

    /// <summary>
    /// Exécute le traitement IsColumnForTable.
    /// </summary>
    /// <param name="columnName">Paramètre columnName.</param>
    /// <param name="tableStem">Paramètre tableStem.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsColumnForTable(string columnName, string tableStem)
    {
        string normalized = Singularize(SqlNameNormalizer.Normalize(columnName));
        return normalized == $"{tableStem}_id"
            || normalized == $"{tableStem}_iden"
            || normalized == $"{tableStem}_ident"
            || normalized == $"{tableStem}_code"
            || normalized == $"id_{tableStem}";
    }

    /// <summary>
    /// Exécute le traitement IsForeignKeyLike.
    /// </summary>
    /// <param name="normalizedColumnName">Paramètre normalizedColumnName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsForeignKeyLike(string normalizedColumnName)
    {
        return normalizedColumnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_IDEN", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_IDENT", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_CODE", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.StartsWith("ID_", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.StartsWith("FK_", StringComparison.OrdinalIgnoreCase);
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
    /// Exécute le traitement TableStem.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string TableStem(string tableName)
    {
        string normalized = SqlNameNormalizer.Normalize(tableName).Trim('_');
        if (normalized.Length == 0)
        {
            return normalized;
        }

        string[] tokens = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1)
        {
            return Singularize(tokens[0]);
        }

        return Singularize(tokens[^1]);
    }

    /// <summary>
    /// Exécute le traitement TableNameStems.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static IEnumerable<string> TableNameStems(string tableName)
    {
        string normalized = SqlNameNormalizer.Normalize(tableName).Trim('_');
        if (normalized.Length == 0)
        {
            yield break;
        }

        yield return Singularize(normalized);
        string[] tokens = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string token in tokens)
        {
            yield return Singularize(token);
        }
    }

    /// <summary>
    /// Exécute le traitement Singularize.
    /// </summary>
    /// <param name="normalizedName">Paramètre normalizedName.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string Singularize(string normalizedName)
    {
        IEnumerable<string> parts = normalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SingularizeToken);
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

        if (token.EndsWith("IES", StringComparison.OrdinalIgnoreCase) && token.Length > 4)
        {
            return token[..^3] + "Y";
        }

        if ((token.EndsWith("S", StringComparison.OrdinalIgnoreCase) || token.EndsWith("X", StringComparison.OrdinalIgnoreCase)) && token.Length > 3)
        {
            return token[..^1];
        }

        return token;
    }

    /// <summary>
    /// Exécute le traitement SameNameRelaxed.
    /// </summary>
    /// <param name="left">Paramètre left.</param>
    /// <param name="right">Paramètre right.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool SameNameRelaxed(string left, string right)
    {
        string a = Singularize(SqlNameNormalizer.Normalize(left));
        string b = Singularize(SqlNameNormalizer.Normalize(right));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement JoinKey.
    /// </summary>
    /// <param name="fromTable">Paramètre fromTable.</param>
    /// <param name="fromColumn">Paramètre fromColumn.</param>
    /// <param name="toTable">Paramètre toTable.</param>
    /// <param name="toColumn">Paramètre toColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string JoinKey(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return string.Join("|",
            NormalizeKeyPart(fromTable),
            NormalizeKeyPart(fromColumn),
            NormalizeKeyPart(toTable),
            NormalizeKeyPart(toColumn));
    }

    /// <summary>
    /// Exécute le traitement NormalizeKeyPart.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Exécute le traitement IsRelationshipDisabled.
    /// </summary>
    /// <param name="relationship">Paramètre relationship.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsRelationshipDisabled(InferredRelationship relationship, IEnumerable<string> disabledAutoJoinKeys)
    {
        ISet<string> disabled = disabledAutoJoinKeys as ISet<string> ?? disabledAutoJoinKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return disabled.Contains(relationship.Key) || disabled.Contains(relationship.ReverseKey);
    }

    /// <summary>
    /// Exécute le traitement IsJoinDisabled.
    /// </summary>
    /// <param name="join">Paramètre join.</param>
    /// <param name="disabledAutoJoinKeys">Paramètre disabledAutoJoinKeys.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool IsJoinDisabled(JoinDefinition join, IEnumerable<string> disabledAutoJoinKeys)
    {
        ISet<string> disabled = disabledAutoJoinKeys as ISet<string> ?? disabledAutoJoinKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string key = RelationshipKey.For(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
        string reverseKey = RelationshipKey.ReverseFor(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
        return disabled.Contains(key) || disabled.Contains(reverseKey);
    }

    /// <summary>
    /// Exécute le traitement BuildSelectItems.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<string> BuildSelectItems(QueryDefinition query, SqlGeneratorOptions options, List<string> warnings)
    {
        List<string> result = [.. query.SelectedColumns.Select(c => ColumnSql(c, options, includeAlias: true))];

        foreach (AggregateSelection aggregate in query.Aggregates)
        {
            string item = BuildAggregateExpression(aggregate, options, warnings);
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(aggregate.Alias))
            {
                item += " AS " + Q(aggregate.Alias, options);
            }
            result.Add(item);
        }

        foreach (CustomColumnSelection custom in query.CustomColumns)
        {
            string expression = BuildCustomExpression(custom, options);
            if (string.IsNullOrWhiteSpace(expression))
            {
                warnings.Add("Colonne personnalisée ignorée: expression vide.");
                continue;
            }
            SqlSafety.EnsureSelectExpressionIsSafe(expression);
            if (!string.IsNullOrWhiteSpace(custom.Alias))
            {
                expression += " AS " + Q(custom.Alias, options);
            }
            result.Add(expression);
        }

        return result;
    }

    /// <summary>
    /// Exécute le traitement BuildAggregateExpression.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildAggregateExpression(AggregateSelection aggregate, SqlGeneratorOptions options, List<string> warnings)
    {
        string fn = aggregate.Function switch
        {
            AggregateFunction.Count => "COUNT",
            AggregateFunction.Sum => "SUM",
            AggregateFunction.Average => "AVG",
            AggregateFunction.Minimum => "MIN",
            AggregateFunction.Maximum => "MAX",
            _ => "COUNT"
        };

        string target = aggregate.Column is null ? "*" : ColumnSql(aggregate.Column, options, includeAlias: false);
        string condition = BuildAggregateCondition(aggregate, options, warnings);
        if (string.IsNullOrWhiteSpace(condition))
        {
            if (aggregate.Distinct && aggregate.Column is not null)
            {
                target = "DISTINCT " + target;
            }

            return $"{fn}({target})";
        }

        if (aggregate.Function == AggregateFunction.Count)
        {
            if (aggregate.Distinct && aggregate.Column is not null)
            {
                return $"COUNT(DISTINCT CASE WHEN {condition} THEN {target} END)";
            }

            string countTarget = aggregate.Column is null ? "1" : target;
            return $"COUNT(CASE WHEN {condition} THEN {countTarget} END)";
        }

        if (aggregate.Column is null)
        {
            warnings.Add($"Agrégat conditionnel {fn} ignoré: choisis une colonne cible. Utilise COUNT pour compter des lignes.");
            return string.Empty;
        }

        string caseExpression = aggregate.Function == AggregateFunction.Sum
            ? $"CASE WHEN {condition} THEN {target} ELSE 0 END"
            : $"CASE WHEN {condition} THEN {target} END";

        if (aggregate.Distinct)
        {
            return $"{fn}(DISTINCT {caseExpression})";
        }

        return $"{fn}({caseExpression})";
    }

    /// <summary>
    /// Exécute le traitement BuildAggregateCondition.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildAggregateCondition(AggregateSelection aggregate, SqlGeneratorOptions options, List<string> warnings)
    {
        if (aggregate.ConditionColumn is null)
        {
            if (!string.IsNullOrWhiteSpace(aggregate.ConditionValue))
            {
                warnings.Add("Condition d'agrégat ignorée: aucune colonne de condition n'a été renseignée.");
            }
            return string.Empty;
        }

        string op = NormalizeOperator(aggregate.ConditionOperator);
        string column = ColumnSql(aggregate.ConditionColumn, options, includeAlias: false);
        return BuildLiteralPredicateSql(column, op, aggregate.ConditionValue, aggregate.ConditionSecondValue, aggregate.ConditionColumn.Key, warnings);
    }

    /// <summary>
    /// Exécute le traitement BuildCustomExpression.
    /// </summary>
    /// <param name="custom">Paramètre custom.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildCustomExpression(CustomColumnSelection custom, SqlGeneratorOptions options)
    {
        if (!string.IsNullOrWhiteSpace(custom.RawExpression))
        {
            return custom.RawExpression.Trim();
        }

        if (custom.CaseColumn is not null && !string.IsNullOrWhiteSpace(custom.CaseOperator))
        {
            string op = NormalizeOperator(custom.CaseOperator);
            string column = ColumnSql(custom.CaseColumn, options, includeAlias: false);
            string compare = op is "IS NULL" or "IS NOT NULL" ? string.Empty : " " + SqlLiteralFormatter.FormatValue(custom.CaseCompareValue);
            return $"CASE WHEN {column} {op}{compare} THEN {SqlLiteralFormatter.FormatValue(custom.CaseThenValue)} ELSE {SqlLiteralFormatter.FormatValue(custom.CaseElseValue)} END";
        }

        return string.Empty;
    }

    /// <summary>
    /// Exécute le traitement BuildFilterPredicates.
    /// </summary>
    /// <param name="Predicate">Paramètre Predicate.</param>
    /// <param name="query">Paramètre query.</param>
    /// <param name="filters">Paramètre filters.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<(string Predicate, LogicalConnector Connector)> BuildFilterPredicates(QueryDefinition query, IReadOnlyList<FilterCondition> filters, DatabaseSchema schema, SqlGeneratorOptions options, List<string> warnings)
    {
        List<(string Predicate, LogicalConnector Connector)> result = [];
        foreach (FilterCondition filter in filters)
        {
            string op = NormalizeOperator(filter.Operator);
            string expression = op is "EXISTS" or "NOT EXISTS" && filter.ValueKind == FilterValueKind.Subquery
                ? string.Empty
                : BuildFilterExpression(query, filter, options, warnings);
            if (string.IsNullOrWhiteSpace(expression) && op is not "EXISTS" and not "NOT EXISTS")
            {
                continue;
            }

            string warningKey = filter.Column?.Key ?? filter.FieldAlias ?? expression;
            string predicate = BuildPredicateSql(expression, op, filter, schema, options, warningKey, warnings);
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                result.Add((predicate, filter.Connector));
            }
        }
        return result;
    }

    /// <summary>
    /// Exécute le traitement AppendPredicateBlock.
    /// </summary>
    /// <param name="sb">Paramètre sb.</param>
    /// <param name="keyword">Paramètre keyword.</param>
    /// <param name="predicates">Paramètre predicates.</param>
    private static void AppendPredicateBlock(StringBuilder sb, string keyword, IReadOnlyList<(string Predicate, LogicalConnector Connector)> predicates)
    {
        if (predicates.Count == 0)
        {
            return;
        }

        sb.Append(keyword).Append(' ').AppendLine(predicates[0].Predicate);
        for (int i = 1; i < predicates.Count; i++)
        {
            sb.Append(predicates[i].Connector == LogicalConnector.Or ? "   OR " : "  AND ").AppendLine(predicates[i].Predicate);
        }
    }

    /// <summary>
    /// Exécute le traitement BuildFilterExpression.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="filter">Paramètre filter.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildFilterExpression(QueryDefinition query, FilterCondition filter, SqlGeneratorOptions options, List<string> warnings)
    {
        if (filter.Column is not null)
        {
            return ColumnSql(filter.Column, options, includeAlias: false);
        }

        if (string.IsNullOrWhiteSpace(filter.FieldAlias))
        {
            warnings.Add("Filtre ignoré: aucune colonne, agrégat ou colonne calculée n'a été choisi.");
            return string.Empty;
        }

        return filter.FieldKind switch
        {
            QueryFieldKind.Aggregate => ResolveAggregateExpressionByAlias(query, filter.FieldAlias!, options, warnings),
            QueryFieldKind.CustomColumn => ResolveCustomExpressionByAlias(query, filter.FieldAlias!, options, warnings),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Exécute le traitement BuildOrderExpression.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="order">Paramètre order.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildOrderExpression(QueryDefinition query, OrderByItem order, SqlGeneratorOptions options, List<string> warnings)
    {
        if (order.Column is not null)
        {
            return ColumnSql(order.Column, options, includeAlias: false);
        }

        if (string.IsNullOrWhiteSpace(order.FieldAlias))
        {
            warnings.Add("Tri ignoré: aucune colonne, agrégat ou colonne calculée n'a été choisi.");
            return string.Empty;
        }

        // ORDER BY alias is supported by SQLite, Oracle, PostgreSQL, SQL Server, etc. It keeps
        // the generated query readable and avoids duplicating long CASE/aggregate expressions.
        return Q(order.FieldAlias!, options);
    }

    /// <summary>
    /// Exécute le traitement ResolveAggregateExpressionByAlias.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="alias">Paramètre alias.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string ResolveAggregateExpressionByAlias(QueryDefinition query, string alias, SqlGeneratorOptions options, List<string> warnings)
    {
        AggregateSelection? aggregate = query.Aggregates.FirstOrDefault(a => string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (aggregate is null)
        {
            warnings.Add($"Filtre d'agrégat ignoré: l'alias '{alias}' n'existe plus.");
            return string.Empty;
        }

        return BuildAggregateExpression(aggregate, options, warnings);
    }

    /// <summary>
    /// Exécute le traitement ResolveCustomExpressionByAlias.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="alias">Paramètre alias.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string ResolveCustomExpressionByAlias(QueryDefinition query, string alias, SqlGeneratorOptions options, List<string> warnings)
    {
        CustomColumnSelection? custom = query.CustomColumns.FirstOrDefault(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (custom is null)
        {
            warnings.Add($"Filtre sur colonne calculée ignoré: l'alias '{alias}' n'existe plus.");
            return string.Empty;
        }

        string expression = BuildCustomExpression(custom, options);
        if (string.IsNullOrWhiteSpace(expression))
        {
            warnings.Add($"Filtre sur colonne calculée ignoré: l'alias '{alias}' n'a pas d'expression utilisable.");
            return string.Empty;
        }

        SqlSafety.EnsureSelectExpressionIsSafe(expression);
        return expression;
    }


    /// <summary>
    /// Exécute le traitement BuildLiteralPredicateSql.
    /// </summary>
    /// <param name="columnSql">Paramètre columnSql.</param>
    /// <param name="op">Paramètre op.</param>
    /// <param name="value">Paramètre value.</param>
    /// <param name="secondValue">Paramètre secondValue.</param>
    /// <param name="warningKey">Paramètre warningKey.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildLiteralPredicateSql(string columnSql, string op, string? value, string? secondValue, string warningKey, List<string> warnings)
    {
        if (op is "IS NULL" or "IS NOT NULL")
        {
            return $"{columnSql} {op}";
        }

        if (op == "BETWEEN")
        {
            return $"{columnSql} BETWEEN {SqlLiteralFormatter.FormatValue(value)} AND {SqlLiteralFormatter.FormatValue(secondValue)}";
        }

        if (op is "IN" or "NOT IN")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                warnings.Add($"Filtre IN ignoré sur {warningKey}: valeur vide.");
                return string.Empty;
            }
            return $"{columnSql} {op} {SqlLiteralFormatter.FormatRawList(value)}";
        }

        return $"{columnSql} {op} {SqlLiteralFormatter.FormatValue(value)}";
    }

    /// <summary>
    /// Exécute le traitement BuildPredicateSql.
    /// </summary>
    /// <param name="columnSql">Paramètre columnSql.</param>
    /// <param name="op">Paramètre op.</param>
    /// <param name="filter">Paramètre filter.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warningKey">Paramètre warningKey.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildPredicateSql(string columnSql, string op, FilterCondition filter, DatabaseSchema schema, SqlGeneratorOptions options, string warningKey, List<string> warnings)
    {
        if (op is "IS NULL" or "IS NOT NULL")
        {
            return $"{columnSql} {op}";
        }

        if (filter.ValueKind == FilterValueKind.Subquery)
        {
            string subquerySql = BuildSubquerySql(filter, schema, options, warnings);
            if (string.IsNullOrWhiteSpace(subquerySql))
            {
                warnings.Add($"Filtre sous-requête ignoré sur {warningKey}: sous-requête vide ou invalide.");
                return string.Empty;
            }

            if (op is "EXISTS" or "NOT EXISTS")
            {
                return $"{op} ({subquerySql})";
            }

            return $"{columnSql} {op} ({subquerySql})";
        }

        string valueSql = FormatFilterValue(filter.Value, filter.ValueKind, warnings);
        string secondValueSql = FormatFilterValue(filter.SecondValue, filter.ValueKind, warnings);

        if (op == "BETWEEN")
        {
            return $"{columnSql} BETWEEN {valueSql} AND {secondValueSql}";
        }

        if (op is "IN" or "NOT IN")
        {
            if (string.IsNullOrWhiteSpace(filter.Value))
            {
                warnings.Add($"Filtre IN ignoré sur {warningKey}: valeur vide.");
                return string.Empty;
            }

            if (filter.ValueKind == FilterValueKind.RawSql)
            {
                string raw = filter.Value!.Trim();
                SqlSafety.EnsureSelectExpressionIsSafe(raw);
                return $"{columnSql} {op} {raw}";
            }

            return $"{columnSql} {op} {SqlLiteralFormatter.FormatRawList(filter.Value!)}";
        }

        return $"{columnSql} {op} {valueSql}";
    }

    /// <summary>
    /// Exécute le traitement FormatFilterValue.
    /// </summary>
    /// <param name="raw">Paramètre raw.</param>
    /// <param name="valueKind">Paramètre valueKind.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string FormatFilterValue(string? raw, FilterValueKind valueKind, List<string> warnings)
    {
        if (valueKind == FilterValueKind.RawSql)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "NULL";
            }

            string trimmed = raw.Trim();
            SqlSafety.EnsureSelectExpressionIsSafe(trimmed);
            return trimmed;
        }

        if (valueKind == FilterValueKind.Parameter)
        {
            string placeholder = string.IsNullOrWhiteSpace(raw) ? "?" : raw.Trim();
            if (!placeholder.StartsWith(':') && !placeholder.StartsWith('@') && !placeholder.StartsWith('?'))
            {
                placeholder = ":" + placeholder;
            }
            return placeholder;
        }

        return SqlLiteralFormatter.FormatValue(raw);
    }

    /// <summary>
    /// Exécute le traitement BuildSubquerySql.
    /// </summary>
    /// <param name="filter">Paramètre filter.</param>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="warnings">Paramètre warnings.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string BuildSubquerySql(FilterCondition filter, DatabaseSchema schema, SqlGeneratorOptions options, List<string> warnings)
    {
        if (filter.Subquery is null)
        {
            return string.Empty;
        }

        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(filter.Subquery, schema, options);
        foreach (string warning in result.Warnings)
        {
            warnings.Add($"Sous-requête {filter.SubqueryName ?? filter.Subquery.Name ?? string.Empty}: {warning}");
        }

        return IndentSql(result.Sql.Trim().TrimEnd(';'), 4);
    }

    /// <summary>
    /// Exécute le traitement IndentSql.
    /// </summary>
    /// <param name="sql">Paramètre sql.</param>
    /// <param name="spaces">Paramètre spaces.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string IndentSql(string sql, int spaces)
    {
        string prefix = new(' ', spaces);
        return string.Join(Environment.NewLine, sql.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(line => prefix + line));
    }

    /// <summary>
    /// Exécute le traitement BuildGroupBy.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<string> BuildGroupBy(QueryDefinition query, SqlGeneratorOptions options)
    {
        return query.GroupBy.Select(c => ColumnSql(c, options, includeAlias: false)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Exécute le traitement NormalizeOperator.
    /// </summary>
    /// <param name="op">Paramètre op.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string NormalizeOperator(string? op)
    {
        string value = string.IsNullOrWhiteSpace(op) ? "=" : op.Trim().ToUpperInvariant();
        return value switch
        {
            "==" => "=",
            "!=" => "<>",
            "ISNULL" => "IS NULL",
            "ISNOTNULL" => "IS NOT NULL",
            "NOTEXISTS" => "NOT EXISTS",
            _ => value
        };
    }

    /// <summary>
    /// Exécute le traitement ColumnSql.
    /// </summary>
    /// <param name="reference">Paramètre reference.</param>
    /// <param name="options">Paramètre options.</param>
    /// <param name="includeAlias">Paramètre includeAlias.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string ColumnSql(ColumnReference reference, SqlGeneratorOptions options, bool includeAlias)
    {
        string sql = $"{Q(reference.Table, options)}.{Q(reference.Column, options)}";
        if (includeAlias && !string.IsNullOrWhiteSpace(reference.Alias))
        {
            sql += " AS " + Q(reference.Alias, options);
        }
        return sql;
    }

    /// <summary>
    /// Exécute le traitement Q.
    /// </summary>
    /// <param name="identifier">Paramètre identifier.</param>
    /// <param name="options">Paramètre options.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string Q(string identifier, SqlGeneratorOptions options)
    {
        return SqlIdentifierQuoter.Quote(identifier, options.Dialect, options.QuoteIdentifiers);
    }
}
