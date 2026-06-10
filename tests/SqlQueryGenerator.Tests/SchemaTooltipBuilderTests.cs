using SqlQueryGenerator.App.Services;
using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Models;

namespace SqlQueryGenerator.Tests;

public sealed class SchemaTooltipBuilderTests
{
    [Fact]
    public void BuildColumnTooltip_WithCommentAndMetadata_IncludesDocumentationAndTechnicalInfo()
    {
        string tooltip = SchemaTooltipBuilder.BuildColumnTooltip(
            tableName: "APP.CLIENT",
            tableDisplayName: "CLIENT",
            columnName: "CLIENT_ID",
            dataType: "NUMBER(10)",
            comment: "Identifiant métier du client.",
            isNullable: false,
            isPrimaryKey: true,
            isDeclaredForeignKey: false,
            foreignKeySummary: "Relation probable vers CONTRAT.CLIENT_ID",
            indexSummary: "IX_CLIENT_ID",
            isUniqueIndexed: true);

        Assert.Contains("Colonne", tooltip);
        Assert.Contains("APP.CLIENT.CLIENT_ID", tooltip);
        Assert.Contains("Type : NUMBER(10)", tooltip);
        Assert.Contains("Nullable : Non", tooltip);
        Assert.Contains("PK", tooltip);
        Assert.Contains("FK probable", tooltip);
        Assert.Contains("Index unique", tooltip);
        Assert.Contains("Relation probable vers CONTRAT.CLIENT_ID", tooltip);
        Assert.Contains("IX_CLIENT_ID", tooltip);
        Assert.Contains("Identifiant métier du client.", tooltip);
    }

    [Fact]
    public void BuildColumnTooltip_WithoutComment_DoesNotCrashAndShowsFallback()
    {
        string tooltip = SchemaTooltipBuilder.BuildColumnTooltip(
            tableName: "CLIENT",
            tableDisplayName: "CLIENT",
            columnName: "NOM",
            dataType: "VARCHAR2(80)",
            comment: null,
            isNullable: true,
            isPrimaryKey: false,
            isDeclaredForeignKey: false,
            foreignKeySummary: null,
            indexSummary: null,
            isUniqueIndexed: false);

        Assert.Contains("CLIENT.NOM", tooltip);
        Assert.Contains("Type : VARCHAR2(80)", tooltip);
        Assert.Contains("Nullable : Oui", tooltip);
        Assert.Contains("Aucune documentation métier disponible.", tooltip);
    }

    [Fact]
    public void BuildTableTooltip_WithComment_IncludesCountsAndDocumentation()
    {
        string tooltip = SchemaTooltipBuilder.BuildTableTooltip(
            tableName: "APP.CLIENT",
            displayName: "CLIENT",
            comment: "Référentiel client.",
            totalColumnCount: 12,
            visibleColumnCount: 5,
            isView: false);

        Assert.Contains("Table", tooltip);
        Assert.Contains("APP.CLIENT", tooltip);
        Assert.Contains("CLIENT", tooltip);
        Assert.Contains("Colonnes visibles : 5/12", tooltip);
        Assert.Contains("Référentiel client.", tooltip);
    }

    [Fact]
    public void ColumnItemViewModel_TooltipText_IncludesColumnCommentAndMetadata()
    {
        ColumnDefinition column = new()
        {
            TableName = "CLIENT",
            Name = "EMAIL",
            DataType = "VARCHAR2(255)",
            Comment = "Adresse email principale.",
            IsNullable = true
        };

        ColumnItemViewModel vm = new(
            column,
            foreignKeySummary: null,
            indexSummary: "IX_CLIENT_EMAIL",
            isUniqueIndexed: false);

        Assert.True(vm.HasComment);
        Assert.Contains("CLIENT.EMAIL", vm.TooltipText);
        Assert.Contains("VARCHAR2(255)", vm.TooltipText);
        Assert.Contains("Adresse email principale.", vm.TooltipText);
        Assert.Contains("IX_CLIENT_EMAIL", vm.TooltipText);
    }

    [Fact]
    public void TableItemViewModel_TooltipText_IncludesTableComment()
    {
        TableDefinition table = new(
            name: "CLIENT",
            comment: "Référentiel client.");

        table.Columns.Add(new ColumnDefinition
        {
            TableName = "CLIENT",
            Name = "CLIENT_ID",
            DataType = "INTEGER"
        });

        TableItemViewModel vm = new(table);

        Assert.True(vm.HasComment);
        Assert.Contains("Table", vm.TooltipText);
        Assert.Contains("CLIENT", vm.TooltipText);
        Assert.Contains("Colonnes : 1", vm.TooltipText);
        Assert.Contains("Référentiel client.", vm.TooltipText);
    }
}
