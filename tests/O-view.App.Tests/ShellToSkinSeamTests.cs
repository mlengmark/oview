using OView.Core.Models;

namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0007 D6's shell-to-skin direction against fakes only: a driving harness
/// stands in for the shell and calls <see cref="IShellToSkin"/> exactly as the shell would,
/// and a fake skin records what it received. This is a structural/contract test proving the
/// seam carries the right calls with the right data — not a real UI integration (that is a
/// later slice; no WPF/Avalonia here).
/// </summary>
public class ShellToSkinSeamTests
{
    private static UsageSnapshot Snapshot() => new(
        DataSourceKind.Live,
        DateTimeOffset.UnixEpoch,
        new UsagePercent(42, UsageValueStatus.Real),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(10, UsageValueStatus.Real),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Amber);

    [Fact]
    public void Driving_shell_calls_ShowSnapshot_and_the_skin_receives_the_exact_snapshot()
    {
        var skin = new FakeSkin();
        var snapshot = Snapshot();

        DriveAsShell(skin).ShowSnapshot(snapshot);

        Assert.Equal(snapshot, skin.LastSnapshot);
    }

    [Fact]
    public void Driving_shell_calls_RaiseEvent_and_the_skin_receives_the_exact_event()
    {
        var skin = new FakeSkin();
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        DriveAsShell(skin).RaiseEvent(usageEvent);

        Assert.Equal(usageEvent, skin.LastEvent);
    }

    [Fact]
    public void Driving_shell_calls_ShowDetail_and_the_skin_receives_the_exact_detail()
    {
        var skin = new FakeSkin();
        var detail = new UsageDetail(Snapshot(), UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        DriveAsShell(skin).ShowDetail(detail);

        Assert.Equal(detail, skin.LastDetail);
    }

    [Fact]
    public void Driving_shell_calls_SetVisible_and_the_skin_receives_the_requested_visibility()
    {
        var skin = new FakeSkin();

        DriveAsShell(skin).SetVisible(true);

        Assert.True(skin.LastVisible);
    }

    [Fact]
    public void Driving_shell_calls_Shutdown_and_the_skin_observes_it()
    {
        var skin = new FakeSkin();

        DriveAsShell(skin).Shutdown();

        Assert.True(skin.ShutdownCalled);
    }

    /// <summary>Stands in for the shell: accepts the interface, not the concrete fake, so
    /// this test proves the call goes through the contract rather than a direct reference.</summary>
    private static IShellToSkin DriveAsShell(IShellToSkin skin) => skin;

    private sealed class FakeSkin : IShellToSkin
    {
        public UsageSnapshot? LastSnapshot { get; private set; }

        public UsageEvent? LastEvent { get; private set; }

        public UsageDetail? LastDetail { get; private set; }

        public bool? LastVisible { get; private set; }

        public bool ShutdownCalled { get; private set; }

        public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

        public void RaiseEvent(UsageEvent usageEvent) => LastEvent = usageEvent;

        public void ShowDetail(UsageDetail detail) => LastDetail = detail;

        public void SetVisible(bool visible) => LastVisible = visible;

        public void Shutdown() => ShutdownCalled = true;
    }
}
