# V34 - SQL comparison view and visible reverse diagnostics

This slice improves the raw SQL workflow inside the `Rétro-ingénierie SQL` tab.

## SQL comparison view

The tab now includes a dedicated `Comparaison SQL` panel.

It supports three explicit comparison modes:

- `Brut / Réécrit`
- `Brut / Constructeur`
- `Réécrit / Constructeur`

The panel is populated automatically after:

- `Réécrire SQL`, defaulting to raw SQL vs rewritten SQL
- `Charger dans le constructeur`, defaulting to raw SQL vs builder-generated SQL

The user can then switch the comparison pair without modifying the current query state.

The comparison is line-based and aligned side by side. Each row shows:

- the change kind (`=`, `~`, `+`, `-`)
- the source line number and text
- the result line number and text

Two optional filters are also available:

- `Ignorer espaces`
- `Ignorer casse`

This makes it easier to validate conservative rewrites and to see what the reverse-imported builder can or cannot reproduce exactly.

## Reverse diagnostics

The reverse workflow already produced:

- clause coverage
- a confidence score
- structured diagnostics

These outputs are now visible in the UI through a `Diagnostics Reverse` panel instead of only feeding internal state and warning text.

## Verification

Coverage added in tests:

- identical SQL comparison
- modified line comparison for implicit join modernization
- added-line comparison for extra clauses
- whitespace-insensitive comparison
- case-insensitive comparison
- rewrite service comparison payload population
