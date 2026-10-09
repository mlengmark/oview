using System.Security.Cryptography;
using System.Text;
using OView.App;
using OView.App.Updates;
using OView.Core.Updates;
using OView.Tray.Updates;

namespace OView.Tray.Tests.Updates;

/// <summary>
/// ADR-0010 slice 4: proves the download-verify-launch path end to end against fakes — no
/// real network, no real process, no real disk beyond the temp file the happy path itself
/// writes. Every D3 refusal case that can reach this composition is covered here; the
/// refusal logic itself (manifest parsing, host allowlisting) is slice 3's own tests.
/// </summary>
public class WindowsUpdateExecutorTests
{
    private const string Tag = "v1.2.3";
    private const string InstallerBody = "fake installer bytes";

    private static readonly string TrustedInstallerUrl =
        $"https://github.com/mlengmark/O-view/releases/download/{Tag}/{ReleaseAssets.WindowsInstallerName}";
    private static readonly string TrustedChecksumsUrl =
        $"https://github.com/mlengmark/O-view/releases/download/{Tag}/{ReleaseAssets.ChecksumsName}";

    [Fact]
    public async Task A_portable_install_never_reaches_the_feed()
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsPortable, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.NotApplicable, outcome);
        Assert.Empty(downloader.RequestedUrls);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task No_update_available_launches_nothing()
    {
        var feed = FeedReturning(UpToDateJson());
        var downloader = new FakeDownloader();
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("9.9.9");

        Assert.Equal(UpdateExecutionOutcome.NoUpdateAvailable, outcome);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task An_installer_url_on_a_non_GitHub_host_is_refused()
    {
        var feed = FeedReturning(ReleaseJson("https://evil.example/O-view-Setup.exe", TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.UntrustedInstallerUrl, outcome);
        Assert.Empty(downloader.RequestedUrls);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task A_release_with_no_checksums_asset_is_refused()
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, checksumsUrl: null));
        var downloader = new FakeDownloader();
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.NoVerifiableChecksums, outcome);
        Assert.Empty(downloader.RequestedUrls);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task A_checksums_url_on_a_non_GitHub_host_is_refused()
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, "https://evil.example/SHA256SUMS"));
        var downloader = new FakeDownloader();
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.NoVerifiableChecksums, outcome);
        Assert.Empty(downloader.RequestedUrls);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task A_manifest_that_fails_to_download_is_refused()
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader(); // no entries configured: every download "fails"
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.ChecksumDownloadFailed, outcome);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task An_empty_manifest_is_refused_as_not_found()
    {
        var outcome = await RunWithManifest(text: "");
        Assert.Equal(UpdateExecutionOutcome.ChecksumNotFound, outcome);
    }

    [Fact]
    public async Task An_unparseable_manifest_line_is_refused_as_not_found()
    {
        var outcome = await RunWithManifest(text: "not a valid checksum line at all\n");
        Assert.Equal(UpdateExecutionOutcome.ChecksumNotFound, outcome);
    }

    [Fact]
    public async Task A_manifest_that_never_names_the_installer_asset_is_refused_as_not_found()
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(InstallerBody)));
        var outcome = await RunWithManifest(text: $"{digest.ToLowerInvariant()}  some-other-file.bin\n");
        Assert.Equal(UpdateExecutionOutcome.ChecksumNotFound, outcome);
    }

    [Fact]
    public async Task An_installer_that_fails_to_download_is_refused()
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(InstallerBody)));
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        downloader.Set(TrustedChecksumsUrl, Encoding.UTF8.GetBytes($"{digest.ToLowerInvariant()}  {ReleaseAssets.WindowsInstallerName}\n"));
        // Installer URL deliberately left unconfigured: downloader returns null for it.
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.InstallerDownloadFailed, outcome);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task A_digest_that_does_not_match_is_refused()
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        downloader.Set(TrustedChecksumsUrl, Encoding.UTF8.GetBytes(
            $"{new string('a', 64)}  {ReleaseAssets.WindowsInstallerName}\n"));
        downloader.Set(TrustedInstallerUrl, Encoding.UTF8.GetBytes(InstallerBody));
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.ChecksumMismatch, outcome);
        Assert.Null(launcher.LaunchedPath);
    }

    [Fact]
    public async Task A_verified_installer_is_launched_and_the_app_is_told_to_exit()
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(InstallerBody)));
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        downloader.Set(TrustedChecksumsUrl, Encoding.UTF8.GetBytes($"{digest.ToLowerInvariant()}  {ReleaseAssets.WindowsInstallerName}\n"));
        downloader.Set(TrustedInstallerUrl, Encoding.UTF8.GetBytes(InstallerBody));
        var launcher = new FakeLauncher();
        var exited = false;
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher, () => exited = true);

        var outcome = await executor.RunAsync("0.1.0");

        Assert.Equal(UpdateExecutionOutcome.Launched, outcome);
        Assert.True(exited);
        Assert.NotNull(launcher.LaunchedPath);
        Assert.EndsWith($"O-view-Setup-1.2.3.exe", launcher.LaunchedPath);
        Assert.Equal(InstallerBody, File.ReadAllText(launcher.LaunchedPath!));

        File.Delete(launcher.LaunchedPath!);
    }

    private static async Task<UpdateExecutionOutcome> RunWithManifest(string text)
    {
        var feed = FeedReturning(ReleaseJson(TrustedInstallerUrl, TrustedChecksumsUrl));
        var downloader = new FakeDownloader();
        downloader.Set(TrustedChecksumsUrl, Encoding.UTF8.GetBytes(text));
        downloader.Set(TrustedInstallerUrl, Encoding.UTF8.GetBytes(InstallerBody));
        var launcher = new FakeLauncher();
        var executor = Executor(InstallKind.WindowsInstaller, feed, downloader, launcher);

        return await executor.RunAsync("0.1.0");
    }

    private static WindowsUpdateExecutor Executor(
        InstallKind kind,
        ReleaseFeed feed,
        IInstallerDownloader downloader,
        IInstallerLauncher launcher,
        Action? onLaunched = null) =>
        new(new FakeInstallKindSource(kind), feed, downloader, launcher, onLaunched ?? (() => { }));

    private static ReleaseFeed FeedReturning(string releaseJson) =>
        new(new FakeTransport(new ReleaseFeedResponse(200, null, null, null, releaseJson)), new FakeClock());

    private static string ReleaseJson(string installerUrl, string? checksumsUrl) => $$"""
        {
            "tag_name": "{{Tag}}",
            "draft": false,
            "prerelease": false,
            "assets": [
                { "name": "{{ReleaseAssets.WindowsInstallerName}}", "browser_download_url": "{{installerUrl}}" }
                {{(checksumsUrl is null ? "" : $$""", { "name": "{{ReleaseAssets.ChecksumsName}}", "browser_download_url": "{{checksumsUrl}}" }""")}}
            ]
        }
        """;

    private static string UpToDateJson() => $$"""
        { "tag_name": "v0.0.1", "draft": false, "prerelease": false, "assets": [] }
        """;

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTransport : IReleaseFeedTransport
    {
        private readonly ReleaseFeedResponse _response;

        public FakeTransport(ReleaseFeedResponse response) => _response = response;

        public Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation) =>
            Task.FromResult(_response);
    }

    private sealed class FakeInstallKindSource : IInstallKindSource
    {
        public FakeInstallKindSource(InstallKind current) => Current = current;

        public InstallKind Current { get; }
    }

    private sealed class FakeDownloader : IInstallerDownloader
    {
        private readonly Dictionary<string, byte[]> _byUrl = new();

        public List<string> RequestedUrls { get; } = new();

        public void Set(string url, byte[] bytes) => _byUrl[url] = bytes;

        public Task<byte[]?> DownloadAsync(string url, CancellationToken cancellation)
        {
            RequestedUrls.Add(url);
            return Task.FromResult(_byUrl.TryGetValue(url, out var bytes) ? bytes : null);
        }
    }

    private sealed class FakeLauncher : IInstallerLauncher
    {
        public string? LaunchedPath { get; private set; }

        public void Launch(string installerPath) => LaunchedPath = installerPath;
    }
}
