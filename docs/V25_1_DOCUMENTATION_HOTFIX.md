# V25.1 — Hotfix documentation du code C#

Cette hotfix ne modifie pas le comportement fonctionnel de SQL Query Generator. Elle standardise la documentation XML du code C#.

## Couverture

Les fichiers `.cs` du projet ont été documentés avec des commentaires XML sur les éléments suivants :

- types : `class`, `record`, `struct`, `interface`, `enum` ;
- constructeurs ;
- méthodes publiques, internes et privées ;
- propriétés et champs ;
- commandes et handlers WPF ;
- membres d'énumération ;
- modèles de requêtes, jointures, filtres, agrégats, vues, paramètres et sauvegarde.

## Règle durable

Toutes les futures versions et hotfixes doivent conserver cette règle :

> Chaque nouveau type, constructeur, méthode, propriété, champ ou membre d'énumération ajouté dans un fichier `.cs` doit recevoir un commentaire XML au moment de son introduction.

## Remarque sur les variables locales

Les commentaires XML C# sont destinés aux types et membres de code. Les variables locales internes aux méthodes ne produisent pas une documentation XML exploitable par le compilateur. Elles doivent rester commentées avec des commentaires classiques uniquement lorsqu'elles portent une logique non évidente.

## Vérification recommandée

Avant livraison :

```powershell
dotnet test -c Release
.\publish-win-x64.ps1
```

Pour une revue rapide de la documentation, rechercher les nouveaux membres C# sans bloc `/// <summary>` adjacent.
