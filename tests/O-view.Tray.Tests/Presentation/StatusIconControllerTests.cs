using OView.App;
using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0008 slice 4 (OVI-371)'s decision logic, proved against fakes only — no NotifyIcon, no GDI handle, no native window.</summary>
public class StatusIconControllerTests
{
    [Fact]
    public void OnSnapshotUpdatedRendersTheNewLevelAtTheCurrentScale()
    {
        var renders = new List<(UsageLevel Level, double DpiScale)>();
        var controller = new StatusIconController(new FakeSkinToShell(), (level, scale) => renders.Add((level, scale)), () => { });

        controller.OnSnapshotUpdated(UsageLevel.Amber);

        Assert.Equal(new[] { (UsageLevel.Amber, 1.0) }, renders);
        Assert.Equal(UsageLevel.Amber, controller.CurrentLevel);
    }

    [Fact]
    public void OnDpiChangedRendersTheCurrentLevelAtTheNewScale()
    {
        var renders = new List<(UsageLevel Level, double DpiScale)>();
        var controller = new StatusIconController(new FakeSkinToShell(), (level, scale) => renders.Add((level, scale)), () => { });

        controller.OnSnapshotUpdated(UsageLevel.Red);
        controller.OnDpiChanged(2.0);

        Assert.Equal(new[] { (UsageLevel.Red, 1.0), (UsageLevel.Red, 2.0) }, renders);
        Assert.Equal(2.0, controller.CurrentDpiScale);
    }

    [Fact]
    public void OnHostRestartedReregistersAndRendersNothing()
    {
        var renderCalls = 0;
        var reregisterCalls = 0;
        var controller = new StatusIconController(new FakeSkinToShell(), (_, _) => renderCalls++, () => reregisterCalls++);

        controller.OnHostRestarted();

        Assert.Equal(1, reregisterCalls);
        Assert.Equal(0, renderCalls);
    }

    [Fact]
    public void OnActivatedCallsRequestWidgetTrueAndNothingElse()
    {
        var skin = new FakeSkinToShell();
        var controller = new StatusIconController(skin, (_, _) => { }, () => { });

        controller.OnActivated();

        Assert.Equal(new List<bool> { true }, skin.RequestWidgetCalls);
        Assert.Empty(skin.OtherCalls);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(null!, (_, _) => { }, () => { }));
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(new FakeSkinToShell(), null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(new FakeSkinToShell(), (_, _) => { }, null!));
    }

    private sealed class FakeSkinToShell : ISkinToShell
    {
        public List<bool> RequestWidgetCalls { get; } = new();

        public List<string> OtherCalls { get; } = new();

        public void RequestWidget(bool visible) => RequestWidgetCalls.Add(visible);

        public void RefreshNow() => OtherCalls.Add(nameof(RefreshNow));

        public void SetThresholdPercent(int percent) => OtherCalls.Add(nameof(SetThresholdPercent));

        public void SetAutoUpdate(bool enabled) => OtherCalls.Add(nameof(SetAutoUpdate));

        public void WriteDiagnosticsBundle() => OtherCalls.Add(nameof(WriteDiagnosticsBundle));

        public void Quit() => OtherCalls.Add(nameof(Quit));
    }
}
