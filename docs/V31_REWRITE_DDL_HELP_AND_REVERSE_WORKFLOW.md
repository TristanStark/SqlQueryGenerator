# v31.0.0 - SQL Rewrite, DDL Export, Help, Reverse Workflow

## Scope

This release covers:

- Issue 14: rewrite imported SQL into a cleaner modern form.
- Issue 15: export the helper SQL used to retrieve DDL from Oracle or SQLite.
- Issue 16: expose a visible `Help` button in the main window.
- Issue 20: keep reverse-imported SQL editable inside the visual builder.

## SQL Rewrite

The raw SQL area now has a `Reecrire SQL` action.

Use it when you want a conservative cleanup of an existing `SELECT` without loading it into the visual builder.

Current conservative rewrites:

- Convert implicit joins written as `FROM A, B WHERE A.id = B.a_id` into explicit `INNER JOIN`.
- Convert Oracle legacy outer joins using `(+)` into explicit `LEFT JOIN`.
- Remove duplicated filters, selected columns, `GROUP BY` items, and `ORDER BY` items.
- Preserve parameter placeholders such as `:name`, `@name`, `?`, `&1`, and `&my_var`.
- Preserve table aliases when the imported SQL uses them.

Warnings are emitted when the source SQL contains advanced constructs such as:

- `WITH`
- nested subqueries
- `UNION`, `INTERSECT`, `MINUS`
- Oracle-specific hierarchical/model clauses

These warnings are intentional. The rewrite service is conservative and should not pretend unsupported fragments are fully modeled.

## Reverse SQL -> Builder

The `Reverse SQL -> constructeur` action still imports the raw `SELECT` into the visual builder.

The important workflow change in v31 is that the reverse-loaded query remains editable in the builder, including:

- selected columns
- explicit or recovered joins
- `WHERE`
- `GROUP BY`
- `HAVING`
- `ORDER BY`
- parameter placeholders
- preserved table aliases

This means you can:

1. Paste a legacy or hand-written `SELECT`.
2. Reverse-load it into the builder.
3. Continue editing it with the UI.
4. Regenerate a cleaned SQL statement from the builder.

## DDL Export Helper

The top bar now exposes an `Export DDL` helper.

It generates and copies a SQL snippet you can run manually in your SQL client:

- `Oracle`: query built around `DBMS_METADATA.GET_DDL(...)` over `all_objects`
- `SQLite`: query built against `sqlite_master`

The `Schema / base` field means:

- Oracle: schema owner, for example `APP_OWNER`
- SQLite: attached database name, usually `main`

The generated snippet is displayed in the UI and copied to the clipboard.

## Help Button

The `Help` button opens this document first when it is available locally.

If the local file is unavailable, the application falls back to the GitHub copy of the same document.

Opening help is best-effort only and does not block startup.
