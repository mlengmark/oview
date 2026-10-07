using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

/// <summary>
/// The bug these prevent: the app fetching an executable from wherever the release feed
/// points, or naming the downloaded file from an attacker-controlled tag, and handing
/// either to <c>Process.Start</c>. The asset <i>name</i> is matched by
/// <see cref="ReleaseAssets"/>; nothing else constrains the <i>URL</i> or the save path.
/// </summary>
public class ReleaseDownloadUrlTests
{
    [Theory]
    [InlineData("https://github.com/mlengmark/O-view/releases/download/v0.7.0/O-view-Setup.exe")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1/2")]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1/2")]
    [InlineData("https://api.github.com/repos/mlengmark/O-view/releases/assets/1")]
    [InlineData("HTTPS://GITHUB.COM/mlengmark/O-view/releases/download/v0.7.0/O-view-Setup.exe")]
    public void GitHubReleaseUrlsAreTrusted(string url)
    {
        Assert.True(ReleaseDownloadUrl.IsTrusted(url));
    }

    [Theory]
    [InlineData("https://evil.example/O-view-Setup.exe")]
    [InlineData("http://github.com/mlengmark/O-view/releases/download/v0.7.0/O-view-Setup.exe")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ftp://github.com/O-view-Setup.exe")]
    [InlineData("/releases/download/v0.7.0/O-view-Setup.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void NonGitHubUrlsAreRefused(string? url)
    {
        Assert.False(ReleaseDownloadUrl.IsTrusted(url));
    }

    [Theory]
    // Reads as github.com to a human; resolves to the attacker's host.
    [InlineData("https://github.com@evil.example/O-view-Setup.exe")]
    [InlineData("https://github.com:token@evil.example/O-view-Setup.exe")]
    // Suffix look-alikes, which a careless EndsWith check would accept.
    [InlineData("https://evil-github.com/O-view-Setup.exe")]
    [InlineData("https://github.com.evil.example/O-view-Setup.exe")]
    [InlineData("https://notgithubusercontent.com/O-view-Setup.exe")]
    public void HostLookalikesAreRefused(string url)
    {
        Assert.False(ReleaseDownloadUrl.IsTrusted(url));
    }

    // ── TempFileName: built from the parsed version, never the raw tag ─────────────

    [Theory]
    [InlineData("v0.7.0", "O-view-Setup-0.7.0.exe")]
    [InlineData("0.7.0", "O-view-Setup-0.7.0.exe")]
    [InlineData("V1.2.3", "O-view-Setup-1.2.3.exe")]
    public void NameIsBuiltFromTheParsedVersion(string tag, string expected)
    {
        Assert.Equal(expected, ReleaseDownloadUrl.TempFileName(tag));
    }

    [Fact]
    public void PathTraversalInTheTagNeverReachesTheName()
    {
        // The source repository's own confirmed bug: ReleaseVersion.TryParse truncates at
        // the first '-' or '+', so this tag parses cleanly to version 9.9.9 while the tag
        // string itself still carries the traversal segments. Only the parsed version may
        // reach the file name, so the result below must contain no path separator and no
        // ".." of any kind — regardless of what the tag says after the version.
        const string maliciousTag = "v9.9.9-../../../../Startup/evil";

        var name = ReleaseDownloadUrl.TempFileName(maliciousTag);

        Assert.Equal("O-view-Setup-9.9.9.exe", name);
        Assert.DoesNotContain("..", name!);
        Assert.DoesNotContain('/', name!);
        Assert.DoesNotContain('\\', name!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("../../evil")]
    public void UnparseableTagsYieldNoName(string? tag)
    {
        Assert.Null(ReleaseDownloadUrl.TempFileName(tag));
    }
}
