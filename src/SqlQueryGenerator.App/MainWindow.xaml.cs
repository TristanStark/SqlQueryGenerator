using Microsoft.Win32;
using SqlQueryGenerator.App.Export;
using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Export;
using System.ComponentModel;
using System.IO;
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
    /// Dernière colonne utilisée comme ancre pour la sélection multiple par raccourci clavier.
    /// </summary>
    /// <value>Colonne d'ancrage utilisée par Shift+clic, ou <c>null</c> si aucune colonne n'a encore été choisie.</value>
    private ColumnItemViewModel? _bulkSelectionAnchor;
    private DdlExportWindow? _ddlExportWindow;
    /// <summary>
    /// Generates Markdown content for the current query documentation export actions.
    /// </summary>
    /// <value>Reusable stateless Markdown exporter.</value>
    private readonly QueryDocumentationMarkdownExporter _markdownExporter = new();

    /// <summary>
    /// Initialise une nouvelle instance de MainWindow.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// Obtient ou définit ViewModel.
    /// </summary>
    /// <value>Valeur de ViewModel.</value>
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(MainViewModel.RawSqlSelectionStart) and not nameof(MainViewModel.RawSqlSelectionLength))
        {
            return;
        }

        if (RawSqlEditorTextBox is null)
        {
            return;
        }

        int start = Math.Max(0, Math.Min(ViewModel.RawSqlSelectionStart, RawSqlEditorTextBox.Text.Length));
        int maxLength = Math.Max(0, RawSqlEditorTextBox.Text.Length - start);
        int length = Math.Max(0, Math.Min(ViewModel.RawSqlSelectionLength, maxLength));

        if (length == 0)
        {
            return;
        }

        RawSqlEditorTextBox.Focus();
        RawSqlEditorTextBox.Select(start, length);
        RawSqlEditorTextBox.ScrollToLine(RawSqlEditorTextBox.GetLineIndexFromCharacterIndex(start));
    }

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
            if (TryReadSchemaFile(dialog.FileName, out string schemaText))
            {
                ImportSchemaTextWithReview(schemaText, dialog.FileName);
            }
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
    /// Opens a raw SQL file and copies its content into the raw SQL editor.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void OpenRawSql_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Importer un SQL brut",
            Filter = "SQL/TXT (*.sql;*.txt)|*.sql;*.txt|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.LoadRawSqlFromFile(dialog.FileName);
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
            ImportSchemaTextWithReview(Clipboard.GetText(), "Presse-papier");
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
    /// Copies the current query documentation as Markdown to the clipboard.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void CopyMarkdownDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildCurrentDocumentationMarkdown(out string markdown))
        {
            return;
        }

        Clipboard.SetText(markdown);
        ViewModel.Status = "Documentation Markdown copiée dans le presse-papier.";
    }

    /// <summary>
    /// Saves the current query documentation as a Markdown file.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void ExportMarkdownDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildCurrentDocumentationMarkdown(out string markdown))
        {
            return;
        }

        if (TrySaveTextFile(
            "Exporter la documentation Markdown",
            "Markdown (*.md)|*.md|Texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
            ".md",
            BuildSafeFileName(ViewModel.QueryName, ".md"),
            markdown))
        {
            ViewModel.Status = "Documentation Markdown exportée.";
        }
    }

    /// <summary>
    /// Saves the current generated SQL query as a .sql file.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void ExportSql_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetExportableSql(out string sql))
        {
            MessageBox.Show(this, "Aucune requête SQL exportable. Génère une requête ou importe un SQL brut avant d'exporter.", "Exporter SQL", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (TrySaveTextFile(
            "Exporter la requête SQL",
            "SQL (*.sql)|*.sql|Texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
            ".sql",
            BuildSafeFileName(ViewModel.QueryName, ".sql"),
            sql))
        {
            ViewModel.Status = "Requête SQL exportée.";
        }
    }

    /// <summary>
    /// Builds the Markdown document for the current query state when exportable SQL is available.
    /// </summary>
    private bool TryBuildCurrentDocumentationMarkdown(out string markdown)
    {
        markdown = string.Empty;
        if (!TryGetExportableSql(out string sql))
        {
            MessageBox.Show(this, "Aucune requête SQL exportable. Génère une requête ou importe un SQL brut avant de créer la documentation.", "Documentation Markdown", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        QueryDocumentationExportContext context = QueryDocumentationExportContextFactory.FromViewModel(ViewModel, DateTimeOffset.Now, sql);
        markdown = _markdownExporter.GenerateMarkdown(context);
        return true;
    }

    /// <summary>
    /// Resolves the SQL text to export, preferring generated SQL and falling back to raw SQL.
    /// </summary>
    private bool TryGetExportableSql(out string sql)
    {
        sql = string.Empty;
        string generatedSql = ViewModel.GeneratedSql ?? string.Empty;
        if (!IsPlaceholderSql(generatedSql))
        {
            sql = generatedSql.Trim();
            return true;
        }

        string rawSql = ViewModel.RawSqlText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(rawSql))
        {
            sql = rawSql.Trim();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects the initial generated SQL placeholder text.
    /// </summary>
    private static bool IsPlaceholderSql(string sql)
    {
        return string.IsNullOrWhiteSpace(sql)
            || sql.TrimStart().StartsWith("-- La requête générée apparaîtra ici.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Opens a save dialog and writes UTF-8 text to the selected file.
    /// </summary>
    private bool TrySaveTextFile(string title, string filter, string defaultExtension, string fileName, string text)
    {
        SaveFileDialog dialog = new()
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = fileName
        };

        if (dialog.ShowDialog(this) != true)
        {
            return false;
        }

        File.WriteAllText(dialog.FileName, text);
        return true;
    }

    /// <summary>
    /// Builds a timestamped filename safe for the current operating system.
    /// </summary>
    private static string BuildSafeFileName(string baseName, string extension)
    {
        string safeName = string.IsNullOrWhiteSpace(baseName) ? "query" : baseName.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidCharacter, '_');
        }

        safeName = safeName.Replace(' ', '_');
        return $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
    }

    private void ImportSchemaTextWithReview(string schemaText, string sourceName)
    {
        if (!ViewModel.TryPreviewBackupTableCandidates(schemaText, out IReadOnlyList<SqlQueryGenerator.Core.Heuristics.BackupTableCandidate> candidates))
        {
            return;
        }

        IReadOnlyCollection<string>? excludedTables = null;
        if (candidates.Count > 0)
        {
            SchemaImportReviewWindow reviewWindow = new(candidates)
            {
                Owner = this
            };

            if (reviewWindow.ShowDialog() != true)
            {
                ViewModel.Status = "Import du schema annule.";
                return;
            }

            excludedTables = reviewWindow.ExcludedTableNames;
        }

        ViewModel.LoadSchemaFromText(schemaText, sourceName, excludedTables);
    }

    private bool TryReadSchemaFile(string filePath, out string schemaText)
    {
        schemaText = string.Empty;
        if (!File.Exists(filePath))
        {
            ViewModel.Status = "Fichier introuvable.";
            return false;
        }

        FileInfo info = new(filePath);
        if (info.Length > 20_000_000)
        {
            MessageBox.Show(this, "Le fichier depasse 20 Mo. Pour eviter de bloquer l'interface, reduis le schema ou augmente la limite dans MainViewModel.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        schemaText = File.ReadAllText(filePath);
        return true;
    }

    /// <summary>
    /// Opens the dedicated DDL export popup and reuses it when already visible.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event arguments.</param>
    private void OpenDdlExportWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_ddlExportWindow is not null)
        {
            _ddlExportWindow.Activate();
            return;
        }

        _ddlExportWindow = new DdlExportWindow(DataContext)
        {
            Owner = this
        };
        _ddlExportWindow.Closed += (_, _) => _ddlExportWindow = null;
        _ddlExportWindow.Show();
    }

    /// <summary>
    /// Exécute le traitement AvailableColumnsTree SelectedItemChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void AvailableColumnsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedAvailableColumn = e.NewValue as ColumnItemViewModel;
        ViewModel.SelectedAvailableTable = e.NewValue as TableItemViewModel;

        if (e.NewValue is ColumnItemViewModel column)
        {
            _bulkSelectionAnchor = column;
        }
    }

    /// <summary>
    /// Gère les raccourcis de sélection multiple sur une colonne de l'arbre.
    /// </summary>
    /// <param name="sender">Élément visuel représentant la colonne cliquée.</param>
    /// <param name="e">Arguments de souris contenant l'état des touches de modification.</param>
    private void ColumnNode_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColumnItemViewModel column })
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            SelectBulkRange(column);
            e.Handled = true;
            return;
        }

        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            column.IsBulkSelected = !column.IsBulkSelected;
            _bulkSelectionAnchor = column;
            ViewModel.SelectedAvailableColumn = column;
            ViewModel.SelectedAvailableTable = null;
            e.Handled = true;
            return;
        }

        _bulkSelectionAnchor = column;
    }

    /// <summary>
    /// Coche toutes les colonnes visibles entre l'ancre de sélection et la colonne cible.
    /// </summary>
    /// <param name="targetColumn">Colonne finale de la plage à sélectionner.</param>
    private void SelectBulkRange(ColumnItemViewModel targetColumn)
    {
        List<ColumnItemViewModel> visibleColumns = ViewModel.Tables
            .SelectMany(table => table.Columns)
            .ToList();

        int targetIndex = visibleColumns.IndexOf(targetColumn);
        if (targetIndex < 0)
        {
            return;
        }

        int anchorIndex = _bulkSelectionAnchor is null ? -1 : visibleColumns.IndexOf(_bulkSelectionAnchor);
        if (anchorIndex < 0)
        {
            targetColumn.IsBulkSelected = true;
            _bulkSelectionAnchor = targetColumn;
            ViewModel.SelectedAvailableColumn = targetColumn;
            ViewModel.SelectedAvailableTable = null;
            return;
        }

        int start = Math.Min(anchorIndex, targetIndex);
        int end = Math.Max(anchorIndex, targetIndex);
        for (int index = start; index <= end; index++)
        {
            visibleColumns[index].IsBulkSelected = true;
        }

        ViewModel.SelectedAvailableColumn = targetColumn;
        ViewModel.SelectedAvailableTable = null;
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
    /// Ajoute une projection <c>table.*</c> au SELECT depuis le menu contextuel d'une table.
    /// </summary>
    /// <param name="sender">Élément de menu ayant déclenché l'action.</param>
    /// <param name="e">Arguments de l'événement WPF.</param>
    private void AddTableWildcardMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        TableItemViewModel? table = element.DataContext as TableItemViewModel;
        if (table is null && element.Parent is ContextMenu contextMenu)
        {
            table = contextMenu.DataContext as TableItemViewModel;
        }

        ViewModel.AddTableWildcard(table);
    }

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
