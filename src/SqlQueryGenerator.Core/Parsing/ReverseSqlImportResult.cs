using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Core.Parsing;

/// <summary>
/// Supported source SQL dialect profiles for reverse import and rewrite.
/// </summary>
public enum SourceSqlDialect
{
    GenericSql,
    OracleLegacy,
    OracleModern,
    Db2,
    SQLite,
    PostgreSql,
    SqlServer,
    MySqlMariaDb,
    CognosAnalytics
}

/// <summary>
/// Status of one clause in the reverse SQL coverage report.
/// </summary>
public enum ReverseSqlCoverageStatus
{
    NotPresent,
    FullyImported,
    PartiallyImported,
    Unsupported,
    Ignored,
    Unknown
}

/// <summary>
/// Severity attached to one reverse SQL diagnostic message.
/// </summary>
public enum ReverseSqlDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// One clause-level coverage entry produced after reverse import.
/// </summary>
public sealed record ReverseSqlClauseCoverage
{
    public required string Clause { get; init; }

    public ReverseSqlCoverageStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Structured diagnostics produced during reverse import.
/// </summary>
public sealed record ReverseSqlDiagnostic
{
    public ReverseSqlDiagnosticSeverity Severity { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Clause { get; init; }

    public string? Fragment { get; init; }

    public int? StartOffset { get; init; }

    public int? Length { get; init; }

    public int? Line { get; init; }

    public int? Column { get; init; }

    public string? SuggestedFix { get; init; }
}

/// <summary>
/// Structured reverse SQL coverage report.
/// </summary>
public sealed class ReverseSqlCoverageReport
{
    public IReadOnlyList<ReverseSqlClauseCoverage> Clauses { get; init; } = Array.Empty<ReverseSqlClauseCoverage>();

    public IReadOnlyList<string> RiskyFragments { get; init; } = Array.Empty<string>();

    public double Confidence { get; init; }
}

/// <summary>
/// Exception thrown when reverse SQL import fails with a structured diagnostic.
/// </summary>
public sealed class ReverseSqlImportException : InvalidOperationException
{
    public ReverseSqlImportException(string message, ReverseSqlDiagnostic diagnostic, Exception? innerException = null)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }

    public ReverseSqlDiagnostic Diagnostic { get; }
}

/// <summary>
/// Represents the result of importing raw SQL into the internal query model.
/// </summary>
public sealed class ReverseSqlImportResult
{
    /// <summary>
    /// Gets or sets the imported query model.
    /// </summary>
    /// <value>Parsed query definition.</value>
    public QueryDefinition Query { get; init; } = new();

    /// <summary>
    /// Gets or sets the source dialect profile used for import.
    /// </summary>
    /// <value>Selected source dialect.</value>
    public SourceSqlDialect SourceDialect { get; init; } = SourceSqlDialect.GenericSql;

    /// <summary>
    /// Gets or sets the normalized SQL text after optional preprocessing.
    /// </summary>
    /// <value>Normalized SQL consumed by the reverse parser.</value>
    public string NormalizedSql { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets warnings collected during import.
    /// </summary>
    /// <value>Conservative warnings about partial support or risky constructs.</value>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets structured diagnostics collected during import.
    /// </summary>
    /// <value>Info/warning diagnostics about preprocessing, partial support or failure context.</value>
    public IReadOnlyList<ReverseSqlDiagnostic> Diagnostics { get; init; } = Array.Empty<ReverseSqlDiagnostic>();

    /// <summary>
    /// Gets or sets the clause-level coverage report.
    /// </summary>
    /// <value>Coverage report and confidence score for the imported SQL.</value>
    public ReverseSqlCoverageReport Coverage { get; init; } = new();
}
