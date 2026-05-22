# V28 — Presets SQL bruts et reverse engineering SQL

Cette version ajoute deux flux de travail destinés aux requêtes legacy déjà écrites à la main.

## Preset SQL brut

Dans l’onglet **Sous-requêtes / sauvegarde**, le bloc **SQL brut / reverse engineering** permet de coller un `SELECT` existant.

Actions disponibles :

- **Sauvegarder SQL brut** : stocke le texte comme preset JSON local dans `saved_queries/`.
- **Charger SQL brut sélectionné** : recharge un preset SQL brut, ou le dernier SQL généré d’un preset construit.
- **Utiliser comme sous-requête filtrante** : fonctionne aussi avec les presets SQL bruts.
- **Depuis SQL généré** : copie la requête générée actuelle dans l’éditeur SQL brut.

Les presets SQL bruts sont validés avant sauvegarde et avant insertion comme sous-requête :

- seuls `SELECT` et `WITH ... SELECT` sont acceptés ;
- `DELETE`, `UPDATE`, `INSERT`, `DROP`, `ALTER`, etc. sont refusés ;
- une seule requête est acceptée ;
- les commentaires SQL sont refusés pour éviter les injections ou collages ambigus.

## Reverse SQL → constructeur

Le bouton **Reverse SQL → constructeur** analyse le SQL collé et remplit le constructeur visuel quand c’est possible.

Reconnaissance prise en charge :

- `SELECT` simple ;
- `DISTINCT` ;
- colonnes simples `table.colonne` ;
- alias `AS alias` ;
- agrégats `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` ;
- `FROM` ;
- `INNER JOIN` / `LEFT JOIN` avec prédicats `a.col = b.col` ;
- jointures composites avec `AND` ;
- filtres `WHERE` simples ;
- paramètres `?`, `:nom_param`, `@nom_param` ;
- sous-requête brute dans un filtre `IN (SELECT ...)` ;
- `GROUP BY` ;
- `ORDER BY`.

## Limites assumées

Le reverse engineering reste heuristique et volontairement prudent. Les cas complexes peuvent rester en colonne calculée brute ou ne pas être reconstruits totalement :

- CTE complexes ;
- fonctions imbriquées très profondes ;
- expressions `OR` complexes ;
- parenthèses de logique booléenne avancée ;
- alias SQL très ambigus.

Dans ces cas, le SQL brut peut quand même être sauvegardé et utilisé tel quel comme preset.
