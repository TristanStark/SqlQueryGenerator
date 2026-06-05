# V35 - Undo/redo history for the query builder

This slice adds builder-state history so the main construction workflow is no longer purely destructive.

## User-facing behavior

The top toolbar now exposes:

- `Annuler`
- `Rétablir`

Keyboard shortcuts are also available:

- `Ctrl+Z`
- `Ctrl+Y`

## Scope

The history restores the current builder session as a snapshot, including:

- base table
- selected columns
- filters
- groupings
- orderings
- aggregates
- joins
- calculated columns
- parameters
- generation options such as target dialect, quoted identifiers, and auto-grouping
- the current raw SQL editor content and selected reverse dialect profile

History is intentionally reset when a new schema is loaded, because previous query states may no longer be compatible with the new metadata.

## Implementation notes

The app now captures immutable `QueryBuilderHistoryState` snapshots and uses a `QueryBuilderHistoryService` in `Core` to manage:

- current state
- undo stack
- redo stack

This keeps the history logic unit-testable outside WPF and avoids coupling undo/redo to individual button handlers.

The first version intentionally keeps a bounded history depth of `100` snapshots to avoid unbounded memory growth.

## Verification

Coverage added in tests:

- changed state enables undo
- identical state does not create duplicate history entries
- redo is cleared after a new branch of edits
- snapshot cloning preserves independence of query models
- oldest undo states are trimmed once the maximum depth is reached
- undo restores raw SQL text, reverse dialect profile, aliases, and query metadata
- loading a reversed SQL query into the builder can be undone
- loading a saved builder query can be undone
