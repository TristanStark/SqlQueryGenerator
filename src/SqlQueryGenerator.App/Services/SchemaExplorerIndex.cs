using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.App.Services
{
    /// <summary>
    /// Prebuilt schema explorer index used to render and search the left schema tree without
    /// recreating WPF-bound view models on every keystroke.
    /// </summary>
    public sealed class SchemaExplorerIndex
    {
        /// <summary>
        /// Empty schema explorer index used before a schema is loaded.
        /// </summary>
        public static SchemaExplorerIndex Empty { get; } = new(
            allTableViewModels: [],
            allColumns: [],
            relationships: [],
            relationshipGroups: [],
            tableNames: [],
            columnNamesByTable: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            columnViewModelsByTable: new Dictionary<string, IReadOnlyList<ColumnItemViewModel>>(StringComparer.OrdinalIgnoreCase),
            searchIndex: [],
            detectedAuxiliaryTableCount: 0,
            hiddenAuxiliaryTableCount: 0);

        private readonly IReadOnlyList<ColumnSearchIndexEntry> _searchIndex;

        private SchemaExplorerIndex(
            IReadOnlyList<TableItemViewModel> allTableViewModels,
            IReadOnlyList<ColumnItemViewModel> allColumns,
            IReadOnlyList<RelationshipItemViewModel> relationships,
            IReadOnlyList<RelationshipGroupViewModel> relationshipGroups,
            IReadOnlyList<string> tableNames,
            IReadOnlyDictionary<string, IReadOnlyList<string>> columnNamesByTable,
            IReadOnlyDictionary<string, IReadOnlyList<ColumnItemViewModel>> columnViewModelsByTable,
            IReadOnlyList<ColumnSearchIndexEntry> searchIndex,
            int detectedAuxiliaryTableCount,
            int hiddenAuxiliaryTableCount)
        {
            AllTableViewModels = allTableViewModels;
            AllColumns = allColumns;
            Relationships = relationships;
            RelationshipGroups = relationshipGroups;
            TableNames = tableNames;
            ColumnNamesByTable = columnNamesByTable;
            ColumnViewModelsByTable = columnViewModelsByTable;
            _searchIndex = searchIndex;
            DetectedAuxiliaryTableCount = detectedAuxiliaryTableCount;
            HiddenAuxiliaryTableCount = hiddenAuxiliaryTableCount;
        }

        /// <summary>
        /// Gets all stable table view models visible in the schema explorer.
        /// </summary>
        public IReadOnlyList<TableItemViewModel> AllTableViewModels { get; }

        /// <summary>
        /// Gets all stable column view models visible in the schema explorer.
        /// </summary>
        public IReadOnlyList<ColumnItemViewModel> AllColumns { get; }

        /// <summary>
        /// Gets all visible relationship view models.
        /// </summary>
        public IReadOnlyList<RelationshipItemViewModel> Relationships { get; }

        /// <summary>
        /// Gets grouped relationship view models for the relationship tree.
        /// </summary>
        public IReadOnlyList<RelationshipGroupViewModel> RelationshipGroups { get; }

        /// <summary>
        /// Gets the visible table names used by combo boxes and join editors.
        /// </summary>
        public IReadOnlyList<string> TableNames { get; }

        /// <summary>
        /// Gets visible column names grouped by table name and display table name.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnNamesByTable { get; }

        /// <summary>
        /// Gets visible column view models grouped by table name and display table name.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<ColumnItemViewModel>> ColumnViewModelsByTable { get; }

        /// <summary>
        /// Gets the number of auxiliary tables detected after import/filtering.
        /// </summary>
        public int DetectedAuxiliaryTableCount { get; }

        /// <summary>
        /// Gets the number of auxiliary tables hidden from the explorer.
        /// </summary>
        public int HiddenAuxiliaryTableCount { get; }

        /// <summary>
        /// Builds a schema explorer index from the current parsed schema.
        /// </summary>
        /// <param name="schema">Parsed database schema.</param>
        /// <param name="auxiliaryTableDetector">Detector used to identify backup/history/temp tables.</param>
        /// <param name="pinnedTableNames">Tables that must remain visible even when auxiliary hiding is enabled.</param>
        /// <param name="hideAuxiliaryTables">Whether likely auxiliary tables should be hidden.</param>
        /// <param name="foreignKeySummaries">Precomputed FK summaries keyed by fully qualified column name.</param>
        /// <param name="indexSummaries">Precomputed index summaries keyed by fully qualified column name.</param>
        /// <param name="uniqueIndexColumns">Precomputed unique-index column keys.</param>
        /// <returns>A reusable schema explorer index.</returns>
        public static SchemaExplorerIndex Build(
            DatabaseSchema schema,
            SchemaAuxiliaryTableDetector auxiliaryTableDetector,
            IReadOnlySet<string> pinnedTableNames,
            bool hideAuxiliaryTables,
            IReadOnlyDictionary<string, string> foreignKeySummaries,
            IReadOnlyDictionary<string, string> indexSummaries,
            IReadOnlySet<string> uniqueIndexColumns)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(auxiliaryTableDetector);

            pinnedTableNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreignKeySummaries ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            indexSummaries ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            uniqueIndexColumns ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            TableDefinition[] sortedSchemaTables = [.. schema.Tables.OrderBy(table => table.FullName, StringComparer.OrdinalIgnoreCase)];

            List<TableItemViewModel> tableViewModels = [];
            List<ColumnItemViewModel> allColumns = [];
            List<ColumnSearchIndexEntry> searchIndex = [];
            List<string> tableNames = [];
            HashSet<string> visibleTableNames = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IReadOnlyList<string>> columnNamesByTable = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IReadOnlyList<ColumnItemViewModel>> columnViewModelsByTable = new(StringComparer.OrdinalIgnoreCase);

            int detectedAuxiliaryTableCount = 0;
            int hiddenAuxiliaryTableCount = 0;

            foreach (TableDefinition table in sortedSchemaTables)
            {
                bool isAuxiliaryCandidate = !table.IsView && auxiliaryTableDetector.IsLikelyAuxiliaryTable(table.FullName);
                if (isAuxiliaryCandidate)
                {
                    detectedAuxiliaryTableCount++;
                }

                if (ShouldHideAuxiliaryTable(table, pinnedTableNames, hideAuxiliaryTables, auxiliaryTableDetector))
                {
                    hiddenAuxiliaryTableCount++;
                    continue;
                }

                tableNames.Add(table.FullName);
                visibleTableNames.Add(table.FullName);
                visibleTableNames.Add(table.Name);

                ColumnDefinition[] sortedColumns = [.. table.Columns.OrderBy(column => column.Name, StringComparer.OrdinalIgnoreCase)];

                ColumnItemViewModel[] columnViewModels = [.. sortedColumns
                    .Select(column => new ColumnItemViewModel(
                        column,
                        LookupSummary(column, foreignKeySummaries),
                        LookupSummary(column, indexSummaries),
                        uniqueIndexColumns.Contains($"{column.TableName}.{column.Name}")))];

                TableItemViewModel tableViewModel = new(table, columnViewModels)
                {
                    IsExpanded = false
                };

                tableViewModels.Add(tableViewModel);

                columnViewModelsByTable[table.FullName] = columnViewModels;
                columnViewModelsByTable[SqlObjectDisplayName.Table(table.FullName)] = columnViewModels;

                IReadOnlyList<string> columnNames = [.. columnViewModels.Select(column => column.Column)];

                columnNamesByTable[table.FullName] = columnNames;
                columnNamesByTable[SqlObjectDisplayName.Table(table.FullName)] = columnNames;

                foreach (ColumnItemViewModel columnViewModel in columnViewModels)
                {
                    allColumns.Add(columnViewModel);
                    searchIndex.Add(new ColumnSearchIndexEntry(
                        tableViewModel,
                        columnViewModel,
                        NormalizeSearchText($"{table.FullName} {tableViewModel.DisplayName} {table.Comment} {columnViewModel.SearchText}")));
                }
            }

            RelationshipItemViewModel[] relationships = [.. schema.Relationships
                .OrderByDescending(relationship => relationship.Confidence)
                .Where(relationship => visibleTableNames.Contains(relationship.FromTable)
                    && visibleTableNames.Contains(relationship.ToTable))
                .Select(relationship => new RelationshipItemViewModel(relationship))];

            RelationshipGroupViewModel[] relationshipGroups = [.. relationships
                .GroupBy(relationship => relationship.FromTable, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new RelationshipGroupViewModel(
                    $"{SqlObjectDisplayName.Table(group.Key)} ({group.Count()})",
                    group.OrderByDescending(relationship => relationship.Confidence))
                {
                    IsExpanded = false
                })];

            return new SchemaExplorerIndex(
                tableViewModels,
                allColumns,
                relationships,
                relationshipGroups,
                tableNames,
                columnNamesByTable,
                columnViewModelsByTable,
                searchIndex,
                detectedAuxiliaryTableCount,
                hiddenAuxiliaryTableCount);
        }

        /// <summary>
        /// Searches the prebuilt schema explorer index and updates stable table view models with
        /// the visible columns for the current query.
        /// </summary>
        /// <param name="query">Search text entered by the user.</param>
        /// <param name="maxVisibleColumnSearchResults">Maximum number of visible matching columns.</param>
        /// <returns>Search result containing table nodes ready to display.</returns>
        public SchemaExplorerSearchResult Search(string? query, int maxVisibleColumnSearchResults)
        {
            string rawQuery = query ?? string.Empty;
            string normalizedQuery = NormalizeSearchText(rawQuery);
            int safeMaxResults = Math.Max(1, maxVisibleColumnSearchResults);

            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                foreach (TableItemViewModel table in AllTableViewModels)
                {
                    table.ResetVisibleColumns();
                    table.IsExpanded = false;
                }

                return new SchemaExplorerSearchResult(
                    rawQuery,
                    normalizedQuery,
                    AllTableViewModels,
                    matchedColumnCount: 0,
                    isTruncated: false);
            }

            Dictionary<TableItemViewModel, List<ColumnItemViewModel>> matchesByTable = [];
            int matchCount = 0;
            bool isTruncated = false;

            foreach (ColumnSearchIndexEntry entry in _searchIndex)
            {
                if (!entry.NormalizedSearchText.Contains(normalizedQuery, StringComparison.Ordinal))
                {
                    continue;
                }

                matchCount++;
                if (matchCount > safeMaxResults)
                {
                    isTruncated = true;
                    break;
                }

                if (!matchesByTable.TryGetValue(entry.Table, out List<ColumnItemViewModel>? columns))
                {
                    columns = [];
                    matchesByTable[entry.Table] = columns;
                }

                columns.Add(entry.Column);
            }

            List<TableItemViewModel> visibleTables = [];

            foreach (TableItemViewModel table in AllTableViewModels)
            {
                if (!matchesByTable.TryGetValue(table, out List<ColumnItemViewModel>? columns))
                {
                    continue;
                }

                table.SetVisibleColumns(columns.Distinct().OrderBy(column => column.Column, StringComparer.OrdinalIgnoreCase));
                table.IsExpanded = true;
                visibleTables.Add(table);
            }

            return new SchemaExplorerSearchResult(
                rawQuery,
                normalizedQuery,
                visibleTables,
                matchCount,
                isTruncated);
        }

        private static bool ShouldHideAuxiliaryTable(
            TableDefinition table,
            IReadOnlySet<string> pinnedTableNames,
            bool hideAuxiliaryTables,
            SchemaAuxiliaryTableDetector auxiliaryTableDetector)
        {
            return hideAuxiliaryTables
                && !table.IsView
                && !pinnedTableNames.Contains(table.FullName)
                && !pinnedTableNames.Contains(table.Name)
                && auxiliaryTableDetector.IsLikelyAuxiliaryTable(table.FullName);
        }

        private static string LookupSummary(ColumnDefinition column, IReadOnlyDictionary<string, string> summaries)
        {
            string key = $"{column.TableName}.{column.Name}";
            return summaries.TryGetValue(key, out string? summary) ? summary : string.Empty;
        }

        private static string NormalizeSearchText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }
    }
}
