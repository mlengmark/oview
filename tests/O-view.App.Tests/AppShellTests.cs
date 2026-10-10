using OView.Core.Models;
using OView.Core.Statistics;

namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0009 slicing table row 2 (OVI-447): <see cref="AppShell"/> is the real
/// <see cref="ISkinToShell"/> that replaces both composition roots' <c>PendingSkinToShell</c>
/// stub. Against fakes only — no real timer, no real clock, no WPF/Avalonia — except the
/// settings-store tests, which use a real temp directory the same way
/// <see cref="ShellSettingsStoreTests"/> does, because the load-before-compose order
/// (ADR-0009 D4 point 1) is the one thing worth proving end to end.
/// </summary>
public class AppShellTests : IDisposable
{
    private readonly string _directory;

    public AppShellTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "oview-app-shell-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static UsageSnapshot Snapshot(DateTimeOffset lastIngestAt) => new(
        DataSourceKind.Live,
        lastIngestAt,
        new UsagePercent(42, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(10, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Green);

    private AppShell CreateShell(
        out UsagePollLoop pollLoop,
        out FakeAppTimer timer,
        ShellSettings? initialSettings = null)
    {
        var settingsStore = new ShellSettingsStore(_directory);
        var settings = initialSettings ?? ShellSettings.Default;
        timer = new FakeAppTimer();
        pollLoop = new UsagePollLoop(
            new FakeUsageProvider(utcNow => Snapshot(utcNow)),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            settings.PollCadence);
        var detailCoordinator = new DetailPushCoordinator(
            new FakeStatisticsSource(), new FakeSkin(), new FakeClock(DateTimeOffset.UnixEpoch), TimeZoneInfo.Utc);
        var diagnosticsWriter = new DiagnosticsBundleWriter(_directory, new FakeClock(DateTimeOffset.UnixEpoch));

        return new AppShell(settingsStore, settings, pollLoop, detailCoordinator, diagnosticsWriter, new FakeSkin(), new StoreLifetime(_directory));
    }

    [Fact]
    public void RefreshNow_polls_the_loop_immediately()
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.RefreshNow();

        Assert.NotEqual(UsageSnapshot.Unavailable, pollLoop.CurrentSnapshot);
    }

    [Fact]
    public void RequestWidget_delegates_to_the_detail_coordinator()
    {
        var skin = new FakeSkin();
        var settingsStore = new ShellSettingsStore(_directory);
        var timer = new FakeAppTimer();
        using var pollLoop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable), new FakeClock(DateTimeOffset.UnixEpoch), timer, TimeSpan.FromMinutes(1));
        var detailCoordinator = new DetailPushCoordinator(
            new FakeStatisticsSource(), skin, new FakeClock(DateTimeOffset.UnixEpoch), TimeZoneInfo.Utc);
        var diagnosticsWriter = new DiagnosticsBundleWriter(_directory, new FakeClock(DateTimeOffset.UnixEpoch));
        var shell = new AppShell(settingsStore, ShellSettings.Default, pollLoop, detailCoordinator, diagnosticsWriter, skin, new StoreLifetime(_directory));

        shell.RequestWidget(true);

        Assert.Equal(true, skin.LastVisible);
    }

    [Fact]
    public void ToggleWidget_delegates_to_the_detail_coordinator()
    {
        var skin = new FakeSkin();
        var settingsStore = new ShellSettingsStore(_directory);
        var timer = new FakeAppTimer();
        using var pollLoop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable), new FakeClock(DateTimeOffset.UnixEpoch), timer, TimeSpan.FromMinutes(1));
        var detailCoordinator = new DetailPushCoordinator(
            new FakeStatisticsSource(), skin, new FakeClock(DateTimeOffset.UnixEpoch), TimeZoneInfo.Utc);
        var diagnosticsWriter = new DiagnosticsBundleWriter(_directory, new FakeClock(DateTimeOffset.UnixEpoch));
        var shell = new AppShell(settingsStore, ShellSettings.Default, pollLoop, detailCoordinator, diagnosticsWriter, skin, new StoreLifetime(_directory));

        shell.ToggleWidget();

        Assert.Equal(true, skin.LastVisible);
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    public void SetThresholdPercent_clamps_to_0_100(int requested, int expectedClamped)
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.SetThresholdPercent(requested);

        Assert.Equal(expectedClamped, shell.Settings.AlertThresholdPercent);
    }

    [Fact]
    public void SetThresholdPercent_persists_the_clamped_value()
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.SetThresholdPercent(200);

        var reloaded = new ShellSettingsStore(_directory).Load();
        Assert.Equal(100, reloaded.AlertThresholdPercent);
    }

    [Fact]
    public void SetAutoUpdate_updates_and_persists_the_setting()
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.SetAutoUpdate(true);

        Assert.True(shell.Settings.AutoUpdateEnabled);
        var reloaded = new ShellSettingsStore(_directory).Load();
        Assert.True(reloaded.AutoUpdateEnabled);
    }

    [Fact]
    public void RecordAnnouncedUpdateTag_updates_and_persists_the_tag()
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.RecordAnnouncedUpdateTag("v1.2.3");

        Assert.Equal("v1.2.3", shell.Settings.LastAnnouncedUpdateTag);
        var reloaded = new ShellSettingsStore(_directory).Load();
        Assert.Equal("v1.2.3", reloaded.LastAnnouncedUpdateTag);
    }

    [Fact]
    public void WriteDiagnosticsBundle_writes_a_bundle_file()
    {
        var shell = CreateShell(out var pollLoop, out _);
        using var pollLoopScope = pollLoop;

        shell.WriteDiagnosticsBundle();

        Assert.True(File.Exists(Path.Combine(_directory, "diagnostics", "diagnostics.json")));
    }

    [Fact]
    public void Quit_shuts_down_the_skin_then_stops_the_poll_loop_then_disposes_the_stores()
    {
        var order = new List<string>();
        var skin = new OrderRecordingSkin(order);
        var timer = new OrderRecordingTimer(order);
        var storeLifetime = new OrderRecordingDisposable(order);
        var settingsStore = new ShellSettingsStore(_directory);
        var pollLoop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable), new FakeClock(DateTimeOffset.UnixEpoch), timer, TimeSpan.FromMinutes(1));
        var detailCoordinator = new DetailPushCoordinator(
            new FakeStatisticsSource(), skin, new FakeClock(DateTimeOffset.UnixEpoch), TimeZoneInfo.Utc);
        var diagnosticsWriter = new DiagnosticsBundleWriter(_directory, new FakeClock(DateTimeOffset.UnixEpoch));
        var shell = new AppShell(settingsStore, ShellSettings.Default, pollLoop, detailCoordinator, diagnosticsWriter, skin, storeLifetime);

        shell.Quit();

        Assert.Equal(new[] { "skin-shutdown", "poll-loop-timer-disposed", "stores-disposed" }, order);
    }

    [Fact]
    public void Composing_with_a_loaded_non_default_cadence_starts_the_poll_loop_on_that_cadence()
    {
        var store = new ShellSettingsStore(_directory);
        store.Save(new ShellSettings(80, TimeSpan.FromSeconds(45), false));

        // Mirrors the composition order ADR-0009 D4 point 1 requires: load settings, then
        // build the poll loop from the loaded cadence — not ShellSettings.Default's.
        var loaded = store.Load();
        var timer = new FakeAppTimer();
        using var pollLoop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable), new FakeClock(DateTimeOffset.UnixEpoch), timer, loaded.PollCadence);

        Assert.Equal(TimeSpan.FromSeconds(45), timer.Interval);
    }

    private sealed class FakeUsageProvider : OView.Core.Providers.IUsageProvider
    {
        private readonly Func<DateTimeOffset, UsageSnapshot> _getSnapshot;

        public FakeUsageProvider(Func<DateTimeOffset, UsageSnapshot> getSnapshot) => _getSnapshot = getSnapshot;

        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow) => _getSnapshot(utcNow);
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

        public bool Disposed { get; private set; }

        public void Start() => Started = true;

        public void Stop() => Started = false;

        public void RaiseElapsed() => Elapsed?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeStatisticsSource : IUsageStatisticsSource
    {
        public UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone) => UsageStatistics.Unavailable;

        public ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone) => ModelUsageBreakdown.Unavailable;

        public DailyUsageSeries GetDailySeries(DateTimeOffset utcNow, TimeZoneInfo zone) => DailyUsageSeries.Unavailable;

        public TokenKindTotals GetTokenKindTotals(DateTimeOffset utcNow, TimeZoneInfo zone, StatisticsWindow window) =>
            TokenKindTotals.Unavailable;

        public WeeklyResetBoundaries GetResetBoundaries(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            WeeklyResetBoundaries.Unavailable;
    }

    private sealed class FakeSkin : IShellToSkin
    {
        public bool? LastVisible { get; private set; }

        public void ShowSnapshot(UsageSnapshot snapshot)
        {
        }

        public void RaiseEvent(UsageEvent usageEvent)
        {
        }

        public void ShowDetail(UsageDetail detail)
        {
        }

        public void SetVisible(bool visible) => LastVisible = visible;

        public void Shutdown()
        {
        }
    }

    /// <summary>Records to a shared order log on <see cref="Shutdown"/>, for
    /// <see cref="Quit_shuts_down_the_skin_then_stops_the_poll_loop_then_disposes_the_stores"/>.</summary>
    private sealed class OrderRecordingSkin : IShellToSkin
    {
        private readonly List<string> _order;

        public OrderRecordingSkin(List<string> order) => _order = order;

        public void ShowSnapshot(UsageSnapshot snapshot)
        {
        }

        public void RaiseEvent(UsageEvent usageEvent)
        {
        }

        public void ShowDetail(UsageDetail detail)
        {
        }

        public void SetVisible(bool visible)
        {
        }

        public void Shutdown() => _order.Add("skin-shutdown");
    }

    /// <summary>Records to a shared order log on <see cref="Dispose"/> — the last thing
    /// <see cref="UsagePollLoop.Dispose"/> does — for
    /// <see cref="Quit_shuts_down_the_skin_then_stops_the_poll_loop_then_disposes_the_stores"/>.</summary>
    private sealed class OrderRecordingTimer : IAppTimer
    {
        private readonly List<string> _order;

        public OrderRecordingTimer(List<string> order) => _order = order;

        public event EventHandler? Elapsed
        {
            add { }
            remove { }
        }

        public TimeSpan Interval { get; set; }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose() => _order.Add("poll-loop-timer-disposed");
    }

    /// <summary>Records to a shared order log on <see cref="Dispose"/>, standing in for
    /// <see cref="StoreLifetime"/> in
    /// <see cref="Quit_shuts_down_the_skin_then_stops_the_poll_loop_then_disposes_the_stores"/>.</summary>
    private sealed class OrderRecordingDisposable : IDisposable
    {
        private readonly List<string> _order;

        public OrderRecordingDisposable(List<string> order) => _order = order;

        public void Dispose() => _order.Add("stores-disposed");
    }
}
