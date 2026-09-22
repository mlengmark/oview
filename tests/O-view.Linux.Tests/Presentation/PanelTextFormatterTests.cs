using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

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

        Assert.Equal("Reading: now", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void FreshnessStampsAPastReadingWithItsOwnLocalTime()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.Live, lastIngestAt);

        Assert.Equal("Reading: 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    /// <summary>
    /// ADR-0001's confirmed source behaviour: a <see cref="DataSourceKind.Stale"/> reading
    /// renders identically to a <see cref="DataSourceKind.Live"/> one at the same age. This
    /// skin words the collapse differently from Tray, but the collapse itself is the same
    /// confirmed behaviour.
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

        Assert.Equal("Reading: 12:30", liveText);
        Assert.Equal(liveText, staleText);
    }

    [Fact]
    public void JsonlFallbackAlsoGetsTheReadingWordingRatherThanTheEstimateFraming()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.JsonlFallback, lastIngestAt);

        Assert.Equal("Reading: 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void EstimateGetsTheLocalEstimateFraming()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
        var lastIngestAt = new DateTimeOffset(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);
        var snapshot = MakeSnapshot(DataSourceKind.Estimate, lastIngestAt);

        Assert.Equal("Local estimate, reading: 12:30", PanelTextFormatter.Freshness(snapshot, utcNow, Utc));
    }

    [Fact]
    public void UnavailableSaysNoDataRegardlessOfTheSentinelLastIngestAt()
    {
        Assert.Equal("No usage data", PanelTextFormatter.Freshness(UsageSnapshot.Unavailable, DateTimeOffset.UtcNow, Utc));
    }

    [Theory]
    [InlineData(0.5, "less than a minute")]
    [InlineData(14, "14m")]
    [InlineData(134, "2h, 14m")]
    [InlineData(4600, "3d, 4h")]
    public void CountdownStepsDownUnitsWithMagnitude(double minutes, string expected)
    {
        Assert.Equal(expected, PanelTextFormatter.Countdown(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void SessionResetSaysNoResetObservedYetWhenNoneHasBeenSeen()
    {
        var result = PanelTextFormatter.SessionReset(null, DateTimeOffset.UtcNow, Utc);

        Assert.Equal("No reset observed yet", result);
    }

    [Fact]
    public void SessionResetRendersTheCountdownAndTimeWithNoApproxSuffixByDefault()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc);

        Assert.Equal("Resets in 2h, 14m, at 12:14", result);
    }

    [Fact]
    public void SessionResetMarksABracketedResetWithAnApproxSuffix()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc, TimeSpan.FromHours(1));

        Assert.Equal("Resets in 2h, 14m, at 12:14 (approx.)", result);
    }

    [Fact]
    public void SessionResetLeavesANarrowUncertaintyUnmarked()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc, TimeSpan.FromMinutes(5));

        Assert.DoesNotContain("approx", result);
    }

    [Fact]
    public void WeeklyResetIncludesTheWeekdayAndIsNeverApproximate()
    {
        var utcNow = new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.WeeklyReset(reset, utcNow, Utc);

        Assert.Equal("Resets in 7d, 3h, at Mon 23:00", result);
        Assert.DoesNotContain("approx", result);
    }

    [Fact]
    public void WeeklyResetConflictStatesTheReportedTimeAndWhatOViewIsDoing()
    {
        var reported = new DateTimeOffset(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.WeeklyResetConflict(reported, Utc);

        Assert.Contains("Mon 23:00", result);
        Assert.Contains("different weekly reset time", result);
        Assert.Contains("Re-enter", result);
    }

    [Fact]
    public void BoostChipWithBothFiguresSpellsOutPercentDateAndCountdown()
    {
        var notice = new BoostNotice("Get 50% more usage until Aug 31st!", 50, new DateOnly(2026, 8, 31));
        var utcNow = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.BoostChip(notice, utcNow, Utc);

        Assert.Equal("Boosted 50%, until 31 Aug, ends in 2w, 4d, 14h", result);
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

        Assert.Equal("Boosted 75%", result);
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
        Assert.Contains("per network", result);
        Assert.Contains("Your connection and install are fine", result);
    }

    [Fact]
    public void RateLimitedNoticeDoesNotInventARetryTimeWhenGitHubSentNone()
    {
        var result = PanelTextFormatter.RateLimitedNotice(null, Utc);

        Assert.DoesNotContain(":", result);
        Assert.Contains("scheduled check", result);
        Assert.Contains("Your connection and install are fine", result);
    }
}
