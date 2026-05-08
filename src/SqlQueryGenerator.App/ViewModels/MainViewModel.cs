using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Persistence;
using SqlQueryGenerator.Core.Query;
using SqlQueryGenerator.Core.Validation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente MainViewModel dans SQL Query Generator.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly SqlSchemaParser _parser = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly SqlQueryGeneratorEngine _generator = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly QueryValidator _validator = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly QueryPurposeDescriber _purposeDescriber = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly QueryPerformanceAnalyzer _performanceAnalyzer = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <param name="CurrentDirectory">Paramètre CurrentDirectory.</param>
    /// <param name="saved_queries">Paramètre saved_queries.</param>
    /// <returns>Résultat du traitement.</returns>
    private readonly SavedQueryStore _savedQueryStore = new(Path.Combine(Environment.CurrentDirectory, "saved_queries"));
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private readonly SchemaDocumentationImporter _documentationImporter = new();
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private DatabaseSchema _schema = new();
    /// <summary>
    /// Stocke la valeur interne  loadedFile.
    /// </summary>
    /// <value>Valeur de _loadedFile.</value>
    private string _loadedFile = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  status.
    /// </summary>
    /// <value>Valeur de _status.</value>
    private string _status = "Charge un schéma SQL/TXT pour commencer.";
    /// <summary>
    /// Stocke la valeur interne  generatedSql.
    /// </summary>
    /// <value>Valeur de _generatedSql.</value>
    private string _generatedSql = "-- La requête générée apparaîtra ici.";
    /// <summary>
    /// Stocke la valeur interne  warnings.
    /// </summary>
    /// <value>Valeur de _warnings.</value>
    private string _warnings = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  queryPurpose.
    /// </summary>
    /// <value>Valeur de _queryPurpose.</value>
    private string _queryPurpose = "Charge un schéma et construis une requête pour obtenir une explication en français.";
    /// <summary>
    /// Stocke la valeur interne  performanceReport.
    /// </summary>
    /// <value>Valeur de _performanceReport.</value>
    private string _performanceReport = "L'analyse de performance apparaîtra ici.";
    /// <summary>
    /// Stocke la valeur interne  queryName.
    /// </summary>
    /// <value>Valeur de _queryName.</value>
    private string _queryName = "nouvelle_requete";
    /// <summary>
    /// Stocke la valeur interne  queryDescription.
    /// </summary>
    /// <value>Valeur de _queryDescription.</value>
    private string _queryDescription = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  selectedSavedQuery.
    /// </summary>
    /// <value>Valeur de _selectedSavedQuery.</value>
    private SavedQueryItemViewModel? _selectedSavedQuery;
    /// <summary>
    /// Stocke la valeur interne  baseTable.
    /// </summary>
    /// <value>Valeur de _baseTable.</value>
    private string _baseTable = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  dialect.
    /// </summary>
    /// <value>Valeur de _dialect.</value>
    private SqlDialect _dialect = SqlDialect.SQLite;
    /// <summary>
    /// Stocke la valeur interne  quoteIdentifiers.
    /// </summary>
    /// <value>Valeur de _quoteIdentifiers.</value>
    private bool _quoteIdentifiers;
    /// <summary>
    /// Stocke la valeur interne  distinct.
    /// </summary>
    /// <value>Valeur de _distinct.</value>
    private bool _distinct;
    /// <summary>
    /// Stocke la valeur interne  autoGroupSelectedColumns.
    /// </summary>
    /// <value>Valeur de _autoGroupSelectedColumns.</value>
    private bool _autoGroupSelectedColumns = true;
    /// <summary>
    /// Stocke la valeur interne  limitRows.
    /// </summary>
    /// <value>Valeur de _limitRows.</value>
    private int? _limitRows;
    /// <summary>
    /// Stocke la valeur interne  selectedAvailableColumn.
    /// </summary>
    /// <value>Valeur de _selectedAvailableColumn.</value>
    private ColumnItemViewModel? _selectedAvailableColumn;
    /// <summary>
    /// Stocke la table sélectionnée dans l'arbre des colonnes disponibles.
    /// </summary>
    /// <value>Table actuellement sélectionnée, ou <c>null</c> si une colonne est sélectionnée.</value>
    private TableItemViewModel? _selectedAvailableTable;
    /// <summary>
    /// Stocke la valeur interne  selectedRelationship.
    /// </summary>
    /// <value>Valeur de _selectedRelationship.</value>
    private RelationshipItemViewModel? _selectedRelationship;
    /// <summary>
    /// Stocke la valeur interne  suppressAutoGenerate.
    /// </summary>
    /// <value>Valeur de _suppressAutoGenerate.</value>
    private bool _suppressAutoGenerate;
    /// <summary>
    /// Stocke la valeur interne  columnSearchText.
    /// </summary>
    /// <value>Valeur de _columnSearchText.</value>
    private string _columnSearchText = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  lastAppliedColumnSearchText.
    /// </summary>
    /// <value>Valeur de _lastAppliedColumnSearchText.</value>
    private string _lastAppliedColumnSearchText = "__not_applied__";
    /// <summary>
    /// Exécute le traitement Dictionary.
    /// </summary>
    /// <param name="OrdinalIgnoreCase">Paramètre OrdinalIgnoreCase.</param>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyDictionary<string, string> _cachedForeignKeySummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Exécute le traitement Dictionary.
    /// </summary>
    /// <param name="OrdinalIgnoreCase">Paramètre OrdinalIgnoreCase.</param>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyDictionary<string, string> _cachedIndexSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Exécute le traitement HashSet.
    /// </summary>
    /// <param name="OrdinalIgnoreCase">Paramètre OrdinalIgnoreCase.</param>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlySet<string> _cachedUniqueIndexColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Exécute le traitement Empty.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyList<TableDefinition> _sortedSchemaTables = Array.Empty<TableDefinition>();
    /// <summary>
    /// Stocke la valeur interne  columnNamesByTable.
    /// </summary>
    /// <value>Valeur de _columnNamesByTable.</value>
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _columnNamesByTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialise une nouvelle instance de MainViewModel.
    /// </summary>
    public MainViewModel()
    {
        GenerateCommand = new RelayCommand(GenerateSql);
        ClearQueryCommand = new RelayCommand(ClearQuery);
        RemoveSelectedColumnCommand = new RelayCommand(obj => RemoveFromCollection(SelectedColumns, obj));
        RemoveFilterCommand = new RelayCommand(obj => RemoveFromCollection(Filters, obj));
        RemoveGroupByCommand = new RelayCommand(obj => RemoveFromCollection(GroupBy, obj));
        RemoveOrderByCommand = new RelayCommand(obj => RemoveFromCollection(OrderBy, obj));
        RemoveAggregateCommand = new RelayCommand(obj => RemoveFromCollection(Aggregates, obj));
        RemoveJoinCommand = new RelayCommand(obj => RemoveFromCollection(Joins, obj));
        RemoveCustomColumnCommand = new RelayCommand(obj => RemoveFromCollection(CustomColumns, obj));
        AddSelectedColumnCommand = new RelayCommand(_ => AddSelectedColumnTo("select"), _ => SelectedAvailableColumn is not null);
        AddCheckedColumnsToSelectCommand = new RelayCommand(AddCheckedColumnsToSelect);
        ClearCheckedColumnsCommand = new RelayCommand(ClearCheckedColumns);
        AddSelectedTableWildcardCommand = new RelayCommand(_ => AddTableWildcard(SelectedAvailableTable), _ => SelectedAvailableTable is not null);
        AddSelectedFilterCommand = new RelayCommand(_ => AddSelectedColumnTo("filter"), _ => SelectedAvailableColumn is not null);
        AddSelectedGroupByCommand = new RelayCommand(_ => AddSelectedColumnTo("group"), _ => SelectedAvailableColumn is not null);
        AddSelectedOrderByCommand = new RelayCommand(_ => AddSelectedColumnTo("order"), _ => SelectedAvailableColumn is not null);
        AddSelectedAggregateCommand = new RelayCommand(_ => AddSelectedColumnTo("aggregate"), _ => SelectedAvailableColumn is not null);
        AddJoinFromRelationshipCommand = new RelayCommand(_ => AddSelectedRelationshipAsJoin(), _ => SelectedRelationship is not null);
        AddManualJoinCommand = new RelayCommand(AddManualJoin);
        AddJoinPairCommand = new RelayCommand(obj => AddJoinPair(obj as JoinRowViewModel));
        RemoveJoinPairCommand = new RelayCommand(obj => RemoveJoinPair(obj as JoinColumnPairRowViewModel));
        AddEmptyCustomColumnCommand = new RelayCommand(() => CustomColumns.Add(new CustomColumnRowViewModel { Alias = BuildDefaultCustomAlias() }));
        AddAggregateFilterCommand = new RelayCommand(obj => AddAggregateToFilter(obj as AggregateRowViewModel));
        AddAggregateOrderByCommand = new RelayCommand(obj => AddAggregateToOrderBy(obj as AggregateRowViewModel));
        AddCustomFilterCommand = new RelayCommand(obj => AddCustomColumnToFilter(obj as CustomColumnRowViewModel));
        AddCustomOrderByCommand = new RelayCommand(obj => AddCustomColumnToOrderBy(obj as CustomColumnRowViewModel));
        AddParameterCommand = new RelayCommand(() => Parameters.Add(new QueryParameterRowViewModel { Name = $"param_{Parameters.Count + 1}", Required = true }));
        RemoveParameterCommand = new RelayCommand(obj => RemoveFromCollection(Parameters, obj));
        SaveCurrentQueryCommand = new RelayCommand(SaveCurrentQuery);
        ReloadSavedQueriesCommand = new RelayCommand(ReloadSavedQueries);
        LoadSelectedQueryCommand = new RelayCommand(_ => LoadSelectedSavedQuery(), _ => SelectedSavedQuery is not null);
        AddSelectedSavedQueryAsSubqueryFilterCommand = new RelayCommand(_ => AddSelectedSavedQueryAsSubqueryFilter(), _ => SelectedSavedQuery is not null);
        ClearColumnSearchCommand = new RelayCommand(() => ColumnSearchText = string.Empty);
        ReloadSavedQueries();

        Joins.CollectionChanged += Joins_CollectionChanged;

        WireAutoGenerate(SelectedColumns);
        WireAutoGenerate(Filters);
        WireAutoGenerate(GroupBy);
        WireAutoGenerate(OrderBy);
        WireAutoGenerate(Aggregates);
        WireAutoGenerate(Joins);
        WireAutoGenerate(CustomColumns);
        WireAutoGenerate(Parameters);
    }

    /// <summary>
    /// Stocke la valeur interne Tables.
    /// </summary>
    /// <value>Valeur de Tables.</value>
    public ObservableCollection<TableItemViewModel> Tables { get; } = [];
    /// <summary>
    /// Stocke la valeur interne AllColumns.
    /// </summary>
    /// <value>Valeur de AllColumns.</value>
    public ObservableCollection<ColumnItemViewModel> AllColumns { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Relationships.
    /// </summary>
    /// <value>Valeur de Relationships.</value>
    public ObservableCollection<RelationshipItemViewModel> Relationships { get; } = [];
    /// <summary>
    /// Stocke la valeur interne RelationshipGroups.
    /// </summary>
    /// <value>Valeur de RelationshipGroups.</value>
    public ObservableCollection<RelationshipGroupViewModel> RelationshipGroups { get; } = [];
    /// <summary>
    /// Stocke la valeur interne SelectedColumns.
    /// </summary>
    /// <value>Valeur de SelectedColumns.</value>
    public ObservableCollection<SelectColumnRowViewModel> SelectedColumns { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Filters.
    /// </summary>
    /// <value>Valeur de Filters.</value>
    public ObservableCollection<FilterRowViewModel> Filters { get; } = [];
    /// <summary>
    /// Stocke la valeur interne GroupBy.
    /// </summary>
    /// <value>Valeur de GroupBy.</value>
    public ObservableCollection<GroupByRowViewModel> GroupBy { get; } = [];
    /// <summary>
    /// Stocke la valeur interne OrderBy.
    /// </summary>
    /// <value>Valeur de OrderBy.</value>
    public ObservableCollection<OrderByRowViewModel> OrderBy { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Aggregates.
    /// </summary>
    /// <value>Valeur de Aggregates.</value>
    public ObservableCollection<AggregateRowViewModel> Aggregates { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Joins.
    /// </summary>
    /// <value>Valeur de Joins.</value>
    public ObservableCollection<JoinRowViewModel> Joins { get; } = [];
    /// <summary>
    /// Stocke la valeur interne CustomColumns.
    /// </summary>
    /// <value>Valeur de CustomColumns.</value>
    public ObservableCollection<CustomColumnRowViewModel> CustomColumns { get; } = [];
    /// <summary>
    /// Stocke la valeur interne Parameters.
    /// </summary>
    /// <value>Valeur de Parameters.</value>
    public ObservableCollection<QueryParameterRowViewModel> Parameters { get; } = [];
    /// <summary>
    /// Stocke la valeur interne SavedQueries.
    /// </summary>
    /// <value>Valeur de SavedQueries.</value>
    public ObservableCollection<SavedQueryItemViewModel> SavedQueries { get; } = [];
    /// <summary>
    /// Stocke la valeur interne TableNames.
    /// </summary>
    /// <value>Valeur de TableNames.</value>
    public ObservableCollection<string> TableNames { get; } = [];
    /// <summary>
    /// Obtient ou définit ColumnNamesByTable.
    /// </summary>
    /// <value>Valeur de ColumnNamesByTable.</value>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnNamesByTable { get => _columnNamesByTable; private set => SetProperty(ref _columnNamesByTable, value); }
    /// <summary>
    /// Stocke la valeur interne Operators.
    /// </summary>
    /// <value>Valeur de Operators.</value>
    public IReadOnlyList<string> Operators { get; } = new[] { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL", "EXISTS", "NOT EXISTS" };
    /// <summary>
    /// Obtient ou définit FilterValueKinds.
    /// </summary>
    /// <value>Valeur de FilterValueKinds.</value>
    public Array FilterValueKinds => Enum.GetValues(typeof(FilterValueKind));
    /// <summary>
    /// Obtient ou définit Dialects.
    /// </summary>
    /// <value>Valeur de Dialects.</value>
    public Array Dialects => Enum.GetValues(typeof(SqlDialect));
    /// <summary>
    /// Obtient ou définit JoinTypes.
    /// </summary>
    /// <value>Valeur de JoinTypes.</value>
    public Array JoinTypes => Enum.GetValues(typeof(JoinType));
    /// <summary>
    /// Obtient ou définit AggregateFunctions.
    /// </summary>
    /// <value>Valeur de AggregateFunctions.</value>
    public Array AggregateFunctions => Enum.GetValues(typeof(AggregateFunction));
    /// <summary>
    /// Obtient ou définit SortDirections.
    /// </summary>
    /// <value>Valeur de SortDirections.</value>
    public Array SortDirections => Enum.GetValues(typeof(SortDirection));
    /// <summary>
    /// Obtient ou définit LogicalConnectors.
    /// </summary>
    /// <value>Valeur de LogicalConnectors.</value>
    public Array LogicalConnectors => Enum.GetValues(typeof(LogicalConnector));

    /// <summary>
    /// Stocke la valeur interne GenerateCommand.
    /// </summary>
    /// <value>Valeur de GenerateCommand.</value>
    public RelayCommand GenerateCommand { get; }
    /// <summary>
    /// Stocke la valeur interne ClearQueryCommand.
    /// </summary>
    /// <value>Valeur de ClearQueryCommand.</value>
    public RelayCommand ClearQueryCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveSelectedColumnCommand.
    /// </summary>
    /// <value>Valeur de RemoveSelectedColumnCommand.</value>
    public RelayCommand RemoveSelectedColumnCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveFilterCommand.
    /// </summary>
    /// <value>Valeur de RemoveFilterCommand.</value>
    public RelayCommand RemoveFilterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveGroupByCommand.
    /// </summary>
    /// <value>Valeur de RemoveGroupByCommand.</value>
    public RelayCommand RemoveGroupByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveOrderByCommand.
    /// </summary>
    /// <value>Valeur de RemoveOrderByCommand.</value>
    public RelayCommand RemoveOrderByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveAggregateCommand.
    /// </summary>
    /// <value>Valeur de RemoveAggregateCommand.</value>
    public RelayCommand RemoveAggregateCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveJoinCommand.
    /// </summary>
    /// <value>Valeur de RemoveJoinCommand.</value>
    public RelayCommand RemoveJoinCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveCustomColumnCommand.
    /// </summary>
    /// <value>Valeur de RemoveCustomColumnCommand.</value>
    public RelayCommand RemoveCustomColumnCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedColumnCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedColumnCommand.</value>
    public RelayCommand AddSelectedColumnCommand { get; }
    /// <summary>
    /// Commande ajoutant au SELECT toutes les colonnes cochées dans l'arbre des colonnes disponibles.
    /// </summary>
    /// <value>Commande d'ajout groupé au SELECT.</value>
    public RelayCommand AddCheckedColumnsToSelectCommand { get; }
    /// <summary>
    /// Commande qui décoche toutes les colonnes actuellement cochées dans l'arbre.
    /// </summary>
    /// <value>Commande d'effacement de la sélection de masse.</value>
    public RelayCommand ClearCheckedColumnsCommand { get; }
    /// <summary>
    /// Commande ajoutant <c>table.*</c> au SELECT pour la table sélectionnée.
    /// </summary>
    /// <value>Commande d'ajout direct de toutes les colonnes d'une table.</value>
    public RelayCommand AddSelectedTableWildcardCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedFilterCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedFilterCommand.</value>
    public RelayCommand AddSelectedFilterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedGroupByCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedGroupByCommand.</value>
    public RelayCommand AddSelectedGroupByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedOrderByCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedOrderByCommand.</value>
    public RelayCommand AddSelectedOrderByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedAggregateCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedAggregateCommand.</value>
    public RelayCommand AddSelectedAggregateCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddJoinFromRelationshipCommand.
    /// </summary>
    /// <value>Valeur de AddJoinFromRelationshipCommand.</value>
    public RelayCommand AddJoinFromRelationshipCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddManualJoinCommand.
    /// </summary>
    /// <value>Valeur de AddManualJoinCommand.</value>
    public RelayCommand AddManualJoinCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddJoinPairCommand.
    /// </summary>
    /// <value>Valeur de AddJoinPairCommand.</value>
    public RelayCommand AddJoinPairCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveJoinPairCommand.
    /// </summary>
    /// <value>Valeur de RemoveJoinPairCommand.</value>
    public RelayCommand RemoveJoinPairCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddEmptyCustomColumnCommand.
    /// </summary>
    /// <value>Valeur de AddEmptyCustomColumnCommand.</value>
    public RelayCommand AddEmptyCustomColumnCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddAggregateFilterCommand.
    /// </summary>
    /// <value>Valeur de AddAggregateFilterCommand.</value>
    public RelayCommand AddAggregateFilterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddAggregateOrderByCommand.
    /// </summary>
    /// <value>Valeur de AddAggregateOrderByCommand.</value>
    public RelayCommand AddAggregateOrderByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddCustomFilterCommand.
    /// </summary>
    /// <value>Valeur de AddCustomFilterCommand.</value>
    public RelayCommand AddCustomFilterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddCustomOrderByCommand.
    /// </summary>
    /// <value>Valeur de AddCustomOrderByCommand.</value>
    public RelayCommand AddCustomOrderByCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddParameterCommand.
    /// </summary>
    /// <value>Valeur de AddParameterCommand.</value>
    public RelayCommand AddParameterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RemoveParameterCommand.
    /// </summary>
    /// <value>Valeur de RemoveParameterCommand.</value>
    public RelayCommand RemoveParameterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne SaveCurrentQueryCommand.
    /// </summary>
    /// <value>Valeur de SaveCurrentQueryCommand.</value>
    public RelayCommand SaveCurrentQueryCommand { get; }
    /// <summary>
    /// Stocke la valeur interne ReloadSavedQueriesCommand.
    /// </summary>
    /// <value>Valeur de ReloadSavedQueriesCommand.</value>
    public RelayCommand ReloadSavedQueriesCommand { get; }
    /// <summary>
    /// Stocke la valeur interne LoadSelectedQueryCommand.
    /// </summary>
    /// <value>Valeur de LoadSelectedQueryCommand.</value>
    public RelayCommand LoadSelectedQueryCommand { get; }
    /// <summary>
    /// Stocke la valeur interne AddSelectedSavedQueryAsSubqueryFilterCommand.
    /// </summary>
    /// <value>Valeur de AddSelectedSavedQueryAsSubqueryFilterCommand.</value>
    public RelayCommand AddSelectedSavedQueryAsSubqueryFilterCommand { get; }
    /// <summary>
    /// Stocke la valeur interne ClearColumnSearchCommand.
    /// </summary>
    /// <value>Valeur de ClearColumnSearchCommand.</value>
    public RelayCommand ClearColumnSearchCommand { get; }

    /// <summary>
    /// Obtient ou définit LoadedFile.
    /// </summary>
    /// <value>Valeur de LoadedFile.</value>
    public string LoadedFile { get => _loadedFile; set => SetProperty(ref _loadedFile, value); }
    /// <summary>
    /// Obtient ou définit Status.
    /// </summary>
    /// <value>Valeur de Status.</value>
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    /// <summary>
    /// Obtient ou définit GeneratedSql.
    /// </summary>
    /// <value>Valeur de GeneratedSql.</value>
    public string GeneratedSql { get => _generatedSql; set => SetProperty(ref _generatedSql, value); }
    /// <summary>
    /// Obtient ou définit Warnings.
    /// </summary>
    /// <value>Valeur de Warnings.</value>
    public string Warnings { get => _warnings; set => SetProperty(ref _warnings, value); }
    /// <summary>
    /// Obtient ou définit QueryPurpose.
    /// </summary>
    /// <value>Valeur de QueryPurpose.</value>
    public string QueryPurpose { get => _queryPurpose; set => SetProperty(ref _queryPurpose, value); }
    /// <summary>
    /// Obtient ou définit PerformanceReport.
    /// </summary>
    /// <value>Valeur de PerformanceReport.</value>
    public string PerformanceReport { get => _performanceReport; set => SetProperty(ref _performanceReport, value); }
    /// <summary>
    /// Obtient ou définit QueryName.
    /// </summary>
    /// <value>Valeur de QueryName.</value>
    public string QueryName { get => _queryName; set => SetProperty(ref _queryName, value); }
    /// <summary>
    /// Obtient ou définit QueryDescription.
    /// </summary>
    /// <value>Valeur de QueryDescription.</value>
    public string QueryDescription { get => _queryDescription; set => SetProperty(ref _queryDescription, value); }

    /// <summary>
    /// Stocke la valeur interne SelectedSavedQuery.
    /// </summary>
    /// <value>Valeur de SelectedSavedQuery.</value>
    public SavedQueryItemViewModel? SelectedSavedQuery
    {
        get => _selectedSavedQuery;
        set
        {
            if (SetProperty(ref _selectedSavedQuery, value))
            {
                LoadSelectedQueryCommand.RaiseCanExecuteChanged();
                AddSelectedSavedQueryAsSubqueryFilterCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne BaseTable.
    /// </summary>
    /// <value>Valeur de BaseTable.</value>
    public string BaseTable
    {
        get => _baseTable;
        set
        {
            if (SetProperty(ref _baseTable, value))
            {
                AutoGenerateSql();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne Dialect.
    /// </summary>
    /// <value>Valeur de Dialect.</value>
    public SqlDialect Dialect
    {
        get => _dialect;
        set
        {
            if (SetProperty(ref _dialect, value))
            {
                AutoGenerateSql();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne QuoteIdentifiers.
    /// </summary>
    /// <value>Valeur de QuoteIdentifiers.</value>
    public bool QuoteIdentifiers
    {
        get => _quoteIdentifiers;
        set
        {
            if (SetProperty(ref _quoteIdentifiers, value))
            {
                AutoGenerateSql();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne Distinct.
    /// </summary>
    /// <value>Valeur de Distinct.</value>
    public bool Distinct
    {
        get => _distinct;
        set
        {
            if (SetProperty(ref _distinct, value))
            {
                AutoGenerateSql();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne AutoGroupSelectedColumns.
    /// </summary>
    /// <value>Valeur de AutoGroupSelectedColumns.</value>
    public bool AutoGroupSelectedColumns
    {
        get => _autoGroupSelectedColumns;
        set
        {
            if (SetProperty(ref _autoGroupSelectedColumns, value))
            {
                AutoGenerateSql();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne LimitRows.
    /// </summary>
    /// <value>Valeur de LimitRows.</value>
    public int? LimitRows
    {
        get => _limitRows;
        set
        {
            if (SetProperty(ref _limitRows, value))
            {
                AutoGenerateSql();
            }
        }
    }


    /// <summary>
    /// Stocke la valeur interne ColumnSearchText.
    /// </summary>
    /// <value>Valeur de ColumnSearchText.</value>
    public string ColumnSearchText
    {
        get => _columnSearchText;
        set
        {
            if (SetProperty(ref _columnSearchText, value))
            {
                ApplyColumnTreeFilter();
                ClearColumnSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit SchemaSummary.
    /// </summary>
    /// <value>Valeur de SchemaSummary.</value>
    public string SchemaSummary => _schema.Tables.Count == 0
        ? "Aucun schéma chargé"
        : $"{_schema.PhysicalTables.Count()} tables · {_schema.Views.Count()} vues · {_schema.Tables.Sum(t => t.Columns.Count)} colonnes · {_schema.Indexes.Count} index · {_schema.Relationships.Count} relations probables";

    /// <summary>
    /// Stocke la valeur interne SelectedAvailableColumn.
    /// </summary>
    /// <value>Valeur de SelectedAvailableColumn.</value>
    public ColumnItemViewModel? SelectedAvailableColumn
    {
        get => _selectedAvailableColumn;
        set
        {
            if (SetProperty(ref _selectedAvailableColumn, value))
            {
                AddSelectedColumnCommand.RaiseCanExecuteChanged();
                AddSelectedFilterCommand.RaiseCanExecuteChanged();
                AddSelectedGroupByCommand.RaiseCanExecuteChanged();
                AddSelectedOrderByCommand.RaiseCanExecuteChanged();
                AddSelectedAggregateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit la table sélectionnée dans l'arbre des colonnes disponibles.
    /// </summary>
    /// <value>Table sélectionnée pour les actions de table, notamment <c>table.*</c>.</value>
    public TableItemViewModel? SelectedAvailableTable
    {
        get => _selectedAvailableTable;
        set
        {
            if (SetProperty(ref _selectedAvailableTable, value))
            {
                AddSelectedTableWildcardCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne SelectedRelationship.
    /// </summary>
    /// <value>Valeur de SelectedRelationship.</value>
    public RelationshipItemViewModel? SelectedRelationship
    {
        get => _selectedRelationship;
        set
        {
            if (SetProperty(ref _selectedRelationship, value))
            {
                AddJoinFromRelationshipCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Exécute le traitement LoadSchemaFromFile.
    /// </summary>
    /// <param name="filePath">Paramètre filePath.</param>
    public void LoadSchemaFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Status = "Fichier introuvable.";
            return;
        }

        FileInfo info = new(filePath);
        if (info.Length > 20_000_000)
        {
            MessageBox.Show("Le fichier dépasse 20 Mo. Pour éviter de bloquer l'interface, réduis le schéma ou augmente la limite dans MainViewModel.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string text = File.ReadAllText(filePath);
        LoadSchemaFromText(text, filePath);
    }

    /// <summary>
    /// Exécute le traitement ImportDocumentationFromFile.
    /// </summary>
    /// <param name="filePath">Paramètre filePath.</param>
    public void ImportDocumentationFromFile(string filePath)
    {
        if (_schema.Tables.Count == 0)
        {
            MessageBox.Show("Charge d'abord un schéma SQL avant d'importer la documentation.", "Documentation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SchemaDocumentationImportResult result = _documentationImporter.ApplyFromFile(_schema, filePath);
            ReloadSchemaViewModels();
            OnPropertyChanged(nameof(SchemaSummary));
            Status = result.ToString();
            Warnings = result.Warnings.Count == 0 ? "Documentation importée sans avertissement." : string.Join(Environment.NewLine, result.Warnings);
            GenerateSql();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            Status = "Erreur pendant l'import de documentation.";
            Warnings = ex.Message;
        }
    }

    /// <summary>
    /// Exécute le traitement LoadSchemaFromText.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <param name="sourceName">Paramètre sourceName.</param>
    public void LoadSchemaFromText(string text, string? sourceName = null)
    {
        try
        {
            _schema = _parser.Parse(text);
            LoadedFile = sourceName ?? "Collé manuellement";
            ReloadSchemaViewModels();
            OnPropertyChanged(nameof(SchemaSummary));
            Status = $"Schéma chargé: {_schema.PhysicalTables.Count()} tables, {_schema.Views.Count()} vues, {_schema.Tables.Sum(t => t.Columns.Count)} colonnes, {_schema.Indexes.Count} index, {_schema.Relationships.Count} relations probables.";
            Warnings = string.Join(Environment.NewLine, _schema.Warnings);
            if (string.IsNullOrWhiteSpace(BaseTable) && TableNames.Count > 0)
            {
                BaseTable = TableNames[0];
            }
            GenerateSql();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            Status = "Erreur de chargement du schéma.";
            Warnings = ex.Message;
        }
    }

    /// <summary>
    /// Exécute le traitement AddColumnToTarget.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="target">Paramètre target.</param>
    public void AddColumnToTarget(ColumnItemViewModel column, string target)
    {
        switch (target)
        {
            case "select":
                EnsureSelectedColumn(column);
                break;
            case "filter":
                Filters.Add(new FilterRowViewModel { Table = column.Table, Column = column.Column });
                break;
            case "group":
                EnsureSelectedColumn(column);
                if (!GroupBy.Any(g => SameColumn(g.Table, g.Column, column.Table, column.Column)))
                {
                    GroupBy.Add(new GroupByRowViewModel { Table = column.Table, Column = column.Column });
                }
                break;
            case "order":
                OrderBy.Add(new OrderByRowViewModel { Table = column.Table, Column = column.Column });
                break;
            case "aggregate":
                Aggregates.Add(new AggregateRowViewModel
                {
                    Table = column.Table,
                    Column = column.Column,
                    Function = AggregateFunction.Count,
                    Alias = AggregateRowViewModel.BuildDefaultAlias(AggregateFunction.Count, column.Column)
                });
                break;
            case "case":
                CustomColumns.Add(new CustomColumnRowViewModel { CaseTable = column.Table, CaseColumn = column.Column, Alias = $"{column.Column}_case" });
                break;
        }
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement EnsureSelectedColumn.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    private void EnsureSelectedColumn(ColumnItemViewModel column)
    {
        if (!SelectedColumns.Any(c => SameColumn(c.Table, c.Column, column.Table, column.Column)))
        {
            SelectedColumns.Add(new SelectColumnRowViewModel { Table = column.Table, Column = column.Column });
        }
    }

    /// <summary>
    /// Ajoute au SELECT toutes les colonnes cochées dans l'arbre des colonnes disponibles.
    /// </summary>
    public void AddCheckedColumnsToSelect()
    {
        ColumnItemViewModel[] checkedColumns = [.. Tables
            .SelectMany(t => t.Columns)
            .Where(c => c.IsBulkSelected)];

        foreach (ColumnItemViewModel? column in checkedColumns)
        {
            EnsureSelectedColumn(column);
        }

        if (checkedColumns.Length > 0)
        {
            GenerateSql();
        }
    }

    /// <summary>
    /// Décoche toutes les colonnes de l'arbre des colonnes disponibles.
    /// </summary>
    public void ClearCheckedColumns()
    {
        foreach (ColumnItemViewModel? column in Tables.SelectMany(t => t.Columns))
        {
            column.IsBulkSelected = false;
        }
    }

    /// <summary>
    /// Ajoute une projection <c>table.*</c> au SELECT pour la table indiquée.
    /// </summary>
    /// <param name="table">Table à projeter entièrement.</param>
    public void AddTableWildcard(TableItemViewModel? table)
    {
        if (table is null || string.IsNullOrWhiteSpace(table.Name))
        {
            return;
        }

        if (!SelectedColumns.Any(c => SameColumn(c.Table, c.Column, table.Name, "*")))
        {
            SelectedColumns.Add(new SelectColumnRowViewModel { Table = table.Name, Column = "*" });
        }

        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement SameColumn.
    /// </summary>
    /// <param name="leftTable">Paramètre leftTable.</param>
    /// <param name="leftColumn">Paramètre leftColumn.</param>
    /// <param name="rightTable">Paramètre rightTable.</param>
    /// <param name="rightColumn">Paramètre rightColumn.</param>
    /// <returns>Résultat du traitement.</returns>
    private static bool SameColumn(string leftTable, string leftColumn, string rightTable, string rightColumn)
    {
        return string.Equals(leftTable, rightTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(leftColumn, rightColumn, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Exécute le traitement AddAggregateToFilter.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    private void AddAggregateToFilter(AggregateRowViewModel? aggregate)
    {
        if (aggregate is null)
        {
            return;
        }

        string alias = EnsureAggregateAlias(aggregate);
        Filters.Add(new FilterRowViewModel
        {
            FieldKind = QueryFieldKind.Aggregate,
            FieldAlias = alias,
            Table = "Agrégat",
            Column = alias,
            Operator = ">"
        });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement AddAggregateToOrderBy.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    private void AddAggregateToOrderBy(AggregateRowViewModel? aggregate)
    {
        if (aggregate is null)
        {
            return;
        }

        string alias = EnsureAggregateAlias(aggregate);
        OrderBy.Add(new OrderByRowViewModel
        {
            FieldKind = QueryFieldKind.Aggregate,
            FieldAlias = alias,
            Table = "Agrégat",
            Column = alias,
            Direction = SortDirection.Descending
        });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement AddCustomColumnToFilter.
    /// </summary>
    /// <param name="custom">Paramètre custom.</param>
    private void AddCustomColumnToFilter(CustomColumnRowViewModel? custom)
    {
        if (custom is null)
        {
            return;
        }

        string alias = EnsureCustomAlias(custom);
        Filters.Add(new FilterRowViewModel
        {
            FieldKind = QueryFieldKind.CustomColumn,
            FieldAlias = alias,
            Table = "Calculé",
            Column = alias,
            Operator = "="
        });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement AddCustomColumnToOrderBy.
    /// </summary>
    /// <param name="custom">Paramètre custom.</param>
    private void AddCustomColumnToOrderBy(CustomColumnRowViewModel? custom)
    {
        if (custom is null)
        {
            return;
        }

        string alias = EnsureCustomAlias(custom);
        OrderBy.Add(new OrderByRowViewModel
        {
            FieldKind = QueryFieldKind.CustomColumn,
            FieldAlias = alias,
            Table = "Calculé",
            Column = alias,
            Direction = SortDirection.Ascending
        });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement EnsureAggregateAlias.
    /// </summary>
    /// <param name="aggregate">Paramètre aggregate.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string EnsureAggregateAlias(AggregateRowViewModel aggregate)
    {
        if (string.IsNullOrWhiteSpace(aggregate.Alias))
        {
            aggregate.Alias = AggregateRowViewModel.BuildDefaultAlias(aggregate.Function, aggregate.Column);
        }

        return aggregate.Alias.Trim();
    }

    /// <summary>
    /// Exécute le traitement EnsureCustomAlias.
    /// </summary>
    /// <param name="custom">Paramètre custom.</param>
    /// <returns>Résultat du traitement.</returns>
    private string EnsureCustomAlias(CustomColumnRowViewModel custom)
    {
        if (string.IsNullOrWhiteSpace(custom.Alias))
        {
            custom.Alias = BuildDefaultCustomAlias();
        }

        return custom.Alias.Trim();
    }

    /// <summary>
    /// Exécute le traitement BuildDefaultCustomAlias.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private string BuildDefaultCustomAlias()
    {
        int index = CustomColumns.Count + 1;
        string alias = $"colonne_calculee_{index}";
        while (CustomColumns.Any(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            alias = $"colonne_calculee_{index}";
        }

        return alias;
    }
    /// <summary>
    /// Exécute le traitement AddSelectedColumnTo.
    /// </summary>
    /// <param name="target">Paramètre target.</param>
    private void AddSelectedColumnTo(string target)
    {
        if (SelectedAvailableColumn is not null)
        {
            AddColumnToTarget(SelectedAvailableColumn, target);
        }
    }

    /// <summary>
    /// Exécute le traitement AddSelectedRelationshipAsJoin.
    /// </summary>
    private void AddSelectedRelationshipAsJoin()
    {
        if (SelectedRelationship is null)
        {
            return;
        }

        Joins.Add(new JoinRowViewModel
        {
            FromTable = SelectedRelationship.FromTable,
            FromColumn = SelectedRelationship.FromColumn,
            ToTable = SelectedRelationship.ToTable,
            ToColumn = SelectedRelationship.ToColumn,
            JoinType = JoinType.Inner
        });
        GenerateSql();
    }


    /// <summary>
    /// Exécute le traitement Joins CollectionChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Joins_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null)
        {
            return;
        }

        foreach (JoinRowViewModel join in e.NewItems)
        {
            join.ColumnNamesProvider = GetColumnNamesForTable;
        }
    }

    /// <summary>
    /// Exécute le traitement GetColumnNamesForTable.
    /// </summary>
    /// <param name="tableName">Paramètre tableName.</param>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyList<string> GetColumnNamesForTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Array.Empty<string>();
        }

        return ColumnNamesByTable.TryGetValue(tableName, out IReadOnlyList<string>? columns)
            ? columns
            : Array.Empty<string>();
    }

    /// <summary>
    /// Exécute le traitement ReloadSchemaViewModels.
    /// </summary>
    private void ReloadSchemaViewModels()
    {
        Tables.Clear();
        AllColumns.Clear();
        Relationships.Clear();
        RelationshipGroups.Clear();
        TableNames.Clear();
        _cachedForeignKeySummaries = BuildForeignKeySummaries();
        _cachedIndexSummaries = BuildIndexSummaries();
        _cachedUniqueIndexColumns = BuildUniqueIndexColumnSet();
        _sortedSchemaTables = _schema.Tables.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase).ToArray();

        Dictionary<string, IReadOnlyList<string>> columnNamesByTable = new(StringComparer.OrdinalIgnoreCase);
        foreach (TableDefinition table in _sortedSchemaTables)
        {
            TableNames.Add(table.FullName);
            ColumnDefinition[] sortedColumns = [.. table.Columns.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];
            columnNamesByTable[table.FullName] = sortedColumns.Select(c => c.Name).ToArray();
            columnNamesByTable[SqlObjectDisplayName.Table(table.FullName)] = sortedColumns.Select(c => c.Name).ToArray();
            foreach (ColumnDefinition? col in sortedColumns)
            {
                AllColumns.Add(new ColumnItemViewModel(
                    col,
                    LookupSummary(col, _cachedForeignKeySummaries),
                    LookupSummary(col, _cachedIndexSummaries),
                    _cachedUniqueIndexColumns.Contains($"{col.TableName}.{col.Name}")));
            }
        }
        ColumnNamesByTable = columnNamesByTable;
        _lastAppliedColumnSearchText = "__schema_reloaded__";
        foreach (InferredRelationship? rel in _schema.Relationships.OrderByDescending(r => r.Confidence).Take(500))
        {
            RelationshipItemViewModel vm = new(rel);
            vm.PropertyChanged += Relationship_PropertyChanged;
            Relationships.Add(vm);
        }

        foreach (IGrouping<string, RelationshipItemViewModel>? group in Relationships
                     .GroupBy(r => r.FromTable, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            RelationshipGroups.Add(new RelationshipGroupViewModel($"{SqlObjectDisplayName.Table(group.Key)} ({group.Count()})", group.OrderByDescending(r => r.Confidence))
            {
                IsExpanded = false
            });
        }

        ApplyColumnTreeFilter();
    }

    /// <summary>
    /// Exécute le traitement ApplyColumnTreeFilter.
    /// </summary>
    private void ApplyColumnTreeFilter()
    {
        string needle = ColumnSearchText?.Trim() ?? string.Empty;
        if (string.Equals(needle, _lastAppliedColumnSearchText, StringComparison.Ordinal))
        {
            return;
        }

        _lastAppliedColumnSearchText = needle;
        Tables.Clear();
        SelectedAvailableColumn = null;
        SelectedAvailableTable = null;

        foreach (TableDefinition table in _sortedSchemaTables)
        {
            IEnumerable<ColumnDefinition> visibleColumns = table.Columns;

            if (!string.IsNullOrWhiteSpace(needle))
            {
                string tableDisplayName = SqlObjectDisplayName.Table(table.FullName);
                bool tableMatches = table.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || tableDisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(table.Comment) && table.Comment.Contains(needle, StringComparison.OrdinalIgnoreCase));

                visibleColumns = tableMatches
                    ? table.Columns
                    : table.Columns.Where(c => c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || (c.DataType?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (c.Comment?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (LookupSummary(c, _cachedForeignKeySummaries).Contains(needle, StringComparison.OrdinalIgnoreCase))
                        || (LookupSummary(c, _cachedIndexSummaries).Contains(needle, StringComparison.OrdinalIgnoreCase)));
            }

            List<ColumnDefinition> visibleList = [.. visibleColumns];
            if (visibleList.Count == 0)
            {
                continue;
            }

            Tables.Add(new TableItemViewModel(table, visibleList, _cachedForeignKeySummaries, _cachedIndexSummaries, _cachedUniqueIndexColumns)
            {
                IsExpanded = !string.IsNullOrWhiteSpace(needle)
            });
        }
    }

    /// <summary>
    /// Exécute le traitement BuildForeignKeySummaries.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyDictionary<string, string> BuildForeignKeySummaries()
    {
        return _schema.Relationships
            .GroupBy(r => $"{r.FromTable}.{r.FromColumn}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => string.Join(" | ", g.OrderByDescending(r => r.Confidence).Take(3).Select(r => $"→ {r.ToTable}.{r.ToColumn} ({r.Confidence:P0})")),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement BuildIndexSummaries.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlyDictionary<string, string> BuildIndexSummaries()
    {
        return _schema.Tables
            .SelectMany(t => t.Columns)
            .Select(c => new
            {
                Key = $"{c.TableName}.{c.Name}",
                Summary = string.Join(" | ", _schema.FindIndexesForColumn(c.TableName, c.Name)
                    .Take(4)
                    .Select(i => $"{(i.IsUnique ? "UNIQUE " : string.Empty)}{i.Name}"))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Summary))
            .ToDictionary(x => x.Key, x => x.Summary, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement BuildUniqueIndexColumnSet.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private IReadOnlySet<string> BuildUniqueIndexColumnSet()
    {
        return _schema.Tables
            .SelectMany(t => t.Columns)
            .Where(c => _schema.IsColumnUniqueIndexed(c.TableName, c.Name))
            .Select(c => $"{c.TableName}.{c.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute le traitement LookupSummary.
    /// </summary>
    /// <param name="column">Paramètre column.</param>
    /// <param name="summaries">Paramètre summaries.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string LookupSummary(ColumnDefinition column, IReadOnlyDictionary<string, string> summaries)
    {
        string key = $"{column.TableName}.{column.Name}";
        return summaries.TryGetValue(key, out string? summary) ? summary : string.Empty;
    }

    /// <summary>
    /// Exécute le traitement Relationship PropertyChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Relationship_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RelationshipItemViewModel.IsEnabled))
        {
            AutoGenerateSql();
        }
    }

    /// <summary>
    /// Exécute le traitement AddManualJoin.
    /// </summary>
    private void AddManualJoin()
    {
        Joins.Add(new JoinRowViewModel
        {
            JoinType = JoinType.Inner,
            FromTable = BaseTable,
            FromColumn = string.Empty,
            ToTable = string.Empty,
            ToColumn = string.Empty
        });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement AddJoinPair.
    /// </summary>
    /// <param name="join">Paramètre join.</param>
    private void AddJoinPair(JoinRowViewModel? join)
    {
        if (join is null)
        {
            return;
        }

        join.AdditionalPairs.Add(new JoinColumnPairRowViewModel { Enabled = true });
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement RemoveJoinPair.
    /// </summary>
    /// <param name="pair">Paramètre pair.</param>
    private void RemoveJoinPair(JoinColumnPairRowViewModel? pair)
    {
        if (pair is null)
        {
            return;
        }

        foreach (JoinRowViewModel join in Joins)
        {
            if (join.AdditionalPairs.Remove(pair))
            {
                GenerateSql();
                return;
            }
        }
    }

    /// <summary>
    /// Exécute le traitement GenerateSql.
    /// </summary>
    private void GenerateSql()
    {
        try
        {
            QueryDefinition query = BuildQueryDefinition();
            IReadOnlyList<string> validationErrors = _validator.Validate(query, _schema);
            SqlGenerationResult result = _generator.Generate(query, _schema, new SqlGeneratorOptions
            {
                Dialect = Dialect,
                QuoteIdentifiers = QuoteIdentifiers,
                AutoGroupSelectedColumnsWhenAggregating = AutoGroupSelectedColumns,
                EmitOptimizationComments = false
            });

            GeneratedSql = result.Sql;
            QueryPurpose = _purposeDescriber.Describe(query, _schema);
            PerformanceReport = _performanceAnalyzer.Analyze(query, _schema).ToString();
            string[] messages = [.. validationErrors.Concat(result.Warnings).Concat(_schema.Warnings).Distinct()];
            Warnings = messages.Length == 0 ? "Aucun avertissement." : string.Join(Environment.NewLine, messages);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            GeneratedSql = "-- Impossible de générer la requête. Corrige les champs signalés.";
            QueryPurpose = "Impossible d'expliquer le but tant que la requête contient des erreurs.";
            PerformanceReport = "Analyse performance indisponible tant que la requête contient des erreurs.";
            Warnings = ex.Message;
        }
    }

    /// <summary>
    /// Exécute le traitement BuildQueryDefinition.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    private QueryDefinition BuildQueryDefinition()
    {
        QueryDefinition query = new()
        {
            Name = BlankToNull(QueryName),
            Description = BlankToNull(QueryDescription),
            BaseTable = string.IsNullOrWhiteSpace(BaseTable) ? null : BaseTable,
            Distinct = Distinct,
            LimitRows = LimitRows is > 0 ? LimitRows : null
        };

        foreach (SelectColumnRowViewModel row in SelectedColumns)
        {
            query.SelectedColumns.Add(new ColumnReference { Table = row.Table, Column = row.Column, Alias = BlankToNull(row.Alias) });
        }

        foreach (FilterRowViewModel row in Filters)
        {
            if (row.FieldKind == QueryFieldKind.Column)
            {
                bool operatorIsExists = string.Equals(row.Operator, "EXISTS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.Operator, "NOT EXISTS", StringComparison.OrdinalIgnoreCase);

                if ((string.IsNullOrWhiteSpace(row.Table) || string.IsNullOrWhiteSpace(row.Column)) && !operatorIsExists)
                {
                    continue;
                }

                query.Filters.Add(new FilterCondition
                {
                    Column = operatorIsExists && string.IsNullOrWhiteSpace(row.Column) ? null : row.ToColumnReference(),
                    FieldKind = QueryFieldKind.Column,
                    Operator = row.Operator,
                    Value = row.Value,
                    SecondValue = row.SecondValue,
                    ValueKind = row.ValueKind,
                    Subquery = row.SavedSubquery?.Query,
                    SubqueryName = BlankToNull(row.SubqueryName),
                    Connector = row.Connector
                });
            }
            else
            {
                string? alias = BlankToNull(row.FieldAlias) ?? BlankToNull(row.Column);
                if (alias is null)
                {
                    continue;
                }

                query.Filters.Add(new FilterCondition
                {
                    FieldKind = row.FieldKind,
                    FieldAlias = alias,
                    Operator = row.Operator,
                    Value = row.Value,
                    SecondValue = row.SecondValue,
                    ValueKind = row.ValueKind,
                    Subquery = row.SavedSubquery?.Query,
                    SubqueryName = BlankToNull(row.SubqueryName),
                    Connector = row.Connector
                });
            }
        }

        foreach (GroupByRowViewModel row in GroupBy)
        {
            query.GroupBy.Add(row.ToColumnReference());
        }

        foreach (OrderByRowViewModel row in OrderBy)
        {
            if (row.FieldKind == QueryFieldKind.Column)
            {
                if (!string.IsNullOrWhiteSpace(row.Table) && !string.IsNullOrWhiteSpace(row.Column))
                {
                    query.OrderBy.Add(new OrderByItem { Column = row.ToColumnReference(), FieldKind = QueryFieldKind.Column, Direction = row.Direction });
                }
            }
            else
            {
                string? alias = BlankToNull(row.FieldAlias) ?? BlankToNull(row.Column);
                if (alias is not null)
                {
                    query.OrderBy.Add(new OrderByItem { FieldKind = row.FieldKind, FieldAlias = alias, Direction = row.Direction });
                }
            }
        }

        foreach (AggregateRowViewModel row in Aggregates)
        {
            query.Aggregates.Add(new AggregateSelection
            {
                Function = row.Function,
                Column = string.IsNullOrWhiteSpace(row.Table) || string.IsNullOrWhiteSpace(row.Column) ? null : row.ToColumnReference(),
                Alias = BlankToNull(row.Alias),
                Distinct = row.Distinct,
                ConditionColumn = string.IsNullOrWhiteSpace(row.ConditionTable) || string.IsNullOrWhiteSpace(row.ConditionColumn)
                    ? null
                    : new ColumnReference { Table = row.ConditionTable, Column = row.ConditionColumn },
                ConditionOperator = BlankToNull(row.ConditionOperator),
                ConditionValue = row.ConditionValue,
                ConditionSecondValue = row.ConditionSecondValue
            });
        }

        foreach (JoinRowViewModel? row in Joins.Where(j => !string.IsNullOrWhiteSpace(j.FromTable)
                     && !string.IsNullOrWhiteSpace(j.FromColumn)
                     && !string.IsNullOrWhiteSpace(j.ToTable)
                     && !string.IsNullOrWhiteSpace(j.ToColumn)))
        {
            JoinDefinition join = new()
            {
                FromTable = row.FromTable,
                FromColumn = row.FromColumn,
                ToTable = row.ToTable,
                ToColumn = row.ToColumn,
                JoinType = row.JoinType
            };

            foreach (JoinColumnPairRowViewModel? pair in row.AdditionalPairs.Where(p => !string.IsNullOrWhiteSpace(p.FromColumn)
                         && !string.IsNullOrWhiteSpace(p.ToColumn)))
            {
                join.AdditionalColumnPairs.Add(new JoinColumnPair
                {
                    FromColumn = pair.FromColumn,
                    ToColumn = pair.ToColumn,
                    Enabled = pair.Enabled
                });
            }

            query.Joins.Add(join);
        }

        foreach (CustomColumnRowViewModel row in CustomColumns)
        {
            query.CustomColumns.Add(new CustomColumnSelection
            {
                Alias = BlankToNull(row.Alias),
                RawExpression = BlankToNull(row.RawExpression),
                CaseColumn = string.IsNullOrWhiteSpace(row.CaseTable) || string.IsNullOrWhiteSpace(row.CaseColumn)
                    ? null
                    : new ColumnReference { Table = row.CaseTable, Column = row.CaseColumn },
                CaseOperator = BlankToNull(row.CaseOperator),
                CaseCompareValue = row.CaseCompareValue,
                CaseThenValue = row.CaseThenValue,
                CaseElseValue = row.CaseElseValue
            });
        }

        foreach (QueryParameterRowViewModel? row in Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            query.Parameters.Add(new QueryParameterDefinition
            {
                Name = row.Name.Trim(),
                Description = BlankToNull(row.Description),
                DefaultValue = BlankToNull(row.DefaultValue),
                Required = row.Required
            });
        }

        AddImplicitParametersFromFilters(query);

        foreach (RelationshipItemViewModel? disabled in Relationships.Where(r => !r.IsEnabled))
        {
            query.DisabledAutoJoinKeys.Add(disabled.Key);
            query.DisabledAutoJoinKeys.Add(disabled.ReverseKey);
        }

        return query;
    }

    /// <summary>
    /// Exécute le traitement AddImplicitParametersFromFilters.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    private static void AddImplicitParametersFromFilters(QueryDefinition query)
    {
        HashSet<string> existing = query.Parameters.Select(p => p.Placeholder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (FilterCondition filter in query.Filters)
        {
            foreach (string? raw in new[] { filter.Value, filter.SecondValue })
            {
                string? placeholder = NormalizeParameterPlaceholder(raw, filter.ValueKind);
                if (placeholder is null || !existing.Add(placeholder))
                {
                    continue;
                }

                query.Parameters.Add(new QueryParameterDefinition
                {
                    Name = placeholder,
                    Description = "Paramètre inféré depuis un filtre",
                    Required = true
                });
            }

            if (filter.Subquery is not null)
            {
                foreach (QueryParameterDefinition subParam in filter.Subquery.Parameters)
                {
                    if (existing.Add(subParam.Placeholder))
                    {
                        query.Parameters.Add(subParam with { Description = subParam.Description ?? $"Paramètre requis par la sous-requête {filter.SubqueryName}" });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Exécute le traitement NormalizeParameterPlaceholder.
    /// </summary>
    /// <param name="raw">Paramètre raw.</param>
    /// <param name="kind">Paramètre kind.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? NormalizeParameterPlaceholder(string? raw, FilterValueKind kind)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return kind == FilterValueKind.Parameter ? "?" : null;
        }

        string value = raw.Trim();
        if (kind == FilterValueKind.Parameter)
        {
            return value.StartsWith(':') || value.StartsWith('@') || value.StartsWith('?') ? value : ":" + value;
        }

        return value.StartsWith(':') || value.StartsWith('@') || value.StartsWith('?') ? value : null;
    }

    /// <summary>
    /// Exécute le traitement ClearQuery.
    /// </summary>
    private void ClearQuery()
    {
        _suppressAutoGenerate = true;
        try
        {
            SelectedColumns.Clear();
            Filters.Clear();
            GroupBy.Clear();
            OrderBy.Clear();
            Aggregates.Clear();
            Joins.Clear();
            CustomColumns.Clear();
            Parameters.Clear();
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GeneratedSql = "-- Requête vidée.";
        QueryPurpose = "Requête vidée.";
        PerformanceReport = "Requête vidée.";
        Warnings = "Aucun avertissement.";
    }

    /// <summary>
    /// Exécute le traitement SaveCurrentQuery.
    /// </summary>
    private void SaveCurrentQuery()
    {
        try
        {
            QueryDefinition query = BuildQueryDefinition();
            string name = string.IsNullOrWhiteSpace(QueryName) ? $"requete_{DateTime.Now:yyyyMMdd_HHmmss}" : QueryName.Trim();
            query.Name = name;
            query.Description = BlankToNull(QueryDescription);
            SavedQueryDefinition saved = new()
            {
                Name = name,
                Description = BlankToNull(QueryDescription),
                Query = query,
                LastGeneratedSql = GeneratedSql
            };
            string path = _savedQueryStore.Save(saved);
            ReloadSavedQueries();
            Status = $"Requête sauvegardée: {path}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Warnings = "Erreur de sauvegarde: " + ex.Message;
        }
    }

    /// <summary>
    /// Exécute le traitement ReloadSavedQueries.
    /// </summary>
    private void ReloadSavedQueries()
    {
        SavedQueries.Clear();
        foreach (SavedQueryDefinition saved in _savedQueryStore.LoadAll())
        {
            SavedQueries.Add(new SavedQueryItemViewModel(saved));
        }
    }

    /// <summary>
    /// Exécute le traitement LoadSelectedSavedQuery.
    /// </summary>
    private void LoadSelectedSavedQuery()
    {
        if (SelectedSavedQuery is null)
        {
            return;
        }

        LoadQueryDefinition(SelectedSavedQuery.Saved.Query, SelectedSavedQuery.Saved.Name, SelectedSavedQuery.Saved.Description);
    }

    /// <summary>
    /// Exécute le traitement AddSelectedSavedQueryAsSubqueryFilter.
    /// </summary>
    private void AddSelectedSavedQueryAsSubqueryFilter()
    {
        if (SelectedSavedQuery is null)
        {
            return;
        }

        FilterRowViewModel filter = new()
        {
            Operator = SelectedAvailableColumn is null ? "EXISTS" : "IN",
            ValueKind = FilterValueKind.Subquery,
            SubqueryName = SelectedSavedQuery.Name,
            SavedSubquery = SelectedSavedQuery.Saved
        };

        if (SelectedAvailableColumn is not null)
        {
            filter.Table = SelectedAvailableColumn.Table;
            filter.Column = SelectedAvailableColumn.Column;
        }

        Filters.Add(filter);
        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement LoadQueryDefinition.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="name">Paramètre name.</param>
    /// <param name="description">Paramètre description.</param>
    private void LoadQueryDefinition(QueryDefinition query, string? name, string? description)
    {
        _suppressAutoGenerate = true;
        try
        {
            QueryName = name ?? query.Name ?? "requete_chargee";
            QueryDescription = description ?? query.Description ?? string.Empty;
            BaseTable = query.BaseTable ?? string.Empty;
            Distinct = query.Distinct;
            LimitRows = query.LimitRows;
            SelectedColumns.Clear();
            Filters.Clear();
            GroupBy.Clear();
            OrderBy.Clear();
            Aggregates.Clear();
            Joins.Clear();
            CustomColumns.Clear();
            Parameters.Clear();

            foreach (ColumnReference c in query.SelectedColumns) SelectedColumns.Add(new SelectColumnRowViewModel { Table = c.Table, Column = c.Column, Alias = c.Alias ?? string.Empty });
            foreach (FilterCondition f in query.Filters)
            {
                Filters.Add(new FilterRowViewModel
                {
                    Table = f.Column?.Table ?? (f.FieldKind == QueryFieldKind.Aggregate ? "Agrégat" : f.FieldKind == QueryFieldKind.CustomColumn ? "Calculé" : string.Empty),
                    Column = f.Column?.Column ?? f.FieldAlias ?? string.Empty,
                    FieldKind = f.FieldKind,
                    FieldAlias = f.FieldAlias ?? string.Empty,
                    Operator = f.Operator,
                    Value = f.Value ?? string.Empty,
                    SecondValue = f.SecondValue ?? string.Empty,
                    ValueKind = f.ValueKind,
                    SubqueryName = f.SubqueryName ?? f.Subquery?.Name ?? string.Empty,
                    SavedSubquery = f.Subquery is null ? null : new SavedQueryDefinition { Name = f.SubqueryName ?? f.Subquery.Name ?? "subquery", Query = f.Subquery },
                    Connector = f.Connector
                });
            }
            foreach (ColumnReference g in query.GroupBy) GroupBy.Add(new GroupByRowViewModel { Table = g.Table, Column = g.Column });
            foreach (OrderByItem o in query.OrderBy) OrderBy.Add(new OrderByRowViewModel { Table = o.Column?.Table ?? (o.FieldKind == QueryFieldKind.Aggregate ? "Agrégat" : "Calculé"), Column = o.Column?.Column ?? o.FieldAlias ?? string.Empty, FieldKind = o.FieldKind, FieldAlias = o.FieldAlias ?? string.Empty, Direction = o.Direction });
            foreach (AggregateSelection a in query.Aggregates) Aggregates.Add(new AggregateRowViewModel { Table = a.Column?.Table ?? string.Empty, Column = a.Column?.Column ?? string.Empty, Function = a.Function, Alias = a.Alias ?? string.Empty, Distinct = a.Distinct, ConditionTable = a.ConditionColumn?.Table ?? string.Empty, ConditionColumn = a.ConditionColumn?.Column ?? string.Empty, ConditionOperator = a.ConditionOperator ?? "=", ConditionValue = a.ConditionValue ?? string.Empty, ConditionSecondValue = a.ConditionSecondValue ?? string.Empty });
            foreach (JoinDefinition j in query.Joins)
            {
                JoinRowViewModel joinVm = new() { FromTable = j.FromTable, FromColumn = j.FromColumn, ToTable = j.ToTable, ToColumn = j.ToColumn, JoinType = j.JoinType };
                foreach (JoinColumnPair pair in j.AdditionalColumnPairs)
                {
                    joinVm.AdditionalPairs.Add(new JoinColumnPairRowViewModel
                    {
                        FromColumn = pair.FromColumn,
                        ToColumn = pair.ToColumn,
                        Enabled = pair.Enabled
                    });
                }

                Joins.Add(joinVm);
            }
            foreach (CustomColumnSelection c in query.CustomColumns) CustomColumns.Add(new CustomColumnRowViewModel { Alias = c.Alias ?? string.Empty, RawExpression = c.RawExpression ?? string.Empty, CaseTable = c.CaseColumn?.Table ?? string.Empty, CaseColumn = c.CaseColumn?.Column ?? string.Empty, CaseOperator = c.CaseOperator ?? "=", CaseCompareValue = c.CaseCompareValue ?? string.Empty, CaseThenValue = c.CaseThenValue ?? string.Empty, CaseElseValue = c.CaseElseValue ?? string.Empty });
            foreach (QueryParameterDefinition p in query.Parameters) Parameters.Add(new QueryParameterRowViewModel { Name = p.Name, Description = p.Description ?? string.Empty, DefaultValue = p.DefaultValue ?? string.Empty, Required = p.Required });
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement WireAutoGenerate.
    /// </summary>
    /// <param name="collection">Paramètre collection.</param>
    private void WireAutoGenerate<T>(ObservableCollection<T> collection) where T : INotifyPropertyChanged
    {
        collection.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (INotifyPropertyChanged item in e.OldItems)
                {
                    item.PropertyChanged -= Row_PropertyChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (INotifyPropertyChanged item in e.NewItems)
                {
                    item.PropertyChanged += Row_PropertyChanged;
                }
            }

            AutoGenerateSql();
        };
    }

    /// <summary>
    /// Exécute le traitement Row PropertyChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e) => AutoGenerateSql();

    /// <summary>
    /// Exécute le traitement AutoGenerateSql.
    /// </summary>
    private void AutoGenerateSql()
    {
        if (_suppressAutoGenerate)
        {
            return;
        }

        GenerateSql();
    }

    /// <summary>
    /// Exécute le traitement BlankToNull.
    /// </summary>
    /// <param name="value">Paramètre value.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Exécute le traitement RemoveFromCollection.
    /// </summary>
    /// <param name="collection">Paramètre collection.</param>
    /// <param name="obj">Paramètre obj.</param>
    private static void RemoveFromCollection<T>(ObservableCollection<T> collection, object? obj)
    {
        if (obj is T item)
        {
            collection.Remove(item);
        }
    }
}
