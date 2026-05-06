using SqlQueryGenerator.Core.Models;
using System.Text;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Représente SchemaDocumentationImporter dans SQL Query Generator.
/// </summary>
public sealed class SchemaDocumentationImporter
{
    /// <summary>
    /// Stocke la valeur interne TableHeaders.
    /// </summary>
    /// <value>Valeur de TableHeaders.</value>
    private static readonly string[] TableHeaders = ["table", "table_name", "nom_table", "table_physique", "objet", "object", "object_name"];
    /// <summary>
    /// Stocke la valeur interne ColumnHeaders.
    /// </summary>
    /// <value>Valeur de ColumnHeaders.</value>
    private static readonly string[] ColumnHeaders = ["column", "column_name", "nom_colonne", "champ", "field"];
    /// <summary>
    /// Stocke la valeur interne DisplayHeaders.
    /// </summary>
    /// <value>Valeur de DisplayHeaders.</value>
    private static readonly string[] DisplayHeaders = ["display", "display_name", "nom_fonctionnel", "libelle", "label", "meaning", "signification"];
    /// <summary>
    /// Stocke la valeur interne DescriptionHeaders.
    /// </summary>
    /// <value>Valeur de DescriptionHeaders.</value>
    private static readonly string[] DescriptionHeaders = ["description", "comment", "commentaire", "definition", "définition", "details", "notes"];

    /// <summary>
    /// Exécute le traitement Apply.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
    public SchemaDocumentationImportResult Apply(DatabaseSchema schema, string text)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SchemaDocumentationImportResult(0, 0, ["Documentation vide."]);
        }

        List<Dictionary<string, string>> rows = ParseRows(text);
        if (rows.Count == 0)
        {
            return new SchemaDocumentationImportResult(0, 0, ["Aucune ligne de documentation exploitable."]);
        }

        int tableUpdated = 0;
        int columnUpdated = 0;
        List<string> warnings = [];

        foreach (Dictionary<string, string> row in rows)
        {
            string? tableName = Get(row, TableHeaders);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            TableDefinition? table = schema.FindTable(tableName);
            if (table is null)
            {
                warnings.Add($"Table documentée introuvable dans le schéma: {tableName}.");
                continue;
            }

            string? columnName = Get(row, ColumnHeaders);
            string? display = Get(row, DisplayHeaders);
            string? description = Get(row, DescriptionHeaders);
            string merged = MergeDocumentation(display, description);
            if (string.IsNullOrWhiteSpace(merged))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(columnName))
            {
                table.Comment = merged;
                tableUpdated++;
                continue;
            }

            int index = table.Columns.ToList().FindIndex(c => SqlNameNormalizer.EqualsName(c.Name, columnName));
            if (index < 0)
            {
                warnings.Add($"Colonne documentée introuvable: {table.FullName}.{columnName}.");
                continue;
            }

            table.Columns[index] = table.Columns[index] with { Comment = merged };
            columnUpdated++;
        }

        return new SchemaDocumentationImportResult(tableUpdated, columnUpdated, warnings);
    }

    /// <summary>
    /// Exécute le traitement ApplyFromFile.
    /// </summary>
    /// <param name="schema">Paramètre schema.</param>
    /// <param name="filePath">Paramètre filePath.</param>
    /// <returns>Résultat du traitement.</returns>
    public SchemaDocumentationImportResult ApplyFromFile(DatabaseSchema schema, string filePath)
    {
        string text = File.ReadAllText(filePath, Encoding.UTF8);
        return Apply(schema, text);
    }

    /// <summary>
    /// Exécute le traitement MergeDocumentation.
    /// </summary>
    /// <param name="display">Paramètre display.</param>
    /// <param name="description">Paramètre description.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string MergeDocumentation(string? display, string? description)
    {
        display = display?.Trim();
        description = description?.Trim();
        if (string.IsNullOrWhiteSpace(display))
        {
            return description ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(description) || string.Equals(display, description, StringComparison.OrdinalIgnoreCase))
        {
            return display;
        }

        return $"{display} — {description}";
    }

    /// <summary>
    /// Exécute le traitement Get.
    /// </summary>
    /// <param name="row">Paramètre row.</param>
    /// <param name="names">Paramètre names.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string? Get(IReadOnlyDictionary<string, string> row, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            if (row.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Exécute le traitement ParseRows.
    /// </summary>
    /// <param name="text">Paramètre text.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<Dictionary<string, string>> ParseRows(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        if (lines.Length == 0)
        {
            return [];
        }

        char delimiter = DetectDelimiter(lines[0]);
        string[] headers = SplitLine(lines[0], delimiter)
            .Select(h => NormalizeHeader(h))
            .ToArray();

        List<Dictionary<string, string>> rows = [];
        for (int i = 1; i < lines.Length; i++)
        {
            List<string> values = SplitLine(lines[i], delimiter);
            Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < headers.Length && j < values.Count; j++)
            {
                if (!string.IsNullOrWhiteSpace(headers[j]))
                {
                    row[headers[j]] = values[j].Trim();
                }
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Exécute le traitement DetectDelimiter.
    /// </summary>
    /// <param name="headerLine">Paramètre headerLine.</param>
    /// <returns>Résultat du traitement.</returns>
    private static char DetectDelimiter(string headerLine)
    {
        char[] candidates = new[] { '\t', ';', ',', '|' };
        return candidates
            .OrderByDescending(c => headerLine.Count(ch => ch == c))
            .First();
    }

    /// <summary>
    /// Exécute le traitement NormalizeHeader.
    /// </summary>
    /// <param name="header">Paramètre header.</param>
    /// <returns>Résultat du traitement.</returns>
    private static string NormalizeHeader(string header)
    {
        return header.Trim().Trim('\uFEFF').ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');
    }

    /// <summary>
    /// Exécute le traitement SplitLine.
    /// </summary>
    /// <param name="line">Paramètre line.</param>
    /// <param name="delimiter">Paramètre delimiter.</param>
    /// <returns>Résultat du traitement.</returns>
    private static List<string> SplitLine(string line, char delimiter)
    {
        List<string> result = [];
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }
}

/// <summary>
/// Représente SchemaDocumentationImportResult dans SQL Query Generator.
/// </summary>
public sealed record SchemaDocumentationImportResult(int TablesUpdated, int ColumnsUpdated, IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Exécute le traitement ToString.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    public override string ToString()
    {
        string message = $"Documentation importée: {TablesUpdated} tables, {ColumnsUpdated} colonnes mises à jour.";
        return Warnings.Count == 0 ? message : message + Environment.NewLine + string.Join(Environment.NewLine, Warnings.Take(20));
    }
}
