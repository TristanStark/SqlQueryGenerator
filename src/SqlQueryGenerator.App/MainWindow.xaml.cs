using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SqlQueryGenerator.App.ViewModels;

namespace SqlQueryGenerator.App;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OpenSchema_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
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

    private void CopySql_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ViewModel.GeneratedSql ?? string.Empty);
    }

    private void AvailableColumnsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedAvailableColumn = e.NewValue as ColumnItemViewModel;
    }

    private void ColumnsTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView { SelectedItem: ColumnItemViewModel column })
        {
            ViewModel.AddColumnToTarget(column, "select");
        }
    }

    private void ColumnsTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStartPoint = e.GetPosition(null);
            return;
        }

        var current = e.GetPosition(null);
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

    private void AddColumnToSelectMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "select");
    private void AddColumnToFilterMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "filter");
    private void AddColumnToGroupMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "group");
    private void AddColumnToOrderMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "order");
    private void AddColumnToAggregateMenu_Click(object sender, RoutedEventArgs e) => AddColumnFromMenu(sender, "aggregate");

    private void AddColumnFromMenu(object sender, string target)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var column = element.DataContext as ColumnItemViewModel;
        if (column is null && element.Parent is ContextMenu contextMenu)
        {
            column = contextMenu.DataContext as ColumnItemViewModel;
        }

        if (column is not null)
        {
            ViewModel.AddColumnToTarget(column, target);
        }
    }

    private void Drop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ColumnItemViewModel)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void JoinLeftColumn_Drop(object sender, DragEventArgs e) => DropJoinColumn(sender, e, isLeftSide: true);
    private void JoinRightColumn_Drop(object sender, DragEventArgs e) => DropJoinColumn(sender, e, isLeftSide: false);

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


    private void Select_Drop(object sender, DragEventArgs e) => DropColumn(e, "select");
    private void Filter_Drop(object sender, DragEventArgs e) => DropColumn(e, "filter");
    private void Group_Drop(object sender, DragEventArgs e) => DropColumn(e, "group");
    private void Order_Drop(object sender, DragEventArgs e) => DropColumn(e, "order");
    private void Aggregate_Drop(object sender, DragEventArgs e) => DropColumn(e, "aggregate");
    private void CustomCase_Drop(object sender, DragEventArgs e) => DropColumn(e, "case");

    private void DropColumn(DragEventArgs e, string target)
    {
        if (e.Data.GetData(typeof(ColumnItemViewModel)) is ColumnItemViewModel column)
        {
            ViewModel.AddColumnToTarget(column, target);
            e.Handled = true;
        }
    }
}
