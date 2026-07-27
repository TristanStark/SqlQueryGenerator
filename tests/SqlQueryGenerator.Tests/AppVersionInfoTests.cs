using SqlQueryGenerator.App.Services;

namespace SqlQueryGenerator.Tests;

public sealed class AppVersionInfoTests
{
    [Fact]
    public void ResolveVersion_UsesHighestAvailableMetadataVersion()
    {
        Version resolved = AppVersionInfo.ResolveVersion(
            new string?[] { "31.0.0+build.42", "31.0.3.0", "31.0.2.0" },
            new Version(1, 0, 0));

        Assert.Equal(new Version(31, 0, 3), resolved);
    }

    [Fact]
    public void ResolveVersion_UsesFallbackWhenMetadataIsInvalid()
    {
        Version resolved = AppVersionInfo.ResolveVersion(
            new string?[] { null, string.Empty, "not-a-version" },
            new Version(32, 0, 0));

        Assert.Equal(new Version(32, 0, 0), resolved);
    }
}
