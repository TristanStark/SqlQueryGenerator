# V21 — Sous-requêtes, sauvegarde et analyse performance

## Requêtes sauvegardées

La v21 ajoute une bibliothèque locale de requêtes au format JSON dans le dossier `saved_queries`.

Une requête sauvegardée contient :

- le nom lisible ;
- une description ;
- la définition structurée de la requête ;
- les paramètres déclarés ;
- le dernier SQL généré.

Les fichiers portent l'extension :

```text
.sqlqg.json
```

## Paramètres

Une valeur de filtre peut être déclarée comme paramètre en choisissant `Parameter` dans la colonne `Type valeur`.

Exemples acceptés :

```sql
?
:acti_iden
@acti_iden
```

Si l'utilisateur saisit `acti_iden`, le générateur produit automatiquement :

```sql
:acti_iden
```

## Sous-requêtes

Une requête sauvegardée peut être réutilisée comme sous-requête filtrante.

Exemple conceptuel :

```sql
SELECT p.ID
FROM PAYMENTS p
WHERE p.ACTI_IDEN IN (
    SELECT a.ACTI_IDEN
    FROM ACTIONS a
    WHERE a.LABEL = :label
)
```

Le but est de permettre un workflow no-code :

1. construire une requête qui retourne une seule colonne ;
2. lui ajouter des paramètres ;
3. la sauvegarder ;
4. la réutiliser dans une autre requête via un filtre `IN`, `=`, `EXISTS`, etc.

## Vues

Le parser détecte maintenant les `CREATE VIEW` et les expose comme tables sélectionnables.

Deux formes sont supportées :

```sql
CREATE VIEW v(a, b) AS SELECT ...
```

et :

```sql
CREATE VIEW v AS SELECT col AS alias, autre_col FROM ...
```

L'inférence de colonnes de vues reste heuristique : les expressions SQL trop complexes peuvent être exposées sous des noms génériques.

## Analyse performance heuristique

La v21 ajoute une analyse statique non-exécutée basée sur :

- colonnes filtrées indexées ou non ;
- colonnes de jointure indexées, uniques ou PK ;
- `GROUP BY` / `ORDER BY` sur colonnes indexées ou non ;
- utilisation de vues ;
- sous-requêtes dans des filtres.

Ce n'est pas un remplaçant d'un vrai `EXPLAIN PLAN`, mais un garde-fou ergonomique pour néophytes.
