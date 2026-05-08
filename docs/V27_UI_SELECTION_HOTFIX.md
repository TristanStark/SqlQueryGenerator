# V27 UI Selection Hotfix

## Objectif

Cette hotfix corrige plusieurs irritants ergonomiques apparus dans l'interface de la V27.

## Changements

- Réduction des largeurs internes du TreeView des colonnes disponibles.
- Désactivation du scroll horizontal du TreeView des colonnes disponibles afin d'éviter de masquer la zone d'expansion des tables.
- Ajout de la sélection multiple par `Ctrl+clic` sur les colonnes.
- Ajout de la sélection par plage avec `Shift+clic` entre la dernière colonne ancre et la colonne cliquée.
- Les sélections multiples utilisent les cases `IsBulkSelected` déjà présentes, puis le bouton `+ Sélection cochée` ajoute les colonnes au `SELECT`.
- Correction de la zone d'action des jointures manuelles : les boutons `Retirer` et `+ Colonne` sont maintenant disposés sur une seule ligne, en deux colonnes, avec une largeur suffisante pour éviter le rognage visuel.

## Notes techniques

Le `TreeView` WPF ne supporte pas nativement la sélection multiple. La hotfix utilise donc le modèle existant de cases cochées comme représentation de la sélection multiple. `Ctrl+clic` bascule la case de la colonne cliquée. `Shift+clic` coche la plage visible entre l'ancre et la colonne cible.

Le comportement de double-clic et de drag & drop existant reste inchangé.
