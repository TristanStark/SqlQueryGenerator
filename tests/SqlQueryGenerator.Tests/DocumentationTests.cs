using SqlQueryGenerator.Core.Models;
using SqlQueryGenerator.Core.Parsing;
using SqlQueryGenerator.Core.Persistence;

namespace SqlQueryGenerator.Tests;

/// <summary>
/// Représente DocumentationTests dans SQL Query Generator.
/// </summary>
public sealed class DocumentationTests
{
    /// <summary>
    /// Exécute le traitement Import TableAndColumnDocumentation UpdatesSchemaComments.
    /// </summary>
    [Fact]
    public void Import_TableAndColumnDocumentation_UpdatesSchemaComments()
    {
        const string sql = @"
CREATE TABLE ACTI (
    ACTI_IDEN INTEGER,
    EMET_IDEN INTEGER
);
";
        const string doc = "table_name\tcolumn_name\tdisplay_name\tdescription\n" +
                           "ACTI\t\tActions\tTable des actions métier\n" +
                           "ACTI\tACTI_IDEN\tIdentifiant action\tClé technique de l'action\n";

        DatabaseSchema schema = new SqlSchemaParser().Parse(sql);
        SchemaDocumentationImportResult result = new SchemaDocumentationImporter().Apply(schema, doc);

        Assert.Equal(1, result.TablesUpdated);
        Assert.Equal(1, result.ColumnsUpdated);
        Assert.Equal("Actions — Table des actions métier", schema.FindTable("ACTI")!.Comment);
        Assert.Equal("Identifiant action — Clé technique de l'action", schema.FindColumn("ACTI", "ACTI_IDEN")!.Comment);
    }
}
