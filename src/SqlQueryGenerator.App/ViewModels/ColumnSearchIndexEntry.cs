namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Lightweight immutable search row used to filter the left column tree without rebuilding search text on every key press.
/// </summary>
internal sealed class ColumnSearchIndexEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnSearchIndexEntry"/> class.
    /// </summary>
    /// <param name="table">Table view model owning the column.</param>
    /// <param name="column">Column view model matched by the search engine.</param>
    /// <param name="normalizedSearchText">Lower-cased concatenation of searchable table, column, documentation and index metadata.</param>
    public ColumnSearchIndexEntry(TableItemViewModel table, ColumnItemViewModel column, string normalizedSearchText)
    {
        Table = table;
        Column = column;
        NormalizedSearchText = normalizedSearchText;
    }

    /// <summary>
    /// Gets the table view model that owns the indexed column.
    /// </summary>
    /// <value>Stable table view model reused by the search tree.</value>
    public TableItemViewModel Table { get; }

    /// <summary>
    /// Gets the column view model matched by the indexed search text.
    /// </summary>
    /// <value>Stable column view model reused by the search tree.</value>
    public ColumnItemViewModel Column { get; }

    /// <summary>
    /// Gets the lower-cased text searched by the left tree filter.
    /// </summary>
    /// <value>Precomputed searchable text, normalized once when the schema is loaded.</value>
    public string NormalizedSearchText { get; }
}
