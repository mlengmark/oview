using OView.Core.Models;
using OView.Core.Providers.Jsonl;
using OView.Core.Statistics;
using OView.Core.Storage;

namespace OView.Core.Tests.Statistics;

/// <summary>
/// Proves ADR-0005 D6c's ledger-read seam, built here for the first time (ADR-0008 D9a,
/// OVI-326): per-model aggregation over a stated window, the Real-with-empty-Rows vs.
/// Unavailable distinction, and that no model is ever priced without a rate table this
/// repository does not have.
/// </summary>
public sealed class LedgerUsageStatisticsSourceTests : IDisposable
{
    private readonly string _directory;

    public LedgerUsageStatisticsSourceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi332-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static TranscriptRecord Record(
        string requestId,
        DateTimeOffset timestampUtc,
        string model = "claude-sonnet-5",
        long inputTokens = 100,
        long outputTokens = 50,
        long cacheCreationInputTokens = 0,
        long cacheReadInputTokens = 0)
        => new(
            requestId,
            timestampUtc,
            model,
            new TranscriptTokens(inputTokens, outputTokens, cacheCreationInputTokens, cacheReadInputTokens, null, null));

    [Fact]
    public void GetStatisticsOnAFreshStoreIsRealWithZeroCounts()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Real, statistics.OutputTokensToday.Status);
        Assert.Equal(0, statistics.OutputTokensToday.Value);
        Assert.Equal(UsageValueStatus.Real, statistics.OutputTokensWindow31d.Status);
        Assert.Equal(0, statistics.OutputTokensWindow31d.Value);
        Assert.Equal(0, statistics.HistoryCoverage.RecordedDays);
        Assert.Equal(31, statistics.HistoryCoverage.WindowDays);
    }

    [Fact]
    public void GetStatisticsNeverPricesAnythingBecauseNoRateCardExists()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-1", utcNow));

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Unavailable, statistics.EstimatedSpendToday.Status);
        Assert.Null(statistics.EstimatedSpendToday.Value);
        Assert.Equal(UsageValueStatus.Unavailable, statistics.EstimatedValueWindow31d.Status);
        Assert.Null(statistics.EstimatedValueWindow31d.Value);
    }

    [Fact]
    public void GetStatisticsCountsOnlyDaysFromFirstRecordedDayOnwardAsCoverage()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        // First activity is 5 days before "now", so only the most recent 5 of the 31-day
        // window are "recorded" — the rest predate the ledger's own history.
        store.Upsert(Record("req-1", utcNow.AddDays(-4)));

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(5, statistics.HistoryCoverage.RecordedDays);
        Assert.Equal(31, statistics.HistoryCoverage.WindowDays);
    }

    [Fact]
    public void GetStatisticsSumsOnlyTodaysOutputTokensIntoTheTodayFigure()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-today", utcNow, outputTokens: 50));
        store.Upsert(Record("req-yesterday", utcNow.AddDays(-1), outputTokens: 30));

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(50, statistics.OutputTokensToday.Value);
        Assert.Equal(80, statistics.OutputTokensWindow31d.Value);
    }

    [Fact]
    public void GetStatisticsExcludesActivityOutsideTheThirtyOneDayWindow()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-in-window", utcNow.AddDays(-30), outputTokens: 10));
        store.Upsert(Record("req-outside-window", utcNow.AddDays(-31), outputTokens: 999));

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(10, statistics.OutputTokensWindow31d.Value);
    }

    [Fact]
    public void GetModelBreakdownOnAnEmptyButRecordedWindowIsRealWithNoRows()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Real, breakdown.Status);
        Assert.Empty(breakdown.Rows);
    }

    [Fact]
    public void GetModelBreakdownAggregatesPerModelOverTheWindow()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-1", utcNow, model: "claude-sonnet-5", inputTokens: 100, outputTokens: 50));
        store.Upsert(Record("req-2", utcNow.AddDays(-1), model: "claude-sonnet-5", inputTokens: 200, outputTokens: 75));
        store.Upsert(Record("req-3", utcNow, model: "claude-opus-5", inputTokens: 10, outputTokens: 5));

        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(2, breakdown.Rows.Count);
        var sonnet = Assert.Single(breakdown.Rows, row => row.ModelId == "claude-sonnet-5");
        Assert.Equal(2, sonnet.RequestCount);
        Assert.Equal(300, sonnet.InputTokens.Value);
        Assert.Equal(125, sonnet.OutputTokens.Value);

        var opus = Assert.Single(breakdown.Rows, row => row.ModelId == "claude-opus-5");
        Assert.Equal(1, opus.RequestCount);
        Assert.Equal(10, opus.InputTokens.Value);
        Assert.Equal(5, opus.OutputTokens.Value);
    }

    [Fact]
    public void GetModelBreakdownAggregatesCacheTokensPerModel()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record(
            "req-1", utcNow, cacheCreationInputTokens: 40, cacheReadInputTokens: 10));
        store.Upsert(Record(
            "req-2", utcNow.AddDays(-1), cacheCreationInputTokens: 60, cacheReadInputTokens: 15));

        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        var row = Assert.Single(breakdown.Rows);
        Assert.Equal(100, row.CacheCreationTokens.Value);
        Assert.Equal(25, row.CacheReadTokens.Value);
    }

    [Fact]
    public void GetModelBreakdownLeavesEstimatedSpendUnavailableForAnUnpricedModel()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-1", utcNow, model: "some-unpriced-model"));

        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        var row = Assert.Single(breakdown.Rows);
        Assert.Equal(UsageValueStatus.Unavailable, row.EstimatedSpend.Status);
        Assert.Null(row.EstimatedSpend.Value);
    }

    [Fact]
    public void GetModelBreakdownReportsTheStatedWindowDates()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(new DateOnly(2026, 9, 2), breakdown.FromLocalDate);
        Assert.Equal(new DateOnly(2026, 10, 2), breakdown.ToLocalDate);
    }

    [Fact]
    public void GetDailySeriesMarksADayBeforeTheLedgersFirstRecordedDayAsAGapNotAZero()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        // First activity is 5 days before "now", so only the most recent 5 of the 31-day
        // window are recorded; the other 26 must render as gaps, not recorded zeroes.
        store.Upsert(Record("req-1", utcNow.AddDays(-4), outputTokens: 7));

        var series = source.GetDailySeries(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Real, series.Status);
        Assert.Equal(31, series.Days.Count);

        var recordedDay = series.Days.Single(d => d.LocalDate == new DateOnly(2026, 9, 28));
        Assert.Equal(UsageValueStatus.Real, recordedDay.OutputTokens.Status);
        Assert.Equal(7, recordedDay.OutputTokens.Value);

        var idleRecordedDay = series.Days.Single(d => d.LocalDate == new DateOnly(2026, 9, 29));
        Assert.Equal(UsageValueStatus.Real, idleRecordedDay.OutputTokens.Status);
        Assert.Equal(0, idleRecordedDay.OutputTokens.Value);

        var gapDay = series.Days.Single(d => d.LocalDate == new DateOnly(2026, 9, 27));
        Assert.Equal(UsageValueStatus.Unavailable, gapDay.OutputTokens.Status);
        Assert.Null(gapDay.OutputTokens.Value);
    }

    [Fact]
    public void GetDailySeriesAttributesARequestOnTheTwentyThreeHourSpringForwardDayToThatLocalDay()
    {
        var zone = CreateDstZone();
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);

        // 2026-03-08 02:30 local does not exist in CreateDstZone (clocks jump 02:00 -> 03:00),
        // so .NET resolves it onto the post-transition offset; either way the request must
        // attribute to local date 2026-03-08, the 23-hour day, not spill into the 7th or 9th.
        var requestLocal = new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified);
        var requestUtc = new DateTimeOffset(requestLocal, zone.GetUtcOffset(requestLocal));
        var utcNow = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-spring-forward", requestUtc, outputTokens: 11));

        var series = source.GetDailySeries(utcNow, zone);

        var springForwardDay = series.Days.Single(d => d.LocalDate == new DateOnly(2026, 3, 8));
        Assert.Equal(11, springForwardDay.OutputTokens.Value);
        Assert.DoesNotContain(series.Days, d => d.LocalDate == new DateOnly(2026, 3, 9) && d.OutputTokens.Value == 11);
    }

    [Fact]
    public void GetDailySeriesAttributesARequestOnTheTwentyFiveHourFallBackDayToThatLocalDay()
    {
        var zone = CreateDstZone();
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);

        // 2026-11-01 01:30 local occurs twice in CreateDstZone (clocks fall back 02:00 -> 01:00);
        // whichever occurrence .NET resolves this to, it must still attribute to local date
        // 2026-11-01, the 25-hour day, not get double-counted onto a second day.
        var requestLocal = new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);
        var requestUtc = new DateTimeOffset(requestLocal, zone.GetUtcOffset(requestLocal));
        var utcNow = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-fall-back", requestUtc, outputTokens: 13));

        var series = source.GetDailySeries(utcNow, zone);

        var fallBackDay = series.Days.Single(d => d.LocalDate == new DateOnly(2026, 11, 1));
        Assert.Equal(13, fallBackDay.OutputTokens.Value);
        Assert.Equal(1, series.Days.Count(d => d.OutputTokens.Value == 13));
    }

    [Fact]
    public void GetTokenKindTotalsSumsEachKindAcrossModelsForTheRequestedWindowOnly()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record(
            "req-today-sonnet", utcNow, model: "claude-sonnet-5",
            inputTokens: 100, outputTokens: 50, cacheCreationInputTokens: 20, cacheReadInputTokens: 5));
        store.Upsert(Record(
            "req-today-opus", utcNow, model: "claude-opus-5",
            inputTokens: 10, outputTokens: 4, cacheCreationInputTokens: 2, cacheReadInputTokens: 1));
        store.Upsert(Record(
            "req-yesterday", utcNow.AddDays(-1), model: "claude-sonnet-5",
            inputTokens: 1000, outputTokens: 1000, cacheCreationInputTokens: 1000, cacheReadInputTokens: 1000));

        var today = source.GetTokenKindTotals(utcNow, TimeZoneInfo.Utc, StatisticsWindow.Today);
        var window31d = source.GetTokenKindTotals(utcNow, TimeZoneInfo.Utc, StatisticsWindow.ThirtyOneDays);

        Assert.Equal(UsageValueStatus.Real, today.Status);
        Assert.Equal(110, today.Input.Tokens.Value);
        Assert.Equal(54, today.Output.Tokens.Value);
        Assert.Equal(22, today.CacheCreation.Tokens.Value);
        Assert.Equal(6, today.CacheRead.Tokens.Value);
        Assert.Equal(110 + 54 + 22 + 6, today.Total.Value);

        Assert.Equal(1110, window31d.Input.Tokens.Value);
        Assert.Equal(1054, window31d.Output.Tokens.Value);
    }

    [Fact]
    public void GetTokenKindTotalsNeverPricesAnythingBecauseNoRateCardExists()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        store.Upsert(Record("req-1", utcNow));

        var totals = source.GetTokenKindTotals(utcNow, TimeZoneInfo.Utc, StatisticsWindow.Today);

        Assert.Equal(UsageValueStatus.Unavailable, totals.Input.EstimatedValue.Status);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Rates.Status);
    }

    [Fact]
    public void GetResetBoundariesFallsBackToMondayWhenNoAnchorHasEverBeenObserved()
    {
        var store = new UsageLedgerStore(_directory);
        var source = new LedgerUsageStatisticsSource(store, anchorStore: null);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero); // a Friday

        var boundaries = source.GetResetBoundaries(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Real, boundaries.Status);
        Assert.NotEmpty(boundaries.Boundaries);
        Assert.All(boundaries.Boundaries, b => Assert.Equal(WeeklyResetBoundaryKind.MondayFallback, b.Kind));
        Assert.All(boundaries.Boundaries, b => Assert.Equal(DayOfWeek.Monday, b.Instant.DayOfWeek));
    }

    [Fact]
    public void GetResetBoundariesTagsTheStoredAnchorObservedAndStepsTheRestFromIt()
    {
        var store = new UsageLedgerStore(_directory);
        var anchorStore = new WeeklyResetAnchorStore(_directory);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        // The anchor is the next upcoming reset: three days into the future from "now", and
        // therefore outside the [windowStart, today] window itself — only the boundaries
        // stepped *back* from it land inside the window.
        var anchorInstant = utcNow.AddDays(3);
        anchorStore.Save(anchorInstant);

        var source = new LedgerUsageStatisticsSource(store, anchorStore);

        var boundaries = source.GetResetBoundaries(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageValueStatus.Real, boundaries.Status);
        Assert.NotEmpty(boundaries.Boundaries);
        Assert.DoesNotContain(boundaries.Boundaries, b => b.Kind == WeeklyResetBoundaryKind.Observed);
        Assert.All(boundaries.Boundaries, b => Assert.Equal(WeeklyResetBoundaryKind.DerivedFromObserved, b.Kind));

        // Every derived boundary is exactly a multiple of 7 days away from the anchor.
        Assert.All(boundaries.Boundaries, b =>
            Assert.Equal(0, (anchorInstant - b.Instant).Days % 7));
    }

    [Fact]
    public void GetResetBoundariesTagsAnAnchorInsideTheWindowAsObserved()
    {
        var store = new UsageLedgerStore(_directory);
        var anchorStore = new WeeklyResetAnchorStore(_directory);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        // An anchor observed yesterday falls inside the [windowStart, today] window itself.
        var anchorInstant = utcNow.AddDays(-1);
        anchorStore.Save(anchorInstant);

        var source = new LedgerUsageStatisticsSource(store, anchorStore);

        var boundaries = source.GetResetBoundaries(utcNow, TimeZoneInfo.Utc);

        var observed = Assert.Single(boundaries.Boundaries, b => b.Kind == WeeklyResetBoundaryKind.Observed);
        Assert.Equal(anchorInstant, observed.Instant);
        Assert.All(
            boundaries.Boundaries.Where(b => b.Kind != WeeklyResetBoundaryKind.Observed),
            b => Assert.Equal(WeeklyResetBoundaryKind.DerivedFromObserved, b.Kind));
    }

    [Fact]
    public void GetResetBoundariesStepsTheCorrectWallClockDayAcrossASpringForwardTransition()
    {
        var zone = CreateDstZone();
        var store = new UsageLedgerStore(_directory);
        var anchorStore = new WeeklyResetAnchorStore(_directory);

        // Anchor is local midnight on 2026-03-14 (daylight time, offset -4), six days after the
        // 2026-03-08 spring-forward (clocks jump 02:00 -> 03:00). Stepping one cadence (7 days)
        // back from it crosses that transition: the previous boundary must still land on local
        // midnight 2026-03-07 (standard time, offset -5) — the StepLocalDays re-resolution this
        // test exists to pin down — not on a UTC instant shifted by a flat 7 * 24 hours, which
        // would land on 2026-03-06 23:00 local, the wrong wall-clock day (ADR-0008 D9e).
        var anchorLocal = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);
        var anchorInstant = new DateTimeOffset(anchorLocal, zone.GetUtcOffset(anchorLocal));
        anchorStore.Save(anchorInstant);

        var source = new LedgerUsageStatisticsSource(store, anchorStore);
        var utcNow = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

        var boundaries = source.GetResetBoundaries(utcNow, zone);

        var expectedLocal = new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Unspecified);
        var expectedInstant = new DateTimeOffset(expectedLocal, zone.GetUtcOffset(expectedLocal));
        var naiveInstant = anchorInstant - TimeSpan.FromDays(7);

        Assert.Equal(TimeSpan.FromHours(-4), anchorInstant.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), expectedInstant.Offset);
        Assert.NotEqual(expectedInstant, naiveInstant);
        Assert.Contains(
            boundaries.Boundaries,
            b => b.Instant == expectedInstant && b.Kind == WeeklyResetBoundaryKind.DerivedFromObserved);
        Assert.DoesNotContain(boundaries.Boundaries, b => b.Instant == naiveInstant);
    }

    /// <summary>
    /// A synthetic zone with a US-shaped DST rule effective only around 2026, so
    /// <see cref="GetDailySeriesAttributesARequestOnTheTwentyThreeHourSpringForwardDayToThatLocalDay"/>
    /// and its 25-hour sibling are deterministic regardless of which OS tz database (or none)
    /// the test runner has — never <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>, which
    /// would make the test depend on the runner's own installed tz data.
    /// </summary>
    private static TimeZoneInfo CreateDstZone()
    {
        var toDaylight = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 8);
        var toStandard = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            toDaylight,
            toStandard);

        return TimeZoneInfo.CreateCustomTimeZone(
            "OView.Tests.Dst",
            TimeSpan.FromHours(-5),
            "Test DST zone",
            "Test standard time",
            "Test daylight time",
            new[] { rule });
    }

    [Fact]
    public void GetStatisticsAndGetModelBreakdownAreUnavailableWhenTheStoreCannotBeTrusted()
    {
        // Same deterministic "still can't be recovered" setup as
        // UsageLedgerStoreTests.ConstructingOverACorruptDatabaseFileReportsUnavailableWhenItCannotBeMovedAside:
        // a corrupt usage.db whose backup destination is occupied by a directory, so
        // CorruptStoreRecovery's move-aside fails on every platform and State lands on Unavailable.
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "usage.db");
        File.WriteAllText(path, "this is not a sqlite database");
        Directory.CreateDirectory(path + ".corrupt");

        var store = new UsageLedgerStore(_directory);
        Assert.Equal(HistoryStoreState.Unavailable, store.State);

        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        var statistics = source.GetStatistics(utcNow, TimeZoneInfo.Utc);
        var breakdown = source.GetModelBreakdown(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(UsageStatistics.Unavailable, statistics);
        Assert.Equal(ModelUsageBreakdown.Unavailable, breakdown);
    }
}
