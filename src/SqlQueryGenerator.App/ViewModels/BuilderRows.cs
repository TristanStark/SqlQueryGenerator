using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.App.ViewModels;

/// <summary>
/// Représente BuilderRowBase dans SQL Query Generator.
/// </summary>
public abstract class BuilderRowBase : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  table.
    /// </summary>
    /// <value>Valeur de _table.</value>
    private string _table = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  column.
    /// </summary>
    /// <value>Valeur de _column.</value>
    private string _column = string.Empty;

    /// <summary>
    /// Stocke la valeur interne Table.
    /// </summary>
    /// <value>Valeur de Table.</value>
    public string Table
    {
        get => _table;
        set
        {
            if (SetProperty(ref _table, value))
            {
                OnPropertyChanged(nameof(TableDisplayName));
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    /// <summary>
    /// Obtient ou définit TableDisplayName.
    /// </summary>
    /// <value>Valeur de TableDisplayName.</value>
    public string TableDisplayName => SqlObjectDisplayName.Table(Table);

    /// <summary>
    /// Stocke la valeur interne Column.
    /// </summary>
    /// <value>Valeur de Column.</value>
    public string Column
    {
        get => _column;
        set
        {
            if (SetProperty(ref _column, value))
            {
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    /// <summary>
    /// Obtient ou définit Display.
    /// </summary>
    /// <value>Valeur de Display.</value>
    public string Display => SqlObjectDisplayName.QualifiedColumn(Table, Column);

    /// <summary>
    /// Exécute le traitement ToColumnReference.
    /// </summary>
    /// <param name="alias">Paramètre alias.</param>
    /// <returns>Résultat du traitement.</returns>
    public ColumnReference ToColumnReference(string? alias = null) => new() { Table = Table, Column = Column, Alias = alias };
}

/// <summary>
/// Représente SelectColumnRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class SelectColumnRowViewModel : BuilderRowBase
{
    /// <summary>
    /// Stocke la valeur interne  alias.
    /// </summary>
    /// <value>Valeur de _alias.</value>
    private string _alias = string.Empty;

    /// <summary>
    /// Stocke la valeur interne Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }
}

/// <summary>
/// Représente FilterRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class FilterRowViewModel : BuilderRowBase
{
    /// <summary>
    /// Stocke la valeur interne  fieldKind.
    /// </summary>
    /// <value>Valeur de _fieldKind.</value>
    private QueryFieldKind _fieldKind = QueryFieldKind.Column;
    /// <summary>
    /// Stocke la valeur interne  fieldAlias.
    /// </summary>
    /// <value>Valeur de _fieldAlias.</value>
    private string _fieldAlias = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  operator.
    /// </summary>
    /// <value>Valeur de _operator.</value>
    private string _operator = "=";
    /// <summary>
    /// Stocke la valeur interne  value.
    /// </summary>
    /// <value>Valeur de _value.</value>
    private string _value = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  secondValue.
    /// </summary>
    /// <value>Valeur de _secondValue.</value>
    private string _secondValue = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  valueKind.
    /// </summary>
    /// <value>Valeur de _valueKind.</value>
    private FilterValueKind _valueKind = FilterValueKind.Literal;
    /// <summary>
    /// Stocke la valeur interne  subqueryName.
    /// </summary>
    /// <value>Valeur de _subqueryName.</value>
    private string _subqueryName = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  connector.
    /// </summary>
    /// <value>Valeur de _connector.</value>
    private LogicalConnector _connector = LogicalConnector.And;

    /// <summary>
    /// Stocke la valeur interne SavedSubquery.
    /// </summary>
    /// <value>Valeur de SavedSubquery.</value>
    public SqlQueryGenerator.Core.Persistence.SavedQueryDefinition? SavedSubquery { get; set; }

    /// <summary>
    /// Stocke la valeur interne FieldKind.
    /// </summary>
    /// <value>Valeur de FieldKind.</value>
    public QueryFieldKind FieldKind
    {
        get => _fieldKind;
        set => SetProperty(ref _fieldKind, value);
    }

    /// <summary>
    /// Stocke la valeur interne FieldAlias.
    /// </summary>
    /// <value>Valeur de FieldAlias.</value>
    public string FieldAlias
    {
        get => _fieldAlias;
        set => SetProperty(ref _fieldAlias, value);
    }

    /// <summary>
    /// Obtient ou définit FieldDisplay.
    /// </summary>
    /// <value>Valeur de FieldDisplay.</value>
    public string FieldDisplay => FieldKind == QueryFieldKind.Column ? Display : FieldAlias;

    /// <summary>
    /// Stocke la valeur interne Operator.
    /// </summary>
    /// <value>Valeur de Operator.</value>
    public string Operator
    {
        get => _operator;
        set => SetProperty(ref _operator, value);
    }

    /// <summary>
    /// Stocke la valeur interne Value.
    /// </summary>
    /// <value>Valeur de Value.</value>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>
    /// Stocke la valeur interne SecondValue.
    /// </summary>
    /// <value>Valeur de SecondValue.</value>
    public string SecondValue
    {
        get => _secondValue;
        set => SetProperty(ref _secondValue, value);
    }

    /// <summary>
    /// Stocke la valeur interne ValueKind.
    /// </summary>
    /// <value>Valeur de ValueKind.</value>
    public FilterValueKind ValueKind
    {
        get => _valueKind;
        set => SetProperty(ref _valueKind, value);
    }

    /// <summary>
    /// Stocke la valeur interne SubqueryName.
    /// </summary>
    /// <value>Valeur de SubqueryName.</value>
    public string SubqueryName
    {
        get => _subqueryName;
        set => SetProperty(ref _subqueryName, value);
    }

    /// <summary>
    /// Stocke la valeur interne Connector.
    /// </summary>
    /// <value>Valeur de Connector.</value>
    public LogicalConnector Connector
    {
        get => _connector;
        set => SetProperty(ref _connector, value);
    }
}

/// <summary>
/// Représente GroupByRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class GroupByRowViewModel : BuilderRowBase
{
}

/// <summary>
/// Représente OrderByRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class OrderByRowViewModel : BuilderRowBase
{
    /// <summary>
    /// Stocke la valeur interne  fieldKind.
    /// </summary>
    /// <value>Valeur de _fieldKind.</value>
    private QueryFieldKind _fieldKind = QueryFieldKind.Column;
    /// <summary>
    /// Stocke la valeur interne  fieldAlias.
    /// </summary>
    /// <value>Valeur de _fieldAlias.</value>
    private string _fieldAlias = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  direction.
    /// </summary>
    /// <value>Valeur de _direction.</value>
    private SortDirection _direction = SortDirection.Ascending;

    /// <summary>
    /// Stocke la valeur interne FieldKind.
    /// </summary>
    /// <value>Valeur de FieldKind.</value>
    public QueryFieldKind FieldKind
    {
        get => _fieldKind;
        set => SetProperty(ref _fieldKind, value);
    }

    /// <summary>
    /// Stocke la valeur interne FieldAlias.
    /// </summary>
    /// <value>Valeur de FieldAlias.</value>
    public string FieldAlias
    {
        get => _fieldAlias;
        set => SetProperty(ref _fieldAlias, value);
    }

    /// <summary>
    /// Obtient ou définit FieldDisplay.
    /// </summary>
    /// <value>Valeur de FieldDisplay.</value>
    public string FieldDisplay => FieldKind == QueryFieldKind.Column ? Display : FieldAlias;

    /// <summary>
    /// Stocke la valeur interne Direction.
    /// </summary>
    /// <value>Valeur de Direction.</value>
    public SortDirection Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value);
    }
}

/// <summary>
/// Représente AggregateRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class AggregateRowViewModel : BuilderRowBase
{
    /// <summary>
    /// Stocke la valeur interne  function.
    /// </summary>
    /// <value>Valeur de _function.</value>
    private AggregateFunction _function = AggregateFunction.Count;
    /// <summary>
    /// Stocke la valeur interne  alias.
    /// </summary>
    /// <value>Valeur de _alias.</value>
    private string _alias = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  distinct.
    /// </summary>
    /// <value>Valeur de _distinct.</value>
    private bool _distinct;
    /// <summary>
    /// Stocke la valeur interne  conditionTable.
    /// </summary>
    /// <value>Valeur de _conditionTable.</value>
    private string _conditionTable = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  conditionColumn.
    /// </summary>
    /// <value>Valeur de _conditionColumn.</value>
    private string _conditionColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  conditionOperator.
    /// </summary>
    /// <value>Valeur de _conditionOperator.</value>
    private string _conditionOperator = "=";
    /// <summary>
    /// Stocke la valeur interne  conditionValue.
    /// </summary>
    /// <value>Valeur de _conditionValue.</value>
    private string _conditionValue = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  conditionSecondValue.
    /// </summary>
    /// <value>Valeur de _conditionSecondValue.</value>
    private string _conditionSecondValue = string.Empty;

    /// <summary>
    /// Stocke la valeur interne Function.
    /// </summary>
    /// <value>Valeur de Function.</value>
    public AggregateFunction Function
    {
        get => _function;
        set
        {
            AggregateFunction previousFunction = _function;
            if (SetProperty(ref _function, value) && ShouldRefreshAlias(previousFunction))
            {
                Alias = BuildDefaultAlias(_function, Column);
            }
        }
    }

    /// <summary>
    /// Stocke la valeur interne Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }

    /// <summary>
    /// Exécute le traitement BuildDefaultAlias.
    /// </summary>
    /// <param name="function">Paramètre function.</param>
    /// <param name="column">Paramètre column.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string BuildDefaultAlias(AggregateFunction function, string column)
    {
        string prefix = function switch
        {
            AggregateFunction.Count => "count",
            AggregateFunction.Sum => "sum",
            AggregateFunction.Average => "avg",
            AggregateFunction.Minimum => "min",
            AggregateFunction.Maximum => "max",
            _ => function.ToString().ToLowerInvariant()
        };

        string cleanColumn = string.IsNullOrWhiteSpace(column) ? "colonne" : column.Trim();
        return $"{prefix}_{cleanColumn}";
    }

    /// <summary>
    /// Exécute le traitement ShouldRefreshAlias.
    /// </summary>
    /// <param name="previousFunction">Paramètre previousFunction.</param>
    /// <returns>Résultat du traitement.</returns>
    private bool ShouldRefreshAlias(AggregateFunction previousFunction)
    {
        if (string.IsNullOrWhiteSpace(Alias))
        {
            return true;
        }

        return string.Equals(Alias, BuildDefaultAlias(previousFunction, Column), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Alias, $"{Column}_agg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stocke la valeur interne Distinct.
    /// </summary>
    /// <value>Valeur de Distinct.</value>
    public bool Distinct
    {
        get => _distinct;
        set => SetProperty(ref _distinct, value);
    }

    /// <summary>
    /// Stocke la valeur interne ConditionTable.
    /// </summary>
    /// <value>Valeur de ConditionTable.</value>
    public string ConditionTable
    {
        get => _conditionTable;
        set => SetProperty(ref _conditionTable, value);
    }

    /// <summary>
    /// Stocke la valeur interne ConditionColumn.
    /// </summary>
    /// <value>Valeur de ConditionColumn.</value>
    public string ConditionColumn
    {
        get => _conditionColumn;
        set => SetProperty(ref _conditionColumn, value);
    }

    /// <summary>
    /// Stocke la valeur interne ConditionOperator.
    /// </summary>
    /// <value>Valeur de ConditionOperator.</value>
    public string ConditionOperator
    {
        get => _conditionOperator;
        set => SetProperty(ref _conditionOperator, value);
    }

    /// <summary>
    /// Stocke la valeur interne ConditionValue.
    /// </summary>
    /// <value>Valeur de ConditionValue.</value>
    public string ConditionValue
    {
        get => _conditionValue;
        set => SetProperty(ref _conditionValue, value);
    }

    /// <summary>
    /// Stocke la valeur interne ConditionSecondValue.
    /// </summary>
    /// <value>Valeur de ConditionSecondValue.</value>
    public string ConditionSecondValue
    {
        get => _conditionSecondValue;
        set => SetProperty(ref _conditionSecondValue, value);
    }
}

/// <summary>
/// Représente JoinColumnPairRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class JoinColumnPairRowViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  fromColumn.
    /// </summary>
    /// <value>Valeur de _fromColumn.</value>
    private string _fromColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  toColumn.
    /// </summary>
    /// <value>Valeur de _toColumn.</value>
    private string _toColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  enabled.
    /// </summary>
    /// <value>Valeur de _enabled.</value>
    private bool _enabled = true;

    /// <summary>
    /// Obtient ou définit FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public string FromColumn { get => _fromColumn; set => SetProperty(ref _fromColumn, value); }
    /// <summary>
    /// Obtient ou définit ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public string ToColumn { get => _toColumn; set => SetProperty(ref _toColumn, value); }
    /// <summary>
    /// Obtient ou définit Enabled.
    /// </summary>
    /// <value>Valeur de Enabled.</value>
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
}

/// <summary>
/// Représente JoinRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class JoinRowViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  fromTable.
    /// </summary>
    /// <value>Valeur de _fromTable.</value>
    private string _fromTable = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  fromColumn.
    /// </summary>
    /// <value>Valeur de _fromColumn.</value>
    private string _fromColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  toTable.
    /// </summary>
    /// <value>Valeur de _toTable.</value>
    private string _toTable = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  toColumn.
    /// </summary>
    /// <value>Valeur de _toColumn.</value>
    private string _toColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  joinType.
    /// </summary>
    /// <value>Valeur de _joinType.</value>
    private JoinType _joinType = JoinType.Inner;
    /// <summary>
    /// Stocke la valeur interne  columnNamesProvider.
    /// </summary>
    /// <value>Valeur de _columnNamesProvider.</value>
    private Func<string, IReadOnlyList<string>>? _columnNamesProvider;

    /// <summary>
    /// Initialise une nouvelle instance de JoinRowViewModel.
    /// </summary>
    public JoinRowViewModel()
    {
        AdditionalPairs.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (JoinColumnPairRowViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= Pair_PropertyChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (JoinColumnPairRowViewModel item in e.NewItems)
                {
                    item.PropertyChanged += Pair_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(AdditionalPairs));
        };
    }

    /// <summary>
    /// Stocke la valeur interne FromTable.
    /// </summary>
    /// <value>Valeur de FromTable.</value>
    public string FromTable
    {
        get => _fromTable;
        set
        {
            if (SetProperty(ref _fromTable, value))
            {
                OnPropertyChanged(nameof(FromTableDisplayName));
                OnPropertyChanged(nameof(FromColumnCandidates));
            }
        }
    }

    /// <summary>
    /// Obtient ou définit FromColumn.
    /// </summary>
    /// <value>Valeur de FromColumn.</value>
    public string FromColumn { get => _fromColumn; set => SetProperty(ref _fromColumn, value); }

    /// <summary>
    /// Stocke la valeur interne ToTable.
    /// </summary>
    /// <value>Valeur de ToTable.</value>
    public string ToTable
    {
        get => _toTable;
        set
        {
            if (SetProperty(ref _toTable, value))
            {
                OnPropertyChanged(nameof(ToTableDisplayName));
                OnPropertyChanged(nameof(ToColumnCandidates));
            }
        }
    }

    /// <summary>
    /// Obtient ou définit ToColumn.
    /// </summary>
    /// <value>Valeur de ToColumn.</value>
    public string ToColumn { get => _toColumn; set => SetProperty(ref _toColumn, value); }
    /// <summary>
    /// Obtient ou définit FromTableDisplayName.
    /// </summary>
    /// <value>Valeur de FromTableDisplayName.</value>
    public string FromTableDisplayName => SqlObjectDisplayName.Table(FromTable);
    /// <summary>
    /// Obtient ou définit ToTableDisplayName.
    /// </summary>
    /// <value>Valeur de ToTableDisplayName.</value>
    public string ToTableDisplayName => SqlObjectDisplayName.Table(ToTable);

    /// <summary>
    /// Stocke la valeur interne ColumnNamesProvider.
    /// </summary>
    /// <value>Valeur de ColumnNamesProvider.</value>
    public Func<string, IReadOnlyList<string>>? ColumnNamesProvider
    {
        get => _columnNamesProvider;
        set
        {
            _columnNamesProvider = value;
            OnPropertyChanged(nameof(FromColumnCandidates));
            OnPropertyChanged(nameof(ToColumnCandidates));
        }
    }

    /// <summary>
    /// Obtient ou définit FromColumnCandidates.
    /// </summary>
    /// <value>Valeur de FromColumnCandidates.</value>
    public IReadOnlyList<string> FromColumnCandidates => ColumnNamesProvider?.Invoke(FromTable) ?? Array.Empty<string>();
    /// <summary>
    /// Obtient ou définit ToColumnCandidates.
    /// </summary>
    /// <value>Valeur de ToColumnCandidates.</value>
    public IReadOnlyList<string> ToColumnCandidates => ColumnNamesProvider?.Invoke(ToTable) ?? Array.Empty<string>();

    /// <summary>
    /// Obtient ou définit JoinType.
    /// </summary>
    /// <value>Valeur de JoinType.</value>
    public JoinType JoinType { get => _joinType; set => SetProperty(ref _joinType, value); }
    /// <summary>
    /// Stocke la valeur interne AdditionalPairs.
    /// </summary>
    /// <value>Valeur de AdditionalPairs.</value>
    public System.Collections.ObjectModel.ObservableCollection<JoinColumnPairRowViewModel> AdditionalPairs { get; } = [];

    /// <summary>
    /// Exécute le traitement Pair PropertyChanged.
    /// </summary>
    /// <param name="sender">Paramètre sender.</param>
    /// <param name="e">Paramètre e.</param>
    private void Pair_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AdditionalPairs));
    }
}

/// <summary>
/// Représente CustomColumnRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class CustomColumnRowViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  alias.
    /// </summary>
    /// <value>Valeur de _alias.</value>
    private string _alias = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  rawExpression.
    /// </summary>
    /// <value>Valeur de _rawExpression.</value>
    private string _rawExpression = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  caseTable.
    /// </summary>
    /// <value>Valeur de _caseTable.</value>
    private string _caseTable = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  caseColumn.
    /// </summary>
    /// <value>Valeur de _caseColumn.</value>
    private string _caseColumn = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  caseOperator.
    /// </summary>
    /// <value>Valeur de _caseOperator.</value>
    private string _caseOperator = "=";
    /// <summary>
    /// Stocke la valeur interne  caseCompareValue.
    /// </summary>
    /// <value>Valeur de _caseCompareValue.</value>
    private string _caseCompareValue = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  caseThenValue.
    /// </summary>
    /// <value>Valeur de _caseThenValue.</value>
    private string _caseThenValue = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  caseElseValue.
    /// </summary>
    /// <value>Valeur de _caseElseValue.</value>
    private string _caseElseValue = string.Empty;

    /// <summary>
    /// Obtient ou définit Alias.
    /// </summary>
    /// <value>Valeur de Alias.</value>
    public string Alias { get => _alias; set => SetProperty(ref _alias, value); }
    /// <summary>
    /// Obtient ou définit RawExpression.
    /// </summary>
    /// <value>Valeur de RawExpression.</value>
    public string RawExpression { get => _rawExpression; set => SetProperty(ref _rawExpression, value); }
    /// <summary>
    /// Obtient ou définit CaseTable.
    /// </summary>
    /// <value>Valeur de CaseTable.</value>
    public string CaseTable { get => _caseTable; set => SetProperty(ref _caseTable, value); }
    /// <summary>
    /// Obtient ou définit CaseColumn.
    /// </summary>
    /// <value>Valeur de CaseColumn.</value>
    public string CaseColumn { get => _caseColumn; set => SetProperty(ref _caseColumn, value); }
    /// <summary>
    /// Obtient ou définit CaseOperator.
    /// </summary>
    /// <value>Valeur de CaseOperator.</value>
    public string CaseOperator { get => _caseOperator; set => SetProperty(ref _caseOperator, value); }
    /// <summary>
    /// Obtient ou définit CaseCompareValue.
    /// </summary>
    /// <value>Valeur de CaseCompareValue.</value>
    public string CaseCompareValue { get => _caseCompareValue; set => SetProperty(ref _caseCompareValue, value); }
    /// <summary>
    /// Obtient ou définit CaseThenValue.
    /// </summary>
    /// <value>Valeur de CaseThenValue.</value>
    public string CaseThenValue { get => _caseThenValue; set => SetProperty(ref _caseThenValue, value); }
    /// <summary>
    /// Obtient ou définit CaseElseValue.
    /// </summary>
    /// <value>Valeur de CaseElseValue.</value>
    public string CaseElseValue { get => _caseElseValue; set => SetProperty(ref _caseElseValue, value); }
}

/// <summary>
/// Représente QueryParameterRowViewModel dans SQL Query Generator.
/// </summary>
public sealed class QueryParameterRowViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  name.
    /// </summary>
    /// <value>Valeur de _name.</value>
    private string _name = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  description.
    /// </summary>
    /// <value>Valeur de _description.</value>
    private string _description = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  defaultValue.
    /// </summary>
    /// <value>Valeur de _defaultValue.</value>
    private string _defaultValue = string.Empty;
    /// <summary>
    /// Stocke la valeur interne  required.
    /// </summary>
    /// <value>Valeur de _required.</value>
    private bool _required = true;

    /// <summary>
    /// Obtient ou définit Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    /// <summary>
    /// Obtient ou définit Description.
    /// </summary>
    /// <value>Valeur de Description.</value>
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    /// <summary>
    /// Obtient ou définit DefaultValue.
    /// </summary>
    /// <value>Valeur de DefaultValue.</value>
    public string DefaultValue { get => _defaultValue; set => SetProperty(ref _defaultValue, value); }
    /// <summary>
    /// Obtient ou définit Required.
    /// </summary>
    /// <value>Valeur de Required.</value>
    public bool Required { get => _required; set => SetProperty(ref _required, value); }
}

/// <summary>
/// Représente SavedQueryItemViewModel dans SQL Query Generator.
/// </summary>
public sealed class SavedQueryItemViewModel : ObservableObject
{
    /// <summary>
    /// Stocke la valeur interne  isSubqueryCandidate.
    /// </summary>
    /// <value>Valeur de _isSubqueryCandidate.</value>
    private bool _isSubqueryCandidate;

    /// <summary>
    /// Initialise une nouvelle instance de SavedQueryItemViewModel.
    /// </summary>
    /// <param name="saved">Paramètre saved.</param>
    public SavedQueryItemViewModel(SqlQueryGenerator.Core.Persistence.SavedQueryDefinition saved)
    {
        Saved = saved;
        _isSubqueryCandidate = true;
    }

    /// <summary>
    /// Stocke la valeur interne Saved.
    /// </summary>
    /// <value>Valeur de Saved.</value>
    public SqlQueryGenerator.Core.Persistence.SavedQueryDefinition Saved { get; }
    /// <summary>
    /// Obtient ou définit Name.
    /// </summary>
    /// <value>Valeur de Name.</value>
    public string Name => Saved.Name;
    /// <summary>
    /// Obtient ou définit Description.
    /// </summary>
    /// <value>Valeur de Description.</value>
    public string Description => Saved.Description ?? string.Empty;
    /// <summary>
    /// Obtient ou définit Parameters.
    /// </summary>
    /// <value>Valeur de Parameters.</value>
    public string Parameters => string.Join(", ", Saved.Query.Parameters.Select(p => p.Placeholder));
    /// <summary>
    /// Obtient ou définit BaseTable.
    /// </summary>
    /// <value>Valeur de BaseTable.</value>
    public string BaseTable => Saved.Query.BaseTable ?? string.Empty;
    /// <summary>
    /// Obtient ou définit BaseTableDisplayName.
    /// </summary>
    /// <value>Valeur de BaseTableDisplayName.</value>
    public string BaseTableDisplayName => SqlObjectDisplayName.Table(BaseTable);
    /// <summary>
    /// Obtient ou définit SelectCount.
    /// </summary>
    /// <value>Valeur de SelectCount.</value>
    public int SelectCount => Saved.Query.SelectedColumns.Count + Saved.Query.Aggregates.Count + Saved.Query.CustomColumns.Count;

    /// <summary>
    /// Stocke la valeur interne IsSubqueryCandidate.
    /// </summary>
    /// <value>Valeur de IsSubqueryCandidate.</value>
    public bool IsSubqueryCandidate
    {
        get => _isSubqueryCandidate;
        set => SetProperty(ref _isSubqueryCandidate, value);
    }
}
