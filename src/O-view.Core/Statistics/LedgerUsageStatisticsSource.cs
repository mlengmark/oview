using Microsoft.Data.Sqlite;
using OView.Core.Models;
using OView.Core.Storage;

namespace OView.Core.Statistics;

/// <summary>
/// The only implementation of <see cref="IUsageStatisticsSource"/> (ADR-0005 D6c, amended by
/// ADR-0008 D9a): both queries aggregate <see cref="UsageLedgerStore"/> at read time, matching
/// ADR-0006 D1 — no rollup is ever persisted, and local-day bucketing is derived fresh from each
/// call's own <see cref="TimeZoneInfo"/>.
///
/// <para><b>No model is ever priced here.</b> This repository carries no rate table
/// (<c>RateCardSource</c> is reserved, nothing emits a <see cref="RateCardStamp"/> yet) — so
/// every <see cref="EstimatedUsd"/> this type produces is
/// <see cref="UsageValueStatus.Unavailable"/>, honestly, rather than a fabricated figure
/// (ADR-0001's "never fabricate a number" rule). Pricing is a later amendment with its own
/// seam, not something this slice improvises.</para>
/// </summary>
public sealed class LedgerUsageStatisticsSource : IUsageStatisticsSource
{
    private const int WindowDays = 31;

    private readonly UsageLedgerStore _store;

    public LedgerUsageStatisticsSource(UsageLedgerStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        if (!TryReadWindow(utcNow, zone, out var window))
        {
            return UsageStatistics.Unavailable;
        }

        var todayOutputTokens = window.TodayBuckets.Sum(b => b.OutputTokens);
        var windowOutputTokens = window.WindowBuckets.Sum(b => b.OutputTokens);

        return new UsageStatistics(
            new TokenCount(todayOutputTokens, UsageValueStatus.Real),
            new EstimatedUsd(null, UsageValueStatus.Unavailable),
            new TokenCount(windowOutputTokens, UsageValueStatus.Real),
            new EstimatedUsd(null, UsageValueStatus.Unavailable),
            new HistoryCoverage(window.RecordedDays, WindowDays));
    }

    /// <inheritdoc />
    public ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        if (!TryReadWindow(utcNow, zone, out var window))
        {
            return ModelUsageBreakdown.Unavailable;
        }

        var rows = window.WindowBuckets
            .GroupBy(b => b.Model, StringComparer.Ordinal)
            .Select(group => new ModelUsageRow(
                group.Key,
                group.Sum(b => b.RequestCount),
                new TokenCount(group.Sum(b => b.InputTokens), UsageValueStatus.Real),
                new TokenCount(group.Sum(b => b.OutputTokens), UsageValueStatus.Real),
                new TokenCount(group.Sum(b => b.CacheCreationInputTokens), UsageValueStatus.Real),
                new TokenCount(group.Sum(b => b.CacheReadInputTokens), UsageValueStatus.Real),
                new EstimatedUsd(null, UsageValueStatus.Unavailable)))
            .OrderByDescending(row => row.OutputTokens.Value)
            .ThenBy(row => row.ModelId, StringComparer.Ordinal)
            .ToList();

        return new ModelUsageBreakdown(
            window.WindowStart,
            window.Today,
            rows,
            new HistoryCoverage(window.RecordedDays, WindowDays),
            RateCardStamp.Unavailable,
            UsageValueStatus.Real);
    }

    /// <summary>
    /// Reads the whole ledger once and buckets it against <paramref name="utcNow"/>'s window, or
    /// returns <see langword="false"/> when the store cannot be trusted or the read fails — the
    /// one place both queries above share their "never throws" obligation.
    /// </summary>
    private bool TryReadWindow(DateTimeOffset utcNow, TimeZoneInfo zone, out LedgerWindow window)
    {
        window = default;

        if (_store.State == HistoryStoreState.Unavailable)
        {
            return false;
        }

        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
            var windowStart = today.AddDays(-(WindowDays - 1));

            var allBuckets = _store.QueryDailyUsage(zone);
            var windowBuckets = allBuckets
                .Where(b => b.LocalDate >= windowStart && b.LocalDate <= today)
                .ToList();
            var todayBuckets = windowBuckets.Where(b => b.LocalDate == today).ToList();

            // Every day from the first day the ledger ever recorded onward is "recorded",
            // whether or not it carried usage — a genuine zero still counts. Only days before
            // that first day are missing. The same line UsageLedgerStore's QueryDailyUsage
            // aggregate draws no further than the data itself answers.
            var firstRecordedDate = allBuckets.Count > 0
                ? allBuckets.Min(b => b.LocalDate)
                : (DateOnly?)null;

            var recordedDays = 0;
            for (var day = windowStart; day <= today; day = day.AddDays(1))
            {
                if (firstRecordedDate is { } first && day >= first)
                {
                    recordedDays++;
                }
            }

            window = new LedgerWindow(today, windowStart, todayBuckets, windowBuckets, recordedDays);
            return true;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private readonly record struct LedgerWindow(
        DateOnly Today,
        DateOnly WindowStart,
        IReadOnlyList<DailyModelUsage> TodayBuckets,
        IReadOnlyList<DailyModelUsage> WindowBuckets,
        int RecordedDays);
}
