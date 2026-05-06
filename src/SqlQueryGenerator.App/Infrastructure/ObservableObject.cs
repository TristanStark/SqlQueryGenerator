using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SqlQueryGenerator.App.Infrastructure;

/// <summary>
/// Représente ObservableObject dans SQL Query Generator.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>
    /// Stocke la valeur interne PropertyChanged.
    /// </summary>
    /// <value>Valeur de PropertyChanged.</value>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Exécute le traitement SetProperty.
    /// </summary>
    /// <param name="field">Paramètre field.</param>
    /// <param name="value">Paramètre value.</param>
    /// <param name="propertyName">Paramètre propertyName.</param>
    /// <returns>Résultat du traitement.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Exécute le traitement OnPropertyChanged.
    /// </summary>
    /// <param name="propertyName">Paramètre propertyName.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
