using OView.App;
using OView.App.Updates;
using OView.Core.Updates;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0009 slice 6 (OVI-480)'s decision logic, proved against fakes only — no
/// ContextMenuStrip, no real registry.</summary>
public class TrayMenuControllerTests
{
    [Fact]
    public void BuildSnapshotReadsStartupLiveFromTheOsEveryCall()
    {
        var startup = new FakeStartupRegistration(enabled: false);
        var controller = new TrayMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup, CreateUpdateCadence());

        Assert.False(controller.BuildSnapshot().StartupEnabled);

        // Something external (Task Manager, a deleted .desktop file equivalent) changed the OS
        // state between two menu opens; the next snapshot must reflect that, not a cached value.
        startup.SetEnabledForTest(true);

        Assert.True(controller.BuildSnapshot().StartupEnabled);
    }

    [Fact]
    public void BuildSnapshotReadsThresholdAndAutoUpdateFromTheShellsPersistedSettings()
    {
        var settings = ShellSettings.Default with { AlertThresholdPercent = 70, AutoUpdateEnabled = true };
        var controller = new TrayMenuController(
            new FakeSkinToShell(), () => settings, new FakeStartupRegistration(enabled: false), CreateUpdateCadence());

        var snapshot = controller.BuildSnapshot();

        Assert.Equal(70, snapshot.ThresholdPercent);
        Assert.True(snapshot.AutoUpdateEnabled);
    }

    [Fact]
    public void OnSetThresholdPercentCallsTheShellWithTheRequestedPercent()
    {
        var skin = new FakeSkinToShell();
        var controller = new TrayMenuController(
            skin, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), CreateUpdateCadence());

        controller.OnSetThresholdPercent(90);

        Assert.Equal(new[] { 90 }, skin.ThresholdCalls);
    }

    [Fact]
    public void OnSetAutoUpdateCallsTheShellWithTheRequestedValue()
    {
        var skin = new FakeSkinToShell();
        var controller = new TrayMenuController(
            skin, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), CreateUpdateCadence());

        controller.OnSetAutoUpdate(true);

        Assert.Equal(new[] { true }, skin.AutoUpdateCalls);
    }

    [Fact]
    public void OnRefreshNowOnShowUsageDetailsOnCopyDiagnosticsAndOnQuitCallTheirSingleShellMember()
    {
        var skin = new FakeSkinToShell();
        var controller = new TrayMenuController(
            skin, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), CreateUpdateCadence());

        controller.OnRefreshNow();
        controller.OnShowUsageDetails();
        controller.OnCopyDiagnostics();
        controller.OnQuit();

        Assert.Equal(1, skin.RefreshNowCalls);
        Assert.Equal(new[] { true }, skin.RequestWidgetCalls);
        Assert.Equal(1, skin.WriteDiagnosticsBundleCalls);
        Assert.Equal(1, skin.QuitCalls);
    }

    [Fact]
    public void OnToggleRunAtStartupRendersTheOsReturnedStateNotTheRequest()
    {
        var startup = new FakeStartupRegistration(enabled: false) { ApplyResult = true };
        var controller = new TrayMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup, CreateUpdateCadence());

        var result = controller.OnToggleRunAtStartup(requested: true);

        Assert.True(result.Enabled);
        Assert.False(result.Failed);
        Assert.Equal(new[] { true }, startup.ApplyCalls);
    }

    [Fact]
    public void OnToggleRunAtStartupReportsFailedWhenTheOsReturnedStateDiffersFromTheRequest()
    {
        // The user asked to turn it on; the registry write failed and it is still off (D3's
        // own example scenario, mirrored from MenuFixtures.RunAtStartupEnableRequestedButFailed).
        var startup = new FakeStartupRegistration(enabled: false) { ApplyResult = false };
        var controller = new TrayMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup, CreateUpdateCadence());

        var result = controller.OnToggleRunAtStartup(requested: true);

        Assert.False(result.Enabled);
        Assert.True(result.Failed);
    }

    [Fact]
    public async Task OnCheckForUpdatesNowDelegatesToTheUpdateCadenceAndReturnsItsOutcome()
    {
        var controller = new TrayMenuController(
            new FakeSkinToShell(), () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), CreateUpdateCadence());

        var result = await controller.OnCheckForUpdatesNow();

        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(null!, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), CreateUpdateCadence()));
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(new FakeSkinToShell(), null!, new FakeStartupRegistration(enabled: false), CreateUpdateCadence()));
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(new FakeSkinToShell(), () => ShellSettings.Default, null!, CreateUpdateCadence()));
        Assert.Throws<ArgumentNullException>(() => new TrayMenuController(new FakeSkinToShell(), () => ShellSettings.Default, new FakeStartupRegistration(enabled: false), null!));
    }

    /// <summary>A real <see cref="UpdateCadence"/> over fakes only — no real HTTP, no real
    /// timer tick — just enough for <see cref="TrayMenuController"/>'s own constructor and
    /// <see cref="TrayMenuController.OnCheckForUpdatesNow"/> to have something to call.</summary>
    private static UpdateCadence CreateUpdateCadence() => new(
        new ReleaseFeed(new FakeReleaseFeedTransport(), new FakeClock(DateTimeOffset.UnixEpoch)),
        new FakeInstallKindSource(),
        "1.0.0",
        () => ShellSettings.Default,
        _ => { },
        _ => { },
        new FakeAppTimer(),
        TimeSpan.FromHours(24));

    private sealed class FakeSkinToShell : ISkinToShell
    {
        public int RefreshNowCalls { get; private set; }
        public List<bool> RequestWidgetCalls { get; } = new();
        public List<int> ThresholdCalls { get; } = new();
        public List<bool> AutoUpdateCalls { get; } = new();
        public int WriteDiagnosticsBundleCalls { get; private set; }
        public int QuitCalls { get; private set; }

        public void RefreshNow() => RefreshNowCalls++;

        public void RequestWidget(bool visible) => RequestWidgetCalls.Add(visible);

        public void SetThresholdPercent(int percent) => ThresholdCalls.Add(percent);

        public void SetAutoUpdate(bool enabled) => AutoUpdateCalls.Add(enabled);

        public void WriteDiagnosticsBundle() => WriteDiagnosticsBundleCalls++;

        public void Quit() => QuitCalls++;
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        private bool _enabled;

        public FakeStartupRegistration(bool enabled) => _enabled = enabled;

        /// <summary>Applies this type's own <see cref="Apply"/> override, so <see cref="ApplyResult"/>
        /// decides the outcome directly rather than going through <see cref="Enable"/>/<see cref="Disable"/>'s
        /// default interface-method composition.</summary>
        public bool ApplyResult { get; set; }

        public List<bool> ApplyCalls { get; } = new();

        public bool IsEnabled() => _enabled;

        public bool Enable()
        {
            _enabled = true;
            return true;
        }

        public bool Disable()
        {
            _enabled = false;
            return true;
        }

        public bool Apply(bool enable)
        {
            ApplyCalls.Add(enable);
            _enabled = ApplyResult;
            return ApplyResult;
        }

        public void SetEnabledForTest(bool enabled) => _enabled = enabled;
    }

    private sealed class FakeInstallKindSource : IInstallKindSource
    {
        public InstallKind Current => InstallKind.WindowsInstaller;
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAppTimer : IAppTimer
    {
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

        public void Dispose()
        {
        }
    }

    private sealed class FakeReleaseFeedTransport : IReleaseFeedTransport
    {
        public Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation) =>
            Task.FromResult(new ReleaseFeedResponse(
                200, null, null, null,
                """{ "tag_name": "v1.0.0", "draft": false, "prerelease": false, "assets": [] }"""));
    }
}
