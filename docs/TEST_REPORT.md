# Rapport de vérification

## Tests inclus

Les tests xUnit couvrent :

- parsing de `CREATE TABLE` ;
- extraction des commentaires Oracle `COMMENT ON COLUMN` ;
- extraction des commentaires inline ;
- inférence de relation par même nom de colonne vers une PK ;
- inférence de relation type `ORD_IDEN` ;
- génération d'un SELECT avec JOIN, WHERE, GROUP BY, SUM et ORDER BY ;
- génération d'une colonne calculée `CASE WHEN` ;
- absence de sous-requête dans le scénario principal testé.

## Vérification effectuée dans l'environnement de génération

L'environnement disponible ici ne contient pas le SDK `dotnet`, et WPF est de toute façon une cible Windows. Je n'ai donc pas pu exécuter `dotnet build` ni `dotnet test` dans ce conteneur Linux.

Une passe statique a été effectuée : arborescence, présence des projets, scripts, README, fichiers WPF, cœur séparé, tests et contrôle grossier d'équilibrage des accolades C#.

## Commandes à lancer sous Windows

```powershell
.\build.ps1
.\run-tests.ps1
```

## Correctif 2026-05-02

Correction appliquée après build utilisateur : ajout de `tests/SqlQueryGenerator.Tests/GlobalUsings.cs` avec `global using Xunit;` afin que les attributs `[Fact]` et `Assert` soient résolus par le compilateur. Le projet de tests a aussi été marqué explicitement avec `<IsTestProject>true</IsTestProject>`.

## UI ergonomics pass v6

Environment note: this environment cannot run WPF or `dotnet test` because the .NET SDK/Desktop SDK is not installed here. The following changes were statically reviewed and the XAML was XML-parsed successfully:

- Main window opens maximized by default and uses a 1920x1040 design baseline.
- SQL output moved to a bottom panel spanning the full width, reducing horizontal compression in the query builder.
- Query builder receives a much wider central area; right-side SQL panel no longer steals space from DataGrid columns.
- Added resizable splitters between schema/browser, builder, SQL output and warnings.
- Added column search over table, column, type and comment.
- Added schema summary badge in the top bar.
- Added larger row/header heights and wider/min-width DataGrid columns.
- Added double-click on an available column to add it directly to the SELECT list.

## v7 - Agrégats conditionnels

Tests ajoutés côté cœur :

- `Generate_ConditionalCount_EmitsCaseWhenCountWithoutSubquery`
- `Generate_ConditionalSum_EmitsCaseWhenSumWithoutSubquery`

Objectif couvert : générer des indicateurs du type `nombre de paiements pour une condition` et `total d'une colonne pour une condition` avec des expressions `CASE WHEN` directement dans les agrégats, sans sous-requête.

Note : les tests doivent être exécutés sur une machine avec SDK .NET 8 :

```powershell
dotnet test -c Release
```

## v8 - Correction inférence FK générique `id = id`

Tests ajoutés :

- `Infer_SingularForeignKeyColumn_ToPluralTableId_WinsOverGenericId`
  - Vérifie que `pnj.job_id` est relié à `jobs.id`.
  - Vérifie que `pnj.id = jobs.id` n'est plus inféré.
- `Infer_GroupId_ToCompoundPluralTableId`
  - Vérifie que `jobs.group_id` peut être relié à `jobs_groups.id`.
- `Generate_AutoJoin_PrefersSpecificForeignKeyOverGenericId`
  - Vérifie que la requête générée contient `INNER JOIN jobs ON pnj.job_id = jobs.id`.
  - Vérifie que la requête générée ne contient pas `pnj.id = jobs.id`.

Note : ces tests doivent être exécutés avec `dotnet test -c Release` sur une machine Windows/.NET SDK.

## v10 — UI colonne et inférence composée

Ajouts vérifiés statiquement dans cette livraison :

- TreeView des colonnes enrichi avec badges PK/FK.
- Couleurs de type par catégorie : TEXT, INTEGER, REAL, DATE, BOOL, BINARY, autre.
- Menu clic droit sur les colonnes : SELECT, filtre, GROUP BY, ORDER BY, agrégat.
- Bouton `+ Jointure manuelle` dans l'onglet Jointures.
- Nouvelle heuristique `CompositeTablePattern` pour les tables composées/spécialisées.

Nouveau test ajouté :

- `Infer_SourceColumnStem_ToSourcePrefixedPluralLookupTable`
  - vérifie `pnj.job_id -> pnj_jobs.id` ;
  - vérifie que `pnj.id -> pnj_jobs.id` n'est pas inféré.

Commande recommandée sous Windows :

```powershell
dotnet test -c Release
.\publish-win-x64.ps1
```

## v11 UI ergonomics patch

Manual verification performed by static review only in this environment:

- XAML parsed successfully as XML.
- Manual join editor now uses editable table dropdowns and left/right column drop zones.
- Dragging a column to a manual join side sets both the table and column on that side.
- Adding a column to GROUP BY now also ensures the same column is present in SELECT.
- New aggregate default aliases use `count_column`, `sum_column`, `avg_column`, `min_column`, `max_column` instead of `column_agg`.

Runtime WPF build/test must be executed on Windows with .NET 8 SDK/Desktop workload:

```powershell
dotnet test -c Release
.\publish-win-x64.ps1
```


## v12 regression fix

Added parser regression coverage for inline comments written after a comma, e.g. `id INTEGER PRIMARY KEY, -- technical id`.

## v18 — Optimisation ForeignKeyInferer

Changements ciblés :

- suppression du parcours global `sourceColumn × targetColumn` ;
- ajout de dictionnaires de colonnes par nom normalisé ;
- ajout de dictionnaires de tables par variante de nom et token ;
- mise en cache des signaux index / unique / PK ;
- remplacement de la déduplication linéaire par un dictionnaire `RelationshipKey -> index` ;
- limitation des relations faibles same-name sur les groupes trop gros non indexés.

À valider localement :

```powershell
dotnet test -c Release
```

L'environnement de génération du zip ne contient pas le SDK .NET/WPF, donc les tests doivent être exécutés sur Windows / .NET 8 côté utilisateur.

## v20 - Tests ajoutés

Ajout de tests unitaires côté génération SQL :

- `Generate_FilterOnAggregate_EmitsHavingWithoutSubquery`
- `Generate_OrderByAggregateAlias_UsesAlias`
- `Generate_FilterAndOrderOnCustomColumn_UsesExpressionForWhereAndAliasForOrder`

Objectif : garantir que les agrégats et colonnes calculées peuvent être réutilisés dans les filtres et tris sans générer de sous-requête inutile.
