using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Heuristics;
using SqlQueryGenerator.Core.Persistence;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;
using SqlQueryGenerator.Core.Validation;

namespace SqlQueryGenerator.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SqlSchemaParser _parser = new();
    private readonly SqlQueryGeneratorEngine _generator = new();
    private readonly QueryValidator _validator = new();
    private readonly QueryPurposeDescriber _purposeDescriber = new();
    private readonly QueryPerformanceAnalyzer _performanceAnalyzer = new();
    private readonly SavedQueryStore _savedQueryStore = new(Path.Combine(Environment.CurrentDirectory, "saved_queries"));
    private DatabaseSchema _schema = new();
    private string _loadedFile = string.Empty;
    private string _status = "Charge un schéma SQL/TXT pour commencer.";
    private string _generatedSql = "-- La requête générée apparaîtra ici.";
    private string _warnings = string.Empty;
    private string _queryPurpose = "Charge un schéma et construis une requête pour obtenir une explication en français.";
    private string _performanceReport = "L'analyse de performance apparaîtra ici.";
    private string _queryName = "nouvelle_requete";
    private string _queryDescription = string.Empty;
    private SavedQueryItemViewModel? _selectedSavedQuery;
    private string _baseTable = string.Empty;
    private SqlDialect _dialect = SqlDialect.SQLite;
    private bool _quoteIdentifiers;
    private bool _distinct;
    private bool _autoGroupSelectedColumns = true;
    private int? _limitRows;
    private ColumnItemViewModel? _selectedAvailableColumn;
    private RelationshipItemViewModel? _selectedRelationship;
    private bool _suppressAutoGenerate;
    private string _columnSearchText = string.Empty;

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
        AddSelectedFilterCommand = new RelayCommand(_ => AddSelectedColumnTo("filter"), _ => SelectedAvailableColumn is not null);
        AddSelectedGroupByCommand = new RelayCommand(_ => AddSelectedColumnTo("group"), _ => SelectedAvailableColumn is not null);
        AddSelectedOrderByCommand = new RelayCommand(_ => AddSelectedColumnTo("order"), _ => SelectedAvailableColumn is not null);
        AddSelectedAggregateCommand = new RelayCommand(_ => AddSelectedColumnTo("aggregate"), _ => SelectedAvailableColumn is not null);
        AddJoinFromRelationshipCommand = new RelayCommand(_ => AddSelectedRelationshipAsJoin(), _ => SelectedRelationship is not null);
        AddManualJoinCommand = new RelayCommand(AddManualJoin);
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

        WireAutoGenerate(SelectedColumns);
        WireAutoGenerate(Filters);
        WireAutoGenerate(GroupBy);
        WireAutoGenerate(OrderBy);
        WireAutoGenerate(Aggregates);
        WireAutoGenerate(Joins);
        WireAutoGenerate(CustomColumns);
        WireAutoGenerate(Parameters);
    }

    public ObservableCollection<TableItemViewModel> Tables { get; } = [];
    public ObservableCollection<ColumnItemViewModel> AllColumns { get; } = [];
    public ObservableCollection<RelationshipItemViewModel> Relationships { get; } = [];
    public ObservableCollection<RelationshipGroupViewModel> RelationshipGroups { get; } = [];
    public ObservableCollection<SelectColumnRowViewModel> SelectedColumns { get; } = [];
    public ObservableCollection<FilterRowViewModel> Filters { get; } = [];
    public ObservableCollection<GroupByRowViewModel> GroupBy { get; } = [];
    public ObservableCollection<OrderByRowViewModel> OrderBy { get; } = [];
    public ObservableCollection<AggregateRowViewModel> Aggregates { get; } = [];
    public ObservableCollection<JoinRowViewModel> Joins { get; } = [];
    public ObservableCollection<CustomColumnRowViewModel> CustomColumns { get; } = [];
    public ObservableCollection<QueryParameterRowViewModel> Parameters { get; } = [];
    public ObservableCollection<SavedQueryItemViewModel> SavedQueries { get; } = [];
    public ObservableCollection<string> TableNames { get; } = [];
    public IReadOnlyList<string> Operators { get; } = new[] { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL", "EXISTS", "NOT EXISTS" };
    public Array FilterValueKinds => Enum.GetValues(typeof(FilterValueKind));
    public Array Dialects => Enum.GetValues(typeof(SqlDialect));
    public Array JoinTypes => Enum.GetValues(typeof(JoinType));
    public Array AggregateFunctions => Enum.GetValues(typeof(AggregateFunction));
    public Array SortDirections => Enum.GetValues(typeof(SortDirection));
    public Array LogicalConnectors => Enum.GetValues(typeof(LogicalConnector));

    public RelayCommand GenerateCommand { get; }
    public RelayCommand ClearQueryCommand { get; }
    public RelayCommand RemoveSelectedColumnCommand { get; }
    public RelayCommand RemoveFilterCommand { get; }
    public RelayCommand RemoveGroupByCommand { get; }
    public RelayCommand RemoveOrderByCommand { get; }
    public RelayCommand RemoveAggregateCommand { get; }
    public RelayCommand RemoveJoinCommand { get; }
    public RelayCommand RemoveCustomColumnCommand { get; }
    public RelayCommand AddSelectedColumnCommand { get; }
    public RelayCommand AddSelectedFilterCommand { get; }
    public RelayCommand AddSelectedGroupByCommand { get; }
    public RelayCommand AddSelectedOrderByCommand { get; }
    public RelayCommand AddSelectedAggregateCommand { get; }
    public RelayCommand AddJoinFromRelationshipCommand { get; }
    public RelayCommand AddManualJoinCommand { get; }
    public RelayCommand AddEmptyCustomColumnCommand { get; }
    public RelayCommand AddAggregateFilterCommand { get; }
    public RelayCommand AddAggregateOrderByCommand { get; }
    public RelayCommand AddCustomFilterCommand { get; }
    public RelayCommand AddCustomOrderByCommand { get; }
    public RelayCommand AddParameterCommand { get; }
    public RelayCommand RemoveParameterCommand { get; }
    public RelayCommand SaveCurrentQueryCommand { get; }
    public RelayCommand ReloadSavedQueriesCommand { get; }
    public RelayCommand LoadSelectedQueryCommand { get; }
    public RelayCommand AddSelectedSavedQueryAsSubqueryFilterCommand { get; }
    public RelayCommand ClearColumnSearchCommand { get; }

    public string LoadedFile { get => _loadedFile; set => SetProperty(ref _loadedFile, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string GeneratedSql { get => _generatedSql; set => SetProperty(ref _generatedSql, value); }
    public string Warnings { get => _warnings; set => SetProperty(ref _warnings, value); }
    public string QueryPurpose { get => _queryPurpose; set => SetProperty(ref _queryPurpose, value); }
    public string PerformanceReport { get => _performanceReport; set => SetProperty(ref _performanceReport, value); }
    public string QueryName { get => _queryName; set => SetProperty(ref _queryName, value); }
    public string QueryDescription { get => _queryDescription; set => SetProperty(ref _queryDescription, value); }

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

    public string SchemaSummary => _schema.Tables.Count == 0
        ? "Aucun schéma chargé"
        : $"{_schema.PhysicalTables.Count()} tables · {_schema.Views.Count()} vues · {_schema.Tables.Sum(t => t.Columns.Count)} colonnes · {_schema.Indexes.Count} index · {_schema.Relationships.Count} relations probables";

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

    public void LoadSchemaFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Status = "Fichier introuvable.";
            return;
        }

        FileInfo info = new FileInfo(filePath);
        if (info.Length > 20_000_000)
        {
            MessageBox.Show("Le fichier dépasse 20 Mo. Pour éviter de bloquer l'interface, réduis le schéma ou augmente la limite dans MainViewModel.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string text = File.ReadAllText(filePath);
        LoadSchemaFromText(text, filePath);
    }

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

    private void EnsureSelectedColumn(ColumnItemViewModel column)
    {
        if (!SelectedColumns.Any(c => SameColumn(c.Table, c.Column, column.Table, column.Column)))
        {
            SelectedColumns.Add(new SelectColumnRowViewModel { Table = column.Table, Column = column.Column });
        }
    }

    private static bool SameColumn(string leftTable, string leftColumn, string rightTable, string rightColumn)
    {
        return string.Equals(leftTable, rightTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(leftColumn, rightColumn, StringComparison.OrdinalIgnoreCase);
    }


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

    private static string EnsureAggregateAlias(AggregateRowViewModel aggregate)
    {
        if (string.IsNullOrWhiteSpace(aggregate.Alias))
        {
            aggregate.Alias = AggregateRowViewModel.BuildDefaultAlias(aggregate.Function, aggregate.Column);
        }

        return aggregate.Alias.Trim();
    }

    private string EnsureCustomAlias(CustomColumnRowViewModel custom)
    {
        if (string.IsNullOrWhiteSpace(custom.Alias))
        {
            custom.Alias = BuildDefaultCustomAlias();
        }

        return custom.Alias.Trim();
    }

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
    private void AddSelectedColumnTo(string target)
    {
        if (SelectedAvailableColumn is not null)
        {
            AddColumnToTarget(SelectedAvailableColumn, target);
        }
    }

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

    private void ReloadSchemaViewModels()
    {
        Tables.Clear();
        AllColumns.Clear();
        Relationships.Clear();
        RelationshipGroups.Clear();
        TableNames.Clear();
        IReadOnlyDictionary<string, string> foreignKeySummaries = BuildForeignKeySummaries();
        IReadOnlyDictionary<string, string> indexSummaries = BuildIndexSummaries();
        IReadOnlySet<string> uniqueIndexColumns = BuildUniqueIndexColumnSet();
        foreach (TableDefinition? table in _schema.Tables.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
        {
            TableNames.Add(table.FullName);
            foreach (ColumnDefinition? col in table.Columns.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                AllColumns.Add(new ColumnItemViewModel(
                    col,
                    LookupSummary(col, foreignKeySummaries),
                    LookupSummary(col, indexSummaries),
                    uniqueIndexColumns.Contains($"{col.TableName}.{col.Name}")));
            }
        }
        foreach (InferredRelationship? rel in _schema.Relationships.OrderByDescending(r => r.Confidence).Take(500))
        {
            RelationshipItemViewModel vm = new RelationshipItemViewModel(rel);
            vm.PropertyChanged += Relationship_PropertyChanged;
            Relationships.Add(vm);
        }

        foreach (IGrouping<string, RelationshipItemViewModel>? group in Relationships
                     .GroupBy(r => r.FromTable, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            RelationshipGroups.Add(new RelationshipGroupViewModel($"{group.Key} ({group.Count()})", group.OrderByDescending(r => r.Confidence))
            {
                IsExpanded = false
            });
        }

        ApplyColumnTreeFilter();
    }

    private void ApplyColumnTreeFilter()
    {
        Tables.Clear();
        SelectedAvailableColumn = null;

        string? needle = ColumnSearchText?.Trim();
        IReadOnlyDictionary<string, string> foreignKeySummaries = BuildForeignKeySummaries();
        IReadOnlyDictionary<string, string> indexSummaries = BuildIndexSummaries();
        IReadOnlySet<string> uniqueIndexColumns = BuildUniqueIndexColumnSet();
        foreach (TableDefinition? table in _schema.Tables.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<ColumnDefinition> visibleColumns = table.Columns;

            if (!string.IsNullOrWhiteSpace(needle))
            {
                bool tableMatches = table.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(table.Comment) && table.Comment.Contains(needle, StringComparison.OrdinalIgnoreCase));

                visibleColumns = tableMatches
                    ? table.Columns
                    : table.Columns.Where(c => c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || (c.DataType?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (c.Comment?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            List<ColumnDefinition> visibleList = visibleColumns.ToList();
            if (visibleList.Count == 0)
            {
                continue;
            }

            Tables.Add(new TableItemViewModel(table, visibleList, foreignKeySummaries, indexSummaries, uniqueIndexColumns)
            {
                IsExpanded = !string.IsNullOrWhiteSpace(needle)
            });
        }
    }

    private IReadOnlyDictionary<string, string> BuildForeignKeySummaries()
    {
        return _schema.Relationships
            .GroupBy(r => $"{r.FromTable}.{r.FromColumn}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => string.Join(" | ", g.OrderByDescending(r => r.Confidence).Take(3).Select(r => $"→ {r.ToTable}.{r.ToColumn} ({r.Confidence:P0})")),
                StringComparer.OrdinalIgnoreCase);
    }

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

    private IReadOnlySet<string> BuildUniqueIndexColumnSet()
    {
        return _schema.Tables
            .SelectMany(t => t.Columns)
            .Where(c => _schema.IsColumnUniqueIndexed(c.TableName, c.Name))
            .Select(c => $"{c.TableName}.{c.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string LookupSummary(ColumnDefinition column, IReadOnlyDictionary<string, string> summaries)
    {
        string key = $"{column.TableName}.{column.Name}";
        return summaries.TryGetValue(key, out string? summary) ? summary : string.Empty;
    }

    private void Relationship_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RelationshipItemViewModel.IsEnabled))
        {
            AutoGenerateSql();
        }
    }

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
            string[] messages = validationErrors.Concat(result.Warnings).Concat(_schema.Warnings).Distinct().ToArray();
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

    private QueryDefinition BuildQueryDefinition()
    {
        QueryDefinition query = new QueryDefinition
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
            query.Joins.Add(new JoinDefinition
            {
                FromTable = row.FromTable,
                FromColumn = row.FromColumn,
                ToTable = row.ToTable,
                ToColumn = row.ToColumn,
                JoinType = row.JoinType
            });
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

    private void SaveCurrentQuery()
    {
        try
        {
            QueryDefinition query = BuildQueryDefinition();
            string name = string.IsNullOrWhiteSpace(QueryName) ? $"requete_{DateTime.Now:yyyyMMdd_HHmmss}" : QueryName.Trim();
            query.Name = name;
            query.Description = BlankToNull(QueryDescription);
            SavedQueryDefinition saved = new SavedQueryDefinition
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

    private void ReloadSavedQueries()
    {
        SavedQueries.Clear();
        foreach (SavedQueryDefinition saved in _savedQueryStore.LoadAll())
        {
            SavedQueries.Add(new SavedQueryItemViewModel(saved));
        }
    }

    private void LoadSelectedSavedQuery()
    {
        if (SelectedSavedQuery is null)
        {
            return;
        }

        LoadQueryDefinition(SelectedSavedQuery.Saved.Query, SelectedSavedQuery.Saved.Name, SelectedSavedQuery.Saved.Description);
    }

    private void AddSelectedSavedQueryAsSubqueryFilter()
    {
        if (SelectedSavedQuery is null)
        {
            return;
        }

        FilterRowViewModel filter = new FilterRowViewModel
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
            foreach (JoinDefinition j in query.Joins) Joins.Add(new JoinRowViewModel { FromTable = j.FromTable, FromColumn = j.FromColumn, ToTable = j.ToTable, ToColumn = j.ToColumn, JoinType = j.JoinType });
            foreach (CustomColumnSelection c in query.CustomColumns) CustomColumns.Add(new CustomColumnRowViewModel { Alias = c.Alias ?? string.Empty, RawExpression = c.RawExpression ?? string.Empty, CaseTable = c.CaseColumn?.Table ?? string.Empty, CaseColumn = c.CaseColumn?.Column ?? string.Empty, CaseOperator = c.CaseOperator ?? "=", CaseCompareValue = c.CaseCompareValue ?? string.Empty, CaseThenValue = c.CaseThenValue ?? string.Empty, CaseElseValue = c.CaseElseValue ?? string.Empty });
            foreach (QueryParameterDefinition p in query.Parameters) Parameters.Add(new QueryParameterRowViewModel { Name = p.Name, Description = p.Description ?? string.Empty, DefaultValue = p.DefaultValue ?? string.Empty, Required = p.Required });
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GenerateSql();
    }

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

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e) => AutoGenerateSql();

    private void AutoGenerateSql()
    {
        if (_suppressAutoGenerate)
        {
            return;
        }

        GenerateSql();
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RemoveFromCollection<T>(ObservableCollection<T> collection, object? obj)
    {
        if (obj is T item)
        {
            collection.Remove(item);
        }
    }
}
