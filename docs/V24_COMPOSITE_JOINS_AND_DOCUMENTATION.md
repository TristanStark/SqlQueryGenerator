# V24 — Jointures composites, performance du planner et documentation métier

## Jointures composites

Une jointure peut maintenant contenir plusieurs couples de colonnes :

```sql
A.acti_iden = B.acti_iden
AND A.emet_iden = B.emet_iden
AND A.soaa_date = B.soaa_date
```

Dans l'onglet **Jointures**, chaque jointure manuelle possède un bouton **+ Colonne**. Chaque colonne supplémentaire peut être activée ou désactivée indépendamment.

Le modèle conserve la compatibilité avec les anciennes requêtes : `FromColumn` / `ToColumn` restent le couple principal, et `AdditionalColumnPairs` contient les couples supplémentaires.

## Jointures composites automatiques

Quand le planner détecte une relation entre deux tables, il cherche maintenant les autres relations fiables entre les mêmes tables et les ajoute comme prédicats `AND`, si elles ressemblent à des composantes de clé composite : mêmes noms, colonnes `*_iden`, `*_id`, `*_code`, `*_date`, etc.

Si une relation auto est décochée dans l'arbre des relations détectées, elle n'est pas ajoutée dans la jointure composite automatique.

## Optimisation du calcul de chemin

La recherche multi-hop utilise maintenant une adjacency map précalculée :

- avant : scan de `schema.Relationships` à chaque nœud BFS ;
- maintenant : dictionnaire `table -> relations sortantes` construit une fois par recherche.

Cela réduit fortement le coût quand beaucoup de relations sont détectées.

## Import de documentation table / colonne

Le bouton **Importer doc CSV/TSV** accepte un fichier exporté d'un tableau Excel, Word ou LibreOffice.

Format recommandé TSV :

```tsv
table_name	column_name	display_name	description
ACTI		Actions	Table des actions métier
ACTI	ACTI_IDEN	Identifiant action	Clé technique de l'action
ACTI	EMET_IDEN	Émetteur	Identifiant de l'émetteur
```

`column_name` vide signifie : documentation de la table.

Délimiteurs reconnus : tabulation, point-virgule, virgule, pipe.

Noms de colonnes acceptés :

- table : `table`, `table_name`, `nom_table`, `table_physique`, `object_name`
- colonne : `column`, `column_name`, `nom_colonne`, `champ`, `field`
- nom lisible : `display_name`, `nom_fonctionnel`, `libelle`, `label`, `meaning`, `signification`
- description : `description`, `comment`, `commentaire`, `definition`, `details`, `notes`

Cette documentation remplace / enrichit les commentaires affichés dans le TreeView, les tooltips et les futures inférences métier.
