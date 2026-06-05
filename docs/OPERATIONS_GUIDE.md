# Guide d'utilisation

## Objet

SQL Query Generator est un assistant de travail en lecture seule pour :

- charger un schéma SQL
- documenter les tables et colonnes
- construire des requêtes `SELECT`
- réécrire un SQL existant
- importer un SQL existant dans le constructeur visuel
- analyser le but probable et quelques risques de performance

L'application ne lance jamais de `DELETE`, `UPDATE` ou `INSERT`. Elle génère uniquement du SQL que vous exécutez vous-même dans votre client SQL.

## Vue d'ensemble de l'interface

La fenêtre principale contient trois zones :

1. Barre supérieure
   Chargement du schéma, import de documentation, copie du SQL, ouverture de la fenêtre d'export DDL, remise à zéro de la requête et ouverture de l'aide.
2. Zone de construction
   Exploration du schéma et édition visuelle de la requête.
3. Zone de sortie et validation
   SQL généré, but probable de la requête, analyse heuristique de performance et avertissements.

La fenêtre est maintenant plus tolérante sur des écrans plus étroits : la barre du haut sépare les actions, le résumé de schéma et la ligne de statut, une colonne de diagnostics reste visible à droite pendant l'édition, et cette colonne utilise des onglets au lieu d'empiler systématiquement but, performance et avertissements.

Dans l'onglet `Rétro-ingénierie SQL`, l'éditeur brut garde maintenant plus de hauteur utile. Les diagnostics détaillés de reverse restent dans la colonne de droite, onglet `Reverse SQL`, pendant que la zone centrale reste concentrée sur l'édition du SQL brut et la comparaison des versions.

## Opérations de la barre supérieure

La barre supérieure contient aussi :

- `Annuler`
- `Rétablir`

Raccourcis clavier :

- `Ctrl+Z`
- `Ctrl+Y`

L'historique restaure l'état du constructeur et du SQL brut associé. Il est réinitialisé lorsqu'un nouveau schéma est chargé.

### `Charger schéma SQL/TXT`

Charge un fichier `.sql` ou `.txt` contenant le schéma :

- `CREATE TABLE`
- `CREATE VIEW`
- index
- commentaires
- contraintes utiles à l'inférence

À utiliser lorsque le schéma existe déjà sur disque.

Si des tables ressemblant à des copies `backup` / `history` / `archive` / `tmp` sont détectées, une fenêtre de revue s'ouvre avant l'import final. Les candidates sont cochées pour exclusion par défaut, mais vous pouvez les conserver une par une, tout garder, ou annuler l'import. Après l'import, la case `Masquer tables auxiliaires restantes` permet encore de masquer les tables auxiliaires non exclues.

### `Importer doc CSV/TSV`

Importe une documentation table/colonne depuis un fichier tabulaire.

À utiliser après le chargement du schéma si vous voulez enrichir les commentaires affichés dans l'arbre et les explications métier.

Consulte aussi `docs/DOCUMENTATION_IMPORT_GUIDE.md` pour le format attendu, les entêtes reconnus et des exemples d'extraction Oracle / DB2 / PostgreSQL / SQL Server.

### `Coller schéma`

Parse un schéma depuis le presse-papier.

À utiliser si le DDL a été copié depuis un autre outil et qu'aucun fichier temporaire n'est nécessaire.

### `Copier SQL`

Copie le contenu actuel de la zone `SQL généré` dans le presse-papier.

### `Générer l'export DDL`

Ouvre la fenêtre dédiée à l'export DDL.

Cette opération sert à construire une requête d'aide permettant d'extraire le DDL depuis :

- Oracle via `DBMS_METADATA.GET_DDL`
- SQLite via `sqlite_master`

### `Vider requête`

Réinitialise la requête en cours :

- colonnes sélectionnées
- filtres
- regroupements
- tris
- jointures
- colonnes calculées
- paramètres
- alias de tables importés

### `Aide`

Ouvre ce guide en local lorsque le fichier est présent, sinon ouvre la copie GitHub.

## Exploration du schéma

Le panneau de gauche affiche les tables et leurs colonnes.

Actions disponibles :

- rechercher par nom de table
- rechercher par nom de colonne
- rechercher par type
- rechercher dans les commentaires
- double-cliquer sur une colonne pour l'ajouter au `SELECT`
- utiliser le menu contextuel pour l'envoyer vers `SELECT`, `WHERE`, `GROUP BY`, `ORDER BY` ou les agrégats
- glisser-déposer les colonnes vers les onglets compatibles
- cocher plusieurs colonnes pour les ajouter en masse au `SELECT`
- ajouter `table.*` depuis un nœud de table

Les badges de colonnes indiquent :

- `PK` : clé primaire
- `FK` : clé étrangère ou relation inférée
- badge d'index : présence d'un index

## Construction de la requête

La zone centrale permet de construire la requête visuellement.

### Barre de base de la requête

En haut de la zone de construction, vous pouvez définir :

- la table de départ
- le dialecte SQL
- `DISTINCT`
- le quoting des identifiants
- l'ajout automatique des colonnes au `GROUP BY`
- la limite de lignes

Le bouton `Générer` reconstruit immédiatement le SQL depuis le modèle courant.

## Onglets de construction

### Onglet `Sélection`

Gère les champs projetés dans le `SELECT`.

Opérations usuelles :

- ajouter une colonne simple
- ajouter `table.*`
- définir un alias de sortie
- retirer une projection

### Onglet `Filtres`

Gère les conditions du `WHERE`.

Types de filtres pris en charge :

- comparaisons
- `LIKE`
- `IN`
- `BETWEEN`
- `IS NULL`
- paramètres
- sous-requêtes sauvegardées
- sous-requêtes SQL brutes

### Onglet `Jointures`

Gère les jointures explicites.

Opérations disponibles :

- ajouter une jointure depuis une relation inférée
- créer une jointure manuelle
- définir des jointures composites avec colonnes supplémentaires
- glisser-déposer des colonnes dans les champs de jointure
- choisir `INNER JOIN` ou `LEFT JOIN`

Les relations probables affichent aussi un état `Ajouter` / `Ajoutee` pour signaler rapidement si une relation est déjà présente dans les jointures courantes.

### Onglet `Groupes / agrégats`

Gère `GROUP BY` et les sélections d'agrégats.

Agrégats pris en charge :

- `COUNT`
- `SUM`
- `AVG`
- `MIN`
- `MAX`

Les agrégats conditionnels sont aussi supportés via les conditions stockées dans le modèle d'agrégat.

### Onglet `Tri`

Gère `ORDER BY`.

Vous pouvez trier par :

- colonne simple
- alias d'agrégat
- alias de colonne calculée

### Onglet `Colonnes calculées`

Construit des expressions contrôlées pour le `SELECT`.

Cas couverts :

- expression brute
- `CASE WHEN`
- filtre basé sur l'expression calculée
- tri basé sur l'alias calculé

### Onglet `Sous-requêtes / sauvegarde`

Cet onglet est maintenant réservé à la sauvegarde et aux sous-requêtes.

Opérations disponibles :

- `Sauvegarder requête`
- `Recharger bibliothèque`
- `Charger sélection`
- `Utiliser comme sous-requête filtrante`
- gestion des paramètres via `+ Paramètre`

La bibliothèque locale contient deux types d'entrées :

- requêtes construites avec l'interface
- presets SQL bruts

## Onglet `Rétro-ingénierie SQL`

Cet onglet regroupe toutes les opérations autour d'un `SELECT` existant.

### `Importer fichier SQL brut`

Charge un fichier `.sql` ou `.txt` dans l'éditeur SQL brut.

### `Depuis SQL généré`

Copie le SQL actuellement généré par le constructeur dans l'éditeur SQL brut.

Utile pour comparer la version visuelle et une version réécrite.

### `Sauvegarder SQL brut`

Sauvegarde le SQL brut comme preset réutilisable dans la bibliothèque locale.

### `Charger SQL brut sélectionné`

Recharge dans l'éditeur SQL brut le preset actuellement sélectionné dans la bibliothèque.

### `Réécrire SQL`

Produit une version plus propre et plus moderne du SQL brut sans modifier le contenu de l'éditeur.

Le profil source sélectionné dans l'onglet est également utilisé ici pour enrichir les avertissements dialecte.

Réécritures conservatrices actuellement prises en charge :

- conversion des jointures implicites en `JOIN` explicites
- conversion de la syntaxe Oracle `(+)` en `LEFT JOIN`
- suppression des doublons de filtres
- suppression des doublons dans `SELECT`
- suppression des doublons dans `GROUP BY`
- suppression des doublons dans `ORDER BY`
- préservation des alias de table
- préservation des paramètres

Des avertissements sont affichés lorsqu'une structure avancée n'est que partiellement modélisée.

Le rapport reverse fournit aussi :

- une couverture par clause (`SELECT`, `FROM/JOIN`, `WHERE`, `GROUP BY`, `HAVING`, `ORDER BY`, etc.) ;
- un score de confiance heuristique ;
- des diagnostics structurés en cas d'échec.

Après réécriture, l'onglet `Comparaison SQL` affiche une comparaison ligne par ligne entre le SQL brut source et le SQL réécrit. Vous pouvez ensuite basculer vers `Réécrit / Constructeur` si vous voulez confronter la réécriture au SQL actuellement régénéré par le constructeur.

### `Charger dans le constructeur`

Analyse le SQL brut et recharge le résultat dans les onglets visuels.

L'objectif est de continuer l'édition dans le constructeur au lieu d'éditer le SQL à la main.

Le chargement tente de préserver :

- colonnes sélectionnées
- jointures
- `WHERE`
- `GROUP BY`
- `HAVING`
- `ORDER BY`
- paramètres
- alias de tables

Le prétraitement reverse ignore maintenant les commentaires SQL `--` et `/* ... */`, et les prompts Cognos `#prompt(...)#` sont conservés comme paramètres bruts quand ils apparaissent dans les filtres.

L'onglet `Comparaison SQL` permet ensuite de comparer :

- le SQL brut initial au SQL régénéré depuis le constructeur
- le SQL brut initial au SQL réécrit
- le SQL réécrit au SQL régénéré depuis le constructeur

Deux options permettent aussi d'ignorer les différences purement liées aux espaces ou à la casse. L'onglet `Diagnostics Reverse` expose la couverture, la confiance et les diagnostics détaillés sans devoir lire uniquement la zone d'avertissements.

## Fenêtre `Export du DDL`

Ouvrez cette fenêtre via `Générer l'export DDL`.

Étapes :

1. choisir le moteur source
2. renseigner `Schéma / base`
3. cliquer sur `Générer la requête d'export DDL`
4. exécuter ensuite la requête dans votre client SQL

Signification du champ `Schéma / base` :

- Oracle : propriétaire du schéma, par exemple `APP_OWNER`
- SQLite : base attachée, généralement `main`

La requête générée est copiée dans le presse-papier à chaque génération.

## Zone de sortie et validation

### Panneau `SQL généré`

Affiche le SQL exact produit par le constructeur ou par la réécriture.

### Panneau `But probable de la requête`

Affiche une explication métier probable de la requête.

### Panneau `Analyse performance heuristique`

Affiche des signaux simples sur des points de vigilance possibles :

- jointures absentes ou fragiles
- scans larges
- coûts de tri
- coûts de regroupement
- hypothèses autour des index

Les heuristiques actuelles couvrent notamment :

- `SELECT *`
- `LIKE '%...` avec wildcard en tête
- filtres basés sur expressions ou fonctions (`UPPER`, `TO_CHAR`, etc.)
- requêtes potentiellement volumineuses sans `LIMIT` / `FETCH` / `TOP`
- `ORDER BY` et `GROUP BY` sur colonnes non indexées ou trop nombreuses
- jointures sans extrémité PK/unique claire

### Zone `Avertissements`

Affiche les avertissements de parsing, d'import, de modélisation et de génération.

Après une rétro-ingénierie ou une réécriture, ces avertissements doivent être relus avant utilisation du SQL.

## Flux de travail recommandés

### Construire une requête à partir d'un schéma

1. charger le schéma
2. importer la documentation si nécessaire
3. choisir la table de départ
4. ajouter colonnes, filtres, jointures et agrégats
5. cliquer sur `Générer`
6. relire le SQL, le but, l'analyse de performance et les avertissements

### Convertir un SQL existant en modèle visuel éditable

1. ouvrir l'onglet `Rétro-ingénierie SQL`
2. coller ou charger le `SELECT`
3. cliquer sur `Charger dans le constructeur`
4. relire les jointures, filtres et alias récupérés
5. continuer l'édition dans les autres onglets

### Moderniser un SQL existant sans le charger dans le constructeur

1. ouvrir l'onglet `Rétro-ingénierie SQL`
2. coller ou charger le `SELECT`
3. cliquer sur `Réécrire SQL`
4. comparer la version source et la version générée
5. relire les avertissements

### Extraire un DDL depuis Oracle ou SQLite

1. cliquer sur `Générer l'export DDL`
2. choisir le moteur
3. renseigner `Schéma / base`
4. cliquer sur `Générer la requête d'export DDL`
5. exécuter la requête dans votre client SQL

## Règles de prudence

- seuls les usages en lecture seule sont pris en charge
- après une rétro-ingénierie ou une réécriture, relisez le SQL généré
- avant tout usage réel, validez toujours le SQL dans votre client cible
