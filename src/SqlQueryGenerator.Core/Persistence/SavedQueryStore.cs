using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Représente SavedQueryStore dans SQL Query Generator.
/// </summary>
public sealed class SavedQueryStore
{
    /// <summary>
    /// Exécute le traitement new.
    /// </summary>
    /// <param name="General">Paramètre General.</param>
    /// <returns>Résultat du traitement.</returns>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initialise une nouvelle instance de SavedQueryStore.
    /// </summary>
    /// <param name="rootDirectory">Paramètre rootDirectory.</param>
    public SavedQueryStore(string rootDirectory)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.CurrentDirectory, "saved_queries")
            : rootDirectory;
    }

    /// <summary>
    /// Stocke la valeur interne RootDirectory.
    /// </summary>
    /// <value>Valeur de RootDirectory.</value>
    public string RootDirectory { get; }

    /// <summary>
    /// Exécute le traitement LoadAll.
    /// </summary>
    /// <returns>Résultat du traitement.</returns>
    public IReadOnlyList<SavedQueryDefinition> LoadAll()
    {
        Directory.CreateDirectory(RootDirectory);
        List<SavedQueryDefinition> result = [];
        foreach (string? file in Directory.EnumerateFiles(RootDirectory, "*.sqlqg.json", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                SavedQueryDefinition item = Load(file);
                item.Name = string.IsNullOrWhiteSpace(item.Name) ? Path.GetFileNameWithoutExtension(file).Replace(".sqlqg", string.Empty) : item.Name;
                result.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A corrupted saved query must not prevent the application from starting.
            }
        }

        return result.OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Exécute le traitement Load.
    /// </summary>
    /// <param name="filePath">Paramètre filePath.</param>
    /// <returns>Résultat du traitement.</returns>
    public SavedQueryDefinition Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        SavedQueryDefinition saved = JsonSerializer.Deserialize<SavedQueryDefinition>(stream, Options)
            ?? throw new InvalidOperationException("Le fichier de requête sauvegardée est vide ou invalide.");
        return saved;
    }

    /// <summary>
    /// Exécute le traitement Save.
    /// </summary>
    /// <param name="saved">Paramètre saved.</param>
    /// <returns>Résultat du traitement.</returns>
    public string Save(SavedQueryDefinition saved)
    {
        if (string.IsNullOrWhiteSpace(saved.Name))
        {
            throw new InvalidOperationException("Impossible de sauvegarder une requête sans nom.");
        }

        Directory.CreateDirectory(RootDirectory);
        saved.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (saved.CreatedAtUtc == default)
        {
            saved.CreatedAtUtc = saved.UpdatedAtUtc;
        }

        string file = Path.Combine(RootDirectory, MakeSafeFileName(saved.Name) + ".sqlqg.json");
        using FileStream stream = File.Create(file);
        JsonSerializer.Serialize(stream, saved, Options);
        return file;
    }

    /// <summary>
    /// Exécute le traitement MakeSafeFileName.
    /// </summary>
    /// <param name="name">Paramètre name.</param>
    /// <returns>Résultat du traitement.</returns>
    public static string MakeSafeFileName(string name)
    {
        HashSet<char> invalid = [.. Path.GetInvalidFileNameChars()];
        char[] chars = [.. name.Trim().Select(c => invalid.Contains(c) ? '_' : c)];
        string cleaned = new(chars);
        return string.IsNullOrWhiteSpace(cleaned) ? "query" : cleaned;
    }
}
