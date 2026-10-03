using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0008 slice 5's wiring (OVI-376), proved against a fake setter only — no NotifyIcon.</summary>
public class TooltipTextControllerTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void OnSnapshotUpdatedPushesTheFormatterOutputToTheSetter()
    {
        var pushed = new List<string>();
        var controller = new TooltipTextController(text => pushed.Add(text), Utc);
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Amber);

        controller.OnSnapshotUpdated(snapshot);

        Assert.Equal(new[] { TooltipFormatter.Format(snapshot, Utc) }, pushed);
        Assert.Equal(new[] { "5h: 57% · resets 20:59 · 7d: 14% · resets Mon 23:00" }, pushed);
    }

    [Fact]
    public void EachSnapshotPushesItsOwnReformattedText()
    {
        var pushed = new List<string>();
        var controller = new TooltipTextController(text => pushed.Add(text), Utc);

        controller.OnSnapshotUpdated(UsageSnapshot.Unavailable);
        controller.OnSnapshotUpdated(new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(47, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(20, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green));

        Assert.Equal(
            new[] { TooltipFormatter.Format(UsageSnapshot.Unavailable, Utc), "5h: 47% · 7d: 20%" },
            pushed);
    }

    [Fact]
    public void ConstructorRejectsNullSetter()
    {
        Assert.Throws<ArgumentNullException>(() => new TooltipTextController(null!));
    }
}
