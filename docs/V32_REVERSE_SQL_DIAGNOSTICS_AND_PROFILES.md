# V32 - Reverse SQL diagnostics, source profiles, comments, and Cognos prompts

This iteration strengthens the reverse SQL workflow in four areas:

- clause-level coverage and confidence;
- source SQL dialect profiles;
- comment-tolerant reverse parsing;
- Cognos prompt preservation;
- clearer reverse SQL error localization.

## Source SQL profile

The `Rétro-ingénierie SQL` tab now exposes a source profile selector.

Available profiles:

- `GenericSql`
- `OracleLegacy`
- `OracleModern`
- `Db2`
- `SQLite`
- `PostgreSql`
- `SqlServer`
- `MySqlMariaDb`

The current parser still stays conservative, but the selected profile is now propagated through reverse import and rewrite so profile-specific warnings can be shown and future dialect extensions have a stable entry point.

## Coverage report

After a reverse import, the workflow now computes a structured coverage report with clause-level status and a confidence score.

Covered clauses include:

- `SELECT`
- `FROM/JOIN`
- `WHERE`
- `GROUP BY`
- `HAVING`
- `ORDER BY`
- `CTE`
- `Subqueries`
- `Set operations`
- `Vendor-specific`

The confidence score is heuristic only. It helps answer:

> "How much of the original SQL was probably understood well enough to keep editing in the visual builder?"

## Reverse diagnostics

Reverse import now produces structured diagnostics instead of plain warning strings only.

Examples:

- comments ignored before parsing;
- advanced constructs only partially modeled;
- incomplete `WHERE` / `GROUP BY` / `HAVING` / `ORDER BY` clauses;
- source-profile compatibility hints.

When a reverse import fails with a localized fragment, the WPF UI can focus the raw SQL editor selection on the problematic area.

## SQL comments are ignored

Reverse SQL preprocessing now strips:

- `-- single-line comments`
- `/* block comments */`

This happens before structural parsing, while preserving string literals such as:

```sql
WHERE message LIKE '%-- not a real comment%'
```

Comments are ignored for reverse import only; they are not preserved in the visual builder model.

## Cognos prompt support

Reverse SQL now recognizes Cognos prompt macros such as:

```sql
#prompt("Customer Id", "integer")#
#prompt('Customer Name', 'string')#
```

Detected prompts are:

- stored as query parameters;
- tagged as `CognosPrompt`;
- preserved as raw expressions;
- regenerated unchanged in SQL filters.

## Notes and limits

- The coverage score is conservative and not a semantic SQL equivalence proof.
- CTEs and set operations are still reported as unsupported or partial rather than fully modeled.
- The dialect profile is an import/rewrite hint; it is not yet a full dedicated parser per engine.
