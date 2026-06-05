# V33 - Index-aware performance hints and probable-join actions

This slice extends the existing heuristic performance panel and tightens the join-candidate workflow in the builder.

## Performance analysis

`QueryPerformanceAnalyzer` now emits more specific hints from the current query shape and loaded schema metadata.

The main additions are:

- warning on `SELECT *`
- warning on leading-wildcard `LIKE '%value'`
- warning on filters built from expressions or functions such as `UPPER(...)` or `TO_CHAR(...)`
- info when a likely large query has no `LIMIT` / `FETCH` / `TOP`
- warning on `ORDER BY` over non-indexed columns
- warning on `GROUP BY` with many columns
- critical signal when multiple tables are involved without explicit joins
- warning when neither side of a join appears to be PK/unique

The report output is also grouped by severity so critical and warning items are easier to scan in the UI.

## Probable joins

The probable-relationships list in the `Jointures` tab now reflects whether a candidate is already used in the current query.

Each row now:

- keeps the existing `Auto` toggle for planner participation
- shows an `Ajouter` / `Ajoutee` action state
- lets the user add the relationship directly from the row without relying on the separate selected-item button

This is intended to reduce friction when iterating on inferred joins in large schemas.

## Verification

Regression coverage was added for:

- `SELECT *` + leading wildcard + missing limit
- expression-based filters
- wide `GROUP BY` + non-indexed `ORDER BY`
- multiple tables without joins
- explicit `LEFT JOIN` generation for manual join rows
- adding a detected relationship into current joins and marking it as used
- removing an active join and refreshing the candidate-used state
