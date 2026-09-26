using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

public class TooltipFormatterTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    /// <summary>
    /// Byte-for-byte regression target: the tooltip Rae II observed live against real data
    /// in OVI-4, "5h: 57% · resets 20:59 · 7d: 14% · resets Mon 23:00" (ADR-0001's "current
    /// codebase status" section). 2026-09-08 20:59 UTC and 2026-09-07 (Monday) 23:00 UTC
    /// reproduce that exact reading under the UTC display zone used throughout this suite.
    /// </summary>
    [Fact]
    public void ReproducesTheLiveTooltipObservedInOvi4()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: 57% · resets 20:59 · 7d: 14% · resets Mon 23:00", tooltip);
    }

    [Fact]
    public void UnknownResetsOmitTheirSegmentsEntirely()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(47, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(20, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: 47% · 7d: 20%", tooltip);
    }

    /// <summary>
    /// A reset Core flags <see cref="UsageValueStatus.Unavailable"/> is omitted even when it
    /// carries a value — matching <c>PanelTextFormatter.SessionReset</c>, which reads the
    /// same pair as "no reset observed" (OVI-144).
    /// </summary>
    [Fact]
    public void AnUnavailableResetCannotCarryAValueSoTheTooltipNeverShowsOne()
    {
        // OVI-144 built an Unavailable reset with a value and proved the tooltip omitted it.
        // OVI-146 makes that pair unbuildable in Core, which pins the same guarantee at the
        // one place that can make it: an unavailable reset never reaches the skin with a value.
        Assert.Throws<ArgumentException>(() =>
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() =>
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Unavailable));

        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(47, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(20, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: 47% · 7d: 20%", tooltip);
    }

    [Fact]
    public void UnavailableSnapshotSaysSoRatherThanShowingZero()
    {
        var tooltip = TooltipFormatter.Format(UsageSnapshot.Unavailable, Utc);

        Assert.Equal("O-view · no usage data", tooltip);
    }

    [Fact]
    public void UnknownPercentagesAdmitTheGapRatherThanGuessing()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Estimate,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("O-view · local estimate · usage % unknown", tooltip);
    }

    [Fact]
    public void FallbackSourcedPercentagesStillRenderNormallyWhenKnown()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.JsonlFallback,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(6, UsageValueStatus.Estimated),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(70, UsageValueStatus.Estimated),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: ~6% · 7d: ~70%", tooltip);
    }

    /// <summary>
    /// OVI-11's blocking finding: <c>Format</c> read only nullness, never <c>.Status</c>,
    /// so an estimated value rendered byte-identical to a real one. A snapshot mixing both
    /// in the same reading must mark only the estimated field.
    /// </summary>
    [Fact]
    public void MixedRealAndEstimatedFieldsAreLabelledPerField()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.JsonlFallback,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Estimated),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Estimated),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: 57% · resets 20:59 · 7d: ~14% · resets ~Mon 23:00", tooltip);
    }

    /// <summary>
    /// OVI-11's second finding: the "local estimate" fallback copy asserts something about
    /// the snapshot's own provenance, so it must only fire when <c>DataSourceKind</c> is
    /// actually <see cref="DataSourceKind.Estimate"/> — never merely because both
    /// percentages happen to be null (e.g. a <c>Live</c> snapshot with nothing sampled yet).
    /// </summary>
    [Fact]
    public void UnknownPercentagesWithALiveSourceDoNotClaimToBeALocalEstimate()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(null, UsageValueStatus.Unavailable),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Green);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: ?", tooltip);
        Assert.DoesNotContain("local estimate", tooltip);
    }

    [Fact]
    public void PercentagesRoundToTheNearestIntegerWithNoDecimal()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(56.5, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            new UsagePercent(13.4, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Amber);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal("5h: 57% · 7d: 13%", tooltip);
    }

    /// <summary>
    /// The 127-character NotifyIcon.Text cap is enforced only here, in the Windows skin —
    /// never in O-view.Core (see O-view.Core.Tests' UsageSnapshotTests, which proves the
    /// Core assembly defines no such member at all).
    /// </summary>
    [Fact]
    public void TheLengthCapIsEnforcedHereInTheWindowsSkin()
    {
        var longText = new string('x', 200);

        var capped = TooltipFormatter.Cap(longText);

        Assert.Equal(TooltipFormatter.MaxLength, capped.Length);
    }

    [Fact]
    public void RealisticOutputNeverExceedsTheNotifyIconCap()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(100, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 23, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(100, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 9, 23, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Red);

        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.True(tooltip.Length <= TooltipFormatter.MaxLength);
    }
}
