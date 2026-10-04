using OView.App;
using OView.Core.Models;
using OView.Linux;

namespace OView.Linux.Tests;

/// <summary>
/// Covers <see cref="LinuxShellToSkin"/> (ADR-0008 slice 8, OVI-397) directly: every
/// <see cref="OView.App.IShellToSkin"/> member records exactly what it received and renders
/// nothing — no status icon, no window, no toast exists yet to assert against.
/// </summary>
public class LinuxShellToSkinTests
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
        var skin = new LinuxShellToSkin();
        var snapshot = Snapshot();

        skin.ShowSnapshot(snapshot);

        Assert.Equal(snapshot, skin.LastSnapshot);
    }

    [Fact]
    public void LastSnapshot_is_null_before_the_first_call()
    {
        var skin = new LinuxShellToSkin();

        Assert.Null(skin.LastSnapshot);
    }

    [Fact]
    public void RaiseEvent_records_the_exact_event()
    {
        var skin = new LinuxShellToSkin();
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        skin.RaiseEvent(usageEvent);

        Assert.Equal(usageEvent, skin.LastEvent);
    }

    [Fact]
    public void ShowDetail_records_the_exact_detail()
    {
        var skin = new LinuxShellToSkin();
        var detail = UsageDetail.Unavailable;

        skin.ShowDetail(detail);

        Assert.Equal(detail, skin.LastDetail);
    }

    [Fact]
    public void SetVisible_records_the_exact_visibility()
    {
        var skin = new LinuxShellToSkin();

        skin.SetVisible(true);

        Assert.True(skin.LastVisible);
    }

    [Fact]
    public void Shutdown_records_that_it_was_called()
    {
        var skin = new LinuxShellToSkin();

        Assert.False(skin.ShutdownCalled);
        skin.Shutdown();

        Assert.True(skin.ShutdownCalled);
    }
}
