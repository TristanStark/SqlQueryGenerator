using SqlQueryGenerator.App.ViewModels;

namespace SqlQueryGenerator.App.Services
{
    /// <summary>
    /// Represents the result of applying a search filter to the schema explorer index.
    /// </summary>
    public sealed class SchemaExplorerSearchResult
    {
        /// <summary>
        /// Initializes a new schema explorer search result.
        /// </summary>
        /// <param name="query">Original user query.</param>
        /// <param name="normalizedQuery">Normalized query used by the search index.</param>
        /// <param name="tables">Table nodes to display in the schema tree.</param>
        /// <param name="matchedColumnCount">Number of matching columns counted before optional truncation.</param>
        /// <param name="isTruncated">Whether the result was truncated to protect UI responsiveness and memory.</param>
        public SchemaExplorerSearchResult(
            string query,
            string normalizedQuery,
            IReadOnlyList<TableItemViewModel> tables,
            int matchedColumnCount,
            bool isTruncated)
        {
            Query = query ?? string.Empty;
            NormalizedQuery = normalizedQuery ?? string.Empty;
            Tables = tables ?? Array.Empty<TableItemViewModel>();
            MatchedColumnCount = Math.Max(0, matchedColumnCount);
            IsTruncated = isTruncated;
        }

        /// <summary>
        /// Gets the raw query entered by the user.
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// Gets the normalized query used for case-insensitive matching.
        /// </summary>
        public string NormalizedQuery { get; }

        /// <summary>
        /// Gets the table nodes to display in the left schema tree.
        /// </summary>
        public IReadOnlyList<TableItemViewModel> Tables { get; }

        /// <summary>
        /// Gets the number of matching columns counted by the search.
        /// </summary>
        public int MatchedColumnCount { get; }

        /// <summary>
        /// Gets the number of table nodes displayed by this result.
        /// </summary>
        public int DisplayedTableCount => Tables.Count;

        /// <summary>
        /// Gets the number of visible column nodes displayed by this result.
        /// </summary>
        public int DisplayedColumnCount => Tables.Sum(table => table.ColumnCount);

        /// <summary>
        /// Gets whether this result corresponds to an empty search.
        /// </summary>
        public bool IsEmptySearch => string.IsNullOrWhiteSpace(NormalizedQuery);

        /// <summary>
        /// Gets whether the result was truncated to preserve UI responsiveness and memory.
        /// </summary>
        public bool IsTruncated { get; }
    }
}
