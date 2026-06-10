# v31 - Version display and GitHub release check

## Goal

Display the current SqlQueryGenerator version in the main window and check GitHub Releases for a newer version without blocking the UI.

## Behavior

The top bar displays the current version:

```text
SqlQueryGenerator v31.0.0
```

A small refresh button checks GitHub Releases manually.

The app also performs one silent check per session on startup. If a newer release exists, a discreet link appears:

```text
Nouvelle version disponible : v31.1.0
```
