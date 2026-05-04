# Architecture technique

## Modules

- `SqlQueryGenerator.Core` contient toute la logique métier : parsing du schéma, inférence des relations, modèle de requête, validation et génération SQL.
- `SqlQueryGenerator.App` contient l'interface WPF et les ViewModels.
- `SqlQueryGenerator.Tests` vérifie les comportements critiques du cœur.

## Flux principal

1. L'utilisateur charge un fichier SQL/TXT.
2. `SqlSchemaParser` extrait tables, colonnes, types, commentaires, PK et FK déclarées.
3. `ForeignKeyInferer` enrichit le schéma avec des relations probables.
4. Le ViewModel expose les colonnes et relations à l'UI.
5. L'utilisateur construit une requête par blocs.
6. `QueryDefinition` représente la requête indépendamment de WPF.
7. `SqlQueryGeneratorEngine` génère un SELECT plat sans sous-requête.

## Choix de conception

Le générateur ne se connecte jamais à la base. Il ne peut donc pas altérer les données. Les expressions brutes de colonnes calculées sont filtrées pour refuser les mots-clés DDL/DML dangereux.

Les jointures automatiques utilisent une stratégie volontairement simple et lisible : choisir les relations les plus fiables permettant de connecter les tables utilisées à la table de départ. Cela produit un SQL compréhensible pour un néophyte et évite les sous-requêtes opaques.

## v7 - Agrégats conditionnels

Le modèle `AggregateSelection` supporte maintenant une condition optionnelle :

- `ConditionColumn`
- `ConditionOperator`
- `ConditionValue`
- `ConditionSecondValue`

Le générateur ne crée pas de sous-requête pour ces cas. Il produit des expressions `CASE WHEN` dans les fonctions de groupe :

- `COUNT(CASE WHEN condition THEN colonne END)`
- `COUNT(DISTINCT CASE WHEN condition THEN colonne END)`
- `SUM(CASE WHEN condition THEN colonne ELSE 0 END)`
- `AVG(CASE WHEN condition THEN colonne END)`
- `MIN(CASE WHEN condition THEN colonne END)`
- `MAX(CASE WHEN condition THEN colonne END)`

Les colonnes utilisées dans les conditions d'agrégat participent aussi au calcul des tables utilisées, donc le plan de jointures automatiques les prend en compte.
