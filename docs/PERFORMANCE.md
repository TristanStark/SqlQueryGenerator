# Performance notes — ForeignKeyInferer v18

## Ancienne complexité

L'ancien `ForeignKeyInferer.Infer()` parcourait :

```text
source table -> source column -> target table -> target column
```

Pour `C` colonnes totales, cela revient à environ `O(C²)` comparaisons de colonnes.

Sur un schéma de production avec environ :

```text
T = 532 tables
C = 7096 colonnes
```

cela représente environ :

```text
C² = 7096² ≈ 50 353 216 comparaisons
```

Chaque comparaison exécutait plusieurs heuristiques coûteuses : normalisation de noms, singularisation, split de tokens, score de table composée, score de commentaire, puis parfois recherche d'index via `schema.Indexes.Any(...)`.

La partie déduplication utilisait aussi une recherche linéaire dans la liste des relations déjà trouvées, ce qui pouvait ajouter un coût proche de `O(R²)` quand beaucoup de relations candidates étaient générées.

## Nouvelle stratégie v18

La v18 construit d'abord des index mémoire :

- colonnes par nom normalisé ;
- tables par variante de nom ;
- tables par token ;
- colonnes identifiantes seulement (`*_id`, `*_iden`, `*_code`, etc.) ;
- colonnes PK / uniques / indexées ;
- lookup direct de relation déjà vue.

L'inférence ne compare donc plus toutes les colonnes avec toutes les colonnes.

## Nouvelle complexité approximative

```text
Build context: O(T + C + I)
Same column: O(sum(group_size²)) avec garde-fou sur les gros groupes faibles
Table-name pattern: O(F * K)
Composite pattern: O(F * M)
Comments: O(F_comment * T_ref)
Dedup: O(1) moyen par relation via dictionnaire
```

Où :

- `F` = nombre de colonnes ressemblant à des identifiants, généralement beaucoup plus petit que `C` ;
- `K` = nombre de tables candidates portant le stem de la colonne (`job_id` -> `jobs`, `pnj_jobs`) ;
- `M` = nombre de tables contenant les tokens utiles ;
- `T_ref` = tables candidates portant une colonne cible plausible.

En pratique, sur des schémas métier, on passe d'un comportement dominé par `~50M` comparaisons lourdes à quelques milliers / dizaines de milliers de candidats utiles.

## v19 — parallélisation contrôlée

La v19 ajoute une parallélisation par `Parallel.ForEach` sur les phases réellement indépendantes :

- groupes de colonnes de même nom normalisé ;
- colonnes identifiantes `*_id`, `*_iden`, `*_code`, etc. ;
- heuristiques de table composée ;
- heuristiques basées sur les commentaires.

Le merge final reste volontairement séquentiel : cela évite les races conditions dans la déduplication, conserve un résultat déterministe, et permet de garder la règle "meilleure confiance gagne".

Schéma d'exécution :

```text
Build context mémoire                  : séquentiel, O(T + C + I)
Declared FK                            : séquentiel, très faible coût
Heuristiques candidates                : parallèle, CPU-bound
Merge + index bias + déduplication     : séquentiel déterministe
Tri final                              : O(R log R)
```

La parallélisation n'est activée efficacement que sur les schémas assez grands. Pour les petits schémas, `MaxDegreeOfParallelism` reste à `1` afin d'éviter que l'overhead de scheduling coûte plus cher que le travail utile.

Sur une base comme `532 tables / 7096 colonnes`, les phases candidates sont celles qui bénéficient le plus du parallélisme, parce que chaque colonne identifiante peut être scorée indépendamment.
