using System.Text.RegularExpressions;

namespace SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;

/// <summary>
/// Infers a user-facing purpose for aggregate-heavy SQL queries.
/// </summary>
public sealed class AggregateGoalHeuristicEngine
{
    /// <summary>
    /// Regular expression used to detect aggregate functions inside raw SQL expressions.
    /// </summary>
    private static readonly Regex AggregateFunctionRegex = new("\\b(count|sum|avg|min|max|median|stddev|stddev_pop|stddev_samp|variance|var_pop|var_samp)\\s*\\(", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Formatter responsible for all human-readable labels and sentences.
    /// </summary>
    private readonly GoalTextFormatter _formatter;

    /// <summary>
    /// Options controlling thresholds and detection keywords.
    /// </summary>
    private readonly AggregateGoalHeuristicOptions _options;

    /// <summary>
    /// Initializes a new aggregate goal heuristic engine with default dependencies.
    /// </summary>
    public AggregateGoalHeuristicEngine()
        : this(new GoalTextFormatter(), AggregateGoalHeuristicOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new aggregate goal heuristic engine.
    /// </summary>
    /// <param name="formatter">The formatter used for labels and summaries.</param>
    /// <param name="options">The heuristic options used for thresholds and keyword lists.</param>
    public AggregateGoalHeuristicEngine(GoalTextFormatter formatter, AggregateGoalHeuristicOptions options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Analyzes a query snapshot and returns the best aggregate-specific goal explanation.
    /// </summary>
    /// <param name="snapshot">The query snapshot produced by the query builder.</param>
    /// <returns>The inferred aggregate goal result.</returns>
    public AggregateGoalHeuristicResult Analyze(AggregateQuerySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        List<AggregateProjection> aggregates = [.. NormalizeAggregates(snapshot)];
        if (aggregates.Count == 0)
        {
            return AggregateGoalHeuristicResult.Empty;
        }

        List<GroupingIntent> groupingIntents = [.. BuildGroupingIntents(snapshot.Groupings)];
        List<AggregateMetricIntent> metricIntents = [.. BuildMetricIntents(aggregates)];
        List<AggregateGoalReason> reasons = [];
        List<string> warnings = [.. BuildWarnings(snapshot, groupingIntents)];

        AggregateSignal signal = BuildSignal(snapshot, aggregates, groupingIntents);
        AggregateGoalKind kind = InferKind(signal, reasons);
        double confidence = ComputeConfidence(snapshot, signal, reasons, warnings);
        GoalConfidenceLevel confidenceLevel = ToConfidenceLevel(confidence);
        string title = BuildTitle(kind, signal, metricIntents, groupingIntents);
        string summary = BuildSummary(snapshot, kind, signal, metricIntents, groupingIntents);
        string suggestedUserPhrase = BuildSuggestedPhrase(snapshot, kind, signal, metricIntents, groupingIntents);

        return new AggregateGoalHeuristicResult
        {
            Kind = kind,
            Title = title,
            Summary = summary,
            Confidence = confidence,
            ConfidenceLevel = confidenceLevel,
            Metrics = metricIntents,
            Groupings = groupingIntents,
            Reasons = reasons.OrderByDescending(reason => reason.Weight).ToArray(),
            Warnings = warnings.ToArray(),
            SuggestedUserPhrase = suggestedUserPhrase
        };
    }

    /// <summary>
    /// Normalizes aggregate projections, including fallback parsing from generated SQL when structured data is incomplete.
    /// </summary>
    /// <param name="snapshot">The query snapshot to normalize.</param>
    /// <returns>The aggregate projections to analyze.</returns>
    private IEnumerable<AggregateProjection> NormalizeAggregates(AggregateQuerySnapshot snapshot)
    {
        foreach (AggregateProjection aggregate in snapshot.Aggregates.Where(aggregate => aggregate.Function != AggregateFunction.None))
        {
            yield return aggregate;
        }

        if (!snapshot.Aggregates.Any() && !string.IsNullOrWhiteSpace(snapshot.GeneratedSql))
        {
            foreach (AggregateProjection parsedAggregate in ParseAggregatesFromSql(snapshot.GeneratedSql))
            {
                yield return parsedAggregate;
            }
        }
    }

    /// <summary>
    /// Parses aggregate function calls from raw SQL as a compatibility fallback.
    /// </summary>
    /// <param name="sql">The SQL text to inspect.</param>
    /// <returns>A sequence of aggregate projections detected in the SQL text.</returns>
    private IEnumerable<AggregateProjection> ParseAggregatesFromSql(string sql)
    {
        foreach (Match match in AggregateFunctionRegex.Matches(sql))
        {
            string functionName = match.Groups[1].Value;
            string expression = ExtractFunctionCall(sql, match.Index);
            AggregateFunction function = MapFunction(functionName, expression);
            yield return new AggregateProjection(expression, function)
            {
                IsDistinct = expression.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    /// <summary>
    /// Extracts a single SQL function call starting at the given index.
    /// </summary>
    /// <param name="sql">The SQL text containing the function call.</param>
    /// <param name="startIndex">The zero-based index where the function name starts.</param>
    /// <returns>The best-effort function-call expression.</returns>
    private static string ExtractFunctionCall(string sql, int startIndex)
    {
        int openIndex = sql.IndexOf('(', startIndex);
        if (openIndex < 0)
        {
            return sql[startIndex..].Trim();
        }

        int depth = 0;
        for (int index = openIndex; index < sql.Length; index++)
        {
            char current = sql[index];
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return sql[startIndex..(index + 1)].Trim();
                }
            }
        }

        return sql[startIndex..].Trim();
    }

    /// <summary>
    /// Maps a SQL aggregate function name to the internal enum.
    /// </summary>
    /// <param name="functionName">The SQL function name.</param>
    /// <param name="expression">The complete aggregate expression.</param>
    /// <returns>The corresponding aggregate function enum value.</returns>
    private static AggregateFunction MapFunction(string functionName, string expression)
    {
        return functionName.ToLowerInvariant() switch
        {
            "count" when expression.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase) => AggregateFunction.CountDistinct,
            "count" => AggregateFunction.Count,
            "sum" => AggregateFunction.Sum,
            "avg" => AggregateFunction.Average,
            "min" => AggregateFunction.Minimum,
            "max" => AggregateFunction.Maximum,
            "median" => AggregateFunction.Median,
            "stddev" or "stddev_pop" or "stddev_samp" => AggregateFunction.StandardDeviation,
            "variance" or "var_pop" or "var_samp" => AggregateFunction.Variance,
            _ => AggregateFunction.Custom
        };
    }

    /// <summary>
    /// Builds metric intents from aggregate projections.
    /// </summary>
    /// <param name="aggregates">The aggregate projections to describe.</param>
    /// <returns>A sequence of metric intents.</returns>
    private IEnumerable<AggregateMetricIntent> BuildMetricIntents(IEnumerable<AggregateProjection> aggregates)
    {
        foreach (AggregateProjection aggregate in aggregates)
        {
            yield return new AggregateMetricIntent(aggregate.Function, _formatter.FormatAggregateLabel(aggregate))
            {
                ExpressionSql = aggregate.ExpressionSql
            };
        }
    }

    /// <summary>
    /// Builds grouping intents from grouping projections.
    /// </summary>
    /// <param name="groupings">The grouping projections to describe.</param>
    /// <returns>A sequence of grouping intents.</returns>
    private IEnumerable<GroupingIntent> BuildGroupingIntents(IEnumerable<GroupingProjection> groupings)
    {
        foreach (GroupingProjection grouping in groupings)
        {
            string label = _formatter.FormatGroupingLabel(grouping);
            bool isTemporal = grouping.IsTemporalOverride ?? LooksTemporal(grouping.ExpressionSql, grouping.SourceColumn, grouping.DisplayName, grouping.SourceColumnComment);
            bool isIdentifier = grouping.IsIdentifierOverride ?? LooksIdentifier(grouping.ExpressionSql, grouping.SourceColumn, grouping.DisplayName);

            yield return new GroupingIntent(label)
            {
                IsTemporal = isTemporal,
                IsIdentifier = isIdentifier
            };
        }
    }

    /// <summary>
    /// Computes the aggregate signal object used by the decision tree.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="aggregates">The normalized aggregate projections.</param>
    /// <param name="groupings">The inferred grouping intents.</param>
    /// <returns>The aggregate signal object.</returns>
    private static AggregateSignal BuildSignal(AggregateQuerySnapshot snapshot, IReadOnlyList<AggregateProjection> aggregates, IReadOnlyList<GroupingIntent> groupings)
    {
        bool hasGrouping = groupings.Count > 0;
        bool hasTemporalGrouping = groupings.Any(grouping => grouping.IsTemporal);
        bool hasHaving = snapshot.HavingConditions.Count > 0;
        bool hasWhereFilter = snapshot.Filters.Count > 0;
        bool hasAggregateOrder = snapshot.Orderings.Any(order => order.IsAggregateOrder || ContainsAggregateFunction(order.ExpressionSql));
        bool hasLimit = snapshot.Limit is > 0;
        bool onlyCounts = aggregates.All(aggregate => aggregate.Function is AggregateFunction.Count or AggregateFunction.CountDistinct);
        bool onlyDistinctCounts = aggregates.All(aggregate => aggregate.Function == AggregateFunction.CountDistinct);
        bool hasCount = aggregates.Any(aggregate => aggregate.Function == AggregateFunction.Count);
        bool hasDistinctCount = aggregates.Any(aggregate => aggregate.Function == AggregateFunction.CountDistinct || aggregate.IsDistinct);
        bool hasSum = aggregates.Any(aggregate => aggregate.Function == AggregateFunction.Sum);
        bool hasAverage = aggregates.Any(aggregate => aggregate.Function == AggregateFunction.Average);
        bool hasExtremes = aggregates.Any(aggregate => aggregate.Function is AggregateFunction.Minimum or AggregateFunction.Maximum);
        bool hasStatisticalMetric = aggregates.Any(aggregate => aggregate.Function is AggregateFunction.Median or AggregateFunction.StandardDeviation or AggregateFunction.Variance);
        bool hasSeveralMetrics = aggregates.Count > 1;
        bool hasMeasure = aggregates.Any(aggregate => aggregate.IsMeasureAggregate);
        bool hasDataQualityPattern = DetectDataQualityPattern(snapshot, aggregates);
        bool hasRankingPattern = hasGrouping && hasAggregateOrder && hasLimit;

        return new AggregateSignal(
            hasGrouping,
            hasTemporalGrouping,
            hasHaving,
            hasWhereFilter,
            hasAggregateOrder,
            hasLimit,
            onlyCounts,
            onlyDistinctCounts,
            hasCount,
            hasDistinctCount,
            hasSum,
            hasAverage,
            hasExtremes,
            hasStatisticalMetric,
            hasSeveralMetrics,
            hasMeasure,
            hasDataQualityPattern,
            hasRankingPattern);
    }

    /// <summary>
    /// Infers the aggregate goal kind from the aggregate signal object.
    /// </summary>
    /// <param name="signal">The aggregate signal object.</param>
    /// <param name="reasons">The mutable reason list to enrich.</param>
    /// <returns>The inferred aggregate goal kind.</returns>
    private static AggregateGoalKind InferKind(AggregateSignal signal, ICollection<AggregateGoalReason> reasons)
    {
        AddReason(reasons, "aggregate.detected", "La requête contient au moins un agrégat.", 0.20);

        if (signal.HasDataQualityPattern)
        {
            AddReason(reasons, "aggregate.data_quality", "Le motif ressemble à un contrôle de complétude ou de doublons.", 0.28);
            return AggregateGoalKind.DataQuality;
        }

        if (signal.HasRankingPattern)
        {
            AddReason(reasons, "aggregate.ranking", "Un tri sur agrégat avec limite indique un classement.", 0.30);
            return AggregateGoalKind.Ranking;
        }

        if (signal.HasTemporalGrouping)
        {
            AddReason(reasons, "grouping.temporal", "Le GROUP BY utilise une dimension temporelle.", 0.27);
            return AggregateGoalKind.TimeSeries;
        }

        if (signal.HasGrouping && signal.OnlyCounts)
        {
            AddReason(reasons, "grouping.count_distribution", "COUNT combiné à GROUP BY indique une répartition.", 0.26);
            return AggregateGoalKind.Distribution;
        }

        if (!signal.HasGrouping && signal.OnlyDistinctCounts)
        {
            AddReason(reasons, "count.distinct", "COUNT DISTINCT indique un comptage de valeurs uniques.", 0.25);
            return AggregateGoalKind.DistinctCount;
        }

        if (!signal.HasGrouping && signal.HasWhereFilter)
        {
            AddReason(reasons, "aggregate.filtered_kpi", "Un agrégat sans GROUP BY mais avec filtre ressemble à un KPI filtré.", 0.22);
            return AggregateGoalKind.FilteredKpi;
        }

        if (signal.HasGrouping && signal.HasSeveralMetrics)
        {
            AddReason(reasons, "aggregate.dashboard", "Plusieurs agrégats par groupe indiquent un résumé statistique.", 0.24);
            return AggregateGoalKind.DashboardSummary;
        }

        if (signal.HasGrouping && signal.HasSum)
        {
            AddReason(reasons, "aggregate.sum_by_group", "SUM combiné à GROUP BY indique un total par dimension.", 0.24);
            return AggregateGoalKind.MetricTotal;
        }

        if (signal.HasGrouping && signal.HasAverage)
        {
            AddReason(reasons, "aggregate.avg_by_group", "AVG combiné à GROUP BY indique une moyenne par dimension.", 0.24);
            return AggregateGoalKind.MetricAverage;
        }

        if (signal.HasExtremes)
        {
            AddReason(reasons, "aggregate.extremes", "MIN ou MAX indique une recherche d'extrêmes.", 0.21);
            return AggregateGoalKind.MetricExtremes;
        }

        if (!signal.HasGrouping && signal.HasCount)
        {
            AddReason(reasons, "aggregate.row_count", "COUNT sans GROUP BY indique un comptage global.", 0.22);
            return AggregateGoalKind.RowCount;
        }

        if (signal.HasSum)
        {
            AddReason(reasons, "aggregate.sum", "SUM indique le calcul d'un total.", 0.18);
            return AggregateGoalKind.MetricTotal;
        }

        if (signal.HasAverage)
        {
            AddReason(reasons, "aggregate.avg", "AVG indique le calcul d'une moyenne.", 0.18);
            return AggregateGoalKind.MetricAverage;
        }

        AddReason(reasons, "aggregate.mixed", "Les agrégats détectés ne forment pas un motif unique évident.", 0.12);
        return AggregateGoalKind.MixedAggregateSummary;
    }

    /// <summary>
    /// Computes a confidence score from detected signals and warnings.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="signal">The aggregate signal object.</param>
    /// <param name="reasons">The reason list created by inference.</param>
    /// <param name="warnings">The warnings created by inference.</param>
    /// <returns>A confidence score between 0 and 1.</returns>
    private static double ComputeConfidence(AggregateQuerySnapshot snapshot, AggregateSignal signal, IReadOnlyCollection<AggregateGoalReason> reasons, IReadOnlyCollection<string> warnings)
    {
        double score = reasons.Sum(reason => reason.Weight);

        if (signal.HasGrouping)
        {
            score += 0.16;
        }

        if (signal.HasHaving)
        {
            score += 0.06;
        }

        if (signal.HasWhereFilter)
        {
            score += 0.04;
        }

        if (signal.HasSeveralMetrics)
        {
            score += 0.08;
        }

        if (signal.HasMeasure)
        {
            score += 0.05;
        }

        if (snapshot.Tables.Count > 0)
        {
            score += 0.04;
        }

        if (warnings.Count > 0)
        {
            score -= Math.Min(0.12, warnings.Count * 0.03);
        }

        return Math.Clamp(score, 0, 0.97);
    }

    /// <summary>
    /// Converts a numeric confidence score to a confidence level.
    /// </summary>
    /// <param name="confidence">The confidence score between 0 and 1.</param>
    /// <returns>The corresponding confidence level.</returns>
    private GoalConfidenceLevel ToConfidenceLevel(double confidence)
    {
        if (confidence >= _options.HighConfidenceThreshold)
        {
            return GoalConfidenceLevel.High;
        }

        return confidence >= _options.MediumConfidenceThreshold
            ? GoalConfidenceLevel.Medium
            : GoalConfidenceLevel.Low;
    }

    /// <summary>
    /// Builds a short UI title for an aggregate goal.
    /// </summary>
    /// <param name="kind">The inferred aggregate goal kind.</param>
    /// <param name="signal">The aggregate signal object.</param>
    /// <param name="metrics">The detected metric intents.</param>
    /// <param name="groupings">The detected grouping intents.</param>
    /// <returns>A short French title.</returns>
    private string BuildTitle(AggregateGoalKind kind, AggregateSignal signal, IReadOnlyList<AggregateMetricIntent> metrics, IReadOnlyList<GroupingIntent> groupings)
    {
        string metricLabel = _formatter.JoinLabels(metrics.Select(metric => metric.Label), _options.MaxInlineMetricLabels);
        string groupingLabel = _formatter.JoinLabels(groupings.Select(grouping => grouping.Label), _options.MaxInlineGroupingLabels);

        return kind switch
        {
            AggregateGoalKind.RowCount => "Comptage global",
            AggregateGoalKind.DistinctCount => "Comptage de valeurs uniques",
            AggregateGoalKind.MetricTotal when signal.HasGrouping => $"Total par {groupingLabel}",
            AggregateGoalKind.MetricTotal => $"Total de {metricLabel}",
            AggregateGoalKind.MetricAverage when signal.HasGrouping => $"Moyenne par {groupingLabel}",
            AggregateGoalKind.MetricAverage => $"Moyenne de {metricLabel}",
            AggregateGoalKind.MetricExtremes => "Recherche d'extrêmes",
            AggregateGoalKind.Distribution => $"Répartition par {groupingLabel}",
            AggregateGoalKind.Ranking => $"Classement par {metricLabel}",
            AggregateGoalKind.TimeSeries => $"Évolution temporelle de {metricLabel}",
            AggregateGoalKind.DashboardSummary => $"Résumé statistique par {groupingLabel}",
            AggregateGoalKind.FilteredKpi => "KPI filtré",
            AggregateGoalKind.DataQuality => "Contrôle de qualité des données",
            AggregateGoalKind.MixedAggregateSummary => "Résumé agrégé mixte",
            _ => "But agrégé"
        };
    }

    /// <summary>
    /// Builds a longer UI summary for an aggregate goal.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="kind">The inferred aggregate goal kind.</param>
    /// <param name="signal">The aggregate signal object.</param>
    /// <param name="metrics">The detected metric intents.</param>
    /// <param name="groupings">The detected grouping intents.</param>
    /// <returns>A French natural-language summary.</returns>
    private string BuildSummary(AggregateQuerySnapshot snapshot, AggregateGoalKind kind, AggregateSignal signal, IReadOnlyList<AggregateMetricIntent> metrics, IReadOnlyList<GroupingIntent> groupings)
    {
        string entityLabel = _formatter.FormatRootEntity(snapshot);
        string metricLabel = _formatter.JoinLabels(metrics.Select(metric => metric.Label), _options.MaxInlineMetricLabels);
        string groupingLabel = _formatter.JoinLabels(groupings.Select(grouping => grouping.Label), _options.MaxInlineGroupingLabels);
        string filterSuffix = snapshot.Filters.Count > 0 ? " après application des filtres" : string.Empty;
        string havingSuffix = snapshot.HavingConditions.Count > 0 ? " puis conserve seulement les groupes qui respectent le HAVING" : string.Empty;

        return kind switch
        {
            AggregateGoalKind.RowCount => $"La requête compte le nombre de {entityLabel}{filterSuffix}.",
            AggregateGoalKind.DistinctCount => $"La requête compte les valeurs distinctes de {metricLabel}{filterSuffix}.",
            AggregateGoalKind.MetricTotal when signal.HasGrouping => $"La requête calcule {metricLabel} pour chaque {groupingLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.MetricTotal => $"La requête calcule {metricLabel} sur {entityLabel}{filterSuffix}.",
            AggregateGoalKind.MetricAverage when signal.HasGrouping => $"La requête compare {metricLabel} pour chaque {groupingLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.MetricAverage => $"La requête calcule {metricLabel} sur {entityLabel}{filterSuffix}.",
            AggregateGoalKind.MetricExtremes when signal.HasGrouping => $"La requête cherche les valeurs extrêmes de {metricLabel} pour chaque {groupingLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.MetricExtremes => $"La requête cherche les valeurs extrêmes de {metricLabel} sur {entityLabel}{filterSuffix}.",
            AggregateGoalKind.Distribution => $"La requête répartit les {entityLabel} par {groupingLabel} en utilisant {metricLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.Ranking => $"La requête classe les groupes de {groupingLabel} selon {metricLabel}{filterSuffix}.",
            AggregateGoalKind.TimeSeries => $"La requête suit {metricLabel} dans le temps, avec un regroupement par {groupingLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.DashboardSummary => $"La requête produit un résumé statistique avec {metricLabel} pour chaque {groupingLabel}{filterSuffix}{havingSuffix}.",
            AggregateGoalKind.FilteredKpi => $"La requête calcule un indicateur global ({metricLabel}) sur les {entityLabel} filtrés.",
            AggregateGoalKind.DataQuality => $"La requête ressemble à un contrôle de qualité des données, probablement pour repérer des valeurs manquantes, des doublons ou des groupes anormaux.",
            AggregateGoalKind.MixedAggregateSummary => $"La requête calcule plusieurs agrégats ({metricLabel}){(signal.HasGrouping ? $" par {groupingLabel}" : string.Empty)}{filterSuffix}{havingSuffix}.",
            _ => $"La requête calcule {metricLabel}{filterSuffix}."
        };
    }

    /// <summary>
    /// Builds a concise user-intent phrase that can be shown as "but compris".
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="kind">The inferred aggregate goal kind.</param>
    /// <param name="signal">The aggregate signal object.</param>
    /// <param name="metrics">The detected metric intents.</param>
    /// <param name="groupings">The detected grouping intents.</param>
    /// <returns>A concise French phrase representing the interpreted user intent.</returns>
    private string BuildSuggestedPhrase(AggregateQuerySnapshot snapshot, AggregateGoalKind kind, AggregateSignal signal, IReadOnlyList<AggregateMetricIntent> metrics, IReadOnlyList<GroupingIntent> groupings)
    {
        string entityLabel = _formatter.FormatRootEntity(snapshot);
        string metricLabel = _formatter.JoinLabels(metrics.Select(metric => metric.Label), _options.MaxInlineMetricLabels);
        string groupingLabel = _formatter.JoinLabels(groupings.Select(grouping => grouping.Label), _options.MaxInlineGroupingLabels);

        return kind switch
        {
            AggregateGoalKind.RowCount => $"Compter les {entityLabel}",
            AggregateGoalKind.DistinctCount => $"Compter les valeurs uniques de {metricLabel}",
            AggregateGoalKind.MetricTotal when signal.HasGrouping => $"Calculer {metricLabel} par {groupingLabel}",
            AggregateGoalKind.MetricTotal => $"Calculer {metricLabel}",
            AggregateGoalKind.MetricAverage when signal.HasGrouping => $"Comparer {metricLabel} par {groupingLabel}",
            AggregateGoalKind.MetricAverage => $"Calculer {metricLabel}",
            AggregateGoalKind.MetricExtremes => $"Trouver les extrêmes de {metricLabel}",
            AggregateGoalKind.Distribution => $"Voir la répartition par {groupingLabel}",
            AggregateGoalKind.Ranking => $"Classer par {metricLabel}",
            AggregateGoalKind.TimeSeries => $"Suivre {metricLabel} dans le temps",
            AggregateGoalKind.DashboardSummary => $"Résumer {metricLabel} par {groupingLabel}",
            AggregateGoalKind.FilteredKpi => $"Calculer un KPI filtré sur {entityLabel}",
            AggregateGoalKind.DataQuality => "Contrôler la qualité des données",
            _ => $"Calculer {metricLabel}"
        };
    }

    /// <summary>
    /// Builds warnings related to aggregate interpretation and likely performance pitfalls.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="groupings">The inferred grouping intents.</param>
    /// <returns>A sequence of warning messages.</returns>
    private IEnumerable<string> BuildWarnings(AggregateQuerySnapshot snapshot, IReadOnlyList<GroupingIntent> groupings)
    {
        if (snapshot.HasStarProjection && snapshot.Aggregates.Count > 0)
        {
            yield return "La requête mélange table.* et agrégats : vérifie que le GROUP BY généré reste valide pour Oracle/SQLite.";
        }

        if (_options.WarnOnHavingWithoutWhere && snapshot.HavingConditions.Count > 0 && snapshot.Filters.Count == 0)
        {
            yield return "HAVING filtre après agrégation : si possible, ajoute aussi un WHERE pour réduire les lignes avant GROUP BY.";
        }

        if (_options.WarnOnUnindexedGroupings && snapshot.Groupings.Count > 0 && snapshot.IndexCoverage.Count > 0)
        {
            foreach (GroupingProjection grouping in snapshot.Groupings)
            {
                string tableName = grouping.SourceTable ?? string.Empty;
                string columnName = grouping.SourceColumn ?? grouping.ExpressionSql;
                IndexCoverageSummary? coverage = snapshot.IndexCoverage.FirstOrDefault(index =>
                    string.Equals(index.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(index.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

                if (coverage is { IsIndexed: false })
                {
                    string label = _formatter.FormatGroupingLabel(grouping);
                    yield return $"Le GROUP BY sur {label} ne semble pas indexé : les gros volumes risquent de trier/agréger lourdement.";
                }
            }
        }

        if (groupings.Count >= 4)
        {
            yield return "La requête groupe sur beaucoup de dimensions : le résultat peut exploser en cardinalité.";
        }
    }

    /// <summary>
    /// Detects whether a label or expression appears to describe a temporal dimension.
    /// </summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>True when one of the values looks temporal.</returns>
    private bool LooksTemporal(params string?[] values)
    {
        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!))
        {
            string normalized = value.ToLowerInvariant();
            if (_options.TemporalKeywords.Any(keyword => normalized.Contains(keyword.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (_options.TemporalSqlFunctions.Any(function => normalized.Contains(function + "(", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects whether a label or expression appears to describe a technical identifier.
    /// </summary>
    /// <param name="values">The values to inspect.</param>
    /// <returns>True when one of the values looks like an identifier.</returns>
    private bool LooksIdentifier(params string?[] values)
    {
        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!))
        {
            string normalized = _formatter.CleanLabel(value).ToLowerInvariant();
            string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Any(part => _options.IdentifierKeywords.Contains(part)) || normalized.EndsWith(" id", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(" iden", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects raw aggregate function calls inside an SQL expression.
    /// </summary>
    /// <param name="expressionSql">The SQL expression to inspect.</param>
    /// <returns>True when the expression contains an aggregate function call.</returns>
    private static bool ContainsAggregateFunction(string expressionSql)
    {
        return !string.IsNullOrWhiteSpace(expressionSql) && AggregateFunctionRegex.IsMatch(expressionSql);
    }

    /// <summary>
    /// Detects aggregate query shapes commonly used for data-quality checks.
    /// </summary>
    /// <param name="snapshot">The query snapshot being analyzed.</param>
    /// <param name="aggregates">The normalized aggregate projections.</param>
    /// <returns>True when the query resembles a quality check.</returns>
    private static bool DetectDataQualityPattern(AggregateQuerySnapshot snapshot, IReadOnlyList<AggregateProjection> aggregates)
    {
        bool hasCount = aggregates.Any(aggregate => aggregate.Function is AggregateFunction.Count or AggregateFunction.CountDistinct);
        bool hasHavingCount = snapshot.HavingConditions.Any(condition => condition.ExpressionSql.Contains("COUNT", StringComparison.OrdinalIgnoreCase));
        bool hasNullFilter = snapshot.Filters.Any(filter => filter.ExpressionSql.Contains(" IS NULL", StringComparison.OrdinalIgnoreCase) || filter.ExpressionSql.Contains(" IS NOT NULL", StringComparison.OrdinalIgnoreCase));
        bool hasDuplicatePattern = snapshot.Groupings.Count > 0 && hasCount && hasHavingCount;

        return hasNullFilter || hasDuplicatePattern;
    }

    /// <summary>
    /// Adds a reason to the mutable reason list.
    /// </summary>
    /// <param name="reasons">The mutable reason list.</param>
    /// <param name="code">The stable machine-readable reason code.</param>
    /// <param name="message">The user-facing reason message.</param>
    /// <param name="weight">The confidence weight contributed by the reason.</param>
    private static void AddReason(ICollection<AggregateGoalReason> reasons, string code, string message, double weight)
    {
        reasons.Add(new AggregateGoalReason(code, message, weight));
    }

    /// <summary>
    /// Compact immutable aggregate signal record used by the decision tree.
    /// </summary>
    /// <param name="HasGrouping">Indicates whether the query has GROUP BY dimensions.</param>
    /// <param name="HasTemporalGrouping">Indicates whether a GROUP BY dimension is temporal.</param>
    /// <param name="HasHaving">Indicates whether the query has HAVING predicates.</param>
    /// <param name="HasWhereFilter">Indicates whether the query has WHERE predicates.</param>
    /// <param name="HasAggregateOrder">Indicates whether the query orders by an aggregate expression.</param>
    /// <param name="HasLimit">Indicates whether the query has a limit or top clause.</param>
    /// <param name="OnlyCounts">Indicates whether all aggregate projections are count-based.</param>
    /// <param name="OnlyDistinctCounts">Indicates whether all aggregate projections are count-distinct projections.</param>
    /// <param name="HasCount">Indicates whether the query has COUNT.</param>
    /// <param name="HasDistinctCount">Indicates whether the query has COUNT DISTINCT or a distinct aggregate.</param>
    /// <param name="HasSum">Indicates whether the query has SUM.</param>
    /// <param name="HasAverage">Indicates whether the query has AVG.</param>
    /// <param name="HasExtremes">Indicates whether the query has MIN or MAX.</param>
    /// <param name="HasStatisticalMetric">Indicates whether the query has statistical aggregates such as median or standard deviation.</param>
    /// <param name="HasSeveralMetrics">Indicates whether the query has multiple aggregate projections.</param>
    /// <param name="HasMeasure">Indicates whether the query has a non-count measure.</param>
    /// <param name="HasDataQualityPattern">Indicates whether the query resembles a data-quality check.</param>
    /// <param name="HasRankingPattern">Indicates whether the query resembles a ranking.</param>
    private sealed record AggregateSignal(
        bool HasGrouping,
        bool HasTemporalGrouping,
        bool HasHaving,
        bool HasWhereFilter,
        bool HasAggregateOrder,
        bool HasLimit,
        bool OnlyCounts,
        bool OnlyDistinctCounts,
        bool HasCount,
        bool HasDistinctCount,
        bool HasSum,
        bool HasAverage,
        bool HasExtremes,
        bool HasStatisticalMetric,
        bool HasSeveralMetrics,
        bool HasMeasure,
        bool HasDataQualityPattern,
        bool HasRankingPattern);
}
