# SQL Query Generator — package v27 — Aggregate Goal Heuristics

Ce package ajoute une version suivante centrée sur l'amélioration de l'heuristique du **but**, spécifiquement quand la requête contient des agrégats.

## Contenu

- Moteur `AggregateGoalHeuristicEngine`
- Modèles de snapshot `AggregateQuerySnapshot`, `AggregateProjection`, `GroupingProjection`, etc.
- Builder pratique `AggregateQuerySnapshotBuilder`
- Facade `QueryGoalHeuristicServiceV27`
- Tests xUnit `AggregateGoalHeuristicEngineTests`
- Documentation d'intégration `docs/v27-aggregate-goal-heuristic.md`

## Points forts

- Détection plus fine de `COUNT`, `COUNT DISTINCT`, `SUM`, `AVG`, `MIN`, `MAX`, `MEDIAN`, `STDDEV`, `VARIANCE`
- Distinction entre comptage, répartition, KPI filtré, total par dimension, moyenne par dimension, série temporelle, classement, dashboard et contrôle qualité
- Avertissements sur les cas coûteux : `HAVING` sans `WHERE`, `GROUP BY` non indexé, trop de dimensions, `table.*` avec agrégats
- Pas d'IA / LLM : heuristique déterministe
- Pas de parcours complet du schéma : complexité linéaire sur la requête courante
- Nouveau code documenté avec commentaires XML

## Installation manuelle

Copier :

```text
src/SqlQueryGenerator.Core/Heuristics/Goals/Aggregates/*
src/SqlQueryGenerator.Core/Heuristics/Goals/QueryGoalHeuristicServiceV27.cs
```

Puis ajouter le test :

```text
tests/SqlQueryGenerator.Core.Tests/AggregateGoalHeuristicEngineTests.cs
```

## Intégration minimale

Dans le service qui calcule déjà le but :

```csharp
QueryGoalHeuristicServiceV27 service = new();
AggregateGoalHeuristicResult aggregateGoal = service.AnalyzeAggregateGoal(snapshot);

if (service.ShouldPreferAggregateGoal(aggregateGoal, genericGoal.Confidence))
{
    // Remplacer l'explication générique par aggregateGoal.Title / aggregateGoal.Summary.
}
```

Le point d'adaptation principal est la conversion de ton `QueryModel` existant vers `AggregateQuerySnapshot`.
