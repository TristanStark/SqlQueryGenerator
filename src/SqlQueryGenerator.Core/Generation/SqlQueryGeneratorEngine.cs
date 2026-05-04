using System.Text;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Generation;

public sealed class SqlQueryGeneratorEngine
{
    public SqlGenerationResult Generate(QueryDefinition query, DatabaseSchema schema, SqlGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);
        options ??= new SqlGeneratorOptions();

        var warnings = new List<string>();
        var baseTable = ResolveBaseTable(query, warnings);
        if (baseTable is null)
        {
            return new SqlGenerationResult { Sql = "-- Sélectionne au moins une colonne ou une table de départ.", Warnings = warnings };
        }

        var usedTables = CollectUsedTables(query);
        usedTables.Add(baseTable);
        var joins = BuildJoinPlan(query, schema, baseTable, usedTables, warnings);

        var selectItems = BuildSelectItems(query, options, warnings);
        if (selectItems.Count == 0)
        {
            selectItems.Add("*");
            warnings.Add("Aucune colonne sélectionnée: SELECT * généré par défaut.");
        }

        var sb = new StringBuilder();
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

        foreach (var join in joins)
        {
            sb.Append(join.JoinType == JoinType.Left ? "LEFT JOIN " : "INNER JOIN ");
            sb.Append(Q(join.ToTable, options));
            sb.Append(" ON ");
            sb.Append(Q(join.FromTable, options)).Append('.').Append(Q(join.FromColumn, options));
            sb.Append(" = ");
            sb.Append(Q(join.ToTable, options)).Append('.').Append(Q(join.ToColumn, options));
            if (join.AutoInferred && options.EmitOptimizationComments)
            {
                sb.Append(" /* jointure inférée */");
            }
            sb.AppendLine();
        }

        var where = BuildWhere(query, options, warnings);
        if (where.Count > 0)
        {
            sb.Append("WHERE ").AppendLine(where[0]);
            for (var i = 1; i < where.Count; i++)
            {
                var connector = query.Filters.Count > i ? query.Filters[i].Connector : LogicalConnector.And;
                sb.Append(connector == LogicalConnector.Or ? "   OR " : "  AND ").AppendLine(where[i]);
            }
        }

        var groupBy = BuildGroupBy(query, options);
        if (query.Aggregates.Count > 0 && options.AutoGroupSelectedColumnsWhenAggregating)
        {
            foreach (var selected in query.SelectedColumns)
            {
                var expr = ColumnSql(selected, options, includeAlias: false);
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

        if (query.OrderBy.Count > 0)
        {
            var orderItems = query.OrderBy.Select(o => $"{ColumnSql(o.Column, options, includeAlias: false)} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            sb.Append("ORDER BY ").AppendLine(string.Join(", ", orderItems));
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

    private static string? ResolveBaseTable(QueryDefinition query, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(query.BaseTable))
        {
            return query.BaseTable.Trim();
        }

        var first = query.SelectedColumns.FirstOrDefault()?.Table
            ?? query.Filters.FirstOrDefault()?.Column.Table
            ?? query.GroupBy.FirstOrDefault()?.Table
            ?? query.OrderBy.FirstOrDefault()?.Column.Table
            ?? query.Aggregates.FirstOrDefault(a => a.Column is not null)?.Column?.Table
            ?? query.Aggregates.FirstOrDefault(a => a.ConditionColumn is not null)?.ConditionColumn?.Table
            ?? query.CustomColumns.FirstOrDefault(c => c.CaseColumn is not null)?.CaseColumn?.Table;

        if (!string.IsNullOrWhiteSpace(first))
        {
            warnings.Add($"Table de départ non renseignée: {first} utilisée automatiquement.");
        }

        return first;
    }

    private static HashSet<string> CollectUsedTables(QueryDefinition query)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in query.SelectedColumns) result.Add(c.Table);
        foreach (var f in query.Filters) result.Add(f.Column.Table);
        foreach (var g in query.GroupBy) result.Add(g.Table);
        foreach (var o in query.OrderBy) result.Add(o.Column.Table);
        foreach (var a in query.Aggregates.Where(a => a.Column is not null)) result.Add(a.Column!.Table);
        foreach (var a in query.Aggregates.Where(a => a.ConditionColumn is not null)) result.Add(a.ConditionColumn!.Table);
        foreach (var c in query.CustomColumns.Where(c => c.CaseColumn is not null)) result.Add(c.CaseColumn!.Table);
        return result;
    }

    private static IReadOnlyList<JoinDefinition> BuildJoinPlan(QueryDefinition query, DatabaseSchema schema, string baseTable, HashSet<string> usedTables, List<string> warnings)
    {
        var joins = new List<JoinDefinition>();
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseTable };
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddJoin(JoinDefinition join, bool autoInferred)
        {
            var key = JoinKey(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
            if (!emitted.Add(key))
            {
                return;
            }

            joins.Add(join with { AutoInferred = autoInferred });
            connected.Add(join.FromTable);
            connected.Add(join.ToTable);
        }

        foreach (var explicitJoin in query.Joins)
        {
            AddJoin(explicitJoin, autoInferred: false);
        }

        var remaining = usedTables.Where(t => !connected.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safety = 0;
        while (remaining.Count > 0 && safety++ < 256)
        {
            var bestPath = FindBestJoinPath(schema, connected, remaining, query.DisabledAutoJoinKeys);
            if (bestPath.Count == 0)
            {
                foreach (var table in remaining.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"Aucune jointure fiable trouvée depuis {baseTable} vers {table}. La table n'a pas été jointe automatiquement.");
                }
                break;
            }

            foreach (var join in bestPath)
            {
                AddJoin(join, autoInferred: true);
            }

            remaining = usedTables.Where(t => !connected.Contains(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return joins;
    }

    private static List<JoinDefinition> FindBestJoinPath(DatabaseSchema schema, HashSet<string> connected, HashSet<string> remaining, IEnumerable<string> disabledAutoJoinKeys)
    {
        var best = new List<JoinDefinition>();
        var bestScore = double.NegativeInfinity;

        // First-class rule: if a clean junction table exists between a connected table and
        // the requested target table, always prefer that short business path. This prevents
        // graph-search detours such as PNJ -> lineage -> quests -> pnj_item -> items.
        foreach (var start in connected)
        {
            foreach (var target in remaining)
            {
                var bridgePath = TryFindBestDirectJunctionPath(schema, start, target, disabledAutoJoinKeys);
                if (bridgePath.Count > 0)
                {
                    var score = ScoreJoinPath(schema, bridgePath, start, target) + 5.0;
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

        foreach (var start in connected)
        {
            foreach (var target in remaining)
            {
                foreach (var path in FindCandidatePaths(schema, start, target, maxDepth: 2, disabledAutoJoinKeys))
                {
                    if (path.Count == 0)
                    {
                        continue;
                    }

                    var score = ScoreJoinPath(schema, path, start, target);
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
        return bestScore >= 0.70 ? best : new List<JoinDefinition>();
    }

    private static List<JoinDefinition> TryFindBestDirectJunctionPath(DatabaseSchema schema, string startTable, string targetTable, IEnumerable<string> disabledAutoJoinKeys)
    {
        var start = schema.FindTable(startTable);
        var target = schema.FindTable(targetTable);
        if (start is null || target is null)
        {
            return new List<JoinDefinition>();
        }

        var bestScore = double.NegativeInfinity;
        var best = new List<JoinDefinition>();
        foreach (var bridge in schema.Tables)
        {
            if (SqlNameNormalizer.EqualsName(bridge.FullName, startTable)
                || SqlNameNormalizer.EqualsName(bridge.FullName, targetTable))
            {
                continue;
            }

            var bridgeScore = ScoreJunctionTableCandidate(bridge, start, target);
            if (bridgeScore <= 0)
            {
                continue;
            }

            var startBridgeColumn = FindBridgeColumnForTable(bridge, start);
            var targetBridgeColumn = FindBridgeColumnForTable(bridge, target);
            if (startBridgeColumn is null || targetBridgeColumn is null)
            {
                continue;
            }

            var startKey = FindBestKeyColumnForBridge(start, startBridgeColumn);
            var targetKey = FindBestKeyColumnForBridge(target, targetBridgeColumn);
            if (startKey is null || targetKey is null)
            {
                continue;
            }

            if (SqlNameNormalizer.EqualsName(startBridgeColumn.Name, targetBridgeColumn.Name))
            {
                continue;
            }

            var score = bridgeScore;
            score += KeyColumnScore(startKey, start) + KeyColumnScore(targetKey, target);
            score += BridgeColumnScore(startBridgeColumn, start) + BridgeColumnScore(targetBridgeColumn, target);
            score -= Math.Max(0, bridge.Columns.Count - 3) * 0.02;

            var candidate = new List<JoinDefinition>
            {
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
            };

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

        return bestScore >= 1.25 ? best : new List<JoinDefinition>();
    }

    private static double ScoreJunctionTableCandidate(TableDefinition bridge, TableDefinition left, TableDefinition right)
    {
        var leftStem = TableStem(left.FullName);
        var rightStem = TableStem(right.FullName);
        var bridgeNorm = Singularize(SqlNameNormalizer.Normalize(bridge.FullName));
        var bridgeNameOnly = Singularize(SqlNameNormalizer.Normalize(bridge.Name));
        var tokens = bridgeNameOnly.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var score = 0.0;
        var exactA = $"{leftStem}_{rightStem}";
        var exactB = $"{rightStem}_{leftStem}";
        if (bridgeNameOnly.Equals(exactA, StringComparison.OrdinalIgnoreCase)
            || bridgeNameOnly.Equals(exactB, StringComparison.OrdinalIgnoreCase)
            || bridgeNorm.EndsWith("_" + exactA, StringComparison.OrdinalIgnoreCase)
            || bridgeNorm.EndsWith("_" + exactB, StringComparison.OrdinalIgnoreCase))
        {
            score += 2.50;
        }
        else
        {
            var containsLeft = tokens.Any(t => SameNameRelaxed(t, leftStem));
            var containsRight = tokens.Any(t => SameNameRelaxed(t, rightStem));
            if (!containsLeft || !containsRight)
            {
                return 0.0;
            }

            score += 1.10;
            var extraTokens = tokens.Count(t => !SameNameRelaxed(t, leftStem) && !SameNameRelaxed(t, rightStem));
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

        var suspiciousTokens = new[] { "focus", "tmp", "temp", "staging", "archive", "history", "historique", "log", "audit", "backup", "old", "lineage", "quest", "quests" };
        if (tokens.Any(t => suspiciousTokens.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            score -= 1.20;
        }

        return score;
    }

    private static ColumnDefinition? FindBridgeColumnForTable(TableDefinition bridge, TableDefinition table)
    {
        var stems = TableNameStems(table.FullName).Concat(TableNameStems(table.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var stem in stems)
        {
            var direct = bridge.Columns.FirstOrDefault(c => IsColumnForTable(c.Name, stem));
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

    private static ColumnDefinition? FindBestKeyColumnForBridge(TableDefinition table, ColumnDefinition bridgeColumn)
    {
        var tableStem = TableStem(table.FullName);
        var bridgeNorm = Singularize(SqlNameNormalizer.Normalize(bridgeColumn.Name));

        var candidates = table.Columns
            .Select(c => new { Column = c, Score = CandidateKeyScore(c, tableStem, bridgeNorm) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Column.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.FirstOrDefault()?.Column;
    }

    private static double CandidateKeyScore(ColumnDefinition column, string tableStem, string bridgeColumnNorm)
    {
        var col = Singularize(SqlNameNormalizer.Normalize(column.Name));
        var score = 0.0;
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

    private static double KeyColumnScore(ColumnDefinition column, TableDefinition table)
    {
        var col = Singularize(SqlNameNormalizer.Normalize(column.Name));
        var score = 0.0;
        if (column.IsPrimaryKey) score += 0.45;
        if (IsGenericIdentifier(col)) score += 0.35;
        if (col == $"{TableStem(table.FullName)}_id") score += 0.15;
        return score;
    }

    private static double BridgeColumnScore(ColumnDefinition column, TableDefinition targetTable)
    {
        var stems = TableNameStems(targetTable.FullName).Concat(TableNameStems(targetTable.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
        return stems.Any(stem => IsColumnForTable(column.Name, stem)) ? 0.35 : 0.0;
    }

    private static IEnumerable<List<JoinDefinition>> FindCandidatePaths(DatabaseSchema schema, string startTable, string targetTable, int maxDepth, IEnumerable<string> disabledAutoJoinKeys)
    {
        var results = new List<List<JoinDefinition>>();
        var queue = new Queue<(string Table, List<JoinDefinition> Path, HashSet<string> Visited)>();
        queue.Enqueue((startTable, new List<JoinDefinition>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startTable }));

        while (queue.Count > 0 && results.Count < 128)
        {
            var current = queue.Dequeue();
            if (current.Path.Count >= maxDepth)
            {
                continue;
            }

            var edges = schema.Relationships
                .Where(r => !IsRelationshipDisabled(r, disabledAutoJoinKeys))
                .Where(r => SqlNameNormalizer.EqualsName(r.FromTable, current.Table) || SqlNameNormalizer.EqualsName(r.ToTable, current.Table))
                .OrderByDescending(r => r.Confidence)
                .Take(80)
                .ToArray();

            foreach (var edge in edges)
            {
                var nextTable = SqlNameNormalizer.EqualsName(edge.FromTable, current.Table) ? edge.ToTable : edge.FromTable;
                if (current.Visited.Contains(nextTable) && !SqlNameNormalizer.EqualsName(nextTable, targetTable))
                {
                    continue;
                }

                var oriented = SqlNameNormalizer.EqualsName(edge.FromTable, current.Table)
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

                var nextPath = current.Path.Concat(new[] { oriented }).ToList();
                if (SqlNameNormalizer.EqualsName(nextTable, targetTable))
                {
                    results.Add(nextPath);
                    continue;
                }

                var visited = new HashSet<string>(current.Visited, StringComparer.OrdinalIgnoreCase) { nextTable };
                queue.Enqueue((nextTable, nextPath, visited));
            }
        }

        return results;
    }

    private static bool IsBadFkToFkHop(DatabaseSchema schema, JoinDefinition join, string finalTargetTable)
    {
        var fromCol = schema.FindColumn(join.FromTable, join.FromColumn);
        var toCol = schema.FindColumn(join.ToTable, join.ToColumn);
        var fromName = SqlNameNormalizer.Normalize(join.FromColumn);
        var toName = SqlNameNormalizer.Normalize(join.ToColumn);
        var bothFkLike = IsForeignKeyLike(fromName) && IsForeignKeyLike(toName);
        if (!bothFkLike)
        {
            return false;
        }

        var touchesFinalTarget = SqlNameNormalizer.EqualsName(join.FromTable, finalTargetTable) || SqlNameNormalizer.EqualsName(join.ToTable, finalTargetTable);
        if (touchesFinalTarget)
        {
            return false;
        }

        return fromCol?.IsPrimaryKey != true && toCol?.IsPrimaryKey != true;
    }

    private static double ScoreJoinPath(DatabaseSchema schema, IReadOnlyList<JoinDefinition> path, string startTable, string targetTable)
    {
        var score = path.Sum(j => RelationshipScore(schema, j));

        // A shorter path is usually easier to understand and cheaper.
        score -= Math.Max(0, path.Count - 1) * 0.45;

        if (path.Count == 2)
        {
            var bridge = path[0].ToTable;
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

    private static double RelationshipScore(DatabaseSchema schema, JoinDefinition join)
    {
        var relationship = FindRelationship(schema, join);
        var score = relationship?.Confidence ?? 0.45;

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

        var fromCol = schema.FindColumn(join.FromTable, join.FromColumn);
        var toCol = schema.FindColumn(join.ToTable, join.ToColumn);
        var fromName = SqlNameNormalizer.Normalize(join.FromColumn);
        var toName = SqlNameNormalizer.Normalize(join.ToColumn);

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

    private static double RelationshipConfidence(DatabaseSchema schema, JoinDefinition join)
    {
        return FindRelationship(schema, join)?.Confidence ?? 0.50;
    }

    private static double JunctionBridgeScore(DatabaseSchema schema, string startTable, string bridgeTable, string targetTable)
    {
        var bridge = schema.FindTable(bridgeTable);
        if (bridge is null)
        {
            return 0.0;
        }

        var startStem = TableStem(startTable);
        var targetStem = TableStem(targetTable);
        var bridgeNorm = SqlNameNormalizer.Normalize(bridgeTable);
        var bridgeSingular = Singularize(bridgeNorm);
        var tokens = bridgeSingular.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();

        var score = 0.0;
        var exactA = $"{startStem}_{targetStem}";
        var exactB = $"{targetStem}_{startStem}";
        if (bridgeSingular.Equals(exactA, StringComparison.OrdinalIgnoreCase) || bridgeSingular.Equals(exactB, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.25;
        }
        else
        {
            var containsStart = tokens.Any(t => SameNameRelaxed(t, startStem));
            var containsTarget = tokens.Any(t => SameNameRelaxed(t, targetStem));
            if (containsStart && containsTarget)
            {
                score += 0.55;
                var extraTokens = tokens.Count(t => !SameNameRelaxed(t, startStem) && !SameNameRelaxed(t, targetStem));
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

        var suspiciousTokens = new[] { "focus", "tmp", "temp", "staging", "archive", "history", "historique", "log", "audit", "backup", "old", "lineage", "quest", "quests" };
        if (tokens.Any(t => suspiciousTokens.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            score -= 0.95;
        }

        return score;
    }

    private static bool IsColumnForTable(string columnName, string tableStem)
    {
        var normalized = Singularize(SqlNameNormalizer.Normalize(columnName));
        return normalized == $"{tableStem}_id"
            || normalized == $"{tableStem}_iden"
            || normalized == $"{tableStem}_ident"
            || normalized == $"{tableStem}_code"
            || normalized == $"id_{tableStem}";
    }

    private static bool IsForeignKeyLike(string normalizedColumnName)
    {
        return normalizedColumnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_IDEN", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_IDENT", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.EndsWith("_CODE", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.StartsWith("ID_", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.StartsWith("FK_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericIdentifier(string normalizedColumnName)
    {
        return normalizedColumnName is "ID" or "IDEN" or "IDENT";
    }

    private static string TableStem(string tableName)
    {
        var normalized = SqlNameNormalizer.Normalize(tableName).Trim('_');
        if (normalized.Length == 0)
        {
            return normalized;
        }

        var tokens = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1)
        {
            return Singularize(tokens[0]);
        }

        return Singularize(tokens[^1]);
    }

    private static IEnumerable<string> TableNameStems(string tableName)
    {
        var normalized = SqlNameNormalizer.Normalize(tableName).Trim('_');
        if (normalized.Length == 0)
        {
            yield break;
        }

        yield return Singularize(normalized);
        var tokens = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            yield return Singularize(token);
        }
    }

    private static string Singularize(string normalizedName)
    {
        var parts = normalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SingularizeToken);
        return string.Join('_', parts);
    }

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

    private static bool SameNameRelaxed(string left, string right)
    {
        var a = Singularize(SqlNameNormalizer.Normalize(left));
        var b = Singularize(SqlNameNormalizer.Normalize(right));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    private static string JoinKey(string fromTable, string fromColumn, string toTable, string toColumn)
    {
        return string.Join("|",
            NormalizeKeyPart(fromTable),
            NormalizeKeyPart(fromColumn),
            NormalizeKeyPart(toTable),
            NormalizeKeyPart(toColumn));
    }

    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static bool IsRelationshipDisabled(InferredRelationship relationship, IEnumerable<string> disabledAutoJoinKeys)
    {
        var disabled = disabledAutoJoinKeys as ISet<string> ?? disabledAutoJoinKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return disabled.Contains(relationship.Key) || disabled.Contains(relationship.ReverseKey);
    }

    private static bool IsJoinDisabled(JoinDefinition join, IEnumerable<string> disabledAutoJoinKeys)
    {
        var disabled = disabledAutoJoinKeys as ISet<string> ?? disabledAutoJoinKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var key = RelationshipKey.For(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
        var reverseKey = RelationshipKey.ReverseFor(join.FromTable, join.FromColumn, join.ToTable, join.ToColumn);
        return disabled.Contains(key) || disabled.Contains(reverseKey);
    }

    private static List<string> BuildSelectItems(QueryDefinition query, SqlGeneratorOptions options, List<string> warnings)
    {
        var result = new List<string>();
        result.AddRange(query.SelectedColumns.Select(c => ColumnSql(c, options, includeAlias: true)));

        foreach (var aggregate in query.Aggregates)
        {
            var item = BuildAggregateExpression(aggregate, options, warnings);
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

        foreach (var custom in query.CustomColumns)
        {
            var expression = BuildCustomExpression(custom, options);
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

    private static string BuildAggregateExpression(AggregateSelection aggregate, SqlGeneratorOptions options, List<string> warnings)
    {
        var fn = aggregate.Function switch
        {
            AggregateFunction.Count => "COUNT",
            AggregateFunction.Sum => "SUM",
            AggregateFunction.Average => "AVG",
            AggregateFunction.Minimum => "MIN",
            AggregateFunction.Maximum => "MAX",
            _ => "COUNT"
        };

        var target = aggregate.Column is null ? "*" : ColumnSql(aggregate.Column, options, includeAlias: false);
        var condition = BuildAggregateCondition(aggregate, options, warnings);
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

            var countTarget = aggregate.Column is null ? "1" : target;
            return $"COUNT(CASE WHEN {condition} THEN {countTarget} END)";
        }

        if (aggregate.Column is null)
        {
            warnings.Add($"Agrégat conditionnel {fn} ignoré: choisis une colonne cible. Utilise COUNT pour compter des lignes.");
            return string.Empty;
        }

        var caseExpression = aggregate.Function == AggregateFunction.Sum
            ? $"CASE WHEN {condition} THEN {target} ELSE 0 END"
            : $"CASE WHEN {condition} THEN {target} END";

        if (aggregate.Distinct)
        {
            return $"{fn}(DISTINCT {caseExpression})";
        }

        return $"{fn}({caseExpression})";
    }

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

        var op = NormalizeOperator(aggregate.ConditionOperator);
        var column = ColumnSql(aggregate.ConditionColumn, options, includeAlias: false);
        return BuildPredicateSql(column, op, aggregate.ConditionValue, aggregate.ConditionSecondValue, aggregate.ConditionColumn.Key, warnings);
    }

    private static string BuildCustomExpression(CustomColumnSelection custom, SqlGeneratorOptions options)
    {
        if (!string.IsNullOrWhiteSpace(custom.RawExpression))
        {
            return custom.RawExpression.Trim();
        }

        if (custom.CaseColumn is not null && !string.IsNullOrWhiteSpace(custom.CaseOperator))
        {
            var op = NormalizeOperator(custom.CaseOperator);
            var column = ColumnSql(custom.CaseColumn, options, includeAlias: false);
            var compare = op is "IS NULL" or "IS NOT NULL" ? string.Empty : " " + SqlLiteralFormatter.FormatValue(custom.CaseCompareValue);
            return $"CASE WHEN {column} {op}{compare} THEN {SqlLiteralFormatter.FormatValue(custom.CaseThenValue)} ELSE {SqlLiteralFormatter.FormatValue(custom.CaseElseValue)} END";
        }

        return string.Empty;
    }

    private static List<string> BuildWhere(QueryDefinition query, SqlGeneratorOptions options, List<string> warnings)
    {
        var result = new List<string>();
        foreach (var filter in query.Filters)
        {
            var op = NormalizeOperator(filter.Operator);
            var column = ColumnSql(filter.Column, options, includeAlias: false);
            var predicate = BuildPredicateSql(column, op, filter.Value, filter.SecondValue, filter.Column.Key, warnings);
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                result.Add(predicate);
            }
        }
        return result;
    }

    private static string BuildPredicateSql(string columnSql, string op, string? value, string? secondValue, string warningKey, List<string> warnings)
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

    private static List<string> BuildGroupBy(QueryDefinition query, SqlGeneratorOptions options)
    {
        return query.GroupBy.Select(c => ColumnSql(c, options, includeAlias: false)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeOperator(string? op)
    {
        var value = string.IsNullOrWhiteSpace(op) ? "=" : op.Trim().ToUpperInvariant();
        return value switch
        {
            "==" => "=",
            "!=" => "<>",
            "ISNULL" => "IS NULL",
            "ISNOTNULL" => "IS NOT NULL",
            _ => value
        };
    }

    private static string ColumnSql(ColumnReference reference, SqlGeneratorOptions options, bool includeAlias)
    {
        var sql = $"{Q(reference.Table, options)}.{Q(reference.Column, options)}";
        if (includeAlias && !string.IsNullOrWhiteSpace(reference.Alias))
        {
            sql += " AS " + Q(reference.Alias, options);
        }
        return sql;
    }

    private static string Q(string identifier, SqlGeneratorOptions options)
    {
        return SqlIdentifierQuoter.Quote(identifier, options.Dialect, options.QuoteIdentifiers);
    }
}
