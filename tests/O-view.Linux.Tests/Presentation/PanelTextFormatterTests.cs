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
