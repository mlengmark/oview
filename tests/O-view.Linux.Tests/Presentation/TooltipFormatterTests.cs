using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

public class TooltipFormatterTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void KnownGoodReadingRendersSessionAndWeeklyWithResets()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 57%, resets 20:59 / Week 14%, resets Mon 23:00", tooltip);
    }

    [Fact]
    public void UnknownResetsOmitTheirSegmentsEntirely()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new UsagePercent(47, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(20, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 47% / Week 20%", tooltip);
    }

    [Fact]
    public void UnavailableSnapshotSaysSoRatherThanShowingZero()
    {
        var tooltip = TooltipFormatter.Format(UsageSnapshot.Unavailable, Utc);

        Assert.Equal("O-view: usage data unavailable", tooltip);
    }

    [Fact]
    public void UnknownPercentagesAdmitTheGapRatherThanGuessing()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Estimate,
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("O-view: estimated reading, percentages not yet known", tooltip);
    }

    [Fact]
    public void EstimatedValuesAreMarkedPerField()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.JsonlFallback,
            new UsagePercent(6, UsageValueStatus.Estimated),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(70, UsageValueStatus.Estimated),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 6% (est.) / Week 70% (est.)", tooltip);
    }

    [Fact]
    public void MixedRealAndEstimatedFieldsAreLabelledPerField()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.JsonlFallback,
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Estimated),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Estimated),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 57%, resets 20:59 / Week 14% (est.), resets Mon 23:00 (est.)", tooltip);
    }

    [Fact]
    public void UnknownPercentagesWithALiveSourceDoNotClaimToBeAnEstimatedReading()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session unknown", tooltip);
        Assert.DoesNotContain("estimated", tooltip);
    }

    [Fact]
    public void PercentagesRoundToTheNearestIntegerWithNoDecimal()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new UsagePercent(56.5, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(13.4, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 57% / Week 13%", tooltip);
    }

    /// <summary>
    /// Unlike <c>O-view.Tray</c>'s formatter, this skin defines no length cap at all — the
    /// 127-character <c>NotifyIcon.Text</c> limit is a Windows API fact that must not travel
    /// here (ADR-0001, ADR-0002). A maximal reading renders in full, untruncated.
    /// </summary>
    [Fact]
    public void RealisticOutputIsNeverTruncated()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new UsagePercent(100, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 23, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(100, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 9, 23, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Red);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("Session 100%, resets 23:59 / Week 100%, resets Wed 23:59", tooltip);
    }
}
