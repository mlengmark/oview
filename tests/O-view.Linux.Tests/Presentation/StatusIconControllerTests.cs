using OView.App;
using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0008 slice 9 (OVI-403)'s decision logic, proved against fakes only — no
/// TrayIcon, no Avalonia Bitmap, no display.</summary>
public class StatusIconControllerTests
{
    [Fact]
    public void OnSnapshotUpdatedDoesNotRenderBeforeAHostIsObserved()
    {
        var renderCalls = 0;
        var controller = new StatusIconController(new FakeSkinToShell(), _ => renderCalls++, () => { });

        controller.OnSnapshotUpdated(UsageLevel.Amber);

        Assert.Equal(0, renderCalls);
        Assert.Equal(UsageLevel.Amber, controller.CurrentLevel);
        Assert.False(controller.HostPresent);
    }

    [Fact]
    public void OnHostObservedPresentRegistersAndRendersTheCurrentLevel()
    {
        var renders = new List<UsageLevel>();
        var registerCalls = 0;
        var controller = new StatusIconController(new FakeSkinToShell(), renders.Add, () => registerCalls++);

        controller.OnSnapshotUpdated(UsageLevel.Red);
        controller.OnHostObserved(present: true);

        Assert.Equal(1, registerCalls);
        Assert.Equal(new[] { UsageLevel.Red }, renders);
        Assert.True(controller.HostPresent);
    }

    [Fact]
    public void OnHostObservedAbsentRegistersAndRendersNothing()
    {
        var renderCalls = 0;
        var registerCalls = 0;
        var controller = new StatusIconController(new FakeSkinToShell(), _ => renderCalls++, () => registerCalls++);

        controller.OnHostObserved(present: false);

        Assert.Equal(0, registerCalls);
        Assert.Equal(0, renderCalls);
        Assert.False(controller.HostPresent);
    }

    [Fact]
    public void OnSnapshotUpdatedRendersWhileAHostIsPresent()
    {
        var renders = new List<UsageLevel>();
        var controller = new StatusIconController(new FakeSkinToShell(), renders.Add, () => { });

        controller.OnHostObserved(present: true);
        renders.Clear();
        controller.OnSnapshotUpdated(UsageLevel.Amber);

        Assert.Equal(new[] { UsageLevel.Amber }, renders);
    }

    [Fact]
    public void OnHostAppearedRegistersAndRendersWithoutARestart()
    {
        var renders = new List<UsageLevel>();
        var registerCalls = 0;
        var controller = new StatusIconController(new FakeSkinToShell(), renders.Add, () => registerCalls++);

        controller.OnSnapshotUpdated(UsageLevel.Green);
        controller.OnHostObserved(present: false);
        Assert.Equal(0, registerCalls);

        controller.OnHostAppeared();

        Assert.Equal(1, registerCalls);
        Assert.Equal(new[] { UsageLevel.Green }, renders);
        Assert.True(controller.HostPresent);
    }

    [Fact]
    public void OnActivatedCallsRequestWidgetTrueAndNothingElse()
    {
        var skin = new FakeSkinToShell();
        var controller = new StatusIconController(skin, _ => { }, () => { });

        controller.OnActivated();

        Assert.Equal(new List<bool> { true }, skin.RequestWidgetCalls);
        Assert.Empty(skin.OtherCalls);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(null!, _ => { }, () => { }));
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(new FakeSkinToShell(), null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new StatusIconController(new FakeSkinToShell(), _ => { }, null!));
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
