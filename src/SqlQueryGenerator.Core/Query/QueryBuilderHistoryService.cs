namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Tracks query-builder snapshots to provide undo/redo navigation.
/// </summary>
public sealed class QueryBuilderHistoryService
{
    private readonly int _maxDepth;
    private readonly Stack<QueryBuilderHistoryState> _undo = [];
    private readonly Stack<QueryBuilderHistoryState> _redo = [];
    private QueryBuilderHistoryState? _current;

    /// <summary>
    /// Initializes a new history service.
    /// </summary>
    /// <param name="maxDepth">Maximum number of undo states to keep.</param>
    public QueryBuilderHistoryService(int maxDepth = 100)
    {
        _maxDepth = Math.Max(1, maxDepth);
    }

    /// <summary>
    /// Gets whether an undo action is currently available.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// Gets whether a redo action is currently available.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Resets the full history around one current state.
    /// </summary>
    /// <param name="current">Current builder state.</param>
    public void Reset(QueryBuilderHistoryState current)
    {
        ArgumentNullException.ThrowIfNull(current);

        _undo.Clear();
        _redo.Clear();
        _current = current.Clone();
    }

    /// <summary>
    /// Tracks one post-mutation state and pushes the previous one to undo history if needed.
    /// </summary>
    /// <param name="current">Current builder state after a mutation.</param>
    public void Track(QueryBuilderHistoryState current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (_current is null)
        {
            _current = current.Clone();
            return;
        }

        if (string.Equals(_current.Signature, current.Signature, StringComparison.Ordinal))
        {
            return;
        }

        _undo.Push(_current.Clone());
        TrimUndo();
        _redo.Clear();
        _current = current.Clone();
    }

    /// <summary>
    /// Replaces the current state without touching undo or redo stacks.
    /// </summary>
    /// <param name="current">Current builder state.</param>
    public void ReplaceCurrent(QueryBuilderHistoryState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        _current = current.Clone();
    }

    /// <summary>
    /// Moves one step back in history.
    /// </summary>
    /// <returns>Snapshot to restore.</returns>
    public QueryBuilderHistoryState Undo()
    {
        if (_current is null || _undo.Count == 0)
        {
            throw new InvalidOperationException("Undo indisponible.");
        }

        QueryBuilderHistoryState restored = _undo.Pop();
        _redo.Push(_current.Clone());
        _current = restored.Clone();
        return restored.Clone();
    }

    /// <summary>
    /// Moves one step forward in history.
    /// </summary>
    /// <returns>Snapshot to restore.</returns>
    public QueryBuilderHistoryState Redo()
    {
        if (_current is null || _redo.Count == 0)
        {
            throw new InvalidOperationException("Redo indisponible.");
        }

        QueryBuilderHistoryState restored = _redo.Pop();
        _undo.Push(_current.Clone());
        TrimUndo();
        _current = restored.Clone();
        return restored.Clone();
    }

    private void TrimUndo()
    {
        if (_undo.Count <= _maxDepth)
        {
            return;
        }

        QueryBuilderHistoryState[] trimmed = _undo.Take(_maxDepth).ToArray();
        _undo.Clear();
        for (int index = trimmed.Length - 1; index >= 0; index--)
        {
            _undo.Push(trimmed[index]);
        }
    }
}
