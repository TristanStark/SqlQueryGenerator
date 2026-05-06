namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Liste les valeurs possibles de SqlDialect.
/// </summary>
public enum SqlDialect
{
    /// <summary>
    /// Valeur Generic de l'énumération.
    /// </summary>
    Generic,
    /// <summary>
    /// Valeur SQLite de l'énumération.
    /// </summary>
    SQLite,
    /// <summary>
    /// Valeur Oracle de l'énumération.
    /// </summary>
    Oracle
}

/// <summary>
/// Liste les valeurs possibles de JoinType.
/// </summary>
public enum JoinType
{
    /// <summary>
    /// Valeur Inner de l'énumération.
    /// </summary>
    Inner,
    /// <summary>
    /// Valeur Left de l'énumération.
    /// </summary>
    Left
}

/// <summary>
/// Liste les valeurs possibles de SortDirection.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Valeur Ascending de l'énumération.
    /// </summary>
    Ascending,
    /// <summary>
    /// Valeur Descending de l'énumération.
    /// </summary>
    Descending
}

/// <summary>
/// Liste les valeurs possibles de AggregateFunction.
/// </summary>
public enum AggregateFunction
{
    /// <summary>
    /// Valeur Count de l'énumération.
    /// </summary>
    Count,
    /// <summary>
    /// Valeur Sum de l'énumération.
    /// </summary>
    Sum,
    /// <summary>
    /// Valeur Average de l'énumération.
    /// </summary>
    Average,
    /// <summary>
    /// Valeur Minimum de l'énumération.
    /// </summary>
    Minimum,
    /// <summary>
    /// Valeur Maximum de l'énumération.
    /// </summary>
    Maximum
}

/// <summary>
/// Liste les valeurs possibles de LogicalConnector.
/// </summary>
public enum LogicalConnector
{
    /// <summary>
    /// Valeur And de l'énumération.
    /// </summary>
    And,
    /// <summary>
    /// Valeur Or de l'énumération.
    /// </summary>
    Or
}
