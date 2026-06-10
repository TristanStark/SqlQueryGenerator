using SqlQueryGenerator.App.Services;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SqlQueryGenerator.Tests;

public sealed class GitHubLatestReleaseCheckerTests
{
    [Theory]
    [InlineData("v31.0.0", 31, 0, 0)]
    [InlineData("31.1.2", 31, 1, 2)]
    [InlineData("v32.0.0-beta.1", 32, 0, 0)]
    [InlineData("v33.2.1+build.5", 33, 2, 1)]
    public void TryParseSemanticVersion_ParsesCommonReleaseTags(string tag, int major, int minor, int build)
    {
        bool parsed = GitHubLatestReleaseChecker.TryParseSemanticVersion(tag, out Version version);

        Assert.True(parsed);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
    }

    [Theory]
    [InlineData("v31.1.0", "31.0.0", true)]
    [InlineData("v31.0.0", "31.0.0", false)]
    [InlineData("v30.9.9", "31.0.0", false)]
    [InlineData("v31.0.1", "31.0.0", true)]
    public void IsNewerVersion_ComparesSemanticVersions(string latestTag, string currentTag, bool expected)
    {
        Assert.True(GitHubLatestReleaseChecker.TryParseSemanticVersion(latestTag, out Version latest));
        Assert.True(GitHubLatestReleaseChecker.TryParseSemanticVersion(currentTag, out Version current));

        bool actual = GitHubLatestReleaseChecker.IsNewerVersion(latest, current);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenNewerReleaseExists_ReturnsUpdateAvailable()
    {
        HttpRequestMessage? capturedRequest = null;
        using HttpClient client = new(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
            {
              "tag_name": "v31.1.0",
              "html_url": "https://github.com/TristanStark/SqlQueryGenerator/releases/tag/v31.1.0",
              "prerelease": false
            }
            """);
        }));

        GitHubLatestReleaseChecker checker = new(client);

        GitHubReleaseCheckResult result = await checker.CheckLatestReleaseAsync(new Version(31, 0, 0));

        Assert.True(result.Succeeded);
        Assert.True(result.UpdateAvailable);
        Assert.Equal("v31.1.0", result.LatestTag);
        Assert.NotNull(result.ReleaseUri);
        Assert.Contains("SqlQueryGenerator", capturedRequest?.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenCurrentVersionIsLatest_ReturnsNoUpdate()
    {
        using HttpClient client = new(new StubHttpMessageHandler(_ => JsonResponse("""
        {
          "tag_name": "v31.0.0",
          "html_url": "https://github.com/TristanStark/SqlQueryGenerator/releases/tag/v31.0.0",
          "prerelease": false
        }
        """)));

        GitHubLatestReleaseChecker checker = new(client);

        GitHubReleaseCheckResult result = await checker.CheckLatestReleaseAsync(new Version(31, 0, 0));

        Assert.True(result.Succeeded);
        Assert.False(result.UpdateAvailable);
        Assert.Equal("v31.0.0", result.LatestTag);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenPrereleaseAndIgnored_ReturnsNoUpdate()
    {
        using HttpClient client = new(new StubHttpMessageHandler(_ => JsonResponse("""
        {
          "tag_name": "v31.1.0-beta.1",
          "html_url": "https://github.com/TristanStark/SqlQueryGenerator/releases/tag/v31.1.0-beta.1",
          "prerelease": true
        }
        """)));

        GitHubLatestReleaseChecker checker = new(client);

        GitHubReleaseCheckResult result = await checker.CheckLatestReleaseAsync(
            new Version(31, 0, 0),
            includePrereleases: false);

        Assert.True(result.Succeeded);
        Assert.False(result.UpdateAvailable);
        Assert.Contains("prerelease", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenGitHubReturnsRateLimit_DoesNotThrow()
    {
        using HttpClient client = new(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("rate limit")
            }));

        GitHubLatestReleaseChecker checker = new(client);

        GitHubReleaseCheckResult result = await checker.CheckLatestReleaseAsync(new Version(31, 0, 0));

        Assert.False(result.Succeeded);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenResponseIsInvalid_DoesNotThrow()
    {
        using HttpClient client = new(new StubHttpMessageHandler(_ => JsonResponse("""
        {
          "name": "not a release"
        }
        """)));

        GitHubLatestReleaseChecker checker = new(client);

        GitHubReleaseCheckResult result = await checker.CheckLatestReleaseAsync(new Version(31, 0, 0));

        Assert.False(result.Succeeded);
        Assert.False(result.UpdateAvailable);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
