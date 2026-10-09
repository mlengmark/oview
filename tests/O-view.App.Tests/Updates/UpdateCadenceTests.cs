using System.Net.Http;
using System.Runtime.InteropServices;
using OView.App.Updates;
using OView.Core.Updates;

namespace OView.App.Tests.Updates;

/// <summary>
/// ADR-0010 slicing table row 5 (OVI-557): the background cadence, persisted notify-once-per-
/// version, and the manual "Check for updates now" path. Against fakes only — no real HTTP,
/// no real timer, and no real <see cref="IInstallKindSource"/> (<see cref="WindowsUpdateExecutorTests"/>'s
/// own boundary: this slice's tests never hit the live network either).
/// </summary>
public class UpdateCadenceTests
{
    private static readonly ReleaseAssetSelector Installer = ReleaseAssets.WindowsInstaller;

    [Fact]
    public void Background_tick_does_nothing_when_AutoUpdateEnabled_is_false()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised);
        settings.AutoUpdateEnabled = false;

        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Equal(0, transport.CallCount);
        Assert.Empty(raised);
        Assert.Null(settings.Settings.LastAnnouncedUpdateTag);
    }

    [Fact]
    public void Background_tick_raises_UpdateAvailable_and_persists_the_tag_when_a_newer_release_exists()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised);
        settings.AutoUpdateEnabled = true;

        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Single(raised);
        Assert.Equal(UsageEventKind.UpdateAvailable, raised[0].Kind);
        Assert.Equal("v2.0.0", settings.Settings.LastAnnouncedUpdateTag);
    }

    [Fact]
    public void Background_tick_does_not_re_announce_the_same_version_twice()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised);
        settings.AutoUpdateEnabled = true;

        timer.RaiseElapsed();
        WaitForPendingWork();
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpdateAvailableJson("v2.0.0"));
        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Single(raised);
    }

    [Fact]
    public void Background_tick_announces_a_second_distinct_newer_version()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised);
        settings.AutoUpdateEnabled = true;

        timer.RaiseElapsed();
        WaitForPendingWork();
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpdateAvailableJson("v2.1.0"));
        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Equal(2, raised.Count);
        Assert.Equal("v2.1.0", settings.Settings.LastAnnouncedUpdateTag);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v0.1.0", "draft": false, "prerelease": false, "assets": [] }""")]
    public void Background_tick_raises_nothing_when_up_to_date(string releaseJson)
    {
        var transport = new FakeTransport(releaseJson);
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised, currentVersion: "1.0.0");
        settings.AutoUpdateEnabled = true;

        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Empty(raised);
        Assert.Null(settings.Settings.LastAnnouncedUpdateTag);
    }

    [Fact]
    public async Task CheckNowAsync_runs_regardless_of_AutoUpdateEnabled_and_returns_the_outcome()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out _, out var settings, out var raised);
        settings.AutoUpdateEnabled = false;

        var result = await cadence.CheckNowAsync();

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.Equal("v2.0.0", result.Available?.Tag);
        Assert.Empty(raised);
    }

    [Fact]
    public async Task CheckNowAsync_records_a_newly_seen_tag_without_raising_an_event()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out _, out var settings, out var raised);

        await cadence.CheckNowAsync();

        Assert.Equal("v2.0.0", settings.Settings.LastAnnouncedUpdateTag);
        Assert.Empty(raised);
    }

    [Fact]
    public async Task CheckNowAsync_after_a_manual_discovery_suppresses_the_next_background_announcement()
    {
        var transport = new FakeTransport(UpdateAvailableJson("v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised);

        await cadence.CheckNowAsync();
        settings.AutoUpdateEnabled = true;
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpdateAvailableJson("v2.0.0"));
        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Empty(raised);
    }

    [Fact]
    public async Task CheckNowAsync_reports_RateLimited_rather_than_collapsing_it_to_Unknown_or_UpToDate()
    {
        var transport = new FakeTransport(new ReleaseFeedResponse(429, "0", null, "3600", ""));
        var cadence = CreateCadence(transport, out _, out _, out _);

        var result = await cadence.CheckNowAsync();

        Assert.Equal(UpdateOutcome.RateLimited, result.Outcome);
    }

    [Fact]
    public async Task CheckNowAsync_reports_Unknown_on_a_transport_failure_rather_than_fabricating_UpToDate()
    {
        var transport = new FakeTransport(throwOnFetch: true);
        var cadence = CreateCadence(transport, out _, out _, out _);

        var result = await cadence.CheckNowAsync();

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    /// <summary>
    /// ADR-0010 slice 6 (D4): both Linux install kinds still notify on the same background
    /// cadence Windows uses — <see cref="UpdatePolicy.MayDownloadAndRun"/> only gates download
    /// and launch, never the notice itself, so "notify-only" must not regress into "no notice
    /// at all" the way the source's own apt bug once silenced detection entirely.
    /// </summary>
    [Theory]
    [InlineData(InstallKind.LinuxPackage)]
    [InlineData(InstallKind.LinuxTarball)]
    public void Background_tick_still_notifies_for_a_Linux_install_kind_that_may_never_download(InstallKind kind)
    {
        Assert.False(UpdatePolicy.MayDownloadAndRun(kind));

        var transport = new FakeTransport(UpdateAvailableJsonFor(kind, "v2.0.0"));
        var cadence = CreateCadence(transport, out var timer, out var settings, out var raised, installKind: kind);
        settings.AutoUpdateEnabled = true;

        timer.RaiseElapsed();
        WaitForPendingWork();

        Assert.Single(raised);
        Assert.Equal(UsageEventKind.UpdateAvailable, raised[0].Kind);
        Assert.Equal("v2.0.0", settings.Settings.LastAnnouncedUpdateTag);
    }

    /// <summary>The manual "Check for updates now" path is reachable for a Linux kind too —
    /// D4's notify-only guarantee is about download and launch, not about hiding the check.</summary>
    [Theory]
    [InlineData(InstallKind.LinuxPackage)]
    [InlineData(InstallKind.LinuxTarball)]
    public async Task CheckNowAsync_also_reports_UpdateAvailable_for_a_Linux_install_kind(InstallKind kind)
    {
        var transport = new FakeTransport(UpdateAvailableJsonFor(kind, "v2.0.0"));
        var cadence = CreateCadence(transport, out _, out _, out var raised, installKind: kind);

        var result = await cadence.CheckNowAsync();

        Assert.Equal(UpdateOutcome.UpdateAvailable, result.Outcome);
        Assert.Empty(raised);
    }

    /// <summary>
    /// ADR-0010 D4's own words: "a later edit that makes this head 'helpfully' install
    /// something trips a test rather than shipping". <see cref="LayeringStructuralTests"/>
    /// already proves <c>O-view.App</c> cannot reference the project that defines
    /// <c>IInstallerDownloader</c>/<c>IInstallerLauncher</c> (<c>O-view.Tray</c>); this test
    /// proves the narrower, type-level fact directly on <see cref="UpdateCadence"/> itself —
    /// its constructor takes no parameter shaped like either seam — so neither the background
    /// cadence nor the manual check can reach a download or a launch for any install kind,
    /// Linux included.
    /// </summary>
    [Fact]
    public void UpdateCadence_has_no_constructor_dependency_able_to_download_or_launch_an_installer()
    {
        var parameterTypeNames = typeof(UpdateCadence)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        Assert.DoesNotContain(parameterTypeNames, name =>
            name.Contains("Downloader", StringComparison.Ordinal) ||
            name.Contains("Launcher", StringComparison.Ordinal));
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        var releaseFeed = new ReleaseFeed(new FakeTransport(UpToDateJson()), new FakeClock(DateTimeOffset.UnixEpoch));
        var installKindSource = new FakeInstallKindSource();
        var settings = new FakeSettingsHolder();
        var timer = new FakeAppTimer();

        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            null!, installKindSource, "1.0.0", () => settings.Settings, settings.Record, _ => { }, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            releaseFeed, null!, "1.0.0", () => settings.Settings, settings.Record, _ => { }, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentException>(() => new UpdateCadence(
            releaseFeed, installKindSource, "", () => settings.Settings, settings.Record, _ => { }, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            releaseFeed, installKindSource, "1.0.0", null!, settings.Record, _ => { }, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            releaseFeed, installKindSource, "1.0.0", () => settings.Settings, null!, _ => { }, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            releaseFeed, installKindSource, "1.0.0", () => settings.Settings, settings.Record, null!, timer, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentNullException>(() => new UpdateCadence(
            releaseFeed, installKindSource, "1.0.0", () => settings.Settings, settings.Record, _ => { }, null!, TimeSpan.FromHours(1)));
    }

    private static UpdateCadence CreateCadence(
        FakeTransport transport,
        out FakeAppTimer timer,
        out FakeSettingsHolder settings,
        out List<UsageEvent> raised,
        string currentVersion = "1.0.0",
        InstallKind installKind = InstallKind.WindowsInstaller)
    {
        var releaseFeed = new ReleaseFeed(transport, new FakeClock(DateTimeOffset.UnixEpoch));
        timer = new FakeAppTimer();
        var settingsHolder = new FakeSettingsHolder();
        settings = settingsHolder;
        var raisedEvents = new List<UsageEvent>();
        raised = raisedEvents;

        return new UpdateCadence(
            releaseFeed,
            new FakeInstallKindSource { Current = installKind },
            currentVersion,
            () => settingsHolder.Settings,
            settingsHolder.Record,
            raisedEvents.Add,
            timer,
            TimeSpan.FromHours(24),
            architecture: Architecture.X64);
    }

    /// <summary>
    /// A release JSON whose asset matches whatever <paramref name="kind"/> actually detects
    /// on (pinned to <see cref="Architecture.X64"/> in <see cref="CreateCadence"/>) — Windows
    /// detects on the installer, the two Linux kinds on their own package names
    /// (<see cref="UpdatePolicy.DetectionAsset"/>), so a Windows-shaped asset would never match
    /// a Linux selector and the test would prove nothing.
    /// </summary>
    private static string UpdateAvailableJsonFor(InstallKind kind, string tag)
    {
        var assetName = kind switch
        {
            InstallKind.LinuxPackage => $"o-view_{tag.TrimStart('v')}_amd64.deb",
            InstallKind.LinuxTarball => $"o-view-{tag.TrimStart('v')}-linux-x64.tar.gz",
            _ => "O-view-Setup.exe",
        };

        return $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": false,
          "assets": [ { "name": "{{assetName}}", "browser_download_url": "https://github.com/mlengmark/O-view/releases/download/{{tag}}/{{assetName}}" } ]
        }
        """;
    }

    /// <summary>The timer's handler runs <c>async void</c> fire-and-forget (the real
    /// composition root's own shape); tests against a <see cref="FakeTransport"/> that
    /// completes synchronously still need one await point for the continuation to run.</summary>
    private static void WaitForPendingWork() => Task.Delay(1).GetAwaiter().GetResult();

    private static string UpdateAvailableJson(string tag) =>
        $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": false,
          "assets": [ { "name": "O-view-Setup.exe", "browser_download_url": "https://github.com/mlengmark/O-view/releases/download/{{tag}}/O-view-Setup.exe" } ]
        }
        """;

    private static string UpToDateJson() =>
        """{ "tag_name": "v0.1.0", "draft": false, "prerelease": false, "assets": [] }""";

    private sealed class FakeSettingsHolder
    {
        public ShellSettings Settings { get; set; } = ShellSettings.Default;

        public bool AutoUpdateEnabled
        {
            set => Settings = Settings with { AutoUpdateEnabled = value };
        }

        public void Record(string tag) => Settings = Settings with { LastAnnouncedUpdateTag = tag };
    }

    private sealed class FakeInstallKindSource : IInstallKindSource
    {
        public InstallKind Current { get; set; } = InstallKind.WindowsInstaller;
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAppTimer : IAppTimer
    {
        public event EventHandler? Elapsed;

        public TimeSpan Interval { get; set; }

        public bool Started { get; private set; }

        public void Start() => Started = true;

        public void Stop() => Started = false;

        public void RaiseElapsed() => Elapsed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
        }
    }

    private sealed class FakeTransport : IReleaseFeedTransport
    {
        private readonly bool _throwOnFetch;

        public FakeTransport(string upToDateOrAvailableJson)
            : this(new ReleaseFeedResponse(200, null, null, null, upToDateOrAvailableJson))
        {
        }

        public FakeTransport(ReleaseFeedResponse? nextResponse = null, bool throwOnFetch = false)
        {
            NextResponse = nextResponse;
            _throwOnFetch = throwOnFetch;
        }

        public ReleaseFeedResponse? NextResponse { get; set; }

        public int CallCount { get; private set; }

        public Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation)
        {
            CallCount++;

            if (_throwOnFetch)
            {
                throw new HttpRequestException("simulated network failure");
            }

            return Task.FromResult(NextResponse!);
        }
    }
}
