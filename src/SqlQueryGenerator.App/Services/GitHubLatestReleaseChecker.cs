using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SqlQueryGenerator.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer SqlQueryGenerator version without requiring authentication.
/// </summary>
public sealed class GitHubLatestReleaseChecker
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly Uri _latestReleaseUri;

    /// <summary>
    /// Initializes a new GitHub latest-release checker.
    /// </summary>
    /// <param name="httpClient">HTTP client. When omitted, a default client is created.</param>
    /// <param name="latestReleaseUri">GitHub latest-release endpoint.</param>
    public GitHubLatestReleaseChecker(HttpClient? httpClient = null, Uri? latestReleaseUri = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _latestReleaseUri = latestReleaseUri ?? AppVersionInfo.LatestReleaseApiUri;
    }

    /// <summary>
    /// Checks asynchronously whether a newer GitHub release exists.
    /// </summary>
    /// <param name="currentVersion">Current application version.</param>
    /// <param name="includePrereleases">Whether prereleases should be considered.</param>
    /// <param name="timeout">Maximum request duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Release check result. Failures are returned as data and never thrown for normal network errors.</returns>
    public async Task<GitHubReleaseCheckResult> CheckLatestReleaseAsync(
        Version currentVersion,
        bool includePrereleases = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout ?? DefaultTimeout);

            using HttpRequestMessage request = new(HttpMethod.Get, _latestReleaseUri);
            request.Headers.UserAgent.ParseAdd($"{AppVersionInfo.ProductName}/{AppVersionInfo.CurrentVersionText}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return GitHubReleaseCheckResult.Failed(
                    $"GitHub release check failed with HTTP {(int)response.StatusCode} {response.StatusCode}.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token).ConfigureAwait(false);

            JsonElement root = document.RootElement;
            string? tagName = TryGetString(root, "tag_name");
            string? htmlUrl = TryGetString(root, "html_url");
            bool prerelease = TryGetBoolean(root, "prerelease");

            if (string.IsNullOrWhiteSpace(tagName)
                || string.IsNullOrWhiteSpace(htmlUrl)
                || !TryParseSemanticVersion(tagName, out Version latestVersion))
            {
                return GitHubReleaseCheckResult.Failed("GitHub release response is invalid or does not contain a semantic version tag.");
            }

            if (prerelease && !includePrereleases)
            {
                return GitHubReleaseCheckResult.Success(
                    latestTag: tagName,
                    latestVersion: latestVersion,
                    releaseUri: new Uri(htmlUrl),
                    updateAvailable: false,
                    message: "Latest release is a prerelease and prereleases are ignored.");
            }

            bool updateAvailable = IsNewerVersion(latestVersion, currentVersion);

            return GitHubReleaseCheckResult.Success(
                latestTag: tagName,
                latestVersion: latestVersion,
                releaseUri: new Uri(htmlUrl),
                updateAvailable: updateAvailable,
                message: updateAvailable
                    ? $"New version available: {tagName}"
                    : "Current version is up to date.");
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or TaskCanceledException
                                   or OperationCanceledException
                                   or JsonException
                                   or UriFormatException
                                   or InvalidOperationException)
        {
            return GitHubReleaseCheckResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Checks whether the latest version is newer than the current version.
    /// </summary>
    /// <param name="latestVersion">Latest release version.</param>
    /// <param name="currentVersion">Current application version.</param>
    /// <returns><c>true</c> when latest is newer.</returns>
    public static bool IsNewerVersion(Version latestVersion, Version currentVersion)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        ArgumentNullException.ThrowIfNull(currentVersion);

        return Normalize(latestVersion).CompareTo(Normalize(currentVersion)) > 0;
    }

    /// <summary>
    /// Parses semantic version tags such as <c>v31.0.0</c>, <c>31.0.0</c>, or <c>v31.1.0-beta.1</c>.
    /// </summary>
    /// <param name="tag">Raw tag or version string.</param>
    /// <param name="version">Parsed version.</param>
    /// <returns><c>true</c> when parsing succeeded.</returns>
    public static bool TryParseSemanticVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string value = tag.Trim();

        int metadataIndex = value.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
        {
            value = value[..metadataIndex];
        }

        int prereleaseIndex = value.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            value = value[..prereleaseIndex];
        }

        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        if (value.Count(c => c == '.') == 0)
        {
            value += ".0";
        }

        return Version.TryParse(value, out version!);
    }

    private static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryGetBoolean(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement property)
               && property.ValueKind == JsonValueKind.True;
    }
}

/// <summary>
/// Result of a GitHub latest-release check.
/// </summary>
public sealed record GitHubReleaseCheckResult
{
    private GitHubReleaseCheckResult(
        bool succeeded,
        bool updateAvailable,
        string? latestTag,
        Version? latestVersion,
        Uri? releaseUri,
        string message)
    {
        Succeeded = succeeded;
        UpdateAvailable = updateAvailable;
        LatestTag = latestTag;
        LatestVersion = latestVersion;
        ReleaseUri = releaseUri;
        Message = message;
    }

    /// <summary>
    /// Gets whether the remote check succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets whether a newer version exists.
    /// </summary>
    public bool UpdateAvailable { get; }

    /// <summary>
    /// Gets the latest release tag.
    /// </summary>
    public string? LatestTag { get; }

    /// <summary>
    /// Gets the latest parsed version.
    /// </summary>
    public Version? LatestVersion { get; }

    /// <summary>
    /// Gets the GitHub release page URL.
    /// </summary>
    public Uri? ReleaseUri { get; }

    /// <summary>
    /// Gets a diagnostic message. This is safe to keep internal or display discreetly.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a successful release check result.
    /// </summary>
    public static GitHubReleaseCheckResult Success(
        string latestTag,
        Version latestVersion,
        Uri releaseUri,
        bool updateAvailable,
        string message)
    {
        return new GitHubReleaseCheckResult(
            succeeded: true,
            updateAvailable: updateAvailable,
            latestTag: latestTag,
            latestVersion: latestVersion,
            releaseUri: releaseUri,
            message: message);
    }

    /// <summary>
    /// Creates a failed release check result.
    /// </summary>
    public static GitHubReleaseCheckResult Failed(string message)
    {
        return new GitHubReleaseCheckResult(
            succeeded: false,
            updateAvailable: false,
            latestTag: null,
            latestVersion: null,
            releaseUri: null,
            message: message);
    }
}
