using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Represents one selectable SELECT branch of a compound query in the visual builder.
/// </summary>
public sealed class CompoundQueryBranchItemViewModel
{
    /// <summary>
    /// Gets the stable path of this branch inside the compound-query tree.
    /// </summary>
    /// <value>Human-readable path such as 1, 2 or 2.1.</value>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the nesting depth of this branch.
    /// </summary>
    /// <value>Zero for the first/root SELECT branch.</value>
    public int Depth { get; init; }

    /// <summary>
    /// Gets the set operator introducing this branch, when it is not the root branch.
    /// </summary>
    /// <value>Incoming set operator, or <c>null</c> for SELECT 1.</value>
    public SetOperationKind? Operator { get; init; }

    /// <summary>
    /// Gets whether the incoming operator uses ALL.
    /// </summary>
    /// <value><c>true</c> for UNION ALL, INTERSECT ALL or EXCEPT ALL.</value>
    public bool All { get; init; }

    /// <summary>
    /// Gets the live query node edited by the visual controls.
    /// </summary>
    /// <value>Query branch inside the compound-query template.</value>
    public required QueryDefinition Query { get; init; }

    /// <summary>
    /// Gets the text displayed in the branch selector.
    /// </summary>
    /// <value>Indented SELECT branch label with operator and base table.</value>
    public string DisplayName
    {
        get
        {
            string indentation = new('·', Depth * 2);
            string operatorText = Operator is null
                ? "branche principale"
                : Operator + (All ? " ALL" : string.Empty);
            string tableText = string.IsNullOrWhiteSpace(Query.BaseTable)
                ? "table non résolue"
                : Query.BaseTable;
            return $"{indentation} SELECT {Path} — {operatorText} — {tableText}";
        }
    }
}
