using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlQueryGenerator.Core.Generation;
using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Query;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Fixture-driven regression coverage for reverse SQL and reverse-then-rewrite behavior.
/// </summary>
public sealed class ReverseSqlRegressionTests
{
    /// <summary>
    /// Executes every SQL fixture found under Fixtures/ReverseSql.
    /// </summary>
    /// <param name="caseName">Fixture case name.</param>
    [Theory]
    [MemberData(nameof(GetCaseNames))]
    public void ReverseSqlCorpus_MatchesExpectedFixtures(string caseName)
    {
        ReverseSqlFixture fixture = ReverseSqlFixture.Load(caseName);

        if (fixture.ExpectedError is not null)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new SqlSelectReverseParser().Parse(fixture.InputSql));
            Assert.Equal(fixture.ExpectedError, NormalizeSingleLine(error.Message));
            return;
        }

        QueryDefinition query = new SqlSelectReverseParser().Parse(fixture.InputSql);

        if (fixture.ExpectedModelJson is not null)
        {
            string actualModelJson = NormalizeJson(JsonSerializer.Serialize(ReverseSqlFixtureModel.FromQuery(query), ReverseSqlFixture.SerializerOptions));
            Assert.Equal(fixture.ExpectedModelJson, actualModelJson);
        }

        if (fixture.ExpectedSql is null && fixture.ExpectedWarnings is null)
        {
            return;
        }

        DatabaseSchema schema = fixture.SchemaSql is null
            ? new DatabaseSchema()
            : new SqlSchemaParser().Parse(fixture.SchemaSql);
        SqlGenerationResult result = new SqlQueryGeneratorEngine().Generate(query, schema);

        if (fixture.ExpectedSql is not null)
        {
            Assert.Equal(fixture.ExpectedSql, NormalizeSql(result.Sql));
        }

        if (fixture.ExpectedWarnings is not null)
        {
            Assert.Equal(fixture.ExpectedWarnings, NormalizeWarnings(result.Warnings));
        }
    }

    /// <summary>
    /// Enumerates corpus case names from the fixture folder.
    /// </summary>
    public static IEnumerable<object[]> GetCaseNames()
    {
        return ReverseSqlFixture.EnumerateCaseNames().Select(name => new object[] { name });
    }

    internal static string NormalizeSql(string sql)
    {
        StringBuilder sb = new();
        bool inString = false;
        bool previousWhitespace = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            if (c == '\'')
            {
                if (!previousWhitespace || sb.Length == 0 || sb[^1] != ' ')
                {
                    previousWhitespace = false;
                }

                sb.Append(c);
                if (inString && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    sb.Append(sql[i + 1]);
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString && char.IsWhiteSpace(c))
            {
                if (!previousWhitespace && sb.Length > 0)
                {
                    sb.Append(' ');
                    previousWhitespace = true;
                }

                continue;
            }

            sb.Append(c);
            previousWhitespace = false;
        }

        return sb.ToString().Trim();
    }

    internal static string NormalizeWarnings(IEnumerable<string> warnings)
    {
        return string.Join('\n', warnings
            .Select(line => line.Trim())
            .Where(line => line.Length > 0));
    }

    private static string NormalizeJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, ReverseSqlFixture.SerializerOptions);
    }

    internal static string NormalizeSingleLine(string text)
    {
        return string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

internal sealed class ReverseSqlFixture
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReverseSql");

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public required string CaseName { get; init; }

    public required string InputSql { get; init; }

    public string? SchemaSql { get; init; }

    public string? ExpectedSql { get; init; }

    public string? ExpectedWarnings { get; init; }

    public string? ExpectedModelJson { get; init; }

    public string? ExpectedError { get; init; }

    public static IReadOnlyList<string> EnumerateCaseNames()
    {
        return Directory.EnumerateFiles(FixtureRoot, "*.input.sql", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Where(name => name is not null)
            .Select(name => name![..^".input.sql".Length])
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ReverseSqlFixture Load(string caseName)
    {
        return new ReverseSqlFixture
        {
            CaseName = caseName,
            InputSql = ReadRequired(caseName, ".input.sql"),
            SchemaSql = ReadOptional(caseName, ".schema.sql"),
            ExpectedSql = ReadOptional(caseName, ".expected.sql", static content => ReverseSqlRegressionTests.NormalizeSql(content)),
            ExpectedWarnings = ReadOptional(caseName, ".expected-warnings.txt", static content => string.Join('\n', content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0))),
            ExpectedModelJson = ReadOptional(caseName, ".expected-model.json", static content =>
            {
                using JsonDocument document = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(document.RootElement, SerializerOptions);
            }),
            ExpectedError = ReadOptional(caseName, ".expected-error.txt", static content => ReverseSqlRegressionTests.NormalizeSingleLine(content))
        };
    }

    private static string ReadRequired(string caseName, string suffix)
    {
        string path = Path.Combine(FixtureRoot, caseName + suffix);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture file not found: {path}", path);
        }

        return File.ReadAllText(path);
    }

    private static string? ReadOptional(string caseName, string suffix, Func<string, string>? normalize = null)
    {
        string path = Path.Combine(FixtureRoot, caseName + suffix);
        if (!File.Exists(path))
        {
            return null;
        }

        string content = File.ReadAllText(path);
        return normalize is null ? content : normalize(content);
    }
}

internal sealed record ReverseSqlFixtureModel(
    string? WithClauseSql,
    string? BaseTable,
    bool Distinct,
    int? LimitRows,
    IReadOnlyList<ReverseSqlColumnModel> SelectedColumns,
    IReadOnlyList<ReverseSqlJoinModel> Joins,
    IReadOnlyList<ReverseSqlFilterModel> Filters,
    IReadOnlyList<ReverseSqlColumnModel> GroupBy,
    IReadOnlyList<ReverseSqlOrderByModel> OrderBy,
    IReadOnlyList<ReverseSqlAggregateModel> Aggregates,
    IReadOnlyList<ReverseSqlCustomColumnModel> CustomColumns,
    IReadOnlyList<ReverseSqlParameterModel> Parameters)
{
    public static ReverseSqlFixtureModel FromQuery(QueryDefinition query)
    {
        return new ReverseSqlFixtureModel(
            query.WithClauseSql,
            query.BaseTable,
            query.Distinct,
            query.LimitRows,
            query.SelectedColumns.Select(ReverseSqlColumnModel.FromColumn).ToArray(),
            query.Joins.Select(ReverseSqlJoinModel.FromJoin).ToArray(),
            query.Filters.Select(ReverseSqlFilterModel.FromFilter).ToArray(),
            query.GroupBy.Select(ReverseSqlColumnModel.FromColumn).ToArray(),
            query.OrderBy.Select(ReverseSqlOrderByModel.FromOrder).ToArray(),
            query.Aggregates.Select(ReverseSqlAggregateModel.FromAggregate).ToArray(),
            query.CustomColumns.Select(ReverseSqlCustomColumnModel.FromCustomColumn).ToArray(),
            query.Parameters.Select(ReverseSqlParameterModel.FromParameter).ToArray());
    }
}

internal sealed record ReverseSqlColumnModel(string Table, string Column, string? Alias)
{
    public static ReverseSqlColumnModel FromColumn(ColumnReference column)
    {
        return new ReverseSqlColumnModel(column.Table, column.Column, column.Alias);
    }
}

internal sealed record ReverseSqlJoinModel(
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn,
    string JoinType,
    IReadOnlyList<ReverseSqlJoinPairModel> AdditionalColumnPairs)
{
    public static ReverseSqlJoinModel FromJoin(JoinDefinition join)
    {
        return new ReverseSqlJoinModel(
            join.FromTable,
            join.FromColumn,
            join.ToTable,
            join.ToColumn,
            join.JoinType.ToString(),
            join.AdditionalColumnPairs
                .Select(pair => new ReverseSqlJoinPairModel(pair.FromColumn, pair.ToColumn, pair.Enabled))
                .ToArray());
    }
}

internal sealed record ReverseSqlJoinPairModel(string FromColumn, string ToColumn, bool Enabled);

internal sealed record ReverseSqlFilterModel(
    ReverseSqlColumnModel? Column,
    string FieldKind,
    string? FieldAlias,
    string Operator,
    string? Value,
    string? SecondValue,
    string ValueKind,
    string? RawSubquerySql,
    string? SubqueryName,
    string Connector)
{
    public static ReverseSqlFilterModel FromFilter(FilterCondition filter)
    {
        return new ReverseSqlFilterModel(
            filter.Column is null ? null : ReverseSqlColumnModel.FromColumn(filter.Column),
            filter.FieldKind.ToString(),
            filter.FieldAlias,
            filter.Operator,
            filter.Value,
            filter.SecondValue,
            filter.ValueKind.ToString(),
            filter.RawSubquerySql,
            filter.SubqueryName,
            filter.Connector.ToString());
    }
}

internal sealed record ReverseSqlOrderByModel(
    ReverseSqlColumnModel? Column,
    string FieldKind,
    string? FieldAlias,
    string Direction)
{
    public static ReverseSqlOrderByModel FromOrder(OrderByItem order)
    {
        return new ReverseSqlOrderByModel(
            order.Column is null ? null : ReverseSqlColumnModel.FromColumn(order.Column),
            order.FieldKind.ToString(),
            order.FieldAlias,
            order.Direction.ToString());
    }
}

internal sealed record ReverseSqlAggregateModel(
    string Function,
    ReverseSqlColumnModel? Column,
    bool Distinct,
    string? Alias,
    ReverseSqlColumnModel? ConditionColumn,
    string? ConditionOperator,
    string? ConditionValue,
    string? ConditionSecondValue)
{
    public static ReverseSqlAggregateModel FromAggregate(AggregateSelection aggregate)
    {
        return new ReverseSqlAggregateModel(
            aggregate.Function.ToString(),
            aggregate.Column is null ? null : ReverseSqlColumnModel.FromColumn(aggregate.Column),
            aggregate.Distinct,
            aggregate.Alias,
            aggregate.ConditionColumn is null ? null : ReverseSqlColumnModel.FromColumn(aggregate.ConditionColumn),
            aggregate.ConditionOperator,
            aggregate.ConditionValue,
            aggregate.ConditionSecondValue);
    }
}

internal sealed record ReverseSqlCustomColumnModel(string? Alias, string? RawExpression)
{
    public static ReverseSqlCustomColumnModel FromCustomColumn(CustomColumnSelection column)
    {
        return new ReverseSqlCustomColumnModel(column.Alias, column.RawExpression);
    }
}

internal sealed record ReverseSqlParameterModel(string Name, string Placeholder)
{
    public static ReverseSqlParameterModel FromParameter(QueryParameterDefinition parameter)
    {
        return new ReverseSqlParameterModel(parameter.Name, parameter.Placeholder);
    }
}
