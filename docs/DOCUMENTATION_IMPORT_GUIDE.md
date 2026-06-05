# Documentation Import Guide

SqlQueryGenerator can enrich the schema tree with business-facing table and column descriptions loaded from a tabular file.

This guide documents the importer as it is implemented today.

## Supported file types

- `.csv`
- `.tsv`
- `.txt`
- Delimiters automatically detected from the header line: tab, semicolon, comma, or pipe

Quoted values are supported with standard CSV double quotes.

## Required and recognized headers

The importer normalizes headers to lowercase and replaces spaces / `-` with `_`.

Recognized table headers:

- `table`
- `table_name`
- `nom_table`
- `table_physique`
- `objet`
- `object`
- `object_name`

Recognized column headers:

- `column`
- `column_name`
- `nom_colonne`
- `champ`
- `field`

Recognized display-name headers:

- `display`
- `display_name`
- `nom_fonctionnel`
- `libelle`
- `label`
- `meaning`
- `signification`

Recognized description headers:

- `description`
- `comment`
- `commentaire`
- `definition`
- `définition`
- `details`
- `notes`

## Minimal example

```csv
table,column,description
CUSTOMER,CUSTOMER_ID,Unique customer identifier
CUSTOMER,NAME,Customer display name
ORDERS,ORDER_ID,Unique order identifier
```

## Recommended example

Extra columns such as `schema`, `type`, or `nullable` are ignored by the current importer, but they are still useful for humans and for exporting from catalog views.

```csv
schema,table,column,display_name,description,type,nullable
APP,CUSTOMER,CUSTOMER_ID,Customer id,Unique customer identifier,NUMBER(10),NO
APP,CUSTOMER,NAME,Customer name,Customer display name,VARCHAR2(255),YES
APP,ORDERS,ORDER_ID,Order id,Unique order identifier,NUMBER(10),NO
```

## TSV example

```tsv
schema	table	column	display_name	description
APP	CUSTOMER	CUSTOMER_ID	Customer id	Unique customer identifier
APP	CUSTOMER	NAME	Customer name	Customer display name
```

## Matching rules

- The importer requires a table column.
- If the `column` field is empty, the row updates the table comment.
- If the `column` field is filled, the row updates that column comment.
- The importer merges `display_name` + `description` into one UI string.
- If both exist and differ, the stored comment becomes `display — description`.
- If only one exists, that value is stored as the comment.
- Extra headers are ignored.

## Case sensitivity and naming

- Table and column matching is case-insensitive.
- The importer matches against the physical table and column names already parsed from the loaded DDL.
- Schema-qualified names are not required in the import file.
- If your DDL uses quoted names with spaces or unusual casing, prefer exporting the exact table and column names that appear in the DDL.

## In-app workflow

1. Load the SQL schema first with `Charger schéma SQL/TXT` or `Coller schéma`.
2. Click `Importer doc CSV/TSV`.
3. Select the documentation file.
4. Review the status and warnings message.
5. Browse the left schema tree; imported comments appear in tooltips and business explanations.

## Troubleshooting

- `Table documentée introuvable`: the file references a table name that was not parsed from the loaded schema.
- `Colonne documentée introuvable`: the table exists, but the referenced column name does not match the parsed schema.
- `Aucune ligne de documentation exploitable`: the file is empty, the delimiter is wrong, or required headers are missing.
- If comments do not appear, verify that the table and column names match the loaded DDL, not only the functional labels.

## Read-only extraction examples

### Oracle

```sql
SELECT
    c.owner AS schema,
    c.table_name AS table,
    c.column_name AS column,
    cc.comments AS description
FROM all_tab_columns c
LEFT JOIN all_col_comments cc
    ON cc.owner = c.owner
   AND cc.table_name = c.table_name
   AND cc.column_name = c.column_name
WHERE c.owner = :schema_owner
ORDER BY c.owner, c.table_name, c.column_id;
```

### DB2

```sql
SELECT
    tabschema AS schema,
    tabname AS table,
    colname AS column,
    remarks AS description
FROM syscat.columns
WHERE tabschema = UPPER(?)
ORDER BY tabschema, tabname, colno;
```

### PostgreSQL

```sql
SELECT
    n.nspname AS schema,
    c.relname AS table,
    a.attname AS column,
    pg_catalog.col_description(a.attrelid, a.attnum) AS description
FROM pg_catalog.pg_attribute a
JOIN pg_catalog.pg_class c
    ON c.oid = a.attrelid
JOIN pg_catalog.pg_namespace n
    ON n.oid = c.relnamespace
WHERE a.attnum > 0
  AND NOT a.attisdropped
  AND n.nspname = :schema_name
ORDER BY n.nspname, c.relname, a.attnum;
```

### SQL Server

```sql
SELECT
    s.name AS schema,
    t.name AS table,
    c.name AS column,
    CAST(ep.value AS nvarchar(max)) AS description
FROM sys.tables t
JOIN sys.schemas s
    ON s.schema_id = t.schema_id
JOIN sys.columns c
    ON c.object_id = t.object_id
LEFT JOIN sys.extended_properties ep
    ON ep.major_id = c.object_id
   AND ep.minor_id = c.column_id
   AND ep.name = 'MS_Description'
WHERE s.name = @schema_name
ORDER BY s.name, t.name, c.column_id;
```

### SQLite

SQLite does not store rich column comments in the engine itself. A common approach is to export documentation from an external catalog, spreadsheet, or metadata repository and then import it with the CSV/TSV workflow above.
