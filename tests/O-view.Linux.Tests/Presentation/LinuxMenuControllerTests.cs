using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0009 slice 9 (OVI-484)'s decision logic, proved against fakes only — no
/// NativeMenu, no real <c>.desktop</c> file.</summary>
public class LinuxMenuControllerTests
{
    [Fact]
    public void SnapshotReadsStartupLiveFromTheOsEveryCall()
    {
        var startup = new FakeStartupRegistration(enabled: false);
        var controller = new LinuxMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup);

        Assert.False(controller.Snapshot().StartupEnabled);

        // Something external (a deleted .desktop file, same as a user clearing Task Manager's
        // startup page on Windows) changed the OS state between two menu opens; the next
        // snapshot must reflect that, not a cached value.
        startup.SetEnabledForTest(true);

        Assert.True(controller.Snapshot().StartupEnabled);
    }

    [Fact]
    public void SnapshotReadsThresholdAndAutoUpdateFromTheShellsPersistedSettings()
    {
        var settings = ShellSettings.Default with { AlertThresholdPercent = 70, AutoUpdateEnabled = true };
        var controller = new LinuxMenuController(new FakeSkinToShell(), () => settings, new FakeStartupRegistration(enabled: false));

        var snapshot = controller.Snapshot();

        Assert.Equal(70, snapshot.ThresholdPercent);
        Assert.True(snapshot.AutoUpdateEnabled);
    }

    [Fact]
    public void SetThresholdPercentCallsTheShellWithTheRequestedPercent()
    {
        var shell = new FakeSkinToShell();
        var controller = new LinuxMenuController(shell, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false));

        controller.SetThresholdPercent(90);

        Assert.Equal(new[] { 90 }, shell.ThresholdCalls);
    }

    [Fact]
    public void SetAutoUpdateCallsTheShellWithTheRequestedValue()
    {
        var shell = new FakeSkinToShell();
        var controller = new LinuxMenuController(shell, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false));

        controller.SetAutoUpdate(true);

        Assert.Equal(new[] { true }, shell.AutoUpdateCalls);
    }

    [Fact]
    public void RefreshNowShowUsageDetailsCopyDiagnosticsAndQuitEachCallTheirSingleShellMember()
    {
        var shell = new FakeSkinToShell();
        var controller = new LinuxMenuController(shell, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false));

        controller.RefreshNow();
        controller.ShowUsageDetails();
        controller.CopyDiagnostics();
        controller.Quit();

        Assert.Equal(1, shell.RefreshNowCalls);
        Assert.Equal(new[] { true }, shell.RequestWidgetCalls);
        Assert.Equal(1, shell.WriteDiagnosticsBundleCalls);
        Assert.Equal(1, shell.QuitCalls);
    }

    [Fact]
    public void ToggleRunAtStartupRendersTheOsReturnedStateNotTheRequest()
    {
        var startup = new FakeStartupRegistration(enabled: false) { ApplyResult = true };
        var controller = new LinuxMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup);

        var outcome = controller.ToggleRunAtStartup(requestedEnabled: true);

        Assert.True(outcome.Enabled);
        Assert.False(outcome.Failed);
        Assert.Equal(new[] { true }, startup.ApplyCalls);
    }

    [Fact]
    public void ToggleRunAtStartupReportsFailedWhenTheOsReturnedStateDiffersFromTheRequest()
    {
        // The user asked to turn it on; writing the .desktop file failed and it is still off
        // (D3's own example scenario, mirrored from MenuFixtures.RunAtStartupEnableRequestedButFailed).
        var startup = new FakeStartupRegistration(enabled: false) { ApplyResult = false };
        var controller = new LinuxMenuController(new FakeSkinToShell(), () => ShellSettings.Default, startup);

        var outcome = controller.ToggleRunAtStartup(requestedEnabled: true);

        Assert.False(outcome.Enabled);
        Assert.True(outcome.Failed);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new LinuxMenuController(null!, () => ShellSettings.Default, new FakeStartupRegistration(enabled: false)));
        Assert.Throws<ArgumentNullException>(() => new LinuxMenuController(new FakeSkinToShell(), null!, new FakeStartupRegistration(enabled: false)));
        Assert.Throws<ArgumentNullException>(() => new LinuxMenuController(new FakeSkinToShell(), () => ShellSettings.Default, null!));
    }

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

        /// <summary>Drives <see cref="Apply"/>'s own override below directly, rather than its
        /// default interface-method composition over <see cref="Enable"/>/<see cref="Disable"/>.</summary>
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
}
