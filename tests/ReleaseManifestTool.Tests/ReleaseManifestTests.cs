using OView.Core.Updates;

namespace OView.Tools.ReleaseManifest.Tests;

/// <summary>
/// Exercises <see cref="ReleaseManifest"/> against a staged directory shaped like the release
/// workflow's own (ADR-0010 D5, slicing table row 9) — one file per
/// <see cref="ReleaseAssets"/> artefact — without needing Inno Setup, <c>dpkg-deb</c> or a
/// network. The release job runs the same code via <c>Program.cs</c>; this is what proves it
/// correct.
/// </summary>
public sealed class ReleaseManifestTests : IDisposable
{
    private readonly string _stageDir = Directory.CreateTempSubdirectory("ovi-763-stage-").FullName;

    private static readonly string[] RequiredAssetNames =
    [
        ReleaseAssets.WindowsInstallerName,
        ReleaseAssets.WindowsPortableName,
        "o-view_1.4.0_amd64.deb",
        "o-view_1.4.0_arm64.deb",
        "o-view-1.4.0-linux-x64.tar.gz",
        "o-view-1.4.0-linux-arm64.tar.gz",
    ];

    public void Dispose() => Directory.Delete(_stageDir, recursive: true);

    private void StageAllRequiredAssets()
    {
        foreach (var name in RequiredAssetNames)
        {
            File.WriteAllText(Path.Combine(_stageDir, name), $"contents of {name}");
        }
    }

    [Fact]
    public void GeneratedManifestHasOneEntryPerAssetReleaseAssetsDefines()
    {
        StageAllRequiredAssets();

        var exitCode = ReleaseManifest.GenerateManifest(_stageDir, TextWriter.Null);

        Assert.Equal(0, exitCode);
        var manifestPath = Path.Combine(_stageDir, ReleaseAssets.ChecksumsName);
        Assert.True(File.Exists(manifestPath));

        var manifestText = File.ReadAllText(manifestPath);
        foreach (var name in RequiredAssetNames)
        {
            Assert.NotNull(ChecksumFile.DigestFor(manifestText, name));
        }

        var entryCount = manifestText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(RequiredAssetNames.Length, entryCount);
    }

    [Fact]
    public void VerifyPassesWhenEveryRequiredAssetIsStagedAndTheManifestMatchesTheBytes()
    {
        StageAllRequiredAssets();
        ReleaseManifest.GenerateManifest(_stageDir, TextWriter.Null);

        var exitCode = ReleaseManifest.Verify(_stageDir, TextWriter.Null);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void VerifyFailsWhenARequiredAssetIsMissing()
    {
        StageAllRequiredAssets();
        File.Delete(Path.Combine(_stageDir, ReleaseAssets.WindowsInstallerName));
        ReleaseManifest.GenerateManifest(_stageDir, TextWriter.Null);

        var exitCode = ReleaseManifest.Verify(_stageDir, TextWriter.Null);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void VerifyFailsWhenAStagedFileMatchesNoReleaseAssetsArtefact()
    {
        StageAllRequiredAssets();
        ReleaseManifest.GenerateManifest(_stageDir, TextWriter.Null);
        File.WriteAllText(Path.Combine(_stageDir, "not-a-release-asset.txt"), "stray");

        var exitCode = ReleaseManifest.Verify(_stageDir, TextWriter.Null);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void VerifyFailsWhenAStagedAssetWasModifiedAfterTheManifestWasGenerated()
    {
        StageAllRequiredAssets();
        ReleaseManifest.GenerateManifest(_stageDir, TextWriter.Null);
        File.WriteAllText(Path.Combine(_stageDir, ReleaseAssets.WindowsPortableName), "tampered bytes");

        var exitCode = ReleaseManifest.Verify(_stageDir, TextWriter.Null);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void VerifyFailsWithNoManifestStaged()
    {
        StageAllRequiredAssets();

        var exitCode = ReleaseManifest.Verify(_stageDir, TextWriter.Null);

        Assert.Equal(1, exitCode);
    }

    [Theory]
    [InlineData("windows-installer", ReleaseAssets.WindowsInstallerName)]
    [InlineData("windows-portable", ReleaseAssets.WindowsPortableName)]
    [InlineData("checksums", ReleaseAssets.ChecksumsName)]
    public void NameReturnsTheExactReleaseAssetsConstant(string key, string expected)
    {
        Assert.Equal(expected, ReleaseManifest.Name(key));
    }

    [Fact]
    public void NameReturnsNullForAnUnknownKey()
    {
        Assert.Null(ReleaseManifest.Name("something-else"));
    }
}
