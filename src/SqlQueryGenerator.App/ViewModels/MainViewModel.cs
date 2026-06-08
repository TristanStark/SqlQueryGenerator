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
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SqlQueryGenerator.App.Services;

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
    /// Reverse-engineers pasted SELECT SQL into the visual query model when possible.
    /// </summary>
    /// <value>Reusable best-effort SQL reverse parser.</value>
    private readonly ReverseSqlImportService _reverseImportService = new();
    private readonly SqlRewriteSuggestionService _rewriteSuggestionService = new();
    private readonly SqlComparisonService _sqlComparisonService = new();
    private readonly QueryBuilderHistoryService _history = new();
    private readonly SchemaAuxiliaryTableDetector _auxiliaryTableDetector = new();
    private readonly DdlCommandExportService _ddlCommandExportService = new();
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
    /// Stores raw SELECT SQL used for raw presets and reverse engineering.
    /// </summary>
    /// <value>Raw SQL text pasted or loaded by the user.</value>
    private string _rawSqlText = string.Empty;
    private string _sqlComparisonSummary = "Aucune comparaison SQL active. Utilise Réécrire SQL ou Charger dans le constructeur.";
    private string _sqlComparisonSourceLabel = "SQL source";
    private string _sqlComparisonTargetLabel = "SQL résultat";
    private string _sqlComparisonBaselineSql = string.Empty;
    private string _lastRewrittenSql = string.Empty;
    private bool _sqlComparisonTracksGeneratedSql;
    private bool _ignoreSqlComparisonWhitespaceChanges;
    private bool _ignoreSqlComparisonCaseChanges;
    private string _reverseSqlCoverageReport = "Le rapport de couverture Reverse SQL apparaîtra ici.";
    private string _reverseSqlDiagnosticsReport = "Les diagnostics Reverse SQL apparaîtront ici.";
    private string _reverseSqlConfidenceSummary = "Confiance Reverse SQL : aucune analyse.";
    private SourceSqlDialect _sourceSqlDialect = SourceSqlDialect.OracleLegacy;
    private int _rawSqlSelectionStart;
    private int _rawSqlSelectionLength;
    private string _ddlSchemaName = "main";
    private string _materializedViewName = "mv_nouvelle_requete";
    private string _createMaterializedViewSql = string.Empty;
    private string _ddlExportSql = string.Empty;
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
    private bool _hideAuxiliaryTables;
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

    private IReadOnlyDictionary<string, IReadOnlyList<string>> _columnNamesByTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tableAliases = new(StringComparer.OrdinalIgnoreCase);
    private DdlExportDialect _ddlExportDialect = DdlExportDialect.SQLite;

    /// <summary>
    /// Maximum number of column rows rendered by a search to keep WPF memory usage bounded on large schemas.
    /// </summary>
    private const int MaxVisibleColumnSearchResults = 650;

    /// <summary>
    /// Debounces left-tree searches so filtering is not executed for every keystroke.
    /// </summary>
    private readonly DispatcherTimer _columnSearchDebounceTimer;

    /// <summary>
    /// Reusable schema explorer index built once per schema reload.
    /// </summary>
    private SchemaExplorerIndex _schemaExplorerIndex = SchemaExplorerIndex.Empty;

    private bool _isColumnSearchTruncated;
    private int _importDetectedBackupCandidateCount;
    private int _importExcludedBackupTableCount;
    private int _importKeptBackupCandidateCount;
    private bool _isRestoringHistory;

    /// <summary>
    /// Initialise une nouvelle instance de MainViewModel.
    /// </summary>
    public MainViewModel()
    {
        _columnSearchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(275)
        };
        _columnSearchDebounceTimer.Tick += ColumnSearchDebounceTimer_Tick;

        GenerateCommand = new RelayCommand(GenerateSql);
        UndoCommand = new RelayCommand(UndoHistory, () => _history.CanUndo);
        RedoCommand = new RelayCommand(RedoHistory, () => _history.CanRedo);
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
        AddJoinFromRelationshipCommand = new RelayCommand(_ => AddSelectedRelationshipAsJoin(), _ => SelectedRelationship is not null && !SelectedRelationship.IsUsed);
        AddRelationshipAsJoinCommand = new RelayCommand(obj => AddRelationshipAsJoin(obj as RelationshipItemViewModel), obj => obj is RelationshipItemViewModel relationship && !relationship.IsUsed);
        AddManualJoinCommand = new RelayCommand(AddManualJoin);
        AddJoinPairCommand = new RelayCommand(obj => AddJoinPair(obj as JoinRowViewModel));
        RemoveJoinPairCommand = new RelayCommand(obj => RemoveJoinPair(obj as JoinColumnPairRowViewModel));
        AddEmptyCustomColumnCommand = new RelayCommand(() => CustomColumns.Add(new CustomColumnRowViewModel { Alias = BuildDefaultCustomAlias() }));
        AddAggregateFilterCommand = new RelayCommand(obj => AddAggregateToFilter(obj as AggregateRowViewModel));
        AddAggregateOrderByCommand = new RelayCommand(obj => AddAggregateToOrderBy(obj as AggregateRowViewModel));
        AddCustomFilterCommand = new RelayCommand(obj => AddCustomColumnToFilter(obj as CustomColumnRowViewModel));
        AddCustomOrderByCommand = new RelayCommand(obj => AddCustomColumnToOrderBy(obj as CustomColumnRowViewModel));
        AddParameterCommand = new RelayCommand(() => Parameters.Add(new QueryParameterRowViewModel
        {
            Name = $"param_{Parameters.Count + 1}",
            DeclaredType = Dialect == SqlDialect.CognosAnalytics ? "string" : string.Empty,
            UseCognosPrompt = Dialect == SqlDialect.CognosAnalytics,
            Required = true
        }));
        RemoveParameterCommand = new RelayCommand(obj => RemoveFromCollection(Parameters, obj));
        SaveCurrentQueryCommand = new RelayCommand(SaveCurrentQuery);
        SaveRawSqlPresetCommand = new RelayCommand(SaveRawSqlPreset);
        LoadSelectedRawSqlCommand = new RelayCommand(_ => LoadSelectedRawSqlPreset(), _ => SelectedSavedQuery is not null);
        ReverseEngineerRawSqlCommand = new RelayCommand(ReverseEngineerRawSql);
        RewriteRawSqlCommand = new RelayCommand(RewriteRawSql);
        UseGeneratedSqlAsRawInputCommand = new RelayCommand(UseGeneratedSqlAsRawInput);
        CompareRawVsRewrittenSqlCommand = new RelayCommand(CompareRawVsRewrittenSql, () => !string.IsNullOrWhiteSpace(_lastRewrittenSql));
        CompareRawVsBuilderSqlCommand = new RelayCommand(CompareRawVsBuilderSql);
        CompareRewrittenVsBuilderSqlCommand = new RelayCommand(CompareRewrittenVsBuilderSql, () => !string.IsNullOrWhiteSpace(_lastRewrittenSql));
        GenerateDdlExportCommand = new RelayCommand(GenerateDdlExportSql);
        GenerateCreateMaterializedViewCommand = new RelayCommand(GenerateCreateMaterializedViewSql);
        OpenHelpCommand = new RelayCommand(OpenHelpDocumentation);
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
        ResetHistoryToCurrentState();
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
    /// Stores the aligned SQL comparison rows shown in the reverse/rewrite workflow.
    /// </summary>
    /// <value>Line-by-line comparison between source and result SQL.</value>
    public ObservableCollection<SqlComparisonLine> SqlComparisonLines { get; } = [];
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
    public Array SourceSqlDialects => Enum.GetValues(typeof(SourceSqlDialect));
    public Array DdlExportDialects => Enum.GetValues(typeof(DdlExportDialect));
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
    /// Stocke la valeur interne UndoCommand.
    /// </summary>
    /// <value>Valeur de UndoCommand.</value>
    public RelayCommand UndoCommand { get; }
    /// <summary>
    /// Stocke la valeur interne RedoCommand.
    /// </summary>
    /// <value>Valeur de RedoCommand.</value>
    public RelayCommand RedoCommand { get; }
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
    /// Adds one specific probable relationship into the current query joins.
    /// </summary>
    /// <value>Per-row join-add command used by the probable-joins list.</value>
    public RelayCommand AddRelationshipAsJoinCommand { get; }
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
    /// Saves the raw SQL text as a read-only SQL preset.
    /// </summary>
    /// <value>Command bound to the raw SQL save button.</value>
    public RelayCommand SaveRawSqlPresetCommand { get; }
    /// <summary>
    /// Loads the selected raw SQL preset into the raw SQL editor.
    /// </summary>
    /// <value>Command bound to the raw SQL load button.</value>
    public RelayCommand LoadSelectedRawSqlCommand { get; }
    /// <summary>
    /// Reverse-engineers the raw SQL editor content into the visual builder.
    /// </summary>
    /// <value>Command bound to the reverse SQL button.</value>
    public RelayCommand ReverseEngineerRawSqlCommand { get; }
    /// <summary>
    /// Rewrites the raw SQL editor content into a cleaner SQL statement without loading it into the builder.
    /// </summary>
    /// <value>Command bound to the SQL rewrite button.</value>
    public RelayCommand RewriteRawSqlCommand { get; }
    /// <summary>
    /// Copies the currently generated SQL into the raw SQL editor.
    /// </summary>
    /// <value>Command bound to the generated-to-raw helper button.</value>
    public RelayCommand UseGeneratedSqlAsRawInputCommand { get; }
    /// <summary>
    /// Compares the raw SQL editor content against the latest rewritten SQL.
    /// </summary>
    public RelayCommand CompareRawVsRewrittenSqlCommand { get; }
    /// <summary>
    /// Compares the raw SQL editor content against the current builder-generated SQL.
    /// </summary>
    public RelayCommand CompareRawVsBuilderSqlCommand { get; }
    /// <summary>
    /// Compares the latest rewritten SQL against the current builder-generated SQL.
    /// </summary>
    public RelayCommand CompareRewrittenVsBuilderSqlCommand { get; }
    /// <summary>
    /// Builds and copies a DDL extraction helper command for Oracle or SQLite.
    /// </summary>
    /// <value>Command bound to the DDL helper button.</value>
    public RelayCommand GenerateDdlExportCommand { get; }

    /// <summary>
    /// Generates a CREATE MATERIALIZED VIEW statement from the current visual query.
    /// </summary>
    public RelayCommand GenerateCreateMaterializedViewCommand { get; }

    /// <summary>
    /// Opens the user documentation.
    /// </summary>
    /// <value>Command bound to the Help button.</value>
    public RelayCommand OpenHelpCommand { get; }
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
    public string ReverseSqlCoverageReport { get => _reverseSqlCoverageReport; set => SetProperty(ref _reverseSqlCoverageReport, value); }
    public string ReverseSqlDiagnosticsReport { get => _reverseSqlDiagnosticsReport; set => SetProperty(ref _reverseSqlDiagnosticsReport, value); }
    public string ReverseSqlConfidenceSummary { get => _reverseSqlConfidenceSummary; set => SetProperty(ref _reverseSqlConfidenceSummary, value); }
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
    public string SqlComparisonSummary { get => _sqlComparisonSummary; set => SetProperty(ref _sqlComparisonSummary, value); }
    public string SqlComparisonSourceLabel { get => _sqlComparisonSourceLabel; set => SetProperty(ref _sqlComparisonSourceLabel, value); }
    public string SqlComparisonTargetLabel { get => _sqlComparisonTargetLabel; set => SetProperty(ref _sqlComparisonTargetLabel, value); }
    public bool IgnoreSqlComparisonWhitespaceChanges
    {
        get => _ignoreSqlComparisonWhitespaceChanges;
        set
        {
            if (SetProperty(ref _ignoreSqlComparisonWhitespaceChanges, value))
            {
                RefreshCurrentSqlComparison();
            }
        }
    }

    public bool IgnoreSqlComparisonCaseChanges
    {
        get => _ignoreSqlComparisonCaseChanges;
        set
        {
            if (SetProperty(ref _ignoreSqlComparisonCaseChanges, value))
            {
                RefreshCurrentSqlComparison();
            }
        }
    }
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
    /// Gets or sets the raw SELECT SQL used by raw presets and reverse engineering.
    /// </summary>
    /// <value>Raw SQL editor content.</value>
    public string RawSqlText { get => _rawSqlText; set => SetProperty(ref _rawSqlText, value); }
    public SourceSqlDialect SourceSqlDialect { get => _sourceSqlDialect; set => SetProperty(ref _sourceSqlDialect, value); }
    public int RawSqlSelectionStart { get => _rawSqlSelectionStart; set => SetProperty(ref _rawSqlSelectionStart, Math.Max(0, value)); }
    public int RawSqlSelectionLength { get => _rawSqlSelectionLength; set => SetProperty(ref _rawSqlSelectionLength, Math.Max(0, value)); }
    /// <summary>
    /// Gets or sets the schema owner or database name used for DDL helper generation.
    /// </summary>
    /// <value>Oracle schema owner or SQLite attached database name.</value>
    public string DdlSchemaName { get => _ddlSchemaName; set => SetProperty(ref _ddlSchemaName, value); }
    /// <summary>
    /// Gets or sets the source engine targeted by the DDL helper command.
    /// </summary>
    /// <value>DDL export dialect.</value>
    public DdlExportDialect DdlExportDialect
    {
        get => _ddlExportDialect;
        set => SetProperty(ref _ddlExportDialect, value);
    }
    /// <summary>
    /// Gets or sets the generated DDL helper SQL.
    /// </summary>
    /// <value>Read-only helper SQL shown to the user and copied on demand.</value>
    public string DdlExportSql { get => _ddlExportSql; set => SetProperty(ref _ddlExportSql, value); }

    /// <summary>
    /// Gets or sets the materialized view name generated from the current query.
    /// </summary>
    public string MaterializedViewName
    {
        get => _materializedViewName;
        set => SetProperty(ref _materializedViewName, value);
    }

    /// <summary>
    /// Gets or sets the CREATE MATERIALIZED VIEW SQL generated from the current query.
    /// </summary>
    public string CreateMaterializedViewSql
    {
        get => _createMaterializedViewSql;
        set => SetProperty(ref _createMaterializedViewSql, value);
    }

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
                LoadSelectedRawSqlCommand.RaiseCanExecuteChanged();
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
                ScheduleColumnTreeFilter();
                ClearColumnSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HideAuxiliaryTables
    {
        get => _hideAuxiliaryTables;
        set
        {
            if (SetProperty(ref _hideAuxiliaryTables, value))
            {
                if (_schema.Tables.Count > 0)
                {
                    ReloadSchemaViewModels();
                    OnPropertyChanged(nameof(SchemaSummary));
                    OnPropertyChanged(nameof(SchemaFilterSummary));
                }
            }
        }
    }
    public string SchemaFilterSummary => _schema.Tables.Count == 0
        ? "Charge un schema pour activer le filtrage."
        : BuildSchemaFilterSummary();

    public string SchemaSummary => _schema.Tables.Count == 0
        ? "Aucun schéma chargé"
        : $"{_schema.PhysicalTables.Count()} tables · {_schema.Views.Count()} vues · {_schema.MaterializedViews.Count()} vues matérialisées · {_schema.Tables.Sum(t => t.Columns.Count)} colonnes · {_schema.Indexes.Count} index · {_schema.Relationships.Count} relations probables";

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
    /// Loads a raw SQL SELECT file into the raw SQL editor for saving or reverse engineering.
    /// </summary>
    /// <param name="filePath">Path to a SQL or text file containing one SELECT statement.</param>
    public void LoadRawSqlFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Status = "Fichier SQL brut introuvable.";
            return;
        }

        FileInfo info = new(filePath);
        if (info.Length > 5_000_000)
        {
            MessageBox.Show("Le fichier SQL brut dépasse 5 Mo. Réduis le fichier avant import.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RawSqlText = File.ReadAllText(filePath);
        QueryName = Path.GetFileNameWithoutExtension(filePath);
        ClearStoredRewrittenSql();
        ClearSqlComparison("SQL brut chargé. Utilise Réécrire SQL ou Charger dans le constructeur pour comparer les versions.");
        Status = $"SQL brut chargé: {filePath}";
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
    /// Parses a schema only to detect backup candidates before the final import.
    /// </summary>
    /// <param name="text">DDL text to inspect.</param>
    /// <param name="candidates">Detected review candidates.</param>
    /// <returns><c>true</c> when preview parsing succeeded.</returns>
    public bool TryPreviewBackupTableCandidates(string text, out IReadOnlyList<BackupTableCandidate> candidates)
    {
        candidates = Array.Empty<BackupTableCandidate>();

        try
        {
            DatabaseSchema previewSchema = _parser.Parse(text);
            candidates = _auxiliaryTableDetector.DetectBackupCandidates(previewSchema);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            Status = "Erreur de chargement du schÃ©ma.";
            Warnings = ex.Message;
            return false;
        }
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
            OnPropertyChanged(nameof(SchemaFilterSummary));
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
    public void LoadSchemaFromText(string text, string? sourceName = null, IReadOnlyCollection<string>? excludedTables = null)
    {
        try
        {
            DatabaseSchema parsedSchema = _parser.Parse(text);
            SchemaImportFilterResult importResult = _auxiliaryTableDetector.ApplyImportSelection(parsedSchema, excludedTables);
            _schema = importResult.Schema;
            _importDetectedBackupCandidateCount = importResult.DetectedCandidates.Count;
            _importExcludedBackupTableCount = importResult.ExcludedCandidates.Count;
            _importKeptBackupCandidateCount = importResult.KeptCandidates.Count;
            LoadedFile = sourceName ?? "Collé manuellement";
            ReloadSchemaViewModels();
            OnPropertyChanged(nameof(SchemaSummary));
            OnPropertyChanged(nameof(SchemaFilterSummary));
            Status = $"Schéma chargé: {_schema.PhysicalTables.Count()} tables, {_schema.Views.Count()} vues, {_schema.Tables.Sum(t => t.Columns.Count)} colonnes, {_schema.Indexes.Count} index, {_schema.Relationships.Count} relations probables.";
            if (_importDetectedBackupCandidateCount > 0)
            {
                Status = $"Schéma chargé: {_schema.PhysicalTables.Count()} tables, {_schema.Views.Count()} vues, {_schema.MaterializedViews.Count()} vues matérialisées, {_schema.Tables.Sum(t => t.Columns.Count)} colonnes, {_schema.Indexes.Count} index, {_schema.Relationships.Count} relations probables.";
            }

            Warnings = string.Join(Environment.NewLine, _schema.Warnings);
            if (string.IsNullOrWhiteSpace(BaseTable) && TableNames.Count > 0)
            {
                BaseTable = TableNames[0];
            }
            GenerateSql();
            ResetHistoryToCurrentState();
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
        ColumnItemViewModel[] checkedColumns = AllColumns
            .Where(c => c.IsBulkSelected)
            .ToArray();

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
        foreach (ColumnItemViewModel? column in AllColumns)
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

        AddRelationshipAsJoin(SelectedRelationship);
    }

    /// <summary>
    /// Refreshes the probable-join list to show which candidates are already used.
    /// </summary>
    private void RefreshRelationshipUsage()
    {
        foreach (RelationshipItemViewModel relationship in Relationships)
        {
            relationship.IsUsed = IsRelationshipUsed(relationship);
        }

        AddJoinFromRelationshipCommand.RaiseCanExecuteChanged();
        AddRelationshipAsJoinCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Indicates whether one probable relationship is already present in the current joins.
    /// </summary>
    /// <param name="relationship">Candidate relationship.</param>
    /// <returns><c>true</c> when a matching join exists.</returns>
    private bool IsRelationshipUsed(RelationshipItemViewModel relationship)
    {
        return Joins.Any(join =>
            SameJoinEndpoint(join.FromTable, relationship.FromTable)
            && SameJoinEndpoint(join.FromColumn, relationship.FromColumn)
            && SameJoinEndpoint(join.ToTable, relationship.ToTable)
            && SameJoinEndpoint(join.ToColumn, relationship.ToColumn))
            || Joins.Any(join =>
                SameJoinEndpoint(join.FromTable, relationship.ToTable)
                && SameJoinEndpoint(join.FromColumn, relationship.ToColumn)
                && SameJoinEndpoint(join.ToTable, relationship.FromTable)
                && SameJoinEndpoint(join.ToColumn, relationship.FromColumn));
    }

    private static bool SameJoinEndpoint(string left, string right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a probable relationship to the current query joins if it is not already present.
    /// </summary>
    /// <param name="relationship">Probable relationship selected from the candidate list.</param>
    private void AddRelationshipAsJoin(RelationshipItemViewModel? relationship)
    {
        if (relationship is null || IsRelationshipUsed(relationship))
        {
            return;
        }

        Joins.Add(new JoinRowViewModel
        {
            FromTable = relationship.FromTable,
            FromColumn = relationship.FromColumn,
            ToTable = relationship.ToTable,
            ToColumn = relationship.ToColumn,
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
            RefreshRelationshipUsage();
            return;
        }

        foreach (JoinRowViewModel join in e.NewItems)
        {
            join.ColumnNamesProvider = GetColumnNamesForTable;
        }

        RefreshRelationshipUsage();
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
    /// Rebuilds the schema explorer index and refreshes the WPF-bound collections.
    /// </summary>
    private void ReloadSchemaViewModels()
    {
        foreach (RelationshipItemViewModel relationship in Relationships)
        {
            relationship.PropertyChanged -= Relationship_PropertyChanged;
        }

        Tables.Clear();
        AllColumns.Clear();
        Relationships.Clear();
        RelationshipGroups.Clear();
        TableNames.Clear();

        _cachedForeignKeySummaries = BuildForeignKeySummaries();
        _cachedIndexSummaries = BuildIndexSummaries();
        _cachedUniqueIndexColumns = BuildUniqueIndexColumnSet();

        _schemaExplorerIndex = SchemaExplorerIndex.Build(
            _schema,
            _auxiliaryTableDetector,
            BuildPinnedSchemaTableNames(),
            HideAuxiliaryTables,
            _cachedForeignKeySummaries,
            _cachedIndexSummaries,
            _cachedUniqueIndexColumns);

        ColumnNamesByTable = _schemaExplorerIndex.ColumnNamesByTable;

        foreach (ColumnItemViewModel column in _schemaExplorerIndex.AllColumns)
        {
            AllColumns.Add(column);
        }

        foreach (string tableName in _schemaExplorerIndex.TableNames)
        {
            TableNames.Add(tableName);
        }

        foreach (RelationshipItemViewModel relationship in _schemaExplorerIndex.Relationships)
        {
            relationship.PropertyChanged += Relationship_PropertyChanged;
            Relationships.Add(relationship);
        }

        foreach (RelationshipGroupViewModel group in _schemaExplorerIndex.RelationshipGroups)
        {
            RelationshipGroups.Add(group);
        }

        _lastAppliedColumnSearchText = "__schema_reloaded__";
        _isColumnSearchTruncated = false;

        ApplyColumnTreeFilter();
        RefreshRelationshipUsage();
        OnPropertyChanged(nameof(SchemaFilterSummary));
    }

    private HashSet<string> BuildPinnedSchemaTableNames()
    {
        HashSet<string> pinned = new(StringComparer.OrdinalIgnoreCase);

        void Add(string? tableName)
        {
            if (!string.IsNullOrWhiteSpace(tableName)
                && !string.Equals(tableName, "Agrégat", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tableName, "Calculé", StringComparison.OrdinalIgnoreCase))
            {
                pinned.Add(tableName);
            }
        }

        Add(BaseTable);
        Add(SelectedAvailableTable?.Name);
        Add(SelectedAvailableColumn?.Table);

        foreach (SelectColumnRowViewModel row in SelectedColumns) Add(row.Table);
        foreach (FilterRowViewModel row in Filters) Add(row.Table);
        foreach (GroupByRowViewModel row in GroupBy) Add(row.Table);
        foreach (OrderByRowViewModel row in OrderBy) Add(row.Table);
        foreach (AggregateRowViewModel row in Aggregates)
        {
            Add(row.Table);
            Add(row.ConditionTable);
        }

        foreach (JoinRowViewModel row in Joins)
        {
            Add(row.FromTable);
            Add(row.ToTable);
        }

        foreach (CustomColumnRowViewModel row in CustomColumns) Add(row.CaseTable);

        return pinned;
    }

    private bool ShouldHideAuxiliaryTable(TableDefinition table, IReadOnlySet<string> pinnedTables)
    {
        return HideAuxiliaryTables
            && !table.IsView
            && !pinnedTables.Contains(table.FullName)
            && !pinnedTables.Contains(table.Name)
            && _auxiliaryTableDetector.IsLikelyAuxiliaryTable(table.FullName);
    }


    private string BuildSchemaFilterSummary()
    {
        List<string> parts = [];

        if (_importDetectedBackupCandidateCount > 0)
        {
            parts.Add($"{_importDetectedBackupCandidateCount} candidats backup detectes a l'import");
            if (_importExcludedBackupTableCount > 0)
            {
                parts.Add($"{_importExcludedBackupTableCount} exclus");
            }

            if (_importKeptBackupCandidateCount > 0)
            {
                parts.Add($"{_importKeptBackupCandidateCount} conserves");
            }
        }

        if (_schemaExplorerIndex.DetectedAuxiliaryTableCount == 0)
        {
            parts.Add("aucune table auxiliaire restante detectee");
        }
        else if (HideAuxiliaryTables)
        {
            parts.Add($"{_schemaExplorerIndex.HiddenAuxiliaryTableCount} tables auxiliaires restantes masquees");
        }
        else
        {
            parts.Add($"{_schemaExplorerIndex.DetectedAuxiliaryTableCount} tables auxiliaires restantes visibles");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Schedules a debounced refresh of the left schema tree search results.
    /// </summary>
    private void ScheduleColumnTreeFilter()
    {
        _columnSearchDebounceTimer.Stop();
        if (string.IsNullOrWhiteSpace(ColumnSearchText))
        {
            ApplyColumnTreeFilter();
            return;
        }

        _columnSearchDebounceTimer.Start();
    }

    /// <summary>
    /// Applies the pending column search after the debounce interval has elapsed.
    /// </summary>
    /// <param name="sender">Timer that triggered the refresh.</param>
    /// <param name="e">Timer event arguments.</param>
    private void ColumnSearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _columnSearchDebounceTimer.Stop();
        ApplyColumnTreeFilter();
    }

    /// <summary>
    /// Applies the current left-tree filter using the reusable schema explorer index.
    /// </summary>
    private void ApplyColumnTreeFilter()
    {
        string needle = string.IsNullOrWhiteSpace(ColumnSearchText)
            ? string.Empty
            : ColumnSearchText.Trim().ToUpperInvariant();

        if (string.Equals(needle, _lastAppliedColumnSearchText, StringComparison.Ordinal))
        {
            return;
        }

        _lastAppliedColumnSearchText = needle;
        SelectedAvailableColumn = null;
        SelectedAvailableTable = null;

        SchemaExplorerSearchResult result = _schemaExplorerIndex.Search(
            ColumnSearchText,
            MaxVisibleColumnSearchResults);

        _isColumnSearchTruncated = result.IsTruncated;

        Tables.Clear();
        foreach (TableItemViewModel table in result.Tables)
        {
            Tables.Add(table);
        }

        if (result.IsEmptySearch)
        {
            Status = BuildSchemaLoadedStatus();
            return;
        }

        string suffix = result.IsTruncated
            ? $" Recherche tronquée aux {MaxVisibleColumnSearchResults} premières colonnes pour préserver la RAM. Affine le filtre."
            : string.Empty;

        Status = $"Recherche '{ColumnSearchText}': {result.MatchedColumnCount:N0} colonne(s) correspondante(s), {result.DisplayedTableCount:N0} table(s) affichée(s).{suffix}";
    }

    /// <summary>
    /// Builds a compact status message for a fully loaded schema.
    /// </summary>
    /// <returns>Schema summary for the status bar.</returns>
    private string BuildSchemaLoadedStatus()
    {
        return $"Schéma chargé: {_schema.Tables.Count} objets, {_schema.Tables.Sum(t => t.Columns.Count)} colonnes, {_schema.Relationships.Count} relations probables.";
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
        TrackHistoryAfterMutation();
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

            RefreshRelationshipUsage();
            GeneratedSql = result.Sql;
            RefreshTrackedSqlComparison();
            QueryPurpose = _purposeDescriber.Describe(query, _schema);
            PerformanceReport = _performanceAnalyzer.Analyze(query, _schema).ToString();
            string[] messages = validationErrors.Concat(result.Warnings).Concat(_schema.Warnings).Distinct().ToArray();
            Warnings = messages.Length == 0 ? "Aucun avertissement." : string.Join(Environment.NewLine, messages);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            GeneratedSql = "-- Impossible de générer la requête. Corrige les champs signalés.";
            QueryPurpose = "Impossible d'expliquer le but tant que la requête contient des erreurs.";
            PerformanceReport = "Analyse performance indisponible tant que la requête contient des erreurs.";
            SuspendTrackedSqlComparison("Comparaison SQL indisponible tant que la génération du constructeur échoue.");
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

        foreach ((string table, string alias) in _tableAliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            query.TableAliases.Add(new TableAliasDefinition
            {
                Table = table,
                Alias = alias
            });
        }

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
                    Subquery = row.SavedSubquery?.Kind == SavedQueryKind.Builder ? row.SavedSubquery.Query : null,
                    RawSubquerySql = row.SavedSubquery?.Kind == SavedQueryKind.RawSql ? row.SavedSubquery.RawSql : null,
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
                    Subquery = row.SavedSubquery?.Kind == SavedQueryKind.Builder ? row.SavedSubquery.Query : null,
                    RawSubquerySql = row.SavedSubquery?.Kind == SavedQueryKind.RawSql ? row.SavedSubquery.RawSql : null,
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
            QueryParameterSourceKind sourceKind = row.UseCognosPrompt || Dialect == SqlDialect.CognosAnalytics
                ? QueryParameterSourceKind.CognosPrompt
                : QueryParameterSourceKind.Standard;
            query.Parameters.Add(new QueryParameterDefinition
            {
                Name = row.Name.Trim(),
                Description = BlankToNull(row.Description),
                DefaultValue = BlankToNull(row.DefaultValue),
                DeclaredType = BlankToNull(row.DeclaredType),
                SourceKind = sourceKind,
                Required = row.Required
            });
        }

        AddImplicitParametersFromFilters(query, Dialect);

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
    private static void AddImplicitParametersFromFilters(QueryDefinition query, SqlDialect dialect)
    {
        HashSet<string> existing = query.Parameters
            .Select(GetParameterIdentity)
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (FilterCondition filter in query.Filters)
        {
            foreach (string? raw in new[] { filter.Value, filter.SecondValue })
            {
                string? placeholder = NormalizeParameterPlaceholder(raw, filter.ValueKind, dialect);
                if (string.IsNullOrWhiteSpace(placeholder) || !existing.Add(placeholder))
                {
                    continue;
                }

                query.Parameters.Add(new QueryParameterDefinition
                {
                    Name = placeholder,
                    Description = "Paramètre inféré depuis un filtre",
                    DeclaredType = BuildImplicitParameterType(raw),
                    RawExpression = BuildImplicitParameterRawExpression(raw),
                    SourceKind = BuildImplicitParameterSourceKind(raw, dialect),
                    Required = true
                });
            }

            if (filter.Subquery is not null)
            {
                foreach (QueryParameterDefinition subParam in filter.Subquery.Parameters)
                {
                    if (existing.Add(GetParameterIdentity(subParam)))
                    {
                        query.Parameters.Add(subParam with { Description = subParam.Description ?? $"Paramètre requis par la sous-requête {filter.SubqueryName}" });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.RawSubquerySql))
            {
                foreach (QueryParameterDefinition subParam in SqlSelectReverseParser.ExtractParameters(filter.RawSubquerySql))
                {
                    if (existing.Add(GetParameterIdentity(subParam)))
                    {
                        query.Parameters.Add(subParam with { Description = subParam.Description ?? $"Paramètre requis par la sous-requête SQL brute {filter.SubqueryName}" });
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
    private static string? NormalizeParameterPlaceholder(string? raw, FilterValueKind kind, SqlDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return kind == FilterValueKind.Parameter && dialect != SqlDialect.CognosAnalytics ? "?" : null;
        }

        string value = raw.Trim();
        if (CognosPromptSyntax.TryExtractPromptExpression(value, out string promptExpression, out _)
            && CognosPromptSyntax.TryParsePromptExpression(promptExpression, out string promptName, out _))
        {
            return promptName;
        }

        if (kind == FilterValueKind.Parameter)
        {
            if (dialect == SqlDialect.CognosAnalytics)
            {
                return value;
            }

            return TrimParameterPrefix(value);
        }

        return value.StartsWith(':') || value.StartsWith('@') || value.StartsWith('?') || value.StartsWith('&') ? TrimParameterPrefix(value) : null;
    }

    private static string? BuildImplicitParameterType(string? raw)
    {
        string trimmed = raw?.Trim() ?? string.Empty;
        if (CognosPromptSyntax.TryExtractPromptExpression(trimmed, out string promptExpression, out _)
            && CognosPromptSyntax.TryParsePromptExpression(promptExpression, out _, out string promptType))
        {
            return BlankToNull(promptType);
        }

        return null;
    }

    private static string? BuildImplicitParameterRawExpression(string? raw)
    {
        string trimmed = raw?.Trim() ?? string.Empty;
        return CognosPromptSyntax.TryExtractPromptExpression(trimmed, out string promptExpression, out _)
            ? promptExpression
            : null;
    }

    private static QueryParameterSourceKind BuildImplicitParameterSourceKind(string? raw, SqlDialect dialect)
    {
        string trimmed = raw?.Trim() ?? string.Empty;
        return CognosPromptSyntax.TryExtractPromptExpression(trimmed, out _, out _) || dialect == SqlDialect.CognosAnalytics
            ? QueryParameterSourceKind.CognosPrompt
            : QueryParameterSourceKind.Standard;
    }

    private static string GetParameterIdentity(QueryParameterDefinition parameter)
    {
        if (parameter.SourceKind == QueryParameterSourceKind.CognosPrompt
            && CognosPromptSyntax.TryParsePromptExpression(parameter.Placeholder, out string promptName, out _))
        {
            return promptName;
        }

        return TrimParameterPrefix(string.IsNullOrWhiteSpace(parameter.Name) ? parameter.Placeholder : parameter.Name);
    }

    private static string TrimParameterPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string trimmed = value.Trim();
        return trimmed.StartsWith(':') || trimmed.StartsWith('@') || trimmed.StartsWith('&')
            ? trimmed[1..]
            : trimmed;
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
            _tableAliases.Clear();
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GeneratedSql = "-- Requête vidée.";
        QueryPurpose = "Requête vidée.";
        PerformanceReport = "Requête vidée.";
        Warnings = "Aucun avertissement.";
        ClearSqlComparison("Aucune comparaison SQL active. Utilise Réécrire SQL ou Charger dans le constructeur.");
        TrackHistoryAfterMutation();
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
                Kind = SavedQueryKind.Builder,
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
    /// Saves the raw SQL editor content as a raw SELECT preset that can be reused as a subquery.
    /// </summary>
    private void SaveRawSqlPreset()
    {
        try
        {
            string rawSql = SqlSafety.NormalizeRawSelectQuery(RawSqlText);
            string name = string.IsNullOrWhiteSpace(QueryName) ? $"sql_brut_{DateTime.Now:yyyyMMdd_HHmmss}" : QueryName.Trim();
            QueryDefinition metadataQuery = new()
            {
                Name = name,
                Description = BlankToNull(QueryDescription)
            };
            foreach (QueryParameterDefinition parameter in SqlSelectReverseParser.ExtractParameters(rawSql))
            {
                metadataQuery.Parameters.Add(parameter);
            }

            SavedQueryDefinition saved = new()
            {
                Kind = SavedQueryKind.RawSql,
                Name = name,
                Description = BlankToNull(QueryDescription),
                Query = metadataQuery,
                RawSql = rawSql,
                LastGeneratedSql = rawSql
            };

            string path = _savedQueryStore.Save(saved);
            ReloadSavedQueries();
            Status = $"Preset SQL brut sauvegardé: {path}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Warnings = "Erreur de sauvegarde SQL brut: " + ex.Message;
        }
    }

    /// <summary>
    /// Loads the selected raw SQL preset into the raw SQL editor and generated SQL panel.
    /// </summary>
    private void LoadSelectedRawSqlPreset()
    {
        if (SelectedSavedQuery is null)
        {
            return;
        }

        SavedQueryDefinition saved = SelectedSavedQuery.Saved;
        RawSqlText = saved.Kind == SavedQueryKind.RawSql
            ? saved.RawSql ?? saved.LastGeneratedSql ?? string.Empty
            : saved.LastGeneratedSql ?? string.Empty;
        QueryName = saved.Name;
        QueryDescription = saved.Description ?? string.Empty;
        ClearStoredRewrittenSql();
        ClearSqlComparison("Preset SQL brut chargé. Réécris-le ou recharge-le dans le constructeur pour afficher la comparaison.");
        if (!string.IsNullOrWhiteSpace(RawSqlText))
        {
            GeneratedSql = RawSqlText.TrimEnd() + Environment.NewLine;
            QueryPurpose = "Preset SQL brut chargé. Utilise Reverse SQL pour le transformer en constructeur visuel.";
            PerformanceReport = "Analyse performance limitée tant que la requête n'est pas convertie en modèle visuel.";
            Warnings = saved.Kind == SavedQueryKind.RawSql ? "Preset SQL brut chargé." : "SQL généré du preset visuel chargé dans l'éditeur brut.";
        }
    }

    /// <summary>
    /// Reverse-engineers the raw SQL editor content into the visual query builder.
    /// </summary>
    private void ReverseEngineerRawSql()
    {
        try
        {
            string sourceSql = RawSqlText;
            ReverseSqlImportResult imported = _reverseImportService.Import(RawSqlText, SourceSqlDialect);
            QueryDefinition query = imported.Query;
            query.Name = BlankToNull(QueryName) ?? "requete_reverse";
            query.Description = BlankToNull(QueryDescription) ?? "Requête reconstruite depuis du SQL brut.";
            LoadQueryDefinition(query, query.Name, query.Description);
            ApplyReverseSqlResult(imported);
            CompareRawVsRewrittenSql();
            StartGeneratedSqlComparison(sourceSql, "SQL brut source", "SQL régénéré depuis le constructeur");
            Status = "Reverse SQL terminé: les clauses reconnues ont été replacées dans le constructeur visuel.";
            Warnings = BuildReverseSqlFeedbackText(imported, imported.Warnings.Count == 0
                ? "Reverse SQL termine sans avertissement."
                : string.Join(Environment.NewLine, imported.Warnings));
        }
        catch (ReverseSqlImportException ex)
        {
            ClearStoredRewrittenSql();
            ApplyReverseSqlFailure(ex.Diagnostic);
            ClearSqlComparison("Comparaison SQL indisponible tant que le reverse échoue.");
            Warnings = BuildReverseSqlFailureText(ex.Diagnostic);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ClearStoredRewrittenSql();
            ClearSqlComparison("Comparaison SQL indisponible tant que le reverse échoue.");
            Warnings = "Reverse SQL impossible: " + ex.Message;
        }
    }

    /// <summary>
    /// Rewrites the raw SQL editor content into a cleaner canonical SQL statement.
    /// </summary>
    private void RewriteRawSql()
    {
        ClearStoredRewrittenSql();
        try
        {
            string sourceSql = RawSqlText;
            ReverseSqlImportResult imported = _reverseImportService.Import(RawSqlText, SourceSqlDialect);
            SqlRewriteResult result = _rewriteSuggestionService.Rewrite(RawSqlText, new SqlGeneratorOptions
            {
                Dialect = Dialect,
                QuoteIdentifiers = QuoteIdentifiers,
                AutoGroupSelectedColumnsWhenAggregating = AutoGroupSelectedColumns,
                EmitOptimizationComments = false
            }, SourceSqlDialect);

            GeneratedSql = result.RewrittenSql;
            SetLastRewrittenSql(result.RewrittenSql);
            ApplyReverseSqlResult(imported);
            Status = $"Réécriture SQL terminée: {result.AppliedTransformations.Count} transformation(s) appliquée(s).";
            CompareRawVsRewrittenSql();
            Warnings = BuildReverseSqlFeedbackText(imported, result.Warnings.Count == 0
                ? "Réécriture SQL terminée sans avertissement."
                : string.Join(Environment.NewLine, result.Warnings));
            QueryPurpose = "SQL réécrit sans modifier l'éditeur brut. Compare le SQL source et la version modernisée.";
            PerformanceReport = result.AppliedTransformations.Count == 0
                ? "Aucune transformation conservatrice n'a été appliquée."
                : string.Join(", ", result.AppliedTransformations);
        }
        catch (ReverseSqlImportException ex)
        {
            ApplyReverseSqlFailure(ex.Diagnostic);
            ClearSqlComparison("Comparaison SQL indisponible tant que la réécriture échoue.");
            Warnings = BuildReverseSqlFailureText(ex.Diagnostic);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ClearSqlComparison("Comparaison SQL indisponible tant que la réécriture échoue.");
            Warnings = "Réécriture SQL impossible: " + ex.Message;
        }
    }

    private void UseGeneratedSqlAsRawInput()
    {
        RawSqlText = GeneratedSql;
        ClearStoredRewrittenSql();
        ClearSqlComparison("SQL généré copié dans l'éditeur brut. Réécris-le ou recharge-le dans le constructeur pour comparer les versions.");
    }

    private QueryBuilderHistoryState CaptureHistoryState()
    {
        return new QueryBuilderHistoryState
        {
            Query = QueryDefinitionCloner.Clone(BuildQueryDefinition()),
            Dialect = Dialect,
            QuoteIdentifiers = QuoteIdentifiers,
            AutoGroupSelectedColumns = AutoGroupSelectedColumns,
            RawSqlText = RawSqlText,
            SourceSqlDialect = SourceSqlDialect
        };
    }

    private void TrackHistoryAfterMutation()
    {
        if (_suppressAutoGenerate || _isRestoringHistory)
        {
            return;
        }

        _history.Track(CaptureHistoryState());
        UpdateHistoryCommands();
    }

    private void ResetHistoryToCurrentState()
    {
        _history.Reset(CaptureHistoryState());
        UpdateHistoryCommands();
    }

    private void UndoHistory()
    {
        RestoreHistoryState(_history.Undo(), "Historique: annulation appliquée.");
    }

    private void RedoHistory()
    {
        RestoreHistoryState(_history.Redo(), "Historique: rétablissement appliqué.");
    }

    private void RestoreHistoryState(QueryBuilderHistoryState state, string status)
    {
        _isRestoringHistory = true;
        try
        {
            _suppressAutoGenerate = true;
            try
            {
                Dialect = state.Dialect;
                QuoteIdentifiers = state.QuoteIdentifiers;
                AutoGroupSelectedColumns = state.AutoGroupSelectedColumns;
                RawSqlText = state.RawSqlText;
                SourceSqlDialect = state.SourceSqlDialect;
                ClearStoredRewrittenSql();
                ClearSqlComparison("Historique restauré. Relance Reverse SQL ou Réécrire SQL pour recalculer une comparaison.");
            }
            finally
            {
                _suppressAutoGenerate = false;
            }

            LoadQueryDefinition(QueryDefinitionCloner.Clone(state.Query), state.Query.Name, state.Query.Description);
            Status = status;
        }
        finally
        {
            _isRestoringHistory = false;
            _history.ReplaceCurrent(CaptureHistoryState());
            UpdateHistoryCommands();
        }
    }

    private void UpdateHistoryCommands()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void CompareRawVsRewrittenSql()
    {
        if (string.IsNullOrWhiteSpace(_lastRewrittenSql))
        {
            ClearSqlComparison("Comparaison indisponible: aucune réécriture SQL récente à comparer.");
            return;
        }

        SqlComparisonReport comparison = _sqlComparisonService.Compare(RawSqlText, _lastRewrittenSql, BuildSqlComparisonOptions());
        ApplySqlComparison(comparison, "SQL brut source", "SQL réécrit", RawSqlText, tracksGeneratedSql: false);
    }

    private void CompareRawVsBuilderSql()
    {
        StartGeneratedSqlComparison(RawSqlText, "SQL brut source", "SQL généré depuis le constructeur");
    }

    private void CompareRewrittenVsBuilderSql()
    {
        if (string.IsNullOrWhiteSpace(_lastRewrittenSql))
        {
            ClearSqlComparison("Comparaison indisponible: aucune réécriture SQL récente à comparer au constructeur.");
            return;
        }

        StartGeneratedSqlComparison(_lastRewrittenSql, "SQL réécrit", "SQL généré depuis le constructeur");
    }

    private void StartGeneratedSqlComparison(string sourceSql, string sourceLabel, string targetLabel)
    {
        _sqlComparisonBaselineSql = sourceSql ?? string.Empty;
        _sqlComparisonTracksGeneratedSql = true;
        SqlComparisonSourceLabel = sourceLabel;
        SqlComparisonTargetLabel = targetLabel;
        RefreshTrackedSqlComparison();
    }

    private void RefreshCurrentSqlComparison()
    {
        if (_sqlComparisonTracksGeneratedSql)
        {
            RefreshTrackedSqlComparison();
            return;
        }

        if (string.Equals(SqlComparisonTargetLabel, "SQL réécrit", StringComparison.Ordinal))
        {
            CompareRawVsRewrittenSql();
        }
    }

    private void RefreshTrackedSqlComparison()
    {
        if (!_sqlComparisonTracksGeneratedSql)
        {
            return;
        }

        if (!TryBuildBuilderGeneratedSqlForComparison(out string builderSql, out string failureSummary))
        {
            SuspendTrackedSqlComparison(failureSummary);
            return;
        }

        SqlComparisonReport report = _sqlComparisonService.Compare(_sqlComparisonBaselineSql, builderSql, BuildSqlComparisonOptions());
        ApplySqlComparison(report, SqlComparisonSourceLabel, SqlComparisonTargetLabel, _sqlComparisonBaselineSql, tracksGeneratedSql: true);
    }

    private void SuspendTrackedSqlComparison(string summary)
    {
        if (_sqlComparisonTracksGeneratedSql)
        {
            SqlComparisonLines.Clear();
            SqlComparisonSummary = summary;
        }
    }

    private void ClearSqlComparison(string summary)
    {
        _sqlComparisonBaselineSql = string.Empty;
        _sqlComparisonTracksGeneratedSql = false;
        SqlComparisonSourceLabel = "SQL source";
        SqlComparisonTargetLabel = "SQL résultat";
        SqlComparisonLines.Clear();
        SqlComparisonSummary = summary;
    }

    private void ApplySqlComparison(SqlComparisonReport comparison, string sourceLabel, string targetLabel, string sourceSql, bool tracksGeneratedSql)
    {
        _sqlComparisonBaselineSql = sourceSql ?? string.Empty;
        _sqlComparisonTracksGeneratedSql = tracksGeneratedSql;
        SqlComparisonSourceLabel = sourceLabel;
        SqlComparisonTargetLabel = targetLabel;
        SqlComparisonSummary = comparison.FormatSummary(sourceLabel, targetLabel);

        SqlComparisonLines.Clear();
        foreach (SqlComparisonLine line in comparison.Lines)
        {
            SqlComparisonLines.Add(line);
        }
    }

    private bool TryBuildBuilderGeneratedSqlForComparison(out string sql, out string failureSummary)
    {
        sql = string.Empty;
        failureSummary = "Comparaison SQL indisponible tant que le constructeur ne peut pas générer de SQL valide.";

        try
        {
            QueryDefinition query = BuildQueryDefinition();
            SqlGenerationResult result = _generator.Generate(query, _schema, new SqlGeneratorOptions
            {
                Dialect = Dialect,
                QuoteIdentifiers = QuoteIdentifiers,
                AutoGroupSelectedColumnsWhenAggregating = AutoGroupSelectedColumns,
                EmitOptimizationComments = false
            });

            sql = result.Sql;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            failureSummary = "Comparaison SQL indisponible: le SQL du constructeur ne peut pas être régénéré actuellement. " + ex.Message;
            return false;
        }
    }

    private SqlComparisonOptions BuildSqlComparisonOptions()
    {
        return new SqlComparisonOptions
        {
            IgnoreWhitespaceChanges = IgnoreSqlComparisonWhitespaceChanges,
            IgnoreCaseChanges = IgnoreSqlComparisonCaseChanges
        };
    }

    private void SetLastRewrittenSql(string sql)
    {
        _lastRewrittenSql = sql ?? string.Empty;
        UpdateSqlComparisonCommands();
    }

    private void ClearStoredRewrittenSql()
    {
        _lastRewrittenSql = string.Empty;
        UpdateSqlComparisonCommands();
    }

    private void UpdateSqlComparisonCommands()
    {
        CompareRawVsRewrittenSqlCommand.RaiseCanExecuteChanged();
        CompareRewrittenVsBuilderSqlCommand.RaiseCanExecuteChanged();
    }

    private void ApplyReverseSqlResult(ReverseSqlImportResult imported)
    {
        ReverseSqlCoverageReport = FormatReverseCoverage(imported.Coverage);
        ReverseSqlDiagnosticsReport = FormatReverseDiagnostics(imported.Diagnostics);
        ReverseSqlConfidenceSummary = $"Confiance Reverse SQL: {imported.Coverage.Confidence:P0} ({imported.SourceDialect}).";
        RawSqlSelectionStart = 0;
        RawSqlSelectionLength = 0;
    }

    private void ApplyReverseSqlFailure(ReverseSqlDiagnostic diagnostic)
    {
        ReverseSqlCoverageReport = "Import interrompu avant génération d'un rapport de couverture.";
        ReverseSqlDiagnosticsReport = FormatReverseDiagnostics([diagnostic]);
        ReverseSqlConfidenceSummary = "Confiance Reverse SQL: échec de l'import.";
        RawSqlSelectionStart = diagnostic.StartOffset ?? 0;
        RawSqlSelectionLength = diagnostic.Length ?? 0;
        Status = "Reverse SQL interrompu: corrige le fragment signalé puis relance l'import.";
    }

    private static string FormatReverseCoverage(ReverseSqlCoverageReport coverage)
    {
        if (coverage.Clauses.Count == 0)
        {
            return "Aucune clause analysée.";
        }

        return string.Join(Environment.NewLine, coverage.Clauses.Select(clause =>
            $"{clause.Clause,-14} {FormatCoverageStatus(clause.Status)} {clause.Message}"));
    }

    private static string FormatReverseDiagnostics(IReadOnlyList<ReverseSqlDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return "Aucun diagnostic Reverse SQL.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            diagnostics.Select(diagnostic =>
            {
                List<string> lines =
                [
                    $"[{diagnostic.Severity}] {diagnostic.Message}"
                ];

                if (!string.IsNullOrWhiteSpace(diagnostic.Clause))
                {
                    lines.Add($"Clause: {diagnostic.Clause}");
                }

                if (!string.IsNullOrWhiteSpace(diagnostic.Fragment))
                {
                    lines.Add($"Fragment: {diagnostic.Fragment}");
                }

                if (!string.IsNullOrWhiteSpace(diagnostic.SuggestedFix))
                {
                    lines.Add($"Action suggérée: {diagnostic.SuggestedFix}");
                }

                return string.Join(Environment.NewLine, lines);
            }));
    }

    private static string BuildReverseSqlFeedbackText(ReverseSqlImportResult imported, string headline)
    {
        string[] parts =
        [
            headline,
            $"Confiance Reverse SQL: {imported.Coverage.Confidence:P0} ({imported.SourceDialect})",
            FormatReverseCoverage(imported.Coverage),
            FormatReverseDiagnostics(imported.Diagnostics)
        ];

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildReverseSqlFailureText(ReverseSqlDiagnostic diagnostic)
    {
        string[] parts =
        [
            diagnostic.Message,
            FormatReverseDiagnostics([diagnostic])
        ];

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            parts);
    }

    private static string FormatCoverageStatus(ReverseSqlCoverageStatus status) => status switch
    {
        ReverseSqlCoverageStatus.FullyImported => "OK",
        ReverseSqlCoverageStatus.PartiallyImported => "WARN",
        ReverseSqlCoverageStatus.Unsupported => "NO",
        ReverseSqlCoverageStatus.Ignored => "SKIP",
        ReverseSqlCoverageStatus.Unknown => "?",
        _ => "-"
    };

    /// <summary>
    /// Builds and copies a helper SQL command that extracts DDL from Oracle or SQLite.
    /// </summary>
    private void GenerateDdlExportSql()
    {
        DdlExportSql = _ddlCommandExportService.BuildCommand(DdlExportDialect, DdlSchemaName);
        Clipboard.SetText(DdlExportSql);
        Status = "Commande de récupération du DDL générée et copiée dans le presse-papier.";
        Warnings = "Vérifie le nom de schéma/base avant exécution dans ton client SQL.";
    }


    /// <summary>
    /// Generates a CREATE MATERIALIZED VIEW statement from the current visual query.
    /// </summary>
    private void GenerateCreateMaterializedViewSql()
    {
        try
        {
            QueryDefinition query = BuildQueryDefinition();
            IReadOnlyList<string> validationErrors = _validator.Validate(query, _schema);
            if (validationErrors.Count > 0)
            {
                CreateMaterializedViewSql = "-- Impossible de générer la vue matérialisée : la requête courante contient des erreurs.";
                Warnings = string.Join(Environment.NewLine, validationErrors);
                Status = "Création de vue matérialisée impossible: corrige la requête courante.";
                return;
            }

            SqlGenerationResult result = _generator.Generate(query, _schema, new SqlGeneratorOptions
            {
                Dialect = Dialect,
                QuoteIdentifiers = QuoteIdentifiers,
                AutoGroupSelectedColumnsWhenAggregating = AutoGroupSelectedColumns,
                EmitOptimizationComments = false
            });

            CreateMaterializedViewSql = _ddlCommandExportService.BuildCreateMaterializedViewCommand(
                MaterializedViewName,
                result.Sql,
                Dialect,
                QuoteIdentifiers);

            Clipboard.SetText(CreateMaterializedViewSql);
            Status = $"Instruction CREATE MATERIALIZED VIEW générée et copiée: {MaterializedViewName}.";
            Warnings = result.Warnings.Count == 0
                ? "CREATE MATERIALIZED VIEW généré sans avertissement."
                : string.Join(Environment.NewLine, result.Warnings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            CreateMaterializedViewSql = "-- Impossible de générer l'instruction CREATE MATERIALIZED VIEW.";
            Warnings = ex.Message;
            Status = "Erreur pendant la génération de la vue matérialisée.";
        }
    }

    /// <summary>
    /// Opens the documentation used by the Help button.
    /// </summary>
    private void OpenHelpDocumentation()
    {
        try
        {
            string localPath = Path.Combine(Environment.CurrentDirectory, ApplicationDocumentation.LocalHelpRelativePath);
            string target = File.Exists(localPath) ? localPath : ApplicationDocumentation.RemoteHelpUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            Status = "Documentation ouverte.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or FileNotFoundException)
        {
            Warnings = "Impossible d'ouvrir la documentation: " + ex.Message;
        }
    }

    /// <summary>
    /// Rebuilds a temporary saved query object from a serialized filter subquery.
    /// </summary>
    /// <param name="filter">Filter condition that may reference a raw or structured subquery.</param>
    /// <returns>Saved query object suitable for UI display, or <c>null</c>.</returns>
    private static SavedQueryDefinition? BuildSavedSubqueryForFilter(FilterCondition filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.RawSubquerySql))
        {
            return new SavedQueryDefinition
            {
                Kind = SavedQueryKind.RawSql,
                Name = filter.SubqueryName ?? "sql_brut",
                RawSql = filter.RawSubquerySql,
                LastGeneratedSql = filter.RawSubquerySql
            };
        }

        return filter.Subquery is null
            ? null
            : new SavedQueryDefinition { Kind = SavedQueryKind.Builder, Name = filter.SubqueryName ?? filter.Subquery.Name ?? "subquery", Query = filter.Subquery };
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

        if (SelectedSavedQuery.Saved.Kind == SavedQueryKind.RawSql)
        {
            LoadSelectedRawSqlPreset();
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
            _tableAliases.Clear();

            foreach (TableAliasDefinition alias in query.TableAliases)
            {
                _tableAliases[alias.Table] = alias.Alias;
            }

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
                    SubqueryName = f.SubqueryName ?? f.Subquery?.Name ?? (string.IsNullOrWhiteSpace(f.RawSubquerySql) ? string.Empty : "sql_brut"),
                    SavedSubquery = BuildSavedSubqueryForFilter(f),
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
            foreach (QueryParameterDefinition p in query.Parameters) Parameters.Add(new QueryParameterRowViewModel
            {
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                DefaultValue = p.DefaultValue ?? string.Empty,
                DeclaredType = p.DeclaredType ?? string.Empty,
                UseCognosPrompt = p.SourceKind == QueryParameterSourceKind.CognosPrompt,
                Required = p.Required
            });
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
