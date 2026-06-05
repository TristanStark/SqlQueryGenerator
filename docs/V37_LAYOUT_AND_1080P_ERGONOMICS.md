# V37 - Layout and 1080p ergonomics

This slice improves the main window layout for narrower desktop screens and more crowded builder sessions.

## Main changes

The top chrome is now split into:

- action toolbar
- schema summary badge
- dedicated full-width status band

This prevents the status text from fighting the toolbar for horizontal space.

## Validation area

The diagnostics surface is now a persistent full-height right column instead of sharing the bottom row with generated SQL.

This column uses tabs instead of stacking panels vertically:

- `But`
- `Performance`
- `Avertissements`
- `Reverse SQL`

This reduces vertical pressure, keeps diagnostic text visible while editing, and leaves the generated SQL area wider and easier to copy on 1080p displays.

Inside the reverse-SQL tab, the internal duplicate diagnostics panel was removed so the raw SQL editor keeps a meaningful height. Detailed reverse diagnostics now stay in the persistent right column, while the reverse tab itself focuses on:

- the raw SQL editor
- reverse / rewrite actions
- SQL comparison

## Sizing

Several restrictive minimum sizes were relaxed so the window remains usable at narrower widths:

- lower window minimum size
- smaller left/right split minimums
- less aggressive minimum heights in the main vertical split

The goal is not to redesign the app, but to keep the current feature set usable without requiring a very wide monitor.

## Verification

Verified with:

- `dotnet test`
- `dotnet build src\SqlQueryGenerator.App\SqlQueryGenerator.App.csproj --no-restore`
- rendered window captures via `capture-layout-preview.ps1`

The preview helper now captures:

- a 1920x1080 builder view with the persistent diagnostics column
- the dedicated `Reverse SQL` diagnostics tab
- a reverse-import failure state with the failing fragment selected in the raw SQL editor
- a tighter compact rendering that approximates the reduced workspace felt at higher DPI / 125% scaling
