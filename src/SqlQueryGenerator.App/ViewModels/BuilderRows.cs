using SqlQueryGenerator.App.Infrastructure;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.App.ViewModels;

public abstract class BuilderRowBase : ObservableObject
{
    private string _table = string.Empty;
    private string _column = string.Empty;

    public string Table
    {
        get => _table;
        set => SetProperty(ref _table, value);
    }

    public string Column
    {
        get => _column;
        set => SetProperty(ref _column, value);
    }

    public string Display => $"{Table}.{Column}";

    public ColumnReference ToColumnReference(string? alias = null) => new() { Table = Table, Column = Column, Alias = alias };
}

public sealed class SelectColumnRowViewModel : BuilderRowBase
{
    private string _alias = string.Empty;

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }
}

public sealed class FilterRowViewModel : BuilderRowBase
{
    private QueryFieldKind _fieldKind = QueryFieldKind.Column;
    private string _fieldAlias = string.Empty;
    private string _operator = "=";
    private string _value = string.Empty;
    private string _secondValue = string.Empty;
    private FilterValueKind _valueKind = FilterValueKind.Literal;
    private string _subqueryName = string.Empty;
    private LogicalConnector _connector = LogicalConnector.And;

    public SqlQueryGenerator.Core.Persistence.SavedQueryDefinition? SavedSubquery { get; set; }

    public QueryFieldKind FieldKind
    {
        get => _fieldKind;
        set => SetProperty(ref _fieldKind, value);
    }

    public string FieldAlias
    {
        get => _fieldAlias;
        set => SetProperty(ref _fieldAlias, value);
    }

    public string FieldDisplay => FieldKind == QueryFieldKind.Column ? Display : FieldAlias;

    public string Operator
    {
        get => _operator;
        set => SetProperty(ref _operator, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string SecondValue
    {
        get => _secondValue;
        set => SetProperty(ref _secondValue, value);
    }

    public FilterValueKind ValueKind
    {
        get => _valueKind;
        set => SetProperty(ref _valueKind, value);
    }

    public string SubqueryName
    {
        get => _subqueryName;
        set => SetProperty(ref _subqueryName, value);
    }

    public LogicalConnector Connector
    {
        get => _connector;
        set => SetProperty(ref _connector, value);
    }
}

public sealed class GroupByRowViewModel : BuilderRowBase
{
}

public sealed class OrderByRowViewModel : BuilderRowBase
{
    private QueryFieldKind _fieldKind = QueryFieldKind.Column;
    private string _fieldAlias = string.Empty;
    private SortDirection _direction = SortDirection.Ascending;

    public QueryFieldKind FieldKind
    {
        get => _fieldKind;
        set => SetProperty(ref _fieldKind, value);
    }

    public string FieldAlias
    {
        get => _fieldAlias;
        set => SetProperty(ref _fieldAlias, value);
    }

    public string FieldDisplay => FieldKind == QueryFieldKind.Column ? Display : FieldAlias;

    public SortDirection Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value);
    }
}

public sealed class AggregateRowViewModel : BuilderRowBase
{
    private AggregateFunction _function = AggregateFunction.Count;
    private string _alias = string.Empty;
    private bool _distinct;
    private string _conditionTable = string.Empty;
    private string _conditionColumn = string.Empty;
    private string _conditionOperator = "=";
    private string _conditionValue = string.Empty;
    private string _conditionSecondValue = string.Empty;

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

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }

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

    private bool ShouldRefreshAlias(AggregateFunction previousFunction)
    {
        if (string.IsNullOrWhiteSpace(Alias))
        {
            return true;
        }

        return string.Equals(Alias, BuildDefaultAlias(previousFunction, Column), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Alias, $"{Column}_agg", StringComparison.OrdinalIgnoreCase);
    }

    public bool Distinct
    {
        get => _distinct;
        set => SetProperty(ref _distinct, value);
    }

    public string ConditionTable
    {
        get => _conditionTable;
        set => SetProperty(ref _conditionTable, value);
    }

    public string ConditionColumn
    {
        get => _conditionColumn;
        set => SetProperty(ref _conditionColumn, value);
    }

    public string ConditionOperator
    {
        get => _conditionOperator;
        set => SetProperty(ref _conditionOperator, value);
    }

    public string ConditionValue
    {
        get => _conditionValue;
        set => SetProperty(ref _conditionValue, value);
    }

    public string ConditionSecondValue
    {
        get => _conditionSecondValue;
        set => SetProperty(ref _conditionSecondValue, value);
    }
}

public sealed class JoinColumnPairRowViewModel : ObservableObject
{
    private string _fromColumn = string.Empty;
    private string _toColumn = string.Empty;
    private bool _enabled = true;

    public string FromColumn { get => _fromColumn; set => SetProperty(ref _fromColumn, value); }
    public string ToColumn { get => _toColumn; set => SetProperty(ref _toColumn, value); }
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
}

public sealed class JoinRowViewModel : ObservableObject
{
    private string _fromTable = string.Empty;
    private string _fromColumn = string.Empty;
    private string _toTable = string.Empty;
    private string _toColumn = string.Empty;
    private JoinType _joinType = JoinType.Inner;

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

    public string FromTable { get => _fromTable; set => SetProperty(ref _fromTable, value); }
    public string FromColumn { get => _fromColumn; set => SetProperty(ref _fromColumn, value); }
    public string ToTable { get => _toTable; set => SetProperty(ref _toTable, value); }
    public string ToColumn { get => _toColumn; set => SetProperty(ref _toColumn, value); }
    public JoinType JoinType { get => _joinType; set => SetProperty(ref _joinType, value); }
    public System.Collections.ObjectModel.ObservableCollection<JoinColumnPairRowViewModel> AdditionalPairs { get; } = [];

    private void Pair_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AdditionalPairs));
    }
}

public sealed class CustomColumnRowViewModel : ObservableObject
{
    private string _alias = string.Empty;
    private string _rawExpression = string.Empty;
    private string _caseTable = string.Empty;
    private string _caseColumn = string.Empty;
    private string _caseOperator = "=";
    private string _caseCompareValue = string.Empty;
    private string _caseThenValue = string.Empty;
    private string _caseElseValue = string.Empty;

    public string Alias { get => _alias; set => SetProperty(ref _alias, value); }
    public string RawExpression { get => _rawExpression; set => SetProperty(ref _rawExpression, value); }
    public string CaseTable { get => _caseTable; set => SetProperty(ref _caseTable, value); }
    public string CaseColumn { get => _caseColumn; set => SetProperty(ref _caseColumn, value); }
    public string CaseOperator { get => _caseOperator; set => SetProperty(ref _caseOperator, value); }
    public string CaseCompareValue { get => _caseCompareValue; set => SetProperty(ref _caseCompareValue, value); }
    public string CaseThenValue { get => _caseThenValue; set => SetProperty(ref _caseThenValue, value); }
    public string CaseElseValue { get => _caseElseValue; set => SetProperty(ref _caseElseValue, value); }
}

public sealed class QueryParameterRowViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _defaultValue = string.Empty;
    private bool _required = true;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string DefaultValue { get => _defaultValue; set => SetProperty(ref _defaultValue, value); }
    public bool Required { get => _required; set => SetProperty(ref _required, value); }
}

public sealed class SavedQueryItemViewModel : ObservableObject
{
    private bool _isSubqueryCandidate;

    public SavedQueryItemViewModel(SqlQueryGenerator.Core.Persistence.SavedQueryDefinition saved)
    {
        Saved = saved;
        _isSubqueryCandidate = true;
    }

    public SqlQueryGenerator.Core.Persistence.SavedQueryDefinition Saved { get; }
    public string Name => Saved.Name;
    public string Description => Saved.Description ?? string.Empty;
    public string Parameters => string.Join(", ", Saved.Query.Parameters.Select(p => p.Placeholder));
    public string BaseTable => Saved.Query.BaseTable ?? string.Empty;
    public int SelectCount => Saved.Query.SelectedColumns.Count + Saved.Query.Aggregates.Count + Saved.Query.CustomColumns.Count;

    public bool IsSubqueryCandidate
    {
        get => _isSubqueryCandidate;
        set => SetProperty(ref _isSubqueryCandidate, value);
    }
}
