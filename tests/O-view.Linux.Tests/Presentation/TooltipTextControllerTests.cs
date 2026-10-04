using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0008 slice 11's wiring (OVI-417), proved against a fake setter only — no
/// <c>TrayIcon</c>, matching slice 9/10's own no-toolkit-construction precedent.</summary>
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
        Assert.Equal(new[] { "Session 57%, resets 20:59 / Week 14%, resets Mon 23:00" }, pushed);
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
            new[] { TooltipFormatter.Format(UsageSnapshot.Unavailable, Utc), "Session 47% / Week 20%" },
            pushed);
    }

    [Fact]
    public void ConstructorRejectsNullSetter()
    {
        Assert.Throws<ArgumentNullException>(() => new TooltipTextController(null!));
    }
}
