using System.Globalization;
using System.Windows.Data;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente TableNameDisplayConverter dans SQL Query Generator.
/// </summary>
public sealed class TableNameDisplayConverter : IValueConverter
{
    /// <summary>
    /// Exécute le traitement Convert.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <param name="targetType">Paramètre targetType.</param>
    /// <param name="parameter">Paramètre parameter.</param>
    /// <param name="culture">Paramètre culture.</param>
    /// <returns>Résultat du traitement.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return SqlObjectDisplayName.Table(value as string ?? value?.ToString());
    }

    /// <summary>
    /// Exécute le traitement ConvertBack.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <param name="targetType">Paramètre targetType.</param>
    /// <param name="parameter">Paramètre parameter.</param>
    /// <param name="culture">Paramètre culture.</param>
    /// <returns>Résultat du traitement.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }
}
