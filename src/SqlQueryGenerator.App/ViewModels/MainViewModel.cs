using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Heuristics;
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
    private DatabaseSchema _schema = new();
    private string _loadedFile = string.Empty;
    private string _status = "Charge un schéma SQL/TXT pour commencer.";
    private string _generatedSql = "-- La requête générée apparaîtra ici.";
    private string _warnings = string.Empty;
    private string _queryPurpose = "Charge un schéma et construis une requête pour obtenir une explication en français.";
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
        AddEmptyCustomColumnCommand = new RelayCommand(() => CustomColumns.Add(new CustomColumnRowViewModel { Alias = "colonne_calculee" }));
        ClearColumnSearchCommand = new RelayCommand(() => ColumnSearchText = string.Empty);

        WireAutoGenerate(SelectedColumns);
        WireAutoGenerate(Filters);
        WireAutoGenerate(GroupBy);
        WireAutoGenerate(OrderBy);
        WireAutoGenerate(Aggregates);
        WireAutoGenerate(Joins);
        WireAutoGenerate(CustomColumns);
    }

    public ObservableCollection<TableItemViewModel> Tables { get; } = new();
    public ObservableCollection<ColumnItemViewModel> AllColumns { get; } = new();
    public ObservableCollection<RelationshipItemViewModel> Relationships { get; } = new();
    public ObservableCollection<RelationshipGroupViewModel> RelationshipGroups { get; } = new();
    public ObservableCollection<SelectColumnRowViewModel> SelectedColumns { get; } = new();
    public ObservableCollection<FilterRowViewModel> Filters { get; } = new();
    public ObservableCollection<GroupByRowViewModel> GroupBy { get; } = new();
    public ObservableCollection<OrderByRowViewModel> OrderBy { get; } = new();
    public ObservableCollection<AggregateRowViewModel> Aggregates { get; } = new();
    public ObservableCollection<JoinRowViewModel> Joins { get; } = new();
    public ObservableCollection<CustomColumnRowViewModel> CustomColumns { get; } = new();
    public ObservableCollection<string> TableNames { get; } = new();
    public IReadOnlyList<string> Operators { get; } = new[] { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL" };
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
    public RelayCommand ClearColumnSearchCommand { get; }

    public string LoadedFile { get => _loadedFile; set => SetProperty(ref _loadedFile, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string GeneratedSql { get => _generatedSql; set => SetProperty(ref _generatedSql, value); }
    public string Warnings { get => _warnings; set => SetProperty(ref _warnings, value); }
    public string QueryPurpose { get => _queryPurpose; set => SetProperty(ref _queryPurpose, value); }
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
        : $"{_schema.Tables.Count} tables · {_schema.Tables.Sum(t => t.Columns.Count)} colonnes · {_schema.Indexes.Count} index · {_schema.Relationships.Count} relations probables";

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

        var info = new FileInfo(filePath);
        if (info.Length > 20_000_000)
        {
            MessageBox.Show("Le fichier dépasse 20 Mo. Pour éviter de bloquer l'interface, réduis le schéma ou augmente la limite dans MainViewModel.", "Fichier trop volumineux", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var text = File.ReadAllText(filePath);
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
            Status = $"Schéma chargé: {_schema.Tables.Count} tables, {_schema.Tables.Sum(t => t.Columns.Count)} colonnes, {_schema.Indexes.Count} index, {_schema.Relationships.Count} relations probables.";
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
        var foreignKeySummaries = BuildForeignKeySummaries();
        var indexSummaries = BuildIndexSummaries();
        var uniqueIndexColumns = BuildUniqueIndexColumnSet();
        foreach (var table in _schema.Tables.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
        {
            TableNames.Add(table.FullName);
            foreach (var col in table.Columns.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                AllColumns.Add(new ColumnItemViewModel(
                    col,
                    LookupSummary(col, foreignKeySummaries),
                    LookupSummary(col, indexSummaries),
                    uniqueIndexColumns.Contains($"{col.TableName}.{col.Name}")));
            }
        }
        foreach (var rel in _schema.Relationships.OrderByDescending(r => r.Confidence).Take(500))
        {
            var vm = new RelationshipItemViewModel(rel);
            vm.PropertyChanged += Relationship_PropertyChanged;
            Relationships.Add(vm);
        }

        foreach (var group in Relationships
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

        var needle = ColumnSearchText?.Trim();
        var foreignKeySummaries = BuildForeignKeySummaries();
        var indexSummaries = BuildIndexSummaries();
        var uniqueIndexColumns = BuildUniqueIndexColumnSet();
        foreach (var table in _schema.Tables.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<ColumnDefinition> visibleColumns = table.Columns;

            if (!string.IsNullOrWhiteSpace(needle))
            {
                var tableMatches = table.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(table.Comment) && table.Comment.Contains(needle, StringComparison.OrdinalIgnoreCase));

                visibleColumns = tableMatches
                    ? table.Columns
                    : table.Columns.Where(c => c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || (c.DataType?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (c.Comment?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var visibleList = visibleColumns.ToList();
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
        var key = $"{column.TableName}.{column.Name}";
        return summaries.TryGetValue(key, out var summary) ? summary : string.Empty;
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
            var query = BuildQueryDefinition();
            var validationErrors = _validator.Validate(query, _schema);
            var result = _generator.Generate(query, _schema, new SqlGeneratorOptions
            {
                Dialect = Dialect,
                QuoteIdentifiers = QuoteIdentifiers,
                AutoGroupSelectedColumnsWhenAggregating = AutoGroupSelectedColumns,
                EmitOptimizationComments = false
            });

            GeneratedSql = result.Sql;
            QueryPurpose = _purposeDescriber.Describe(query, _schema);
            var messages = validationErrors.Concat(result.Warnings).Concat(_schema.Warnings).Distinct().ToArray();
            Warnings = messages.Length == 0 ? "Aucun avertissement." : string.Join(Environment.NewLine, messages);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            GeneratedSql = "-- Impossible de générer la requête. Corrige les champs signalés.";
            QueryPurpose = "Impossible d'expliquer le but tant que la requête contient des erreurs.";
            Warnings = ex.Message;
        }
    }

    private QueryDefinition BuildQueryDefinition()
    {
        var query = new QueryDefinition
        {
            BaseTable = string.IsNullOrWhiteSpace(BaseTable) ? null : BaseTable,
            Distinct = Distinct,
            LimitRows = LimitRows is > 0 ? LimitRows : null
        };

        foreach (var row in SelectedColumns)
        {
            query.SelectedColumns.Add(new ColumnReference { Table = row.Table, Column = row.Column, Alias = BlankToNull(row.Alias) });
        }

        foreach (var row in Filters.Where(f => !string.IsNullOrWhiteSpace(f.Table) && !string.IsNullOrWhiteSpace(f.Column)))
        {
            query.Filters.Add(new FilterCondition
            {
                Column = row.ToColumnReference(),
                Operator = row.Operator,
                Value = row.Value,
                SecondValue = row.SecondValue,
                Connector = row.Connector
            });
        }

        foreach (var row in GroupBy)
        {
            query.GroupBy.Add(row.ToColumnReference());
        }

        foreach (var row in OrderBy)
        {
            query.OrderBy.Add(new OrderByItem { Column = row.ToColumnReference(), Direction = row.Direction });
        }

        foreach (var row in Aggregates)
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

        foreach (var row in Joins.Where(j => !string.IsNullOrWhiteSpace(j.FromTable)
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

        foreach (var row in CustomColumns)
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

        foreach (var disabled in Relationships.Where(r => !r.IsEnabled))
        {
            query.DisabledAutoJoinKeys.Add(disabled.Key);
            query.DisabledAutoJoinKeys.Add(disabled.ReverseKey);
        }

        return query;
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
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GeneratedSql = "-- Requête vidée.";
        QueryPurpose = "Requête vidée.";
        Warnings = "Aucun avertissement.";
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
