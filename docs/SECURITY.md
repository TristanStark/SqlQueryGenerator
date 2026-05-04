# Sécurité et hardening

## Garanties

- L'application ne crée aucune connexion base de données.
- Elle n'exécute jamais le SQL généré.
- Le modèle ne permet pas de générer UPDATE, DELETE, INSERT ou DDL.
- Les expressions personnalisées refusent les points-virgules, commentaires SQL et mots-clés DDL/DML.
- Les identifiants SQL sont validés avant génération.
- Les chaînes littérales sont échappées.
- Le parser et l'UI bornent la taille des schémas chargés.

## Limites

Le SQL généré reste à relire avant exécution, surtout si les relations sont inférées et non déclarées. Une inférence heuristique peut proposer une jointure plausible mais incorrecte métier.

## Recommandation d'usage

Pour des bases de production, exécuter d'abord le SQL généré dans un environnement de recette ou en lecture seule. Vérifier les plans d'exécution si la requête touche de gros volumes.
