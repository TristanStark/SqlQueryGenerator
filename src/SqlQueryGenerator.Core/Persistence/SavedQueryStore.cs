using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlQueryGenerator.Core.Persistence;

public sealed class SavedQueryStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SavedQueryStore(string rootDirectory)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.CurrentDirectory, "saved_queries")
            : rootDirectory;
    }

    public string RootDirectory { get; }

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

    public SavedQueryDefinition Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        SavedQueryDefinition saved = JsonSerializer.Deserialize<SavedQueryDefinition>(stream, Options)
            ?? throw new InvalidOperationException("Le fichier de requête sauvegardée est vide ou invalide.");
        return saved;
    }

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

    public static string MakeSafeFileName(string name)
    {
        HashSet<char> invalid = [.. Path.GetInvalidFileNameChars()];
        char[] chars = [.. name.Trim().Select(c => invalid.Contains(c) ? '_' : c)];
        string cleaned = new(chars);
        return string.IsNullOrWhiteSpace(cleaned) ? "query" : cleaned;
    }
}
