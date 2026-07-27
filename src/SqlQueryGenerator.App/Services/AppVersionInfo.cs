using System.Reflection;

namespace SqlQueryGenerator.App.Services;

/// <summary>
/// Provides centralized application version information.
/// </summary>
public static class AppVersionInfo
{
    private static readonly Version FallbackVersion = new(32, 0, 0);

    /// <summary>
    /// Gets the product name displayed in the UI.
    /// </summary>
    public static string ProductName => "SqlQueryGenerator";

    /// <summary>
    /// Gets the GitHub latest-release API endpoint.
    /// </summary>
    public static Uri LatestReleaseApiUri { get; } =
        new("https://api.github.com/repos/TristanStark/SqlQueryGenerator/releases/latest");

    /// <summary>
    /// Gets the current application version from all available assembly metadata.
    /// The highest valid value is selected so a stale InformationalVersion cannot
    /// hide a newer file or assembly version.
    /// </summary>
    public static Version CurrentVersion
    {
        get
        {
            Assembly assembly = typeof(AppVersionInfo).Assembly;
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            string? fileVersion = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;
            string? assemblyVersion = assembly.GetName().Version?.ToString();

            return ResolveVersion(
                new[] { informational, fileVersion, assemblyVersion },
                FallbackVersion);
        }
    }

    /// <summary>
    /// Resolves the highest semantic version from potentially inconsistent metadata.
    /// </summary>
    public static Version ResolveVersion(IEnumerable<string?> candidates, Version fallbackVersion)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(fallbackVersion);

        Version best = NormalizeVersion(fallbackVersion);
        foreach (string? candidate in candidates)
        {
            if (GitHubLatestReleaseChecker.TryParseSemanticVersion(candidate, out Version parsed))
            {
                Version normalized = NormalizeVersion(parsed);
                if (normalized.CompareTo(best) > 0)
                {
                    best = normalized;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Gets the current version as display text without build metadata.
    /// </summary>
    public static string CurrentVersionText => FormatVersion(CurrentVersion);

    /// <summary>
    /// Gets the full application version label.
    /// </summary>
    public static string CurrentVersionLabel => $"{ProductName} v{CurrentVersionText}";

    /// <summary>
    /// Formats a version using major, minor and build components when available.
    /// </summary>
    public static string FormatVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    private static Version NormalizeVersion(Version version)
    {
        int major = Math.Max(0, version.Major);
        int minor = Math.Max(0, version.Minor);
        int build = Math.Max(0, version.Build);

        return new Version(major, minor, build);
    }
}
