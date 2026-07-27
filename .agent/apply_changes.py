from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8-sig")


def write(path: str, content: str) -> None:
    Path(path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(content: str, old: str, new: str, label: str) -> str:
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one match for {label}, found {count}")
    return content.replace(old, new, 1)


# MainViewModel: prevent ObservableCollection reentrancy and load external saved-query files.
path = "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs"
content = read(path)
content = replace_once(
    content,
    """        Joins.CollectionChanged += Joins_CollectionChanged;

        WireAutoGenerate(SelectedColumns);
        WireAutoGenerate(Filters);
        WireAutoGenerate(GroupBy);
        WireAutoGenerate(OrderBy);
        WireAutoGenerate(Aggregates);
        WireAutoGenerate(Joins);
        WireAutoGenerate(CustomColumns);
""",
    """        WireAutoGenerate(SelectedColumns);
        WireAutoGenerate(Filters);
        WireAutoGenerate(GroupBy);
        WireAutoGenerate(OrderBy);
        WireAutoGenerate(Aggregates);
        WireAutoGenerate(Joins, Joins_CollectionChanged);
        WireAutoGenerate(CustomColumns);
""",
    "join collection wiring",
)
content = replace_once(
    content,
    """    private void Joins_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
""",
    """    private void Joins_CollectionChanged(NotifyCollectionChangedEventArgs e)
""",
    "join collection handler signature",
)
content = replace_once(
    content,
    """    private void WireAutoGenerate<T>(ObservableCollection<T> collection) where T : INotifyPropertyChanged
    {
        collection.CollectionChanged += (_, e) =>
        {
""",
    """    private void WireAutoGenerate<T>(
        ObservableCollection<T> collection,
        Action<NotifyCollectionChangedEventArgs>? collectionChanged = null)
        where T : INotifyPropertyChanged
    {
        collection.CollectionChanged += (_, e) =>
        {
""",
    "auto generation collection handler signature",
)
content = replace_once(
    content,
    """            if (e.NewItems is not null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems)
                {
                    item.PropertyChanged += Row_PropertyChanged;
                }
            }

            AutoGenerateSql();
""",
    """            if (e.NewItems is not null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems)
                {
                    item.PropertyChanged += Row_PropertyChanged;
                }
            }

            collectionChanged?.Invoke(e);
            AutoGenerateSql();
""",
    "collection-specific callback",
)
load_saved_method = """
    /// <summary>
    /// Loads a saved SqlQueryGenerator file from any location.
    /// </summary>
    /// <param name="filePath">Path to a <c>.sqlqg.json</c> saved query.</param>
    public void LoadSavedQueryFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Status = "Fichier de sauvegarde introuvable.";
            return;
        }

        FileInfo info = new(filePath);
        if (info.Length > 10_000_000)
        {
            MessageBox.Show("Le fichier de sauvegarde dépasse 10 Mo et ne peut pas être ouvert.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SavedQueryDefinition saved = _savedQueryStore.Load(filePath);
            if (saved.Kind == SavedQueryKind.RawSql)
            {
                RawSqlText = saved.RawSql ?? saved.LastGeneratedSql ?? string.Empty;
                QueryName = saved.Name;
                QueryDescription = saved.Description ?? string.Empty;
                ClearStoredRewrittenSql();
                ClearSqlComparison("Sauvegarde SQL brut chargée. Réécris-la ou charge-la dans le constructeur pour comparer les versions.");
                GeneratedSql = string.IsNullOrWhiteSpace(RawSqlText)
                    ? "-- La sauvegarde SQL brut ne contient aucune requête."
                    : RawSqlText.TrimEnd() + Environment.NewLine;
                QueryPurpose = "Sauvegarde SQL brut chargée. Utilise Reverse SQL pour la transformer en constructeur visuel.";
                PerformanceReport = "Analyse performance limitée tant que la requête n'est pas convertie en modèle visuel.";
                Warnings = "Sauvegarde SQL brut chargée depuis un fichier externe.";
            }
            else
            {
                LoadQueryDefinition(saved.Query, saved.Name, saved.Description);
            }

            Status = $"Sauvegarde chargée: {filePath}";
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or InvalidOperationException
                                   or System.Text.Json.JsonException)
        {
            Status = "Impossible de charger la sauvegarde.";
            Warnings = ex.Message;
        }
    }

"""
content = replace_once(
    content,
    """    /// <summary>
    /// Exécute le traitement LoadSchemaFromFile.
""",
    load_saved_method + """    /// <summary>
    /// Exécute le traitement LoadSchemaFromFile.
""",
    "external saved query loader insertion",
)
write(path, content)


# MainWindow XAML: expose the library, add prominent file actions and enable window-level drops.
path = "src/SqlQueryGenerator.App/MainWindow.xaml"
content = read(path)
content = replace_once(
    content,
    """        MinWidth="1240"
        WindowState="Maximized">
""",
    """        MinWidth="1240"
        WindowState="Maximized"
        AllowDrop="True"
        DragOver="Window_DragOver"
        Drop="Window_Drop">
""",
    "window drop events",
)
content = replace_once(
    content,
    """        <Style x:Key="PanelTitle" TargetType="{x:Type TextBlock}">
""",
    """        <Style x:Key="ToolbarButton" TargetType="{x:Type Button}" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Background" Value="#334155" />
            <Setter Property="Foreground" Value="#F8FAFC" />
            <Setter Property="BorderBrush" Value="#475569" />
            <Setter Property="FontWeight" Value="SemiBold" />
        </Style>
        <Style x:Key="PrimaryToolbarButton" TargetType="{x:Type Button}" BasedOn="{StaticResource ToolbarButton}">
            <Setter Property="Background" Value="#2563EB" />
            <Setter Property="BorderBrush" Value="#3B82F6" />
        </Style>
        <Style x:Key="PanelTitle" TargetType="{x:Type TextBlock}">
""",
    "toolbar styles",
)
content = replace_once(
    content,
    """                <WrapPanel Grid.Row="0" Grid.Column="0" VerticalAlignment="Center">
                    <Button Content="Charger schéma" Click="OpenSchema_Click" ToolTip="Charge un fichier .sql ou .txt contenant des CREATE TABLE et/ou COMMENT ON COLUMN." />
""",
    """                <WrapPanel Grid.Row="0" Grid.Column="0" VerticalAlignment="Center">
                    <Button Content="Ouvrir SQL" Click="OpenRawSql_Click" Style="{StaticResource PrimaryToolbarButton}" ToolTip="Ouvre une requête .sql dans l'éditeur SQL brut. Vous pouvez aussi déposer le fichier dans la fenêtre." />
                    <Button Content="Charger sauvegarde" Click="OpenSavedQuery_Click" Style="{StaticResource ToolbarButton}" ToolTip="Ouvre un fichier de sauvegarde .sqlqg.json depuis n'importe quel dossier." />
                    <Button Content="Bibliothèque" Click="OpenSavedQueries_Click" Style="{StaticResource ToolbarButton}" ToolTip="Affiche directement les presets et requêtes sauvegardées." />
                    <Button Content="Charger schéma" Click="OpenSchema_Click" ToolTip="Charge un fichier .sql ou .txt contenant des CREATE TABLE et/ou COMMENT ON COLUMN." />
""",
    "toolbar file and library actions",
)
content = replace_once(
    content,
    """                            <TextBlock Text="{Binding ApplicationVersion}"
                       Foreground="#E5E7EB"
                       FontWeight="SemiBold"
                       VerticalAlignment="Center" />
""",
    """                            <TextBlock Text="{Binding ApplicationVersion}"
                       Foreground="#E5E7EB"
                       FontWeight="SemiBold"
                       VerticalAlignment="Center"
                       ToolTip="Version détectée depuis les métadonnées du binaire installé." />
""",
    "version tooltip",
)
content = replace_once(
    content,
    """                            <TabControl Grid.Row="2" Margin="4">
""",
    """                            <TabControl x:Name="QueryBuilderTabs" Grid.Row="2" Margin="4">
""",
    "main tab control name",
)
content = replace_once(
    content,
    """                                <TabItem Header="Sous-requêtes / sauvegarde">
""",
    """                                <TabItem x:Name="SavedQueriesTab" Header="Bibliothèque / sous-requêtes">
""",
    "saved query tab accessibility",
)
content = replace_once(
    content,
    """                                        <Border Grid.Row="0" BorderBrush="#CBD5E1" BorderThickness="1" CornerRadius="8" Background="#FFFFFF" Padding="8" Margin="4">
""",
    """                                        <Border Grid.Row="0" BorderBrush="#93C5FD" BorderThickness="1" CornerRadius="8" Background="#EFF6FF" Padding="10" Margin="4">
""",
    "saved query header card",
)
content = replace_once(
    content,
    """                                                    <Button Content="Sauvegarder requête" Command="{Binding SaveCurrentQueryCommand}" />
                                                    <Button Content="Recharger bibliothèque" Command="{Binding ReloadSavedQueriesCommand}" />
                                                    <Button Content="Charger sélection" Command="{Binding LoadSelectedQueryCommand}" />
""",
    """                                                    <Button Content="Charger un fichier" Click="OpenSavedQuery_Click" Style="{StaticResource PrimaryToolbarButton}" ToolTip="Ouvre un fichier .sqlqg.json externe." />
                                                    <Button Content="Sauvegarder requête" Command="{Binding SaveCurrentQueryCommand}" />
                                                    <Button Content="Actualiser" Command="{Binding ReloadSavedQueriesCommand}" />
                                                    <Button Content="Ouvrir la sélection" Command="{Binding LoadSelectedQueryCommand}" />
""",
    "saved query action buttons",
)
content = replace_once(
    content,
    """                                                <TextBlock Text="Sauvegarde locale JSON dans le dossier saved_queries. Les presets peuvent être construits avec l'UI ou être du SQL brut SELECT. Les deux types peuvent être utilisés comme sous-requêtes filtrantes." TextWrapping="Wrap" Foreground="#475569" />
""",
    """                                                <TextBlock Text="Bibliothèque locale du dossier saved_queries. Double-clique une ligne pour l'ouvrir, utilise le bouton ci-dessus pour charger une sauvegarde externe, ou dépose directement un fichier .sqlqg.json dans la fenêtre. Les presets visuels et SQL brut peuvent aussi servir de sous-requêtes filtrantes." TextWrapping="Wrap" Foreground="#1E3A8A" FontWeight="SemiBold" />
""",
    "saved query usage hint",
)
write(path, content)


# MainWindow code-behind: dialogs and drag/drop routing.
path = "src/SqlQueryGenerator.App/MainWindow.xaml.cs"
content = read(path)
content = replace_once(
    content,
    """using SqlQueryGenerator.App.Export;
using SqlQueryGenerator.App.ViewModels;
""",
    """using SqlQueryGenerator.App.Export;
using SqlQueryGenerator.App.Services;
using SqlQueryGenerator.App.ViewModels;
""",
    "dropped file classifier using",
)
new_handlers = """
    /// <summary>
    /// Opens a saved query file from any location.
    /// </summary>
    private void OpenSavedQuery_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Ouvrir une sauvegarde SQL Query Generator",
            Filter = "Sauvegardes SQL Query Generator (*.sqlqg.json)|*.sqlqg.json|Tous les fichiers JSON (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.LoadSavedQueryFromFile(dialog.FileName);
            ShowSavedQueriesLibrary();
        }
    }

    /// <summary>
    /// Navigates directly to the saved query library.
    /// </summary>
    private void OpenSavedQueries_Click(object sender, RoutedEventArgs e) => ShowSavedQueriesLibrary();

    /// <summary>
    /// Shows a copy cursor only for one supported dropped file.
    /// </summary>
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetSingleDroppedFile(e.Data, out string filePath)
                    && DroppedFileClassifier.IsSupportedPath(filePath)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Opens a dropped SQL query, schema, or SqlQueryGenerator backup.
    /// </summary>
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetSingleDroppedFile(e.Data, out string filePath))
        {
            ViewModel.Status = "Dépose un seul fichier .sql ou .sqlqg.json à la fois.";
            e.Handled = true;
            return;
        }

        switch (DroppedFileClassifier.Classify(filePath))
        {
            case DroppedFileKind.SavedQuery:
                ViewModel.LoadSavedQueryFromFile(filePath);
                ShowSavedQueriesLibrary();
                break;
            case DroppedFileKind.SqlSchema:
                if (TryReadSchemaFile(filePath, out string schemaText))
                {
                    ImportSchemaTextWithReview(schemaText, filePath);
                }
                break;
            case DroppedFileKind.RawSql:
                ViewModel.LoadRawSqlFromFile(filePath);
                break;
            default:
                ViewModel.Status = "Format non pris en charge. Utilise un fichier .sql ou .sqlqg.json.";
                break;
        }

        e.Handled = true;
    }

    private static bool TryGetSingleDroppedFile(IDataObject data, out string filePath)
    {
        filePath = string.Empty;
        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return false;
        }

        filePath = files[0];
        return File.Exists(filePath);
    }

    private void ShowSavedQueriesLibrary()
    {
        QueryBuilderTabs.SelectedItem = SavedQueriesTab;
        SavedQueriesTab.BringIntoView();
        SavedQueriesTab.Focus();
    }

"""
content = replace_once(
    content,
    """    /// <summary>
    /// Exécute le traitement PasteSchema Click.
""",
    new_handlers + """    /// <summary>
    /// Exécute le traitement PasteSchema Click.
""",
    "file dialog and drag drop handlers",
)
write(path, content)

print("Application source transformations completed.")
