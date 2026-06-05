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
- Review heuristic performance hints for `SELECT *`, wildcard `LIKE`, expression filters, missing limits, weak joins, and sort/group risks.
- Reverse old `SELECT` statements with source dialect profiles, coverage/confidence diagnostics, ignored SQL comments, and preserved Cognos prompts.
- Compare raw SQL, rewritten SQL, and builder-regenerated SQL with a line-by-line diff view, including optional ignore-whitespace and ignore-case modes.
- Undo and redo builder changes with toolbar actions or `Ctrl+Z` / `Ctrl+Y`.
- Review likely backup/history/tmp/staging tables during DDL import, exclude them before final load, and optionally hide remaining auxiliary tables afterwards.
- Use a denser 1080p-friendly layout with a dedicated status band, a persistent right-side diagnostics column, and tabbed validation panels.

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

### Corpus de régression reverse/rewrite SQL

Les cas de régression reverse SQL sont stockés dans :

```text
tests/SqlQueryGenerator.Tests/Fixtures/ReverseSql/
```

Chaque cas est piloté par un fichier `*.input.sql` et peut ajouter, selon le besoin :

- `case.expected.sql` pour comparer le SQL réécrit après reverse puis génération ;
- `case.expected-model.json` pour figer le modèle `QueryDefinition` reconstruit ;
- `case.expected-warnings.txt` pour figer les avertissements de génération ;
- `case.expected-error.txt` pour les syntaxes explicitement non prises en charge ;
- `case.schema.sql` si une génération a besoin d'un schéma de support.

Pour ajouter un nouveau cas :

1. crée `nom_du_cas.input.sql` ;
2. ajoute seulement les fichiers d'attente utiles pour ce scénario ;
3. lance `dotnet test tests\SqlQueryGenerator.Tests\SqlQueryGenerator.Tests.csproj`.

Le helper de test normalise les différences de whitespace SQL non significatives afin d'éviter des faux positifs sur le formatage seul.

## Utilisation

```powershell
.\publish-win-x64.ps1
```

Publish (self-contained):

```powershell
.\publish-win-x64-self-contained.ps1
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Documentation Import Guide](docs/DOCUMENTATION_IMPORT_GUIDE.md)
- [Publishing](docs/PUBLISHING.md)
- [Performance](docs/PERFORMANCE.md)
- [Security](docs/SECURITY.md)
- [Operations Guide](docs/OPERATIONS_GUIDE.md)
- [Changelog](CHANGELOG.md)

## Roadmap

See GitHub Projects.

## License

No license file is currently included in this repository.
