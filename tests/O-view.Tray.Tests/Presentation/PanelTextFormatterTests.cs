using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

public class PanelTextFormatterTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static UsageSnapshot MakeSnapshot(DataSourceKind kind, DateTimeOffset lastIngestAt) => new(
        kind,
        lastIngestAt,
        new UsagePercent(57, UsageValueStatus.Real),
        new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
        new UsagePercent(14, UsageValueStatus.Real),
        new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
        UsageLevel.Amber);

    [Fact]
    public void FreshnessSaysNowForAReadingCapturedInTheCurrentClockMinute()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 30, 40, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.Live, lastIngestAt);

        Assert.Equal("As of now", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void FreshnessStampsAPastReadingWithItsOwnLocalTime()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.Live, lastIngestAt);

        Assert.Equal("As of 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    /// <summary>
    /// ADR-0001's confirmed source behaviour: a <see cref="DataSourceKind.Stale"/> reading
    /// renders identically to a <see cref="DataSourceKind.Live"/> one at the same age — the
    /// two tiers collapse in <c>Freshness</c> on purpose, because the age itself says more
    /// than the tier does, and both are authoritative either way.
    /// </summary>
    [Fact]
    public void StaleCollapsesIntoTheSameWordingAsLiveAtTheSameAge()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var liveSnapshot = MakeSnapshot(DataSourceKind.Live, lastIngestAt);
        var staleSnapshot = MakeSnapshot(DataSourceKind.Stale, lastIngestAt);

        var liveText = PanelTextFormatter.Freshness(liveSnapshot, utcNow, Utc);
        var staleText = PanelTextFormatter.Freshness(staleSnapshot, utcNow, Utc);

        Assert.Equal("As of 12:30", liveText);
        Assert.Equal(liveText, staleText);
    }

    [Fact]
    public void JsonlFallbackAlsoGetsTheAsOfWordingRatherThanTheEstimateFraming()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.JsonlFallback, lastIngestAt);

        Assert.Equal("As of 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void EstimateGetsTheLocalEstimateFraming()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.Estimate, lastIngestAt);

        Assert.Equal("Local estimate · as of 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void UnavailableSaysNoDataRegardlessOfTheSentinelLastIngestAt()
    {
        Assert.Equal("No data", PanelTextFormatter.Freshness(UsageSnapshot.Unavailable, DateTimeOffset.UtcNow, Utc));
    }

    [Theory]
    [InlineData(0.5, "under a minute")]
    [InlineData(14, "14m")]
    [InlineData(134, "2h 14m")]
    [InlineData(4600, "3d 4h")]
    public void CountdownStepsDownUnitsWithMagnitude(double minutes, string expected)
    {
        Assert.Equal(expected, PanelTextFormatter.Countdown(TimeSpan.FromMinutes(minutes)));
    }

    private static readonly DateTimeOffset SessionUtcNow = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionResetInstant = new(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

    [Fact]
    public void SessionResetSaysUnknownWhenTheResetIsUnavailable()
    {
        var result = PanelTextFormatter.SessionReset(
            new UsageInstant(null, UsageValueStatus.Unavailable), DateTimeOffset.UtcNow, Utc);

        Assert.Equal("Reset time unknown (no reset observed yet)", result);
    }

    [Fact]
    public void AnUnavailableSessionResetCannotCarryAValueSoThePanelSaysUnknown()
    {
        // OVI-139 built an Unavailable reset with a value and proved the panel ignored it.
        // OVI-146 makes that pair unbuildable in Core, so the guarantee is pinned there.
        Assert.Throws<ArgumentException>(() => new UsageInstant(SessionResetInstant, UsageValueStatus.Unavailable));

        var result = PanelTextFormatter.SessionReset(
            new UsageInstant(null, UsageValueStatus.Unavailable), SessionUtcNow, Utc);

        Assert.Equal("Reset time unknown (no reset observed yet)", result);
    }

    [Fact]
    public void SessionResetMarksAnEstimatedResetWithATilde()
    {
        var result = PanelTextFormatter.SessionReset(
            new UsageInstant(SessionResetInstant, UsageValueStatus.Estimated), SessionUtcNow, Utc);

        Assert.Equal("Resets in 2h 14m · ~12:14", result);
    }

    [Fact]
    public void SessionResetLeavesARealResetUnmarked()
    {
        var result = PanelTextFormatter.SessionReset(
            new UsageInstant(SessionResetInstant, UsageValueStatus.Real), SessionUtcNow, Utc);

        Assert.Equal("Resets in 2h 14m · 12:14", result);
    }

    /// <summary>
    /// The panel and the tooltip must agree on <i>whether</i> one session-reset value is
    /// marked approximate (ADR-0001, 2026-09-25 amendment, D2). The snapshot's other fields
    /// are all <see cref="UsageValueStatus.Real"/> (weekly reset unavailable), so any
    /// <c>~</c> in the tooltip can only have come from the session reset.
    /// </summary>
    [Theory]
    [InlineData(UsageValueStatus.Real, false)]
    [InlineData(UsageValueStatus.Estimated, true)]
    [InlineData(UsageValueStatus.Unavailable, false)]
    public void SessionResetAndTheTooltipAgreeOnWhetherTheResetIsMarked(UsageValueStatus status, bool marked)
    {
        var instant = new UsageInstant(status == UsageValueStatus.Unavailable ? null : SessionResetInstant, status);
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            SessionUtcNow,
            new UsagePercent(57, UsageValueStatus.Real),
            instant,
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(null, UsageValueStatus.Unavailable),
            UsageLevel.Amber);

        var panel = PanelTextFormatter.SessionReset(instant, SessionUtcNow, Utc);
        var tooltip = TooltipFormatter.Format(snapshot, Utc);

        Assert.Equal(marked, panel.Contains('~'));
        Assert.Equal(marked, tooltip.Contains('~'));
    }

    [Fact]
    public void WeeklyResetIncludesTheWeekdayAndIsNeverApproximate()
    {
        var utcNow = new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.WeeklyReset(reset, utcNow, Utc);

        Assert.Equal("Resets in 7d 3h · Mon 23:00", result);
        Assert.DoesNotContain("~", result);
    }

    [Fact]
    public void WeeklyResetConflictStatesTheReportedTimeAndWhatOViewIsDoing()
    {
        var reported = new DateTimeOffset(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.WeeklyResetConflict(reported, Utc);

        Assert.Contains("Mon 23:00", result);
        Assert.Contains("does not match", result);
        Assert.Contains("Re-enter", result);
    }

    [Fact]
    public void BoostChipWithBothFiguresSpellsOutPercentDateAndCountdown()
    {
        var notice = new BoostNotice("Get 50% more usage until Aug 31st!", 50, new DateOnly(2026, 8, 31));
        var utcNow = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.BoostChip(notice, utcNow, Utc);

        Assert.Equal("50% Boosted · until 31 Aug · ends in 2w 4d 14h", result);
    }

    [Fact]
    public void BoostChipWithNoFiguresParsedFallsBackToBoostedAlone()
    {
        var notice = new BoostNotice("There's a usage boost active on your account.", null, null);

        var result = PanelTextFormatter.BoostChip(notice, DateTimeOffset.UtcNow, Utc);

        Assert.Equal("Boosted", result);
    }

    [Fact]
    public void BoostChipWithPercentButNoEndDateOmitsTheCountdown()
    {
        var notice = new BoostNotice("Get 75% more usage, no end date given.", 75, null);

        var result = PanelTextFormatter.BoostChip(notice, DateTimeOffset.UtcNow, Utc);

        Assert.Equal("75% Boosted", result);
    }

    [Fact]
    public void BoostChipEndingWithinTheHourSaysUnderAnHourRatherThanZeroUnits()
    {
        var endsOn = new DateOnly(2026, 8, 31);
        var notice = new BoostNotice("Boost ending very soon.", 20, endsOn);
        // End-of-day for 31 Aug UTC is 2026-09-01T00:00Z; 30 minutes before that.
        var utcNow = new DateTimeOffset(2026, 8, 31, 23, 30, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.BoostChip(notice, utcNow, Utc);

        Assert.Contains("ends in under an hour", result);
    }

    [Fact]
    public void BoostCardRelaysTheMessageVerbatimAndNamesTheEndDateAndReadTime()
    {
        var notice = new BoostNotice("Enjoy 50% more usage on your plan until August 31st.", 50, new DateOnly(2026, 8, 31));
        var fetchedAtUtc = new DateTimeOffset(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.BoostCard(notice, fetchedAtUtc, Utc);

        Assert.Contains("Enjoy 50% more usage on your plan until August 31st.", result);
        Assert.Contains("Ends Mon 31 Aug", result);
        Assert.Contains("14:00", result);
    }

    [Fact]
    public void BoostCardWithNoEndDateStillRelaysTheMessageAndReadTime()
    {
        var notice = new BoostNotice("You're getting a usage boost right now.", null, null);
        var fetchedAtUtc = new DateTimeOffset(2026, 8, 20, 9, 5, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.BoostCard(notice, fetchedAtUtc, Utc);

        Assert.Contains("You're getting a usage boost right now.", result);
        Assert.Contains("09:05", result);
        Assert.DoesNotContain("Ends", result);
    }

    [Fact]
    public void RateLimitedNoticeStatesTheRetryTimeWhenGitHubSentOne()
    {
        var retryAfterUtc = new DateTimeOffset(2026, 9, 21, 14, 30, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.RateLimitedNotice(retryAfterUtc, Utc);

        Assert.Contains("14:30", result);
        Assert.Contains("shared", result);
        Assert.Contains("Nothing is wrong with your connection", result);
    }

    [Fact]
    public void RateLimitedNoticeDoesNotInventARetryTimeWhenGitHubSentNone()
    {
        var result = PanelTextFormatter.RateLimitedNotice(null, Utc);

        Assert.DoesNotContain(":", result);
        Assert.Contains("next check", result);
        Assert.Contains("Nothing is wrong with your connection", result);
    }
}
