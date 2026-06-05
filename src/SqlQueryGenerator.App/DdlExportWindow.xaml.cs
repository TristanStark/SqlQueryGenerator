using System.Windows;

namespace SqlQueryGenerator.App;

/// <summary>
/// Dedicated popup used to generate and copy Oracle/SQLite DDL export helper SQL.
/// </summary>
public partial class DdlExportWindow : Window
{
    /// <summary>
    /// Initializes a new export popup bound to the provided data context.
    /// </summary>
    /// <param name="dataContext">Shared application view model.</param>
    public DdlExportWindow(object? dataContext)
    {
        InitializeComponent();
        DataContext = dataContext;
    }

    /// <summary>
    /// Closes the popup.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
