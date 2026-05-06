# V25 — recherche, affichage, alias et sauvegarde

## Optimisations de recherche dans les colonnes

La recherche de colonnes ne reconstruit plus les résumés FK/index à chaque frappe. Les dictionnaires suivants sont construits au chargement du schéma et réutilisés pendant le filtrage :

- résumés de relations probables par colonne ;
- résumés d'index par colonne ;
- colonnes uniques/indexées ;
- tables triées ;
- noms de colonnes par table.

Le filtrage reste instantané sur des schémas larges et évite les rescans coûteux de `DatabaseSchema.Indexes`.

## Optimisations des jointures manuelles

Les champs de colonnes des jointures manuelles utilisent maintenant des `ComboBox` éditables avec suggestions de colonnes selon la table choisie. Les bindings texte ont un délai de 350 ms afin d'éviter de régénérer le SQL à chaque caractère tapé.

## Affichage `schema.table`

Quand les tables sont nommées `nom_schema.nom_table`, l'interface affiche par défaut `nom_table` pour réduire le bruit visuel. La valeur complète reste conservée dans le modèle et dans le SQL généré.

## Alias avec accents et espaces

Les alias comme `Âge moyen`, `Total payé` ou `Nombre de PNJ` sont maintenant générés comme identifiants délimités :

```sql
SELECT pnj.age AS "Âge moyen"
```

Les caractères dangereux (`;`, commentaires SQL, parenthèses, virgules, quotes simples) restent refusés.

## Description de requête sauvegardée

Le champ description de sauvegarde est maintenant plus visible, multi-ligne, et persiste dans le JSON de la requête sauvegardée.
