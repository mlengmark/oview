using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

public class UpdateCheckTests
{
    private static readonly ReleaseAssetSelector Installer = ReleaseAssets.WindowsInstaller;

    private static string ReleaseJson(
        string tag, bool draft = false, bool prerelease = false, string? assetName = "O-view-Setup.exe", string? checksumsName = null) =>
        $$"""
        {
          "tag_name": "{{tag}}",
          "draft": {{draft.ToString().ToLowerInvariant()}},
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "assets": [
            {{(assetName is null ? "" : $$"""{ "name": "{{assetName}}", "browser_download_url": "https://example.test/{{assetName}}" }""")}}
            {{(checksumsName is null ? "" : $$""", { "name": "{{checksumsName}}", "browser_download_url": "https://example.test/{{checksumsName}}" }""")}}
          ]
        }
        """;

    [Fact]
    public void Newer_tag_with_matching_asset_is_UpdateAvailable()
    {
        var result = UpdateCheck.Evaluate("0.4.0", ReleaseJson("v0.5.0"), Installer);

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.NotNull(result.Available);
        Assert.Equal("v0.5.0", result.Available!.Tag);
        Assert.Equal(new ReleaseVersion(0, 5, 0), result.Available.Version);
    }

    [Fact]
    public void Same_or_older_tag_is_UpToDate()
    {
        var result = UpdateCheck.Evaluate("0.5.0", ReleaseJson("v0.5.0"), Installer);

        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public void Newer_tag_with_no_matching_asset_is_Unknown_not_a_dangling_offer()
    {
        var result = UpdateCheck.Evaluate("0.4.0", ReleaseJson("v0.5.0", assetName: "something-else.bin"), Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void Draft_release_is_treated_as_no_update()
    {
        var result = UpdateCheck.Evaluate("0.4.0", ReleaseJson("v0.5.0", draft: true), Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void Prerelease_is_treated_as_no_update()
    {
        var result = UpdateCheck.Evaluate("0.4.0", ReleaseJson("v0.5.0", prerelease: true), Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void Malformed_json_is_Unknown_never_throws()
    {
        var result = UpdateCheck.Evaluate("0.4.0", "{ not json", Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void Unparseable_current_version_is_Unknown()
    {
        var result = UpdateCheck.Evaluate("not-a-version", ReleaseJson("v0.5.0"), Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void Missing_checksums_asset_leaves_ChecksumsUrl_null_without_failing()
    {
        var result = UpdateCheck.Evaluate("0.4.0", ReleaseJson("v0.5.0"), Installer);

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.Null(result.Available!.ChecksumsUrl);
    }

    [Fact]
    public void Present_checksums_asset_is_carried_through()
    {
        var result = UpdateCheck.Evaluate(
            "0.4.0", ReleaseJson("v0.5.0", checksumsName: ReleaseAssets.ChecksumsName), Installer);

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.NotNull(result.Available!.ChecksumsUrl);
    }
}
