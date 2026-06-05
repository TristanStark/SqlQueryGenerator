namespace SqlQueryGenerator.Core.Query;

/// <summary>
/// Creates deep copies of query definitions used by rewrite, history, and reverse workflows.
/// </summary>
public static class QueryDefinitionCloner
{
    /// <summary>
    /// Creates a deep copy of one query definition.
    /// </summary>
    /// <param name="source">Query to clone.</param>
    /// <returns>Independent deep copy.</returns>
    public static QueryDefinition Clone(QueryDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        QueryDefinition clone = new()
        {
            Name = source.Name,
            Description = source.Description,
            BaseTable = source.BaseTable,
            Distinct = source.Distinct,
            LimitRows = source.LimitRows
        };

        foreach (TableAliasDefinition alias in source.TableAliases)
        {
            clone.TableAliases.Add(new TableAliasDefinition
            {
                Table = alias.Table,
                Alias = alias.Alias
            });
        }

        foreach (ColumnReference column in source.SelectedColumns)
        {
            clone.SelectedColumns.Add(Clone(column));
        }

        foreach (JoinDefinition join in source.Joins)
        {
            JoinDefinition clonedJoin = new()
            {
                FromTable = join.FromTable,
                FromColumn = join.FromColumn,
                ToTable = join.ToTable,
                ToColumn = join.ToColumn,
                JoinType = join.JoinType,
                AutoInferred = join.AutoInferred
            };

            foreach (JoinColumnPair pair in join.AdditionalColumnPairs)
            {
                clonedJoin.AdditionalColumnPairs.Add(new JoinColumnPair
                {
                    FromColumn = pair.FromColumn,
                    ToColumn = pair.ToColumn,
                    Enabled = pair.Enabled
                });
            }

            clone.Joins.Add(clonedJoin);
        }

        foreach (FilterCondition filter in source.Filters)
        {
            clone.Filters.Add(new FilterCondition
            {
                Column = filter.Column is null ? null : Clone(filter.Column),
                FieldKind = filter.FieldKind,
                FieldAlias = filter.FieldAlias,
                Operator = filter.Operator,
                Value = filter.Value,
                SecondValue = filter.SecondValue,
                ValueKind = filter.ValueKind,
                Subquery = filter.Subquery is null ? null : Clone(filter.Subquery),
                RawSubquerySql = filter.RawSubquerySql,
                SubqueryName = filter.SubqueryName,
                Connector = filter.Connector
            });
        }

        foreach (ColumnReference groupBy in source.GroupBy)
        {
            clone.GroupBy.Add(Clone(groupBy));
        }

        foreach (OrderByItem orderBy in source.OrderBy)
        {
            clone.OrderBy.Add(new OrderByItem
            {
                Column = orderBy.Column is null ? null : Clone(orderBy.Column),
                FieldKind = orderBy.FieldKind,
                FieldAlias = orderBy.FieldAlias,
                Direction = orderBy.Direction
            });
        }

        foreach (AggregateSelection aggregate in source.Aggregates)
        {
            clone.Aggregates.Add(new AggregateSelection
            {
                Function = aggregate.Function,
                Column = aggregate.Column is null ? null : Clone(aggregate.Column),
                Distinct = aggregate.Distinct,
                Alias = aggregate.Alias,
                ConditionColumn = aggregate.ConditionColumn is null ? null : Clone(aggregate.ConditionColumn),
                ConditionOperator = aggregate.ConditionOperator,
                ConditionValue = aggregate.ConditionValue,
                ConditionSecondValue = aggregate.ConditionSecondValue
            });
        }

        foreach (CustomColumnSelection custom in source.CustomColumns)
        {
            clone.CustomColumns.Add(new CustomColumnSelection
            {
                Alias = custom.Alias,
                RawExpression = custom.RawExpression,
                CaseColumn = custom.CaseColumn is null ? null : Clone(custom.CaseColumn),
                CaseOperator = custom.CaseOperator,
                CaseCompareValue = custom.CaseCompareValue,
                CaseThenValue = custom.CaseThenValue,
                CaseElseValue = custom.CaseElseValue
            });
        }

        foreach (QueryParameterDefinition parameter in source.Parameters)
        {
            clone.Parameters.Add(new QueryParameterDefinition
            {
                Name = parameter.Name,
                Description = parameter.Description,
                DefaultValue = parameter.DefaultValue,
                DeclaredType = parameter.DeclaredType,
                RawExpression = parameter.RawExpression,
                SourceKind = parameter.SourceKind,
                Required = parameter.Required
            });
        }

        foreach (string key in source.DisabledAutoJoinKeys)
        {
            clone.DisabledAutoJoinKeys.Add(key);
        }

        return clone;
    }

    private static ColumnReference Clone(ColumnReference source)
    {
        return new ColumnReference
        {
            Table = source.Table,
            Column = source.Column,
            Alias = source.Alias
        };
    }
}
