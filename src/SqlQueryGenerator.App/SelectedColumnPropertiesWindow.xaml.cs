using System.Windows;

namespace SqlQueryGenerator.App;

/// <summary>
/// Popup used to edit output formatting properties for one selected column.
/// </summary>
public partial class SelectedColumnPropertiesWindow : Window
{
    /// <summary>
    /// Initializes a new selected-column properties window.
    /// </summary>
    /// <param name="dataContext">Selected column row view model.</param>
    public SelectedColumnPropertiesWindow(object dataContext)
    {
        InitializeComponent();
        DataContext = dataContext;
    }

    /// <summary>
    /// Closes the properties window.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
