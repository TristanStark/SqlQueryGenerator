# SQL Query Generator

Visual WPF tool to build safe `SELECT` SQL queries from a schema file (`.sql` or `.txt`) without AI or LLM.

## Screenshot

![SQL Query Generator screenshot](logo.png)

## Features

- Parse schema definitions (`CREATE TABLE`, PK/FK, inline comments, Oracle `COMMENT ON COLUMN`).
- Infer relations when foreign keys are missing (heuristic scoring).
- Build queries visually with drag and drop: select, filters, joins, groups, aggregates, sort, and calculated columns.
- Generate SQL for `Generic`, `SQLite`, and `Oracle` dialects.
- Keep generation read-only (`SELECT` only, no execution, no DDL/DML output).
- Support manual joins and advanced aggregate/filter workflows.

## Download

Latest release: see GitHub Releases.

## Quick start

1. Open the app and load or paste a schema.
2. Choose a base table.
3. Drag columns into query blocks (`SELECT`, `WHERE`, `GROUP BY`, aggregates, `ORDER BY`, calculated fields).
4. Review inferred joins and add manual joins if needed.
5. Copy generated SQL and run it in your SQL client.

Run scripts from PowerShell:

```powershell
.\run-app.ps1
```

## Build from source

Requirements:

- Windows
- Visual Studio 2022+ with `.NET desktop development`
- .NET 8 SDK

Build:

```powershell
.\build.ps1
```

Tests:

```powershell
.\run-tests.ps1
```

Publish (framework-dependent):

```powershell
.\publish-win-x64.ps1
```

Publish (self-contained):

```powershell
.\publish-win-x64-self-contained.ps1
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Publishing](docs/PUBLISHING.md)
- [Performance](docs/PERFORMANCE.md)
- [Security](docs/SECURITY.md)
- [Changelog](CHANGELOD.md)

## Roadmap

See GitHub Projects.

## License

No license file is currently included in this repository.
