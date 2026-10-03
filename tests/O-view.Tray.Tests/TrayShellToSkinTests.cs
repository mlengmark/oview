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
