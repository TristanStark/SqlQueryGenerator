# SQL Query Generator — v27 — Heuristique du but spécialisée agrégats

## Objectif

Cette version améliore l'heuristique de détection du **but utilisateur** lorsque la requête contient des agrégats SQL : `COUNT`, `COUNT DISTINCT`, `SUM`, `AVG`, `MIN`, `MAX`, `MEDIAN`, `STDDEV`, `VARIANCE`.

Le moteur reste **100 % heuristique**, sans LLM, et travaille à partir d'un snapshot léger de la requête construite dans l'UI.

## Nouveaux fichiers

```text
src/SqlQueryGenerator.Core/Heuristics/Goals/Aggregates/
  AggregateGoalHeuristicModels.cs
  AggregateGoalHeuristicOptions.cs
  AggregateGoalHeuristicEngine.cs
  AggregateQuerySnapshotBuilder.cs
  GoalTextFormatter.cs
src/SqlQueryGenerator.Core/Heuristics/Goals/
  QueryGoalHeuristicServiceV27.cs
tests/SqlQueryGenerator.Core.Tests/
  AggregateGoalHeuristicEngineTests.cs
```

## Ce que l'heuristique détecte mieux

### 1. Comptage simple

```sql
SELECT COUNT(*) FROM ORD
```

But détecté : **Compter les ordres**.

### 2. Répartition par dimension

```sql
SELECT STATUT, COUNT(*)
FROM CLIENT
GROUP BY STATUT
```

But détecté : **Voir la répartition par statut client**.

### 3. Total par dimension

```sql
SELECT REGION, SUM(MONTANT_HT)
FROM VENTE
GROUP BY REGION
```

But détecté : **Calculer le total de montant HT par région**.

### 4. Moyenne par dimension

```sql
SELECT CATEGORIE, AVG(PRIX)
FROM PRODUIT
GROUP BY CATEGORIE
```

But détecté : **Comparer la moyenne de prix par catégorie**.

### 5. Évolution temporelle

```sql
SELECT TRUNC(DATE_FACTURE, 'MM'), SUM(MONTANT)
FROM FACTURE
GROUP BY TRUNC(DATE_FACTURE, 'MM')
```

But détecté : **Suivre le total de montant dans le temps**.

### 6. Classement

```sql
SELECT CLIENT.NOM, SUM(VENTE.MONTANT)
FROM VENTE
JOIN CLIENT ON CLIENT.ID = VENTE.CLIENT_ID
GROUP BY CLIENT.NOM
ORDER BY SUM(VENTE.MONTANT) DESC
FETCH FIRST 10 ROWS ONLY
```

But détecté : **Classer les clients par total de montant**.

### 7. Contrôle qualité / doublons

```sql
SELECT EMAIL, COUNT(*)
FROM CLIENT
GROUP BY EMAIL
HAVING COUNT(*) > 1
```

But détecté : **Contrôler la qualité des données**, probablement des doublons.

### 8. KPI filtré

```sql
SELECT SUM(MONTANT)
FROM VENTE
WHERE DATE_VENTE >= DATE '2026-01-01'
```

But détecté : **Calculer un KPI filtré sur les ventes**.

## Intégration recommandée

Dans le service existant qui produit le texte du but, garder le moteur générique comme fallback et appeler le moteur v27 seulement quand la requête contient au moins un agrégat.

Pseudo-code d'intégration :

```csharp
QueryGoalHeuristicServiceV27 aggregateGoalService = new();
AggregateQuerySnapshot snapshot = BuildAggregateSnapshotFromCurrentQuery(queryModel, schemaMetadata, indexStats);
AggregateGoalHeuristicResult aggregateResult = aggregateGoalService.AnalyzeAggregateGoal(snapshot);

if (aggregateGoalService.ShouldPreferAggregateGoal(aggregateResult, genericGoal.Confidence))
{
    goal.Title = aggregateResult.Title;
    goal.Description = aggregateResult.Summary;
    goal.Confidence = aggregateResult.Confidence;
    goal.Reasons = aggregateResult.Reasons.Select(r => r.Message).ToList();
    goal.Warnings = aggregateResult.Warnings.ToList();
}
```

## Construction du snapshot

Le snapshot doit contenir uniquement ce qui est déjà présent dans le builder :

- tables utilisées ;
- agrégats sélectionnés ;
- colonnes du `GROUP BY` ;
- filtres `WHERE` ;
- filtres `HAVING` ;
- tris `ORDER BY` ;
- limite éventuelle ;
- couverture d'index calculée par le module existant.

Exemple :

```csharp
AggregateQuerySnapshot snapshot = new AggregateQuerySnapshotBuilder()
    .AddTable(new TableUsageSummary("VENTE")
    {
        DisplayName = "ventes",
        IsRootTable = true
    })
    .AddAggregate(new AggregateProjection("SUM(VENTE.MONTANT_HT)", AggregateFunction.Sum)
    {
        SourceTable = "VENTE",
        SourceColumn = "MONTANT_HT",
        DisplayName = "montant HT"
    })
    .AddGrouping(new GroupingProjection("AGENCE.REGION")
    {
        SourceTable = "AGENCE",
        SourceColumn = "REGION",
        DisplayName = "région"
    })
    .Build();
```

## Notes de performance

Le moteur ne parcourt pas le schéma complet. Il travaille uniquement sur le snapshot de la requête courante : agrégats, groupings, filtres, orderings et quelques index coverage facts.

Complexité attendue :

```text
O(A + G + F + H + O + I)
```

Avec :

- `A` = nombre d'agrégats sélectionnés ;
- `G` = nombre de groupings ;
- `F` = nombre de filtres WHERE ;
- `H` = nombre de filtres HAVING ;
- `O` = nombre d'ORDER BY ;
- `I` = nombre d'éléments d'index coverage passés dans le snapshot.

Il ne refait pas l'inférence FK et ne retombe pas dans un parcours `O(C²)` du schéma.

## Warnings ajoutés

La v27 remonte aussi quelques avertissements utiles :

- `table.*` mélangé avec agrégats ;
- `HAVING` sans `WHERE`, potentiellement coûteux ;
- `GROUP BY` sur colonne non indexée si l'information d'index est disponible ;
- trop de dimensions de regroupement, qui peuvent exploser la cardinalité.

## Tests fournis

Les tests couvrent :

- `COUNT(*)` simple ;
- `COUNT(*) GROUP BY` ;
- `SUM` temporel ;
- `SUM` par dimension ;
- `AVG` par dimension ;
- `COUNT DISTINCT` ;
- ranking par `ORDER BY` agrégé + limite ;
- résumé multi-agrégats ;
- détection doublons par `HAVING COUNT(*) > 1` ;
- warning sur `GROUP BY` non indexé.
