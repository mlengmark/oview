using OView.Core.Models;
using OView.Core.Providers.Jsonl;
using OView.Core.Storage;

namespace OView.Core.Tests.Storage;

/// <summary>
/// Proves ADR-0006 slice 2's contract: upsert-only writes keyed on <c>request_id</c> (D1's
/// de-duplication/idempotency mechanism) and query-time (local date × model) aggregation that
/// never persists a rollup, including across a UTC-day straddle (source issue #211).
/// </summary>
public sealed class UsageLedgerStoreTests : IDisposable
{
    private readonly string _directory;

    public UsageLedgerStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi230-" + Guid.NewGuid().ToString("N"));
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
        long cacheReadInputTokens = 0,
        long? cacheCreationEphemeral5mTokens = null,
        long? cacheCreationEphemeral1hTokens = null)
        => new(
            requestId,
            timestampUtc,
            model,
            new TranscriptTokens(
                inputTokens,
                outputTokens,
                cacheCreationInputTokens,
                cacheReadInputTokens,
                cacheCreationEphemeral5mTokens,
                cacheCreationEphemeral1hTokens));

    [Fact]
    public void QueryDailyUsageOnAFreshStoreIsEmpty()
    {
        var store = new UsageLedgerStore(_directory);

        var result = store.QueryDailyUsage(TimeZoneInfo.Utc);

        Assert.Empty(result);
    }

    [Fact]
    public void SaveCreatesTheDirectoryWhenItDoesNotAlreadyExist()
    {
        var nested = Path.Combine(_directory, "nested", "path");

        _ = new UsageLedgerStore(nested);

        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void ReingestingTheSameRequestIdRewritesTheRowRatherThanDuplicatingIt()
    {
        var store = new UsageLedgerStore(_directory);
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        store.Upsert(Record("req-1", timestamp, inputTokens: 100));
        store.Upsert(Record("req-1", timestamp, inputTokens: 100));

        var daily = store.QueryDailyUsage(TimeZoneInfo.Utc);

        var bucket = Assert.Single(daily);
        Assert.Equal(1, bucket.RequestCount);
        Assert.Equal(100, bucket.InputTokens);
    }

    [Fact]
    public void ReingestingWithChangedFieldsOverwritesTheStoredRow()
    {
        var store = new UsageLedgerStore(_directory);
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        store.Upsert(Record("req-1", timestamp, inputTokens: 100));
        store.Upsert(Record("req-1", timestamp, inputTokens: 250));

        var daily = store.QueryDailyUsage(TimeZoneInfo.Utc);

        var bucket = Assert.Single(daily);
        Assert.Equal(1, bucket.RequestCount);
        Assert.Equal(250, bucket.InputTokens);
    }

    [Fact]
    public void UpsertRangeAppliesDuplicateRequestIdsInFileOrderSoTheLastOccurrenceWins()
    {
        var store = new UsageLedgerStore(_directory);
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        store.UpsertRange(
        [
            Record("req-1", timestamp, inputTokens: 10),
            Record("req-1", timestamp, inputTokens: 20),
            Record("req-1", timestamp, inputTokens: 30),
        ]);

        var daily = store.QueryDailyUsage(TimeZoneInfo.Utc);

        var bucket = Assert.Single(daily);
        Assert.Equal(1, bucket.RequestCount);
        Assert.Equal(30, bucket.InputTokens);
    }

    [Fact]
    public void DistinctRequestIdsAccumulateSeparately()
    {
        var store = new UsageLedgerStore(_directory);
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        store.UpsertRange(
        [
            Record("req-1", timestamp, inputTokens: 10),
            Record("req-2", timestamp, inputTokens: 20),
        ]);

        var bucket = Assert.Single(store.QueryDailyUsage(TimeZoneInfo.Utc));
        Assert.Equal(2, bucket.RequestCount);
        Assert.Equal(30, bucket.InputTokens);
    }

    [Fact]
    public void QueryDailyUsageGroupsByLocalDateAndModel()
    {
        var store = new UsageLedgerStore(_directory);

        store.UpsertRange(
        [
            Record("req-1", new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero), model: "claude-sonnet-5", inputTokens: 10),
            Record("req-2", new DateTimeOffset(2026, 9, 25, 11, 0, 0, TimeSpan.Zero), model: "claude-sonnet-5", inputTokens: 20),
            Record("req-3", new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero), model: "claude-opus-5-5", inputTokens: 40),
            Record("req-4", new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero), model: "claude-sonnet-5", inputTokens: 5),
        ]);

        var daily = store.QueryDailyUsage(TimeZoneInfo.Utc);

        Assert.Equal(3, daily.Count);

        var sept25Sonnet = daily.Single(d => d.LocalDate == new DateOnly(2026, 9, 25) && d.Model == "claude-sonnet-5");
        Assert.Equal(2, sept25Sonnet.RequestCount);
        Assert.Equal(30, sept25Sonnet.InputTokens);

        var sept25Opus = daily.Single(d => d.LocalDate == new DateOnly(2026, 9, 25) && d.Model == "claude-opus-5-5");
        Assert.Equal(1, sept25Opus.RequestCount);
        Assert.Equal(40, sept25Opus.InputTokens);

        var sept26 = daily.Single(d => d.LocalDate == new DateOnly(2026, 9, 26));
        Assert.Equal(1, sept26.RequestCount);
        Assert.Equal(5, sept26.InputTokens);
    }

    [Fact]
    public void QueryDailyUsageBucketsByTheCallerSuppliedZoneNotUtc()
    {
        // 2026-09-26 01:00 UTC is still 2026-09-25 local in a UTC-8 zone — the straddle
        // source issue #211 describes. A stored utc_date would put this on the wrong day;
        // query-time bucketing against the caller's zone must not.
        var store = new UsageLedgerStore(_directory);
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC-8", TimeSpan.FromHours(-8), "UTC-8", "UTC-8");
        var straddlingTimestamp = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);

        store.Upsert(Record("req-1", straddlingTimestamp));

        var utcDaily = store.QueryDailyUsage(TimeZoneInfo.Utc);
        var localDaily = store.QueryDailyUsage(zone);

        Assert.Equal(new DateOnly(2026, 9, 26), Assert.Single(utcDaily).LocalDate);
        Assert.Equal(new DateOnly(2026, 9, 25), Assert.Single(localDaily).LocalDate);
    }

    [Fact]
    public void QueryDailyUsageSumsAllTokenFieldsTreatingAbsentEphemeralCacheAsZero()
    {
        var store = new UsageLedgerStore(_directory);
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        store.UpsertRange(
        [
            Record(
                "req-1", timestamp,
                inputTokens: 10, outputTokens: 20,
                cacheCreationInputTokens: 30, cacheReadInputTokens: 40,
                cacheCreationEphemeral5mTokens: 5, cacheCreationEphemeral1hTokens: null),
            Record(
                "req-2", timestamp,
                inputTokens: 1, outputTokens: 2,
                cacheCreationInputTokens: 3, cacheReadInputTokens: 4,
                cacheCreationEphemeral5mTokens: null, cacheCreationEphemeral1hTokens: 7),
        ]);

        var bucket = Assert.Single(store.QueryDailyUsage(TimeZoneInfo.Utc));

        Assert.Equal(11, bucket.InputTokens);
        Assert.Equal(22, bucket.OutputTokens);
        Assert.Equal(33, bucket.CacheCreationInputTokens);
        Assert.Equal(44, bucket.CacheReadInputTokens);
        Assert.Equal(5, bucket.CacheCreationEphemeral5mTokens);
        Assert.Equal(7, bucket.CacheCreationEphemeral1hTokens);
    }

    [Fact]
    public void ANewStoreInstanceReadsBackRowsWrittenByAPreviousInstance()
    {
        var timestamp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        new UsageLedgerStore(_directory).Upsert(Record("req-1", timestamp, inputTokens: 42));

        var reopened = new UsageLedgerStore(_directory);
        var bucket = Assert.Single(reopened.QueryDailyUsage(TimeZoneInfo.Utc));

        Assert.Equal(42, bucket.InputTokens);
    }

    [Fact]
    public void StateIsOkWhenTheDatabaseFileNeverExistedOrWasNeverCorrupt()
    {
        var store = new UsageLedgerStore(_directory);

        store.Upsert(Record("req-1", new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal(HistoryStoreState.Ok, store.State);
    }

    [Fact]
    public void ACorruptDatabaseFileIsMovedAsideRatherThanLeftOrDeleted()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "usage.db");
        File.WriteAllText(path, "this is not a sqlite database");

        _ = new UsageLedgerStore(_directory);

        Assert.True(File.Exists(path + ".corrupt"));
        Assert.Equal("this is not a sqlite database", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void ConstructingOverACorruptDatabaseFileReportsRebuiltAndStartsAnEmptyUsableStore()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "usage.db"), "this is not a sqlite database");

        var store = new UsageLedgerStore(_directory);

        Assert.Equal(HistoryStoreState.Rebuilt, store.State);
        Assert.Empty(store.QueryDailyUsage(TimeZoneInfo.Utc));

        store.Upsert(Record("req-1", new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)));
        Assert.Single(store.QueryDailyUsage(TimeZoneInfo.Utc));
    }

    [Fact]
    public void ConstructingOverACorruptDatabaseFileReportsUnavailableWhenItCannotBeMovedAside()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "usage.db");
        File.WriteAllText(path, "this is not a sqlite database");
        // Occupy the exact backup destination with a directory: File.Move onto an existing
        // directory always fails, on every platform, regardless of permissions — the
        // deterministic way to force the "still can't be recovered" branch in a test.
        Directory.CreateDirectory(path + ".corrupt");

        var store = new UsageLedgerStore(_directory);

        Assert.Equal(HistoryStoreState.Unavailable, store.State);
        Assert.True(File.Exists(path));
    }
}
