from __future__ import annotations

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    content = read(path)
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one occurrence, found {count}: {old[:120]!r}")
    write(path, content.replace(old, new, 1))


def replace_regex_once(path: str, pattern: str, replacement: str) -> None:
    content = read(path)
    updated, count = re.subn(pattern, replacement, content, count=1, flags=re.MULTILINE | re.DOTALL)
    if count != 1:
        raise RuntimeError(f"{path}: regex expected one occurrence, found {count}: {pattern[:120]!r}")
    write(path, updated)


branch_vm = r'''using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Represents one selectable SELECT branch of a compound query in the visual builder.
/// </summary>
public sealed class CompoundQueryBranchItemViewModel
{
    /// <summary>
    /// Gets the stable path of this branch inside the compound-query tree.
    /// </summary>
    /// <value>Human-readable path such as 1, 2 or 2.1.</value>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the nesting depth of this branch.
    /// </summary>
    /// <value>Zero for the first/root SELECT branch.</value>
    public int Depth { get; init; }

    /// <summary>
    /// Gets the set operator introducing this branch, when it is not the root branch.
    /// </summary>
    /// <value>Incoming set operator, or <c>null</c> for SELECT 1.</value>
    public SetOperationKind? Operator { get; init; }

    /// <summary>
    /// Gets whether the incoming operator uses ALL.
    /// </summary>
    /// <value><c>true</c> for UNION ALL, INTERSECT ALL or EXCEPT ALL.</value>
    public bool All { get; init; }

    /// <summary>
    /// Gets the live query node edited by the visual controls.
    /// </summary>
    /// <value>Query branch inside the compound-query template.</value>
    public required QueryDefinition Query { get; init; }

    /// <summary>
    /// Gets the text displayed in the branch selector.
    /// </summary>
    /// <value>Indented SELECT branch label with operator and base table.</value>
    public string DisplayName
    {
        get
        {
            string indentation = new('·', Depth * 2);
            string operatorText = Operator is null
                ? "branche principale"
                : Operator + (All ? " ALL" : string.Empty);
            string tableText = string.IsNullOrWhiteSpace(Query.BaseTable)
                ? "table non résolue"
                : Query.BaseTable;
            return $"{indentation} SELECT {Path} — {operatorText} — {tableText}";
        }
    }
}
'''
write("src/SqlQueryGenerator.App/ViewModels/CompoundQueryBranchItemViewModel.cs", branch_vm)

main_vm = "src/SqlQueryGenerator.App/ViewModels/MainViewModel.cs"

replace_once(
    main_vm,
    """    private QueryDefinition? _compoundQueryTemplate;
    private DdlExportDialect _ddlExportDialect = DdlExportDialect.SQLite;
""",
    """    private QueryDefinition? _compoundQueryTemplate;
    private CompoundQueryBranchItemViewModel? _selectedCompoundQueryBranch;
    private bool _isSwitchingCompoundQueryBranch;
    private DdlExportDialect _ddlExportDialect = DdlExportDialect.SQLite;
""",
)

replace_once(
    main_vm,
    """    public ObservableCollection<QueryParameterRowViewModel> Parameters { get; } = [];
    /// <summary>
    /// Stocke la valeur interne SavedQueries.
""",
    """    public ObservableCollection<QueryParameterRowViewModel> Parameters { get; } = [];

    /// <summary>
    /// Gets every SELECT branch currently available in the compound-query editor.
    /// </summary>
    /// <value>Flattened root and nested set-operation branches.</value>
    public ObservableCollection<CompoundQueryBranchItemViewModel> CompoundQueryBranches { get; } = [];

    /// <summary>
    /// Gets whether the current builder query contains multiple selectable SELECT branches.
    /// </summary>
    /// <value><c>true</c> when a compound query is loaded.</value>
    public bool HasCompoundQueryBranches => CompoundQueryBranches.Count > 1;

    /// <summary>
    /// Gets a concise description of the currently selected compound-query branch.
    /// </summary>
    /// <value>Branch count and active branch label.</value>
    public string CompoundQueryBranchSummary => SelectedCompoundQueryBranch is null
        ? string.Empty
        : $"{CompoundQueryBranches.Count} branches — édition de {SelectedCompoundQueryBranch.DisplayName}";

    /// <summary>
    /// Gets or sets the SELECT branch currently edited by the visual builder controls.
    /// </summary>
    /// <value>Active branch item.</value>
    public CompoundQueryBranchItemViewModel? SelectedCompoundQueryBranch
    {
        get => _selectedCompoundQueryBranch;
        set
        {
            if (ReferenceEquals(_selectedCompoundQueryBranch, value))
            {
                return;
            }

            CompoundQueryBranchItemViewModel? previous = _selectedCompoundQueryBranch;
            if (!_isSwitchingCompoundQueryBranch
                && previous is not null
                && _compoundQueryTemplate is not null)
            {
                CopyEditableBranchState(previous.Query, BuildVisibleBranchDefinition());
            }

            if (!SetProperty(ref _selectedCompoundQueryBranch, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CompoundQueryBranchSummary));
            if (!_isSwitchingCompoundQueryBranch && value is not null)
            {
                LoadSelectedCompoundQueryBranch(value);
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne SavedQueries.
""",
)

replace_once(
    main_vm,
    """    private QueryDefinition BuildQueryDefinition()
    {
        QueryDefinition query = new()
""",
    """    private QueryDefinition BuildQueryDefinition()
    {
        QueryDefinition visibleBranch = BuildVisibleBranchDefinition();
        if (_compoundQueryTemplate is null || SelectedCompoundQueryBranch is null)
        {
            return visibleBranch;
        }

        CopyEditableBranchState(SelectedCompoundQueryBranch.Query, visibleBranch);
        QueryDefinition completeQuery = QueryDefinitionCloner.Clone(_compoundQueryTemplate);
        completeQuery.Name = visibleBranch.Name;
        completeQuery.Description = visibleBranch.Description;
        return completeQuery;
    }

    /// <summary>
    /// Builds only the SELECT branch currently displayed by the visual controls.
    /// </summary>
    /// <returns>Editable state of the active SELECT branch.</returns>
    private QueryDefinition BuildVisibleBranchDefinition()
    {
        QueryDefinition query = new()
""",
)

replace_once(
    main_vm,
    """        ApplyCompoundQueryTemplate(query);
        return query;
""",
    """        return query;
""",
)

replace_regex_once(
    main_vm,
    r'''    /// <summary>
    /// Restores the non-visible branches and global clauses of the last imported compound query\.
    /// </summary>
    /// <param name="query">Current first branch rebuilt from the visual controls\.</param>
    private void ApplyCompoundQueryTemplate\(QueryDefinition query\)
    \{.*?
    \}

''',
    '''    /// <summary>
    /// Copies the fields editable in the visual builder into an existing compound-query node while
    /// preserving its nested set operations and compound-level clauses.
    /// </summary>
    private static void CopyEditableBranchState(QueryDefinition target, QueryDefinition source)
    {
        QueryDefinition copy = QueryDefinitionCloner.Clone(source);
        target.BaseTable = copy.BaseTable;
        target.Distinct = copy.Distinct;
        target.SelectedColumns = copy.SelectedColumns;
        target.TableAliases = copy.TableAliases;
        target.Joins = copy.Joins;
        target.Filters = copy.Filters;
        target.GroupBy = copy.GroupBy;
        target.OrderBy = copy.OrderBy;
        target.Aggregates = copy.Aggregates;
        target.CustomColumns = copy.CustomColumns;
        target.Parameters = copy.Parameters;
        target.DisabledAutoJoinKeys = copy.DisabledAutoJoinKeys;
        target.LimitRows = copy.LimitRows;
    }

    /// <summary>
    /// Rebuilds the flattened branch selector from the live compound-query tree.
    /// </summary>
    private void RebuildCompoundQueryBranches()
    {
        CompoundQueryBranches.Clear();
        if (_compoundQueryTemplate is null)
        {
            OnPropertyChanged(nameof(HasCompoundQueryBranches));
            OnPropertyChanged(nameof(CompoundQueryBranchSummary));
            return;
        }

        AddCompoundBranchItem(_compoundQueryTemplate, "1", 0, null);
        for (int index = 0; index < _compoundQueryTemplate.SetOperations.Count; index++)
        {
            SetOperationDefinition operation = _compoundQueryTemplate.SetOperations[index];
            AddCompoundBranchItem(operation.Query, (index + 2).ToString(), 0, operation);
        }

        OnPropertyChanged(nameof(HasCompoundQueryBranches));
        OnPropertyChanged(nameof(CompoundQueryBranchSummary));
    }

    private void AddCompoundBranchItem(
        QueryDefinition query,
        string path,
        int depth,
        SetOperationDefinition? incomingOperation)
    {
        CompoundQueryBranches.Add(new CompoundQueryBranchItemViewModel
        {
            Path = path,
            Depth = depth,
            Operator = incomingOperation?.Operator,
            All = incomingOperation?.All == true,
            Query = query
        });

        for (int index = 0; index < query.SetOperations.Count; index++)
        {
            SetOperationDefinition operation = query.SetOperations[index];
            AddCompoundBranchItem(operation.Query, $"{path}.{index + 1}", depth + 1, operation);
        }
    }

    private void LoadSelectedCompoundQueryBranch(CompoundQueryBranchItemViewModel branch)
    {
        _suppressAutoGenerate = true;
        try
        {
            LoadQueryBranchControls(branch.Query);
        }
        finally
        {
            _suppressAutoGenerate = false;
        }

        GenerateSql();
        Status = $"Édition de {branch.DisplayName}. Les autres branches restent intégrées à la requête générée.";
    }

''',
)

replace_once(
    main_vm,
    """    private void ClearQuery()
    {
        _compoundQueryTemplate = null;
        _suppressAutoGenerate = true;
""",
    """    private void ClearQuery()
    {
        _compoundQueryTemplate = null;
        _isSwitchingCompoundQueryBranch = true;
        try
        {
            CompoundQueryBranches.Clear();
            SetProperty(ref _selectedCompoundQueryBranch, null, nameof(SelectedCompoundQueryBranch));
            OnPropertyChanged(nameof(HasCompoundQueryBranches));
            OnPropertyChanged(nameof(CompoundQueryBranchSummary));
        }
        finally
        {
            _isSwitchingCompoundQueryBranch = false;
        }

        _suppressAutoGenerate = true;
""",
)

load_method = r'''    /// <summary>
    /// Exécute le traitement LoadQueryDefinition.
    /// </summary>
    /// <param name="query">Paramètre query.</param>
    /// <param name="name">Paramètre name.</param>
    /// <param name="description">Paramètre description.</param>
    private void LoadQueryDefinition(QueryDefinition query, string? name, string? description)
    {
        _compoundQueryTemplate = query.SetOperations.Count == 0
            ? null
            : QueryDefinitionCloner.Clone(query);
        QueryDefinition branchToLoad = _compoundQueryTemplate ?? query;

        _suppressAutoGenerate = true;
        _isSwitchingCompoundQueryBranch = true;
        try
        {
            QueryName = name ?? query.Name ?? "requete_chargee";
            QueryDescription = description ?? query.Description ?? string.Empty;
            RebuildCompoundQueryBranches();
            CompoundQueryBranchItemViewModel? firstBranch = CompoundQueryBranches.FirstOrDefault();
            SetProperty(ref _selectedCompoundQueryBranch, firstBranch, nameof(SelectedCompoundQueryBranch));
            LoadQueryBranchControls(firstBranch?.Query ?? branchToLoad);
            OnPropertyChanged(nameof(CompoundQueryBranchSummary));
        }
        finally
        {
            _isSwitchingCompoundQueryBranch = false;
            _suppressAutoGenerate = false;
        }

        GenerateSql();
    }

    /// <summary>
    /// Loads one SELECT branch into the visual controls without replacing the compound-query tree.
    /// </summary>
    private void LoadQueryBranchControls(QueryDefinition query)
    {
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

        foreach (ColumnReference c in query.SelectedColumns)
        {
            SelectedColumns.Add(new SelectColumnRowViewModel
            {
                Table = c.Table,
                Column = c.Column,
                Alias = c.Alias ?? string.Empty,
                NullAllowed = c.NullAllowed,
                UseFixedLength = c.UseFixedLength,
                FixedLength = c.FixedLength
            });
        }

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

        foreach (ColumnReference g in query.GroupBy)
        {
            GroupBy.Add(new GroupByRowViewModel { Table = g.Table, Column = g.Column });
        }

        foreach (OrderByItem o in query.OrderBy)
        {
            OrderBy.Add(new OrderByRowViewModel
            {
                Table = o.Column?.Table ?? (o.FieldKind == QueryFieldKind.Aggregate ? "Agrégat" : "Calculé"),
                Column = o.Column?.Column ?? o.FieldAlias ?? string.Empty,
                FieldKind = o.FieldKind,
                FieldAlias = o.FieldAlias ?? string.Empty,
                Direction = o.Direction
            });
        }

        foreach (AggregateSelection a in query.Aggregates)
        {
            Aggregates.Add(new AggregateRowViewModel
            {
                Table = a.Column?.Table ?? string.Empty,
                Column = a.Column?.Column ?? string.Empty,
                Function = a.Function,
                Alias = a.Alias ?? string.Empty,
                Distinct = a.Distinct,
                ConditionTable = a.ConditionColumn?.Table ?? string.Empty,
                ConditionColumn = a.ConditionColumn?.Column ?? string.Empty,
                ConditionOperator = a.ConditionOperator ?? "=",
                ConditionValue = a.ConditionValue ?? string.Empty,
                ConditionSecondValue = a.ConditionSecondValue ?? string.Empty
            });
        }

        foreach (JoinDefinition j in query.Joins)
        {
            Joins.Add(CreateJoinRowViewModel(j));
        }

        foreach (CustomColumnSelection c in query.CustomColumns)
        {
            CustomColumns.Add(new CustomColumnRowViewModel
            {
                Alias = c.Alias ?? string.Empty,
                RawExpression = c.RawExpression ?? string.Empty,
                CaseTable = c.CaseColumn?.Table ?? string.Empty,
                CaseColumn = c.CaseColumn?.Column ?? string.Empty,
                CaseOperator = c.CaseOperator ?? "=",
                CaseCompareValue = c.CaseCompareValue ?? string.Empty,
                CaseThenValue = c.CaseThenValue ?? string.Empty,
                CaseElseValue = c.CaseElseValue ?? string.Empty
            });
        }

        foreach (QueryParameterDefinition p in query.Parameters)
        {
            Parameters.Add(new QueryParameterRowViewModel
            {
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                DefaultValue = p.DefaultValue ?? string.Empty,
                DeclaredType = p.DeclaredType ?? string.Empty,
                UseCognosPrompt = p.SourceKind == QueryParameterSourceKind.CognosPrompt,
                Required = p.Required
            });
        }
    }

'''
replace_regex_once(
    main_vm,
    r'''    /// <summary>
    /// Exécute le traitement LoadQueryDefinition\.
    /// </summary>
    /// <param name="query">Paramètre query\.</param>
    /// <param name="name">Paramètre name\.</param>
    /// <param name="description">Paramètre description\.</param>
    private void LoadQueryDefinition\(QueryDefinition query, string\? name, string\? description\)
    \{.*?
    \}

(?=    /// <summary>
    /// Exécute le traitement WireAutoGenerate\.)''',
    load_method,
)

# Add branch selector to the visual builder.
xaml = "src/SqlQueryGenerator.App/MainWindow.xaml"
replace_once(
    xaml,
    """                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                            </Grid.RowDefinitions>
""",
    """                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                            </Grid.RowDefinitions>
""",
)
replace_once(
    xaml,
    """                            <TabControl Grid.Row="1" Margin="4">
""",
    """                            <Border Grid.Row="1"
                                    Visibility="{Binding HasCompoundQueryBranches, Converter={StaticResource BoolToVisibility}}"
                                    BorderBrush="#60A5FA"
                                    BorderThickness="1"
                                    CornerRadius="6"
                                    Background="#EFF6FF"
                                    Padding="8"
                                    Margin="4,0,4,8">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="380" />
                                        <ColumnDefinition Width="*" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0"
                                               Text="Branche SELECT active"
                                               VerticalAlignment="Center"
                                               FontWeight="SemiBold"
                                               Margin="4" />
                                    <ComboBox Grid.Column="1"
                                              ItemsSource="{Binding CompoundQueryBranches}"
                                              SelectedItem="{Binding SelectedCompoundQueryBranch, Mode=TwoWay}"
                                              DisplayMemberPath="DisplayName"
                                              ToolTip="Choisis la branche SELECT à modifier. Les autres branches restent dans la requête composée." />
                                    <TextBlock Grid.Column="2"
                                               Text="{Binding CompoundQueryBranchSummary}"
                                               VerticalAlignment="Center"
                                               Foreground="#1E3A8A"
                                               TextWrapping="Wrap"
                                               Margin="12,4,4,4" />
                                </Grid>
                            </Border>

                            <TabControl Grid.Row="2" Margin="4">
""",
)

# Strengthen the workflow regression to switch and edit both SELECT branches.
tests = "tests/SqlQueryGenerator.Tests/MainViewModelWorkflowTests.cs"
replace_regex_once(
    tests,
    r'''    \[Fact\]
    public void ReverseImport_CompoundQuery_RemainsCompleteAfterEditingFirstBranch\(\)
    \{.*?
    \}

(?=    \[Fact\]
    public void ReverseImportFailure_)''',
    '''    [Fact]
    public void ReverseImport_CompoundQuery_AllowsEditingEverySelectBranch()
    {
        MainViewModel vm = CreateViewModelWithSchema();
        vm.RawSqlText = @"
            SELECT CUSTOMER.ID
            FROM CUSTOMER
            UNION ALL
            SELECT ORDERS.CUSTOMER_ID
            FROM ORDERS
            WHERE ORDERS.STATUS = :status
            ORDER BY ID
            ";

        vm.ReverseEngineerRawSqlCommand.Execute(null);

        Assert.True(vm.HasCompoundQueryBranches);
        Assert.Equal(2, vm.CompoundQueryBranches.Count);
        Assert.Equal("CUSTOMER", vm.BaseTable);
        Assert.Contains("UNION ALL", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);

        vm.SelectedCompoundQueryBranch = vm.CompoundQueryBranches[1];

        Assert.Equal("ORDERS", vm.BaseTable);
        SelectColumnRowViewModel secondColumn = Assert.Single(vm.SelectedColumns);
        Assert.Equal("CUSTOMER_ID", secondColumn.Column);
        Assert.Single(vm.Filters);
        secondColumn.Alias = "ORDER_CUSTOMER_KEY";

        vm.SelectedCompoundQueryBranch = vm.CompoundQueryBranches[0];
        SelectColumnRowViewModel firstColumn = Assert.Single(vm.SelectedColumns);
        firstColumn.Alias = "CUSTOMER_KEY";

        Assert.Contains("CUSTOMER.ID AS CUSTOMER_KEY", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDERS.CUSTOMER_ID AS ORDER_CUSTOMER_KEY", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNION ALL", vm.GeneratedSql, StringComparison.OrdinalIgnoreCase);

        vm.SelectedCompoundQueryBranch = vm.CompoundQueryBranches[1];
        Assert.Equal("ORDER_CUSTOMER_KEY", Assert.Single(vm.SelectedColumns).Alias);
    }

''',
)

# Update documentation to state that every branch is editable.
docs = "docs/V28_RAW_SQL_PRESETS_AND_REVERSE.md"
replace_once(
    docs,
    """Lorsqu'une requête composée est chargée dans le constructeur, la première branche reste affichée dans les contrôles visuels et les branches suivantes sont conservées dans le modèle. Toute régénération, sauvegarde, restauration d'historique ou réécriture réémet l'ensemble des branches.
""",
    """Lorsqu'une requête composée est chargée dans le constructeur, un sélecteur « Branche SELECT active » permet d'ouvrir et de modifier chaque branche, y compris les branches imbriquées. Toute régénération, sauvegarde, restauration d'historique ou réécriture réémet l'ensemble de l'arbre de requête.
""",
)

print("Compound branch editor patch applied.")
