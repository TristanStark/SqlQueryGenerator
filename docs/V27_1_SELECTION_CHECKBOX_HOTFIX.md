# V27.1 Selection Checkbox Hotfix

## Scope

This hotfix only adjusts the column TreeView selection checkbox layout.

## Changes

- Increased the dedicated checkbox column width from `24` to `34`.
- Overrode the global `CheckBox` margin for column-selection checkboxes.
- Set an explicit checkbox width and zero padding to prevent the selection square from being clipped on the right side.

## Functional impact

No query-generation behavior was changed. This is a UI-only fix.
