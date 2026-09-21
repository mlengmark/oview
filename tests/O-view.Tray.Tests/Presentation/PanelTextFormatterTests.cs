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

    [Fact]
    public void SessionResetSaysUnknownWhenNoResetHasBeenObserved()
    {
        var result = PanelTextFormatter.SessionReset(null, DateTimeOffset.UtcNow, Utc);

        Assert.Equal("Reset time unknown (no reset observed yet)", result);
    }

    [Fact]
    public void SessionResetRendersTheCountdownAndTimeWithNoApproximateMarkerByDefault()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc);

        Assert.Equal("Resets in 2h 14m · 12:14", result);
    }

    [Fact]
    public void SessionResetMarksABracketedResetWithATilde()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc, TimeSpan.FromHours(1));

        Assert.Equal("Resets in 2h 14m · ~12:14", result);
    }

    [Fact]
    public void SessionResetLeavesANarrowUncertaintyUnmarked()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);

        var result = PanelTextFormatter.SessionReset(reset, utcNow, Utc, TimeSpan.FromMinutes(5));

        Assert.Equal("Resets in 2h 14m · 12:14", result);
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
