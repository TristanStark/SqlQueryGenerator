# Champs dérivés dans les filtres et tris

La v20 ajoute la possibilité de réutiliser des champs produits par la requête elle-même :

- agrégats : `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` ;
- colonnes calculées : `CASE WHEN ...` ou expression contrôlée.

## Agrégats

Depuis l'onglet **Groupes / agrégats**, chaque agrégat propose :

- `+ Filtre` : ajoute un filtre sur l'agrégat. Le SQL généré utilise automatiquement `HAVING`.
- `+ Tri` : ajoute l'agrégat dans `ORDER BY` via son alias.

Exemple :

```sql
SELECT pnj.race_id,
       COUNT(pnj.id) AS count_id
FROM pnj
GROUP BY pnj.race_id
HAVING COUNT(pnj.id) > 10
ORDER BY count_id DESC
```

## Colonnes calculées

Depuis l'onglet **Colonnes calculées**, chaque colonne propose :

- `+ Filtre` : ajoute un filtre sur l'expression calculée. Le SQL duplique l'expression dans `WHERE`, car les alias de `SELECT` ne sont pas portables dans `WHERE`.
- `+ Tri` : ajoute l'alias dans `ORDER BY`, ce qui est lisible et portable sur les dialectes visés.

Exemple :

```sql
SELECT CASE WHEN pnj.race_id = 1 THEN 'Humain' ELSE 'Autre' END AS race_label
FROM pnj
WHERE CASE WHEN pnj.race_id = 1 THEN 'Humain' ELSE 'Autre' END = 'Humain'
ORDER BY race_label ASC
```

## Règle SQL appliquée

- Colonne normale filtrée => `WHERE`
- Agrégat filtré => `HAVING`
- Colonne calculée filtrée => `WHERE` avec expression complète
- Agrégat ou colonne calculée triée => `ORDER BY alias`
