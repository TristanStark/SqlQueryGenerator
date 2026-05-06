using System.Windows.Input;

namespace SqlQueryGenerator.App.Infrastructure;

/// <summary>
/// Représente RelayCommand dans SQL Query Generator.
/// </summary>
public sealed class RelayCommand : ICommand
{
    /// <summary>
    /// Stocke la valeur interne  execute.
    /// </summary>
    /// <value>Valeur de _execute.</value>
    private readonly Action<object?> _execute;
    /// <summary>
    /// Stocke la valeur interne  canExecute.
    /// </summary>
    /// <value>Valeur de _canExecute.</value>
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Initialise une nouvelle instance de RelayCommand.
    /// </summary>
    /// <param name="execute">Paramètre execute.</param>
    /// <param name="canExecute">Paramètre canExecute.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    /// <summary>
    /// Initialise une nouvelle instance de RelayCommand.
    /// </summary>
    /// <param name="execute">Paramètre execute.</param>
    /// <param name="canExecute">Paramètre canExecute.</param>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Stocke la valeur interne CanExecuteChanged.
    /// </summary>
    /// <value>Valeur de CanExecuteChanged.</value>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Exécute le traitement CanExecute.
    /// </summary>
    /// <param name="parameter">Paramètre parameter.</param>
    /// <returns>Résultat du traitement.</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Exécute le traitement Execute.
    /// </summary>
    /// <param name="parameter">Paramètre parameter.</param>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Exécute le traitement RaiseCanExecuteChanged.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
