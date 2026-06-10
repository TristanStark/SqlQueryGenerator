using SqlQueryGenerator.Core.Persistence;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// UI item wrapping one saved output profile.
/// </summary>
public sealed class OutputProfileItemViewModel
{
    /// <summary>
    /// Initializes a new output profile item.
    /// </summary>
    /// <param name="definition">Profile definition.</param>
    public OutputProfileItemViewModel(OutputProfileDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>
    /// Gets the underlying profile definition.
    /// </summary>
    /// <value>Output profile definition.</value>
    public OutputProfileDefinition Definition { get; }

    /// <summary>
    /// Gets the profile name.
    /// </summary>
    /// <value>Profile name.</value>
    public string Name => Definition.Name;

    /// <summary>
    /// Gets the profile description.
    /// </summary>
    /// <value>Profile description.</value>
    public string Description => Definition.Description ?? string.Empty;

    /// <summary>
    /// Gets a short profile summary.
    /// </summary>
    /// <value>Field count summary.</value>
    public string Summary => $"{Definition.Fields.Count} champ(s)";

    /// <summary>
    /// Gets the displayed profile name.
    /// </summary>
    /// <returns>Display text.</returns>
    public override string ToString() => $"{Name} — {Summary}";
}
