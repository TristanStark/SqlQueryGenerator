# V29 — optimisation recherche colonne gauche et RAM

## Problème corrigé

Sur des schémas de production volumineux, par exemple environ 532 tables et 7096 colonnes, la recherche dans l'arbre de gauche pouvait devenir très lente et consommer énormément de RAM.

La cause principale était côté UI : chaque frappe reconstruisait l'arbre visible en recréant de nombreux `TableItemViewModel` et `ColumnItemViewModel`. Avec WPF, cela déclenche aussi de nombreux bindings, templates et conteneurs visuels, ce qui augmente fortement la pression mémoire.

## Changements

- Ajout d'un index de recherche préconstruit : `ColumnSearchIndexEntry`.
- Réutilisation des `TableItemViewModel` et `ColumnItemViewModel` existants au lieu de les recréer à chaque recherche.
- Débounce de la recherche avec `DispatcherTimer` : la recherche ne s'exécute plus à chaque caractère instantanément.
- Limitation du nombre de colonnes rendues lors d'une recherche large : 650 résultats visibles maximum.
- Message dans la barre de statut lorsque la recherche est tronquée.
- Activation de la virtualisation/recycling sur le `TreeView` des colonnes.
- Les commandes de sélection groupée utilisent maintenant le cache global `AllColumns`, pas seulement les colonnes visibles.

## Effet attendu

La recherche doit devenir beaucoup plus fluide et éviter les explosions de mémoire lors de filtres trop larges.

Si une recherche est trop générale, l'UI affiche les premiers résultats et demande d'affiner le filtre au lieu de tenter de rendre plusieurs milliers de lignes WPF d'un coup.

## Note technique

La complexité de recherche reste linéaire sur le nombre de colonnes indexées, mais le coût d'allocation est fortement réduit :

```text
Avant : frappe clavier -> recréation de l'arbre + recréation des ViewModels visibles
Après : frappe clavier -> debounce -> scan de chaînes préindexées -> réutilisation des ViewModels
```

Le scan de 7096 chaînes préindexées est peu coûteux. Le vrai gain vient de l'arrêt des allocations répétées et du plafonnement du rendu WPF.
