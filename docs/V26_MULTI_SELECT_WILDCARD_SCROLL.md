# V26 — Multi-sélection, table.* et panneaux scrollables

## Sélection multiple de colonnes

Le panneau **Colonnes disponibles** expose maintenant une case à cocher sur chaque colonne.
Le bouton **+ Sélection cochée** ajoute toutes les colonnes cochées au `SELECT` sans créer de doublons.
Le bouton **Décocher** vide la sélection multiple visuelle.

## Projection table.*

Sélectionner une table puis utiliser **Table .*** ajoute une projection `table.*` au `SELECT`.
Le menu contextuel d'une table propose également **Ajouter table.* au SELECT**.

La génération SQL traite `*` comme un cas spécial : l'identifiant `*` n'est jamais quoté et ne reçoit pas d'alias.

## Panneaux but / performance

Les panneaux **But probable de la requête** et **Analyse performance heuristique** utilisent maintenant des `ScrollViewer`.
Les longues explications ne masquent plus les statistiques d'index ni les avertissements.

## Règle de maintenance

Les nouveaux champs, propriétés, commandes et méthodes introduits en v26 sont documentés avec commentaires XML conformément à la règle de documentation du projet.
