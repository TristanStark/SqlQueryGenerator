# V30 — vues et sous-requêtes avec plusieurs HAVING

Cette version renforce deux zones du générateur SQL : la lecture des vues dans le DDL et les requêtes/sous-requêtes contenant plusieurs conditions `HAVING`.

## Vues

Le parser de schéma gère mieux les vues déclarées par `CREATE VIEW` / `CREATE OR REPLACE VIEW` :

- recherche du `AS` de la vue au niveau SQL principal, pas dans une expression interne ;
- prise en charge plus fiable des vues avec CTE (`WITH ... AS (...) SELECT ...`) ;
- inférence des colonnes depuis le `SELECT` final de la vue ;
- conservation des alias explicites, y compris les alias quotés avec espaces ou accents ;
- expansion de `SELECT *` ou `alias.*` quand la table source est déjà connue dans le schéma chargé ;
- propagation simple du type/commentaire depuis la colonne source quand la vue expose directement une colonne existante.

## Sous-requêtes et HAVING multiples

La génération SQL accepte maintenant mieux les sous-requêtes structurées ayant plusieurs filtres post-agrégation :

```sql
HAVING COUNT(table.id) > 1
  AND SUM(table.amount) > 100
```

Le reverse parser conserve également les `HAVING` écrits directement avec une expression d'agrégat brute, par exemple `COUNT(*) > 1`, même si cette expression n'a pas été créée depuis l'onglet agrégats.

## Sécurité

Les presets SQL bruts restent limités aux `SELECT` / `WITH ... SELECT`. Les requêtes DDL/DML restent refusées.
