using Microsoft.Win32;
using SqlQueryGenerator.App.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SqlQueryGenerator.App;

/// <summary>
/// Représente MainWindow dans SQL Query Generator.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Stocke la valeur interne  dragStartPoint.
    /// </summary>
    /// <value>Valeur de _dragStartPoint.</value>
    private Point _dragStartPoint;

    /// <summary>
    /// Initialise une nouvelle instance de MainWindow.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Obtient ou définit ViewModel.
    /// </summary>
    /// <value>Valeur de ViewModel.</value>
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>
    /// Exécute le traitement OpenSchema Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void OpenSchema_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Charger un schéma SQL ou TXT",
            Filter = "Schémas SQL/TXT (*.sql;*.txt)|*.sql;*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.LoadSchemaFromFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Exécute le traitement OpenDocumentation Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void OpenDocumentation_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Importer une documentation table/colonne",
            Filter = "Documentation CSV/TSV/TXT (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.ImportDocumentationFromFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Exécute le traitement PasteSchema Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void PasteSchema_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            ViewModel.LoadSchemaFromText(Clipboard.GetText(), "Presse-papier");
        }
        else
        {
            MessageBox.Show(this, "Le presse-papier ne contient pas de texte.", "Coller schéma", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Exécute le traitement CopySql Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void CopySql_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ViewModel.GeneratedSql ?? string.Empty);
    }

    /// <summary>
    /// Exécute le traitement AvailableColumnsTree SelectedItemChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AvailableColumnsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedAvailableColumn = e.NewValue as ColumnItemViewModel;
    }

    /// <summary>
    /// Exécute le traitement ColumnsTree MouseDoubleClick.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void ColumnsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView { SelectedItem: ColumnItemViewModel column })
        {
            ViewModel.AddColumnToTarget(column, "select");
        }
    }

    /// <summary>
    /// Exécute le traitement ColumnsTree PreviewMouseMove.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void ColumnsTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStartPoint = e.GetPosition(null);
            return;
        }

        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is TreeView treeView && treeView.SelectedItem is ColumnItemViewModel column)
        {
            DragDrop.DoDragDrop(treeView, column, DragDropEffects.Copy);
        }
    }


    /// <summary>
    /// Exécute le traitement RelationshipsTree SelectedItemChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void RelationshipsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedRelationship = e.NewValue as RelationshipItemViewModel;
    }

    /// <summary>
    /// Exécute le traitement AddColumnToSelectMenu Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AddColumnToSelectMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "select");
    /// <summary>
    /// Exécute le traitement AddColumnToFilterMenu Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AddColumnToFilterMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "filter");
    /// <summary>
    /// Exécute le traitement AddColumnToGroupMenu Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AddColumnToGroupMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "group");
    /// <summary>
    /// Exécute le traitement AddColumnToOrderMenu Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AddColumnToOrderMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "order");
    /// <summary>
    /// Exécute le traitement AddColumnToAggregateMenu Click.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AddColumnToAggregateMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "aggregate");

    /// <summary>
    /// Exécute le traitement AddColumnFromMenu.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="target">Paramètre target.</param>
    private void AddColumnFromMenu(object sender, string target)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        ColumnItemViewModel? column = element.DataContext as ColumnItemViewModel;
        if (column is null && element.Parent is ContextMenu contextMenu)
        {
            column = contextMenu.DataContext as ColumnItemViewModel;
        }

        if (column is not null)
        {
            ViewModel.AddColumnToTarget(column, target);
        }
    }

    /// <summary>
    /// Exécute le traitement Drop DragOver.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Drop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ColumnItemViewModel)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Exécute le traitement JoinLeftColumn Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void JoinLeftColumn_Drop(object sender, DragEventArgs e) => DropJoinColumn(sender, e, isLeftSide: true);
    /// <summary>
    /// Exécute le traitement JoinRightColumn Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void JoinRightColumn_Drop(object sender, DragEventArgs e) => DropJoinColumn(sender, e, isLeftSide: false);
    /// <summary>
    /// Exécute le traitement JoinPairLeftColumn Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void JoinPairLeftColumn_Drop(object sender, DragEventArgs e) => DropJoinPairColumn(sender, e, isLeftSide: true);
    /// <summary>
    /// Exécute le traitement JoinPairRightColumn Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void JoinPairRightColumn_Drop(object sender, DragEventArgs e) => DropJoinPairColumn(sender, e, isLeftSide: false);

    /// <summary>
    /// Exécute le traitement DropJoinColumn.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    /// <param name="isLeftSide">Paramètre isLeftSide.</param>
    private void DropJoinColumn(object sender, DragEventArgs e, bool isLeftSide)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not JoinRowViewModel join
            || e.Data.GetData(typeof(ColumnItemViewModel)) is not ColumnItemViewModel column)
        {
            return;
        }

        if (isLeftSide)
        {
            join.FromTable = column.Table;
            join.FromColumn = column.Column;
        }
        else
        {
            join.ToTable = column.Table;
            join.ToColumn = column.Column;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Exécute le traitement DropJoinPairColumn.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    /// <param name="isLeftSide">Paramètre isLeftSide.</param>
    private void DropJoinPairColumn(object sender, DragEventArgs e, bool isLeftSide)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not JoinColumnPairRowViewModel pair
            || e.Data.GetData(typeof(ColumnItemViewModel)) is not ColumnItemViewModel column)
        {
            return;
        }

        JoinRowViewModel? parentJoin = FindAncestorDataContext<JoinRowViewModel>(element);
        if (isLeftSide)
        {
            pair.FromColumn = column.Column;
            if (parentJoin is not null && string.IsNullOrWhiteSpace(parentJoin.FromTable))
            {
                parentJoin.FromTable = column.Table;
            }
        }
        else
        {
            pair.ToColumn = column.Column;
            if (parentJoin is not null && string.IsNullOrWhiteSpace(parentJoin.ToTable))
            {
                parentJoin.ToTable = column.Table;
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Exécute le traitement FindAncestorDataContext.
    /// </summary>
    /// <param name="element">Paramètre element.</param>
    /// <returns>Résultat du traitement.</returns>
    private static T? FindAncestorDataContext<T>(DependencyObject? element) where T : class
    {
        while (element is not null)
        {
            if (element is FrameworkElement frameworkElement && frameworkElement.DataContext is T typed)
            {
                return typed;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }


    /// <summary>
    /// Exécute le traitement Select Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Select_Drop(object sender, DragEventArgs e) => DropColumn(e, "select");
    /// <summary>
    /// Exécute le traitement Filter Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Filter_Drop(object sender, DragEventArgs e) => DropColumn(e, "filter");
    /// <summary>
    /// Exécute le traitement Group Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Group_Drop(object sender, DragEventArgs e) => DropColumn(e, "group");
    /// <summary>
    /// Exécute le traitement Order Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Order_Drop(object sender, DragEventArgs e) => DropColumn(e, "order");
    /// <summary>
    /// Exécute le traitement Aggregate Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Aggregate_Drop(object sender, DragEventArgs e) => DropColumn(e, "aggregate");
    /// <summary>
    /// Exécute le traitement CustomCase Drop.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void CustomCase_Drop(object sender, DragEventArgs e) => DropColumn(e, "case");

    /// <summary>
    /// Exécute le traitement DropColumn.
    /// </summary>
    /// <param name="e">Paramètre e.</param>
    /// <param name="target">Paramètre target.</param>
    private void DropColumn(DragEventArgs e, string target)
    {
        if (e.Data.GetData(typeof(ColumnItemViewModel)) is ColumnItemViewModel column)
        {
            ViewModel.AddColumnToTarget(column, target);
            e.Handled = true;
        }
    }
}
