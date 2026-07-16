# Changelog

All release notes are centralized here.

## Unreleased

- Added full reverse-engineering and regeneration support for compound SELECT queries using `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT`, and Oracle `MINUS`, including nested parenthesized branches and global `ORDER BY`/row limits.

- Added reverse SQL source dialect profiles for import/rewrite workflows.
- Added clause-level reverse SQL coverage with a confidence score and structured diagnostics.
- Reverse SQL now ignores SQL comments before parsing and preserves Cognos `#prompt(...)#` parameters.
- Reverse SQL failures now expose clearer clause/fragment diagnostics intended for UI localization.
- Expanded heuristic performance analysis with index-aware warnings for `SELECT *`, leading-wildcard `LIKE`, expression filters, missing limits, fragile joins, and high-cardinality grouping/sorting.
- The probable-relationships list now shows whether a candidate join is already used and lets you add a join directly from the row.
- The reverse/rewrite tab now exposes a SQL comparison view with raw/rewrite/builder comparison modes, optional ignore-whitespace / ignore-case diffing, and visible reverse diagnostics panels.
- Added undo/redo history for builder state with toolbar buttons, keyboard shortcuts, and tested snapshot restoration.
- DDL import now reviews likely backup/history/tmp/staging tables before final load, lets the user exclude them explicitly, filters related metadata safely, and still offers a toggle for remaining auxiliary tables.
- Improved the main window ergonomics for narrower/1080p screens with a dedicated status band, a persistent right-side diagnostics column, relaxed minimum sizes, and tabbed validation panels.
- Rebalanced the reverse-SQL tab so the raw editor keeps usable height while detailed reverse diagnostics stay in the persistent right column.
- Added documentation for documentation-file imports and the reverse SQL diagnostics/profile workflow.
- Added documentation for the expanded performance hints and probable-join workflow.
- Added documentation for the SQL comparison workflow in reverse/rewrite mode.
- Added documentation for builder undo/redo history.
- Strengthened undo/redo regression coverage for bounded history depth and full snapshot restoration.
- Added app-level workflow regression coverage for join editing and undo after reverse/saved-query loads.
- Harmonized reverse SQL set-operation handling across `UNION`, `INTERSECT`, `EXCEPT`, and Oracle `MINUS`, including partial-import diagnostics and regression fixtures.
- Added extra regression coverage for severity-grouped performance hints and probable-join action labels.
- Added documentation for auxiliary-table filtering after DDL import.
- Added documentation for the layout/ergonomics pass.
- Added rich schema tooltips for tables and columns, showing comments/documentation plus technical metadata such as type, nullability, PK/FK and index information.
## v31.0.0

- Added a conservative SQL rewrite flow for imported `SELECT` statements, including implicit join modernization, duplicate clause cleanup, alias preservation, and warnings for advanced constructs.
- Added a top-bar DDL export helper that generates copy-ready Oracle `DBMS_METADATA.GET_DDL` and SQLite `sqlite_master` queries.
- Added a visible `Help` button that opens the local v31 workflow guide or falls back to the GitHub documentation page.
- Improved reverse SQL continuity so imported queries remain editable in the visual builder with preserved joins, filters, grouping, ordering, parameters, and table aliases.
- Added regression/unit tests for rewrite behavior, reverse-builder continuity, import warnings, and DDL export command generation.

## v30.1.0

- Reverse SQL now supports Oracle legacy outer-join syntax using `(+)` and converts it to explicit `LEFT JOIN` in the query model.
- Reverse SQL parameter detection now supports Oracle substitution variables such as `&1` and preserves them during regeneration.
- Added regression/unit tests for legacy `(+)` parsing and `&` parameter round-trip generation.

## v30.0.0

- Improved views support and subqueries with multiple `HAVING` conditions.
- Details: `docs/V30_VIEWS_AND_MULTI_HAVING.md`

## v29.0.0

- Left-side search performance improvements.
- Details: `docs/V29_LEFT_SEARCH_PERFORMANCE.md`

## v28.0.0

- Added raw SQL presets and reverse features.
- Details: `docs/V28_RAW_SQL_PRESETS_AND_REVERSE.md`

## v27.2.0

- Portable publishing hotfix.
- Details: `docs/V27_2_PORTABLE_PUBLISHING_HOTFIX.md`

## v27.1.0

- Selection checkbox hotfix.
- Details: `docs/V27_1_SELECTION_CHECKBOX_HOTFIX.md`

## v27.0.0

- UI selection hotfix.
- Aggregate goal heuristic package and service improvements.
- Details: `docs/V27_UI_SELECTION_HOTFIX.md`, `docs/v27-aggregate-goal-heuristic.md`, `README_v27.md`

## v26.0.0

- Multi-select wildcard and scroll improvements.
- Details: `docs/V26_MULTI_SELECT_WILDCARD_SCROLL.md`

## v25.1.0

- Documentation hotfix: XML documentation coverage policy for C# code.
- Details: `docs/V25_1_DOCUMENTATION_HOTFIX.md`

## v25.0.0

- Better large-schema UX and performance.
- Faster column search with FK/index caches.
- Better manual join input suggestions and delayed filtering.
- Cleaner `schema.table` display while preserving generated SQL accuracy.
- Safer alias handling for accents and spaces.
- Stronger saved-query description support.
- Details: `docs/V25_SEARCH_DISPLAY_ALIAS_SAVE.md`

## v24.0.0

- Composite multi-column joins with per-condition toggles.
- Drag and drop support for extra join pairs.
- Composite auto-join planning for reliable same-table pairs.
- Faster join path calculation with adjacency maps.
- Import of table/column documentation from CSV/TSV/TXT.
- Details: `docs/V24_COMPOSITE_JOINS_AND_DOCUMENTATION.md`

## v23.0.0

- TreeView binding fix.
- Details: `docs/V23_TREEVIEW_BINDING_FIX.md`

## v21.0.0

- Added parameters (`?`, `:name`, `@name`).
- Saved/loaded structured queries in JSON.
- Reused saved queries as filtering subqueries.
- Added `CREATE VIEW` parsing.
- Added heuristic performance analysis around indexes, views, filters, sort, and joins.
- Details: `docs/V21_SUBQUERIES_SAVED_QUERIES_PERFORMANCE.md`

## v20.0.0

- Aggregates and calculated columns can now be pushed to filters/sort.
- Aggregate filters generate `HAVING`.
- Aggregate sorts generate `ORDER BY alias`.
- Calculated column filters reuse full expression in `WHERE`.
- Calculated column sorts use alias in `ORDER BY`.
- Details: `docs/DERIVED_FIELDS.md`

## v18.0.0

- Optimized `ForeignKeyInferer.Infer()` for large schemas.
- Reduced near-`O(C^2)` behavior by using dictionaries and plausible candidate generation.
- Details: `docs/PERFORMANCE.md`

## v12.0.0

- Fixed inline column comment parsing regression after column-separating commas.

## v10.0.0

- Improved UI and joins.
- Replaced available-columns panel with a table-grouped TreeView.
- Added `PK`/`FK` badges and type coloring.
- Added right-click actions to send columns directly to `SELECT`, `WHERE`, `GROUP BY`, `ORDER BY`, and aggregates.
- Added `+ Manual join` in the joins tab.
- Improved relation inference for specialized/composite naming patterns.

## v8.0.0

- Improved automatic join detection to avoid generic false joins like `pnj.id = jobs.id` when specific joins exist (for example `pnj.job_id = jobs.id`).
- Added simple plural handling and selected compound-name cases.

## v7.0.0

- Added conditional aggregates in the groups/aggregates tab.
- Added `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` with `Distinct` option.
- Added target + optional condition model for each aggregate.
- Kept flat query generation (`SELECT`/`JOIN`/`WHERE`/`GROUP BY`) without unnecessary subqueries.

## v6.0.0

- Maximized startup window and split UI into two main zones.
- Better resizing behavior for query-building vs SQL output areas.
- Added faster column search and double-click column-to-`SELECT` action.
- Increased grid column minimum widths.
- Added loaded-schema summary in top bar.
- Enlarged SQL output area for easier copy/paste.
