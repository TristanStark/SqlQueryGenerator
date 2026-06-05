# V36 - Backup-table review during DDL import

This slice improves DDL import safety on large production schemas.

## Problem

Enterprise DDL exports often contain physical tables that are only copies of a real business table:

- backup snapshots
- save/archive copies
- dated restore tables
- temporary import tables

These objects pollute the explorer, inferred joins, and base-table selection. More importantly, they should not disappear silently if the user actually wants to keep them.

## Behavior

DDL import now has an explicit review step.

When likely backup tables are detected:

1. the app shows a review window before the final schema load;
2. detected candidates are pre-selected for exclusion;
3. the user can:
   - import without the selected backup tables;
   - keep all tables;
   - cancel the import;
4. confirmed exclusions are removed from the final imported schema together with related indexes, declared foreign keys, and inferred relationships.

Views remain imported. If a kept view appears to reference an excluded table, the import warnings mention it.

After import, the explorer still exposes a `Masquer tables auxiliaires restantes` toggle for any remaining auxiliary-looking tables that the user decided to keep.

## Detection rules

The review step is intentionally conservative:

- a plausible base table must already exist in the same DDL;
- the candidate must start with that base table name plus a separator such as `_`, `-`, or `$`;
- the candidate must end with trailing digits;
- backup-related keywords strengthen the explanation:
  - `backup`, `back`, `bak`, `bkp`
  - `save`, `saved`, `sauvegarde`
  - `old`
  - `archive`, `arch`
  - `tmp`, `temp`
  - `copy`, `copie`

Examples:

- `CUSTOMER` + `CUSTOMER_20240101`
- `CUSTOMER` + `CUSTOMER_BACKUP_20240101`
- `ORDERS` + `ORDERS_SAVE_42`

If only `CUSTOMER_20240101` exists and `CUSTOMER` does not, the table is not suggested by default.

## Verification

Coverage added in tests:

- suspicious auxiliary names are still detected for the post-import toggle;
- backup candidates require an existing base table;
- candidate detection handles plain dated suffixes and keyword-based suffixes;
- confirmed exclusions remove tables, indexes, foreign keys, and inferred relationships safely;
- views referencing excluded tables stay imported with a warning.
