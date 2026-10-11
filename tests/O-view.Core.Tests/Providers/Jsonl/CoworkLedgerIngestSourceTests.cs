using OView.Core.Models;
using OView.Core.Providers.Jsonl;
using OView.Core.Statistics;
using OView.Core.Storage;

namespace OView.Core.Tests.Providers.Jsonl;

/// <summary>
/// Proves ADR-0005 D2's 2026-10-09 amendment end to end: Cowork audit-log records reach the
/// ledger and, through <see cref="LedgerUsageStatisticsSource"/>, the daily series and
/// token-kind totals the detail window reads — without ever becoming a
/// <see cref="OView.Core.Providers.IUsageProvider"/> snapshot. All three of OVI-745's "done
/// when" conditions are covered here: a machine with no Cowork logs is unchanged, ingestion
/// is idempotent, and synthetic Cowork records are visible in both ledger queries.
/// </summary>
public sealed class CoworkLedgerIngestSourceTests : IDisposable
{
    private readonly string _storeDirectory;
    private readonly string _claudeRoot;

    public CoworkLedgerIngestSourceTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ovi745-" + Guid.NewGuid().ToString("N"));
        _storeDirectory = Path.Combine(baseDir, "store");
        _claudeRoot = Path.Combine(baseDir, "claude");
        Directory.CreateDirectory(_storeDirectory);
        Directory.CreateDirectory(_claudeRoot);
    }

    public void Dispose()
    {
        var baseDir = Path.GetDirectoryName(_storeDirectory)!;
        if (Directory.Exists(baseDir))
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    private static string CoworkAuditLine(string requestId, string timestamp, string model, long inputTokens, long outputTokens) =>
        "{\"type\":\"assistant\",\"request_id\":\"" + requestId + "\",\"timestamp\":\"" + timestamp + "\"," +
        "\"message\":{\"model\":\"" + model + "\",\"usage\":{" +
        "\"input_tokens\":" + inputTokens + ",\"output_tokens\":" + outputTokens + "," +
        "\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}";

    [Fact]
    public void NoCoworkLogsPresentLeavesTheLedgerFiguresByteIdenticalToBeforeIngest()
    {
        var store = new UsageLedgerStore(_storeDirectory);
        var source = new LedgerUsageStatisticsSource(store);
        var utcNow = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        var before = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        CoworkLedgerIngestSource.Ingest([_claudeRoot], store);

        var after = source.GetStatistics(utcNow, TimeZoneInfo.Utc);

        Assert.Equal(before, after);
        Assert.Equal(UsageValueStatus.Real, after.OutputTokensToday.Status);
        Assert.Equal(0, after.OutputTokensToday.Value);
    }

    [Fact]
    public void IngestingTheSameAuditLogTwiceDoesNotDoubleCount()
    {
        var sessionDir = Path.Combine(_claudeRoot, "local-agent-mode-sessions", "org", "user", "session-1");
        Directory.CreateDirectory(sessionDir);
        File.WriteAllLines(Path.Combine(sessionDir, "audit.jsonl"),
            [CoworkAuditLine("req-cowork-1", "2026-10-11T10:00:00.000Z", "claude-sonnet-5", 100, 50)]);

        var store = new UsageLedgerStore(_storeDirectory);
        var utcNow = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        CoworkLedgerIngestSource.Ingest([_claudeRoot], store);
        CoworkLedgerIngestSource.Ingest([_claudeRoot], store);

        var rows = store.QueryDailyUsage(TimeZoneInfo.Utc);

        Assert.Equal(1, rows.Single().RequestCount);
        Assert.Equal(50, rows.Single().OutputTokens);
    }

    [Fact]
    public void SyntheticCoworkRecordsAreIncludedInTheLedgersDailySeriesAndTokenKindTotals()
    {
        var sessionDir = Path.Combine(_claudeRoot, "local-agent-mode-sessions", "org", "user", "session-1");
        Directory.CreateDirectory(sessionDir);
        var utcNow = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        File.WriteAllLines(Path.Combine(sessionDir, "audit.jsonl"),
            [CoworkAuditLine("req-cowork-1", utcNow.ToString("O"), "claude-sonnet-5", 200, 75)]);

        var store = new UsageLedgerStore(_storeDirectory);
        CoworkLedgerIngestSource.Ingest([_claudeRoot], store);

        var source = new LedgerUsageStatisticsSource(store);

        var dailySeries = source.GetDailySeries(utcNow, TimeZoneInfo.Utc);
        var today = dailySeries.Days.Single(d => d.LocalDate == DateOnly.FromDateTime(utcNow.UtcDateTime));
        Assert.Equal(UsageValueStatus.Real, today.OutputTokens.Status);
        Assert.Equal(75, today.OutputTokens.Value);

        var tokenKindTotals = source.GetTokenKindTotals(utcNow, TimeZoneInfo.Utc, StatisticsWindow.Today);
        Assert.Equal(UsageValueStatus.Real, tokenKindTotals.Status);
        Assert.Equal(200, tokenKindTotals.Input.Tokens.Value);
        Assert.Equal(75, tokenKindTotals.Output.Tokens.Value);
    }

    [Fact]
    public void RecordsFromBothOnDiskShapesAreIngested()
    {
        var olderBuildSession = Path.Combine(_claudeRoot, "local-agent-mode-sessions", "org", "user", "session-old");
        Directory.CreateDirectory(olderBuildSession);
        File.WriteAllLines(Path.Combine(olderBuildSession, "audit.jsonl"),
            [CoworkAuditLine("req-old", "2026-10-11T09:00:00.000Z", "claude-sonnet-5", 10, 5)]);

        var currentBuildSession = Path.Combine(
            _claudeRoot, "local-agent-mode-sessions", "org", "user", "session-new",
            ".claude", "projects", "encoded-cwd");
        Directory.CreateDirectory(currentBuildSession);
        File.WriteAllLines(Path.Combine(currentBuildSession, "abc123.jsonl"),
            [CoworkAuditLine("req-new", "2026-10-11T09:05:00.000Z", "claude-sonnet-5", 20, 8)]);

        var store = new UsageLedgerStore(_storeDirectory);
        CoworkLedgerIngestSource.Ingest([_claudeRoot], store);

        var rows = store.QueryDailyUsage(TimeZoneInfo.Utc);

        Assert.Single(rows);
        Assert.Equal(2, rows.Single().RequestCount);
        Assert.Equal(13, rows.Single().OutputTokens);
    }
}
