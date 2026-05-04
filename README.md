# SQL Query Generator

Projet C# WPF pour construire des requêtes `SELECT` visuellement à partir d'un schéma SQL/TXT, sans IA ni LLM.

## Objectif

L'application charge un schéma SQL, extrait les tables, colonnes, types, commentaires, clés primaires et clés étrangères déclarées, puis infère des relations probables quand les FK ne sont pas déclarées. L'utilisateur peut ensuite construire une requête par blocs ergonomiques : sélection, filtres, jointures, groupes, agrégats, tris et colonnes calculées.

Le générateur ne produit que des `SELECT`. Il ne se connecte pas à une base et n'exécute jamais de SQL.

## Fonctionnalités livrées

- Chargement d'un fichier `.sql` ou `.txt`.
- Collage direct du schéma depuis le presse-papier.
- Parser heuristique pour `CREATE TABLE`, contraintes PK/FK, commentaires inline et `COMMENT ON COLUMN` Oracle.
- Inférence de relations quand les FK ne sont pas déclarées :
  - FK déclarées : score 100 %.
  - Même nom de colonne vers une PK : score élevé.
  - Colonnes identifiantes identiques (`*_ID`, `*_IDEN`, `CODE`, etc.).
  - Motifs table/colonne comme `ORD.ORD_IDEN` puis `MVTO.ORD_IDEN`.
  - Commentaires contenant des indices de référence.
- Interface WPF avec drag & drop de colonnes vers :
  - colonnes sélectionnées ;
  - filtres ;
  - `GROUP BY` ;
  - agrégats `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` ;
  - tris ;
  - colonnes calculées `CASE WHEN`.
- Jointures automatiques via le graphe de relations inférées.
- Jointures forcées manuellement depuis la liste des relations détectées.
- Dialectes de génération : `Generic`, `SQLite`, `Oracle`.
- Option de guillemets autour des identifiants.
- `LIMIT` pour SQLite/générique, `FETCH FIRST n ROWS ONLY` pour Oracle.
- Protection contre les expressions personnalisées contenant DDL/DML (`DELETE`, `UPDATE`, `DROP`, etc.).
- Projet core séparé et testé.

## Structure

```text
SqlQueryGenerator.sln
src/
  SqlQueryGenerator.Core/      Parser, heuristiques, modèle de requête, générateur SQL
  SqlQueryGenerator.App/       Interface WPF
samples/
  oracle_schema_sample.sql     Schéma d'exemple
tests/
  SqlQueryGenerator.Tests/     Tests xUnit du cœur
```

## Prérequis

- Windows.
- Visual Studio 2022 ou plus récent avec workload `.NET desktop development`.
- SDK .NET 8.

WPF cible `net8.0-windows`, donc l'interface se compile et s'exécute sous Windows. Le projet `SqlQueryGenerator.Core` cible `net8.0` et reste indépendant de WPF.

## Compilation

Depuis PowerShell :

```powershell
.\build.ps1
```

Ou manuellement :

```powershell
dotnet restore .\SqlQueryGenerator.sln
dotnet build .\SqlQueryGenerator.sln -c Release
```

## Tests

```powershell
.\run-tests.ps1
```

## Utilisation

1. Lance `SqlQueryGenerator.App`.
2. Clique sur **Charger schéma SQL/TXT** ou **Coller schéma**.
3. Sélectionne la table de départ.
4. Glisse les colonnes vers les blocs : sélection, filtres, groupe, agrégat, tri ou colonne calculée.
5. Vérifie les jointures inférées dans l'onglet **Jointures**.
6. Ajoute une jointure forcée si l'inférence automatique n'est pas celle attendue.
7. Copie le SQL généré et colle-le dans SQLite Browser, Oracle SQL Developer, DBeaver, etc.

## Exemple de colonne calculée

Pour produire :

```sql
CASE WHEN T.A = 'X' THEN 'Z' ELSE 'Y' END AS label
```

Dans l'onglet **Colonnes calculées** :

- dépose la colonne `T.A` ;
- alias : `label` ;
- opérateur : `=` ;
- si valeur : `X` ;
- alors : `Z` ;
- sinon : `Y`.

## Limites assumées

Ce projet est volontairement heuristique : il ne prétend pas parser 100 % de tous les dialectes SQL existants. Il couvre les DDL courants Oracle/SQLite/PostgreSQL/MySQL-like pour les cas utiles au générateur visuel.

Le générateur minimise les sous-requêtes en générant une requête plate avec `JOIN`, `WHERE`, `GROUP BY`, `ORDER BY`. Les sous-requêtes ne sont pas générées dans cette version.

## Durcissement inclus

- Pas d'exécution SQL.
- Pas de génération `DELETE`, `UPDATE`, `INSERT`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `MERGE`, `EXEC`, etc.
- Taille de fichier bornée côté UI et côté parser.
- Validation d'identifiants SQL avant génération.
- Échappement des chaînes SQL.
- Séparation core/UI pour faciliter les tests.
- Avertissements lorsque les jointures automatiques ne sont pas fiables.

## Améliorations futures naturelles

- Vue graphique du graphe de jointures.
- Profils de conventions par entreprise (`ORD_IDEN`, `MVTO_IDEN`, etc.).
- Sauvegarde/chargement de requêtes `.sqgb.json`.
- Export formaté `.sql`.
- Assistant de jointure en plusieurs chemins avec choix utilisateur.
- Prévisualisation pédagogique en langage naturel : “Tu sélectionnes les clients actifs groupés par ville”.

## Générer un vrai `.exe` Windows

`dotnet build` peut afficher uniquement le `.dll` dans la sortie console, même lorsque l’hôte `.exe` est généré à côté. Vérifie d’abord :

```powershell
Get-ChildItem .\src\SqlQueryGenerator.App\bin\Release\net8.0-windows\*.exe
```

Pour produire un dossier exécutable propre :

```powershell
.\publish-win-x64.ps1
```

Sortie attendue :

```text
artifacts\publish\win-x64-framework-dependent\SqlQueryGenerator.App.exe
```

Cette version nécessite le **Microsoft .NET 8 Desktop Runtime** sur la machine cible.

Pour produire un exécutable autonome, plus lourd, mais transportable sans installer .NET :

```powershell
.\publish-win-x64-self-contained.ps1
```

Sortie attendue :

```text
artifacts\publish\win-x64-self-contained\SqlQueryGenerator.App.exe
```

Pour lancer rapidement depuis le dépôt :

```powershell
.\run-app.ps1
```

## Ergonomie UI v6

La fenêtre s'ouvre désormais maximisée et l'interface est organisée en deux grandes zones :

1. En haut : colonnes disponibles + constructeur de requête large.
2. En bas : SQL généré + avertissements.

Cela évite que les colonnes des grilles soient coupées par le panneau SQL. Les panneaux restent redimensionnables via les séparateurs.

Améliorations ajoutées :

- recherche rapide dans les colonnes disponibles ;
- double-clic sur une colonne pour l'ajouter au SELECT ;
- colonnes de grilles agrandies avec largeurs minimales ;
- résumé du schéma chargé dans la barre supérieure ;
- zone SQL plus large et plus confortable pour copier/coller.

## Agrégats conditionnels v7

L'onglet **Groupes / agrégats** permet maintenant de créer des agrégats classiques et conditionnels sans écrire de sous-requête.

Fonctions disponibles :

- `COUNT`
- `SUM`
- `AVG`
- `MIN`
- `MAX`
- option `Distinct` sur chaque agrégat

Chaque agrégat possède maintenant deux parties :

1. **Cible** : la colonne à compter, sommer, moyenner, etc.
2. **Condition optionnelle** : la colonne et la valeur qui limitent les lignes prises en compte par cet agrégat.

Exemple : nombre de paiements CB par personne :

```sql
COUNT(CASE WHEN PAYMENTS.MODE_REGLEMENT = 'CB' THEN PAYMENTS.PAYMENT_ID END) AS nb_paiements_cb
```

Exemple : total payé par personne uniquement pour les lignes `STATUS = 'PAID'` :

```sql
SUM(CASE WHEN PAYMENTS.STATUS = 'PAID' THEN PAYMENTS.AMOUNT ELSE 0 END) AS total_paye
```

Cas d'usage typique :

1. Dépose `PERSON.NAME`, `PAYMENTS.MODE_REGLEMENT`, etc. dans **Sélection**.
2. Laisse **Auto GROUP BY** coché pour regrouper automatiquement ces colonnes quand tu ajoutes un agrégat.
3. Dépose `PAYMENTS.PAYMENT_ID` dans **Agrégats**, choisis `Count`, alias `nb_paiements_cb`, puis mets la condition `PAYMENTS.MODE_REGLEMENT = CB`.
4. Dépose `PAYMENTS.AMOUNT` dans **Agrégats**, choisis `Sum`, alias `total_cb`, puis mets la même condition.

Le générateur garde une requête plate : `SELECT ... FROM ... JOIN ... WHERE ... GROUP BY ...`, sans sous-requête inutile.

## v8 - Correction détection automatique des jointures

La détection heuristique évite maintenant les fausses jointures génériques du type :

```sql
pnj.id = jobs.id
```

quand une colonne plus spécifique existe, par exemple :

```sql
pnj.job_id = jobs.id
```

La logique gère aussi les pluriels simples (`job_id` → `jobs.id`) et certains noms composés (`group_id` → `jobs_groups.id`).

## v10 — Améliorations UX et jointures

L'écran des colonnes disponibles est maintenant un TreeView regroupé par tables, avec :

- badges `PK` et `FK` ;
- couleurs par type logique (`TEXT`, `INTEGER`, `REAL`, `DATE`, etc.) ;
- menu clic droit sur chaque colonne pour l'ajouter directement au `SELECT`, au `WHERE`, au `GROUP BY`, au `ORDER BY` ou comme agrégat ;
- bouton `+ Jointure manuelle` dans l'onglet Jointures pour créer une jointure librement modifiable.

L'inférence de relations a aussi été élargie pour les tables composées ou spécialisées. Exemple :

```sql
CREATE TABLE pnj (id INTEGER, job_id INTEGER);
CREATE TABLE pnj_jobs (id INTEGER, name TEXT);
```

Le moteur doit maintenant proposer :

```sql
pnj.job_id -> pnj_jobs.id
```

et éviter :

```sql
pnj.id -> pnj_jobs.id
```


## v12

Fix regression in inline column comment parsing: comments written after a column-separating comma are now attached to the preceding column.

## Performance v18

`ForeignKeyInferer.Infer()` a été optimisé pour les schémas volumineux. L'ancienne version comparait quasiment toutes les colonnes entre elles (`O(C²)`), ce qui devient très coûteux avec 7000+ colonnes. La v18 construit des dictionnaires de tables/colonnes/index et génère uniquement des candidats plausibles. Voir `docs/PERFORMANCE.md`.

## v20 - Filtres et tris sur agrégats / colonnes calculées

Les agrégats et colonnes calculées peuvent désormais être ajoutés aux filtres et aux tris depuis leurs onglets respectifs.

- `+ Filtre` sur un agrégat génère automatiquement un `HAVING`.
- `+ Tri` sur un agrégat génère un `ORDER BY alias`.
- `+ Filtre` sur une colonne calculée réutilise l'expression complète dans `WHERE`.
- `+ Tri` sur une colonne calculée utilise l'alias dans `ORDER BY`.

Voir `docs/DERIVED_FIELDS.md`.

## V21 — Sous-requêtes, vues et requêtes sauvegardées

La v21 ajoute :

- création de paramètres (`?`, `:nom`, `@nom`) ;
- sauvegarde/chargement de requêtes structurées en JSON ;
- réutilisation de requêtes sauvegardées comme sous-requêtes filtrantes ;
- parsing de `CREATE VIEW` ;
- analyse heuristique de performance basée sur les index, les vues, les filtres, les tris et les jointures.

Voir `docs/V21_SUBQUERIES_SAVED_QUERIES_PERFORMANCE.md`.
