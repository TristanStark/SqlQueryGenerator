using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlQueryGenerator.Core.Persistence;

/// <summary>
/// Persists reusable output profiles as local JSON files.
/// </summary>
public sealed class OutputProfileStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new output profile store.
    /// </summary>
    /// <param name="rootDirectory">Directory where profile files are stored.</param>
    public OutputProfileStore(string rootDirectory)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.CurrentDirectory, "output_profiles")
            : rootDirectory;
    }

    /// <summary>
    /// Gets the root directory where profiles are stored.
    /// </summary>
    /// <value>Profile storage directory.</value>
    public string RootDirectory { get; }

    /// <summary>
    /// Loads every valid output profile from disk.
    /// </summary>
    /// <returns>Loaded profiles ordered by name.</returns>
    public IReadOnlyList<OutputProfileDefinition> LoadAll()
    {
        Directory.CreateDirectory(RootDirectory);
        List<OutputProfileDefinition> result = [];

        foreach (string file in Directory.EnumerateFiles(RootDirectory, "*.sqlqg.output-profile.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                OutputProfileDefinition item = Load(file);
                item.Name = string.IsNullOrWhiteSpace(item.Name)
                    ? Path.GetFileNameWithoutExtension(file).Replace(".sqlqg.output-profile", string.Empty, StringComparison.OrdinalIgnoreCase)
                    : item.Name;

                result.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A corrupted profile must not prevent the application from starting.
            }
        }

        return result.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Loads one output profile from disk.
    /// </summary>
    /// <param name="filePath">Profile JSON file path.</param>
    /// <returns>Loaded profile.</returns>
    public OutputProfileDefinition Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<OutputProfileDefinition>(stream, Options)
            ?? throw new InvalidOperationException("Le fichier de profil de sortie est vide ou invalide.");
    }

    /// <summary>
    /// Saves one output profile to disk.
    /// </summary>
    /// <param name="profile">Profile to save.</param>
    /// <returns>Saved file path.</returns>
    public string Save(OutputProfileDefinition profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Impossible de sauvegarder un profil de sortie sans nom.");
        }

        Directory.CreateDirectory(RootDirectory);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (profile.CreatedAtUtc == default)
        {
            profile.CreatedAtUtc = profile.UpdatedAtUtc;
        }

        string file = Path.Combine(RootDirectory, MakeSafeFileName(profile.Name) + ".sqlqg.output-profile.json");
        using FileStream stream = File.Create(file);
        JsonSerializer.Serialize(stream, profile, Options);
        return file;
    }

    /// <summary>
    /// Deletes an output profile by name when it exists.
    /// </summary>
    /// <param name="profileName">Profile name.</param>
    /// <returns><c>true</c> when a file was deleted.</returns>
    public bool Delete(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return false;
        }

        string file = Path.Combine(RootDirectory, MakeSafeFileName(profileName) + ".sqlqg.output-profile.json");
        if (!File.Exists(file))
        {
            return false;
        }

        File.Delete(file);
        return true;
    }

    /// <summary>
    /// Builds a filesystem-safe profile file name.
    /// </summary>
    /// <param name="name">Profile name.</param>
    /// <returns>Safe file name without extension.</returns>
    public static string MakeSafeFileName(string name)
    {
        HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
        char[] chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        string cleaned = new(chars);
        return string.IsNullOrWhiteSpace(cleaned) ? "output_profile" : cleaned;
    }
}
