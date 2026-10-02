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
