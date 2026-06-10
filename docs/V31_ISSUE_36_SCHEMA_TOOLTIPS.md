# v31 - Schema column tooltips

## Objectif

Afficher une documentation utile au survol des tables et colonnes dans l'arbre de schéma.

## Comportement

Au survol d'une table, l'application affiche :

- le nom SQL complet ;
- le nom affiché si différent ;
- le type d'objet : table ou vue ;
- le nombre de colonnes visibles / total ;
- la documentation importée ou parsée, si disponible.

Au survol d'une colonne, l'application affiche :

- le nom SQL complet ;
- le type SQL ;
- la nullability ;
- les rôles détectés : PK, FK déclarée, FK probable, index, index unique ;
- les détails de relation ;
- les détails d'index ;
- la documentation importée ou parsée, si disponible.

## Contraintes

- Aucun accès base de données.
- Aucun ralentissement volontaire du chargement de schéma.
- Les tooltips utilisent les métadonnées déjà présentes dans les ViewModels.
- Si aucun commentaire n'existe, un message de fallback est affiché.
