using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

/// <summary>
/// Pins <see cref="ReleaseAssets.DebianPackage"/> and <see cref="ReleaseAssets.Tarball"/>
/// against the exact filenames <c>packaging/linux/build.sh</c> (ADR-0010 D5, slicing table
/// row 8) produces. The script carries its own matching check at build time (string-level,
/// against a real build); this test pins the same shapes from the Core side without needing
/// <c>dpkg-deb</c> or a Linux publish — if either side's filename shape changes, exactly one
/// of this test or the script's own check should start failing, which is the point.
/// </summary>
public class ReleaseAssetsTests
{
    private const string Version = "1.4.0";

    // ── .deb, as build.sh names it: o-view_<version>_<arch>.deb ─────────────────────

    [Theory]
    [InlineData("amd64")]
    [InlineData("arm64")]
    public void MatchesTheDebianPackageBuildShProducesForItsOwnArchitecture(string architecture)
    {
        var name = $"o-view_{Version}_{architecture}.deb";

        Assert.True(ReleaseAssets.DebianPackage(architecture).Matches(name));
    }

    [Fact]
    public void DoesNotMatchTheOtherArchitecturesDebianPackage()
    {
        Assert.False(ReleaseAssets.DebianPackage("amd64").Matches($"o-view_{Version}_arm64.deb"));
        Assert.False(ReleaseAssets.DebianPackage("arm64").Matches($"o-view_{Version}_amd64.deb"));
    }

    [Fact]
    public void DoesNotMatchTheTarballAsADebianPackage()
    {
        Assert.False(ReleaseAssets.DebianPackage("amd64").Matches($"o-view-{Version}-linux-x64.tar.gz"));
    }

    // ── tarball, as build.sh names it: o-view-<version>-<rid>.tar.gz ────────────────

    [Theory]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    public void MatchesTheTarballBuildShProducesForItsOwnRuntimeIdentifier(string runtimeIdentifier)
    {
        var name = $"o-view-{Version}-{runtimeIdentifier}.tar.gz";

        Assert.True(ReleaseAssets.Tarball(runtimeIdentifier).Matches(name));
    }

    [Fact]
    public void DoesNotMatchTheOtherRuntimeIdentifiersTarball()
    {
        Assert.False(ReleaseAssets.Tarball("linux-x64").Matches($"o-view-{Version}-linux-arm64.tar.gz"));
        Assert.False(ReleaseAssets.Tarball("linux-arm64").Matches($"o-view-{Version}-linux-x64.tar.gz"));
    }

    [Fact]
    public void DoesNotMatchTheDebianPackageAsATarball()
    {
        Assert.False(ReleaseAssets.Tarball("linux-x64").Matches($"o-view_{Version}_amd64.deb"));
    }
}
