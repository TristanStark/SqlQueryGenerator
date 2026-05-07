using System.Linq;
using SqlQueryGenerator.Core.Heuristics.Goals.Aggregates;
using Xunit;

namespace SqlQueryGenerator.Core.Tests;

/// <summary>
/// Tests the v27 aggregate goal heuristic with representative query-builder snapshots.
/// </summary>
public sealed class AggregateGoalHeuristicEngineTests
{
    /// <summary>
    /// Engine under test, shared because it is stateless.
    /// </summary>
    private readonly AggregateGoalHeuristicEngine _engine = new();

    /// <summary>
    /// Verifies that COUNT(*) without GROUP BY is understood as a global row count.
    /// </summary>
    [Fact]
    public void Analyze_CountStarWithoutGroupBy_ReturnsRowCount()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("ORD") { DisplayName = "ordres", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(*)", AggregateFunction.Count) { Alias = "nb_ordres" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.RowCount, result.Kind);
        Assert.Contains("compte", result.Summary.ToLowerInvariant());
        Assert.True(result.Confidence >= 0.40);
    }

    /// <summary>
    /// Verifies that COUNT(*) with GROUP BY is understood as a distribution.
    /// </summary>
    [Fact]
    public void Analyze_CountStarWithGroupBy_ReturnsDistribution()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("CLIENT") { DisplayName = "clients", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(*)", AggregateFunction.Count) { Alias = "Nombre clients" })
            .AddGrouping(new GroupingProjection("CLIENT.STATUT") { SourceTable = "CLIENT", SourceColumn = "STATUT", DisplayName = "statut client" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.Distribution, result.Kind);
        Assert.Contains("statut client", result.Title.ToLowerInvariant());
        Assert.True(result.Groupings.Single().Label.Contains("statut", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that SUM grouped by a temporal expression is understood as a time series.
    /// </summary>
    [Fact]
    public void Analyze_SumByMonth_ReturnsTimeSeries()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("FACTURE") { DisplayName = "factures", IsRootTable = true })
            .AddAggregate(new AggregateProjection("SUM(FACTURE.MONTANT)", AggregateFunction.Sum)
            {
                SourceTable = "FACTURE",
                SourceColumn = "MONTANT",
                DisplayName = "montant facturé"
            })
            .AddGrouping(new GroupingProjection("TRUNC(FACTURE.DATE_FACTURE, 'MM')") { DisplayName = "mois de facture" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.TimeSeries, result.Kind);
        Assert.Contains("temps", result.Summary.ToLowerInvariant());
        Assert.True(result.ConfidenceLevel is GoalConfidenceLevel.Medium or GoalConfidenceLevel.High);
    }

    /// <summary>
    /// Verifies that SUM by a non-temporal dimension is understood as a total by dimension.
    /// </summary>
    [Fact]
    public void Analyze_SumByDepartment_ReturnsMetricTotal()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("VENTE") { DisplayName = "ventes", IsRootTable = true })
            .AddAggregate(new AggregateProjection("SUM(VENTE.MONTANT_HT)", AggregateFunction.Sum)
            {
                SourceColumn = "MONTANT_HT",
                DisplayName = "montant HT"
            })
            .AddGrouping(new GroupingProjection("AGENCE.REGION") { DisplayName = "région" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.MetricTotal, result.Kind);
        Assert.Contains("région", result.Summary);
    }

    /// <summary>
    /// Verifies that AVG by a dimension is understood as average comparison by dimension.
    /// </summary>
    [Fact]
    public void Analyze_AverageByCategory_ReturnsMetricAverage()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("PRODUIT") { DisplayName = "produits", IsRootTable = true })
            .AddAggregate(new AggregateProjection("AVG(PRODUIT.PRIX)", AggregateFunction.Average)
            {
                SourceColumn = "PRIX",
                DisplayName = "prix"
            })
            .AddGrouping(new GroupingProjection("PRODUIT.CATEGORIE") { DisplayName = "catégorie" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.MetricAverage, result.Kind);
        Assert.Contains("moyenne", result.Title.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that COUNT DISTINCT without grouping is understood as a unique-value count.
    /// </summary>
    [Fact]
    public void Analyze_CountDistinctWithoutGroupBy_ReturnsDistinctCount()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("COMMANDE") { DisplayName = "commandes", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(DISTINCT COMMANDE.CLIENT_ID)", AggregateFunction.CountDistinct)
            {
                SourceColumn = "CLIENT_ID",
                DisplayName = "client"
            })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.DistinctCount, result.Kind);
        Assert.Contains("distinct", result.Summary.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that ORDER BY aggregate with LIMIT is understood as ranking.
    /// </summary>
    [Fact]
    public void Analyze_GroupedAggregateWithLimitAndAggregateOrder_ReturnsRanking()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("VENTE") { DisplayName = "ventes", IsRootTable = true })
            .AddAggregate(new AggregateProjection("SUM(VENTE.MONTANT)", AggregateFunction.Sum) { DisplayName = "montant" })
            .AddGrouping(new GroupingProjection("CLIENT.NOM") { DisplayName = "client" })
            .AddOrdering(new OrderProjection("SUM(VENTE.MONTANT) DESC") { IsAggregateOrder = true, IsDescending = true })
            .WithLimit(10)
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.Ranking, result.Kind);
        Assert.Contains("classe", result.Summary.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that multiple metrics by dimension are understood as a dashboard summary.
    /// </summary>
    [Fact]
    public void Analyze_MultipleAggregatesByGroup_ReturnsDashboardSummary()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("VENTE") { DisplayName = "ventes", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(*)", AggregateFunction.Count) { Alias = "nombre de ventes" })
            .AddAggregate(new AggregateProjection("SUM(VENTE.MONTANT)", AggregateFunction.Sum) { DisplayName = "montant" })
            .AddAggregate(new AggregateProjection("AVG(VENTE.MONTANT)", AggregateFunction.Average) { DisplayName = "montant" })
            .AddGrouping(new GroupingProjection("AGENCE.REGION") { DisplayName = "région" })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.DashboardSummary, result.Kind);
        Assert.Contains("résumé", result.Summary.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that HAVING COUNT by group is understood as a data-quality duplicate check.
    /// </summary>
    [Fact]
    public void Analyze_HavingCountByNaturalKey_ReturnsDataQuality()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("CLIENT") { DisplayName = "clients", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(*)", AggregateFunction.Count))
            .AddGrouping(new GroupingProjection("CLIENT.EMAIL") { DisplayName = "email" })
            .AddHaving(new HavingCondition("COUNT(*) > 1"))
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Equal(AggregateGoalKind.DataQuality, result.Kind);
        Assert.Contains("qualité", result.Title.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that GROUP BY index coverage warnings are emitted when metadata says a grouping column is not indexed.
    /// </summary>
    [Fact]
    public void Analyze_UnindexedGrouping_ReturnsWarning()
    {
        AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
            .AddTable(new TableUsageSummary("VENTE") { DisplayName = "ventes", IsRootTable = true })
            .AddAggregate(new AggregateProjection("COUNT(*)", AggregateFunction.Count))
            .AddGrouping(new GroupingProjection("VENTE.STATUT") { SourceTable = "VENTE", SourceColumn = "STATUT", DisplayName = "statut" })
            .AddIndexCoverage(new IndexCoverageSummary("VENTE", "STATUT") { IsIndexed = false })
            .Build();

        AggregateGoalHeuristicResult result = _engine.Analyze(snapshot);

        Assert.Contains(result.Warnings, warning => warning.Contains("index", System.StringComparison.OrdinalIgnoreCase));
    }
}
