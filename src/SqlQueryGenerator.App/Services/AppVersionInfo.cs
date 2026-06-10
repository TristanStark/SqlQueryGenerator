using System.Reflection;

namespace SqlQueryGenerator.App.Services;

/// <summary>
/// Provides centralized application version information.
/// </summary>
public static class AppVersionInfo
{
    private static readonly Version FallbackVersion = new(31, 0, 0);

    /// <summary>
    /// Gets the product name displayed in the UI.
    /// </summary>
    /// <value>Application product name.</value>
    public static string ProductName => "SqlQueryGenerator";

    /// <summary>
    /// Gets the GitHub latest-release API endpoint.
    /// </summary>
    /// <value>Unauthenticated GitHub Releases API URL.</value>
    public static Uri LatestReleaseApiUri { get; } =
        new("https://api.github.com/repos/TristanStark/SqlQueryGenerator/releases/latest");

    /// <summary>
    /// Gets the current application version parsed from assembly metadata.
    /// </summary>
    /// <value>Current semantic version.</value>
    public static Version CurrentVersion
    {
        get
        {
            Assembly assembly = typeof(AppVersionInfo).Assembly;
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (GitHubLatestReleaseChecker.TryParseSemanticVersion(informational, out Version parsed))
            {
                return parsed;
            }

            Version? assemblyVersion = assembly.GetName().Version;
            return assemblyVersion is null || assemblyVersion.Major <= 0
                ? FallbackVersion
                : NormalizeVersion(assemblyVersion);
        }
    }

    /// <summary>
    /// Gets the current version as display text without build metadata.
    /// </summary>
    /// <value>Displayable current version.</value>
    public static string CurrentVersionText => FormatVersion(CurrentVersion);

    /// <summary>
    /// Gets the full application version label.
    /// </summary>
    /// <value>Example: <c>SqlQueryGenerator v31.0.0</c>.</value>
    public static string CurrentVersionLabel => $"{ProductName} v{CurrentVersionText}";

    /// <summary>
    /// Formats a version using major, minor and build components when available.
    /// </summary>
    /// <param name="version">Version to format.</param>
    /// <returns>Formatted version.</returns>
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
