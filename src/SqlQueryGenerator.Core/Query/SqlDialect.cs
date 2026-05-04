namespace SqlQueryGenerator.Core.Query;

public enum SqlDialect
{
    Generic,
    SQLite,
    Oracle
}

public enum JoinType
{
    Inner,
    Left
}

public enum SortDirection
{
    Ascending,
    Descending
}

public enum AggregateFunction
{
    Count,
    Sum,
    Average,
    Minimum,
    Maximum
}

public enum LogicalConnector
{
    And,
    Or
}
