using OView.App;
using OView.Core.Models;
using OView.Tray;

namespace OView.Tray.Tests;

/// <summary>
/// Covers <see cref="TrayShellToSkin"/> (ADR-0008 slice 3, OVI-360) directly: every
/// <see cref="IShellToSkin"/> member records exactly what it received and renders nothing —
/// no <c>NotifyIcon</c>, no window, no toast exists yet to assert against.
/// </summary>
public class TrayShellToSkinTests
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
    public void ShowSnapshot_records_the_exact_snapshot()
    {
        var skin = new TrayShellToSkin();
        var snapshot = Snapshot();

        skin.ShowSnapshot(snapshot);

        Assert.Equal(snapshot, skin.LastSnapshot);
    }

    [Fact]
    public void RaiseEvent_records_the_exact_event()
    {
        var skin = new TrayShellToSkin();
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        skin.RaiseEvent(usageEvent);

        Assert.Equal(usageEvent, skin.LastEvent);
    }

    [Fact]
    public void RaiseEvent_raises_EventRaised_with_the_exact_event_exactly_once()
    {
        var skin = new TrayShellToSkin();
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };
        var received = new List<UsageEvent>();
        skin.EventRaised += received.Add;

        skin.RaiseEvent(usageEvent);

        var raised = Assert.Single(received);
        Assert.Equal(usageEvent, raised);
    }

    [Fact]
    public void EventRaised_is_a_no_op_when_nothing_has_subscribed()
    {
        var skin = new TrayShellToSkin();

        var exception = Record.Exception(() => skin.RaiseEvent(new UsageEvent(UsageEventKind.UpdateAvailable)));

        Assert.Null(exception);
    }

    [Fact]
    public void ShowDetail_records_the_exact_detail()
    {
        var skin = new TrayShellToSkin();
        var detail = UsageDetail.Unavailable;

        skin.ShowDetail(detail);

        Assert.Equal(detail, skin.LastDetail);
    }

    [Fact]
    public void SetVisible_records_the_requested_visibility()
    {
        var skin = new TrayShellToSkin();

        skin.SetVisible(true);

        Assert.True(skin.LastVisible);
    }

    [Fact]
    public void Shutdown_is_observed()
    {
        var skin = new TrayShellToSkin();

        skin.Shutdown();

        Assert.True(skin.ShutdownCalled);
    }

    [Fact]
    public void ShowDetail_raises_DetailShown_with_the_exact_detail()
    {
        var skin = new TrayShellToSkin();
        var detail = UsageDetail.Unavailable;
        UsageDetail? raised = null;
        skin.DetailShown += d => raised = d;

        skin.ShowDetail(detail);

        Assert.Equal(detail, raised);
    }

    [Fact]
    public void SetVisible_raises_VisibilityChanged_with_the_exact_visibility()
    {
        var skin = new TrayShellToSkin();
        bool? raised = null;
        skin.VisibilityChanged += v => raised = v;

        skin.SetVisible(true);

        Assert.True(raised);
    }

    [Fact]
    public void ShowDetail_and_SetVisible_do_not_throw_when_nothing_has_subscribed()
    {
        var skin = new TrayShellToSkin();

        skin.ShowDetail(UsageDetail.Unavailable);
        skin.SetVisible(false);

        Assert.Equal(UsageDetail.Unavailable, skin.LastDetail);
        Assert.False(skin.LastVisible);
    }

    [Fact]
    public void Before_any_call_every_recorded_value_is_unset()
    {
        var skin = new TrayShellToSkin();

        Assert.Null(skin.LastSnapshot);
        Assert.Null(skin.LastEvent);
        Assert.Null(skin.LastDetail);
        Assert.Null(skin.LastVisible);
        Assert.False(skin.ShutdownCalled);
    }
}
