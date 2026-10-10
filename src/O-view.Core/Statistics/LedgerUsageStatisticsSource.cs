using Microsoft.Data.Sqlite;
using OView.Core.Models;
using OView.Core.Storage;

namespace OView.Core.Statistics;

/// <summary>
/// The only implementation of <see cref="IUsageStatisticsSource"/> (ADR-0005 D6c, amended by
/// ADR-0008 D9a and, for <see cref="GetDailySeries"/>/<see cref="GetTokenKindTotals"/>/
/// <see cref="GetResetBoundaries"/>, by the 2026-10-09 gate G7 parity amendment/OVI-635):
/// every query aggregates <see cref="UsageLedgerStore"/> (and, for reset boundaries,
/// <see cref="WeeklyResetAnchorStore"/>) at read time, matching ADR-0006 D1 — no rollup is ever
/// persisted, and local-day bucketing is derived fresh from each call's own
/// <see cref="TimeZoneInfo"/>.
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

    /// <summary>The weekly-reset cadence boundaries are stepped by (ADR-0008 D9e's three
    /// boundary kinds). Not configurable — this repository knows of exactly one plan cadence.</summary>
    private const int CadenceDays = 7;

    /// <summary>Bounds how far <see cref="GetResetBoundaries"/> steps away from a stored anchor
    /// before giving up, so a corrupted anchor far outside any plausible date cannot turn a
    /// query into an unbounded loop (ADR-0005 D1's "never throws" obligation extended to "never
    /// hangs"). 1,600 cadence-steps is over 30 years either side of the anchor — far past any
    /// anchor this repository could legitimately observe.</summary>
    private const int MaxCadenceSteps = 1600;

    private readonly UsageLedgerStore _store;
    private readonly WeeklyResetAnchorStore? _anchorStore;

    /// <param name="store">The ledger <see cref="GetStatistics"/>, <see cref="GetModelBreakdown"/>,
    /// <see cref="GetDailySeries"/>, and <see cref="GetTokenKindTotals"/> all read.</param>
    /// <param name="anchorStore">The single stored weekly-reset anchor <see cref="GetResetBoundaries"/>
    /// steps from. Optional: a caller that has not wired this store yet still gets a working
    /// source, just one whose reset boundaries always fall back to the Monday convention.</param>
    public LedgerUsageStatisticsSource(UsageLedgerStore store, WeeklyResetAnchorStore? anchorStore = null)
    {
        _store = store;
        _anchorStore = anchorStore;
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

    /// <inheritdoc />
    public DailyUsageSeries GetDailySeries(DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        if (!TryReadWindow(utcNow, zone, out var window))
        {
            return DailyUsageSeries.Unavailable;
        }

        var days = new List<DailyUsagePoint>(WindowDays);
        for (var day = window.WindowStart; day <= window.Today; day = day.AddDays(1))
        {
            if (window.FirstRecordedDate is not { } first || day < first)
            {
                // Before the ledger's own first recorded day: a gap, not a recorded zero.
                days.Add(new DailyUsagePoint(day, new TokenCount(null, UsageValueStatus.Unavailable)));
                continue;
            }

            var outputTokens = window.WindowBuckets
                .Where(b => b.LocalDate == day)
                .Sum(b => b.OutputTokens);
            days.Add(new DailyUsagePoint(day, new TokenCount(outputTokens, UsageValueStatus.Real)));
        }

        return new DailyUsageSeries(
            window.WindowStart,
            window.Today,
            days,
            new HistoryCoverage(window.RecordedDays, WindowDays),
            UsageValueStatus.Real);
    }

    /// <inheritdoc />
    public TokenKindTotals GetTokenKindTotals(DateTimeOffset utcNow, TimeZoneInfo zone, StatisticsWindow window)
    {
        if (!TryReadWindow(utcNow, zone, out var ledgerWindow))
        {
            return TokenKindTotals.Unavailable;
        }

        var buckets = window == StatisticsWindow.Today ? ledgerWindow.TodayBuckets : ledgerWindow.WindowBuckets;
        var fromDate = window == StatisticsWindow.Today ? ledgerWindow.Today : ledgerWindow.WindowStart;

        var input = buckets.Sum(b => b.InputTokens);
        var output = buckets.Sum(b => b.OutputTokens);
        var cacheCreation = buckets.Sum(b => b.CacheCreationInputTokens);
        var cacheRead = buckets.Sum(b => b.CacheReadInputTokens);
        var total = input + output + cacheCreation + cacheRead;

        var unpriced = new EstimatedUsd(null, UsageValueStatus.Unavailable);

        return new TokenKindTotals(
            fromDate,
            ledgerWindow.Today,
            new TokenKindAmount(new TokenCount(input, UsageValueStatus.Real), unpriced),
            new TokenKindAmount(new TokenCount(output, UsageValueStatus.Real), unpriced),
            new TokenKindAmount(new TokenCount(cacheCreation, UsageValueStatus.Real), unpriced),
            new TokenKindAmount(new TokenCount(cacheRead, UsageValueStatus.Real), unpriced),
            new TokenCount(total, UsageValueStatus.Real),
            RateCardStamp.Unavailable,
            UsageValueStatus.Real);
    }

    /// <inheritdoc />
    public WeeklyResetBoundaries GetResetBoundaries(DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
            var windowStart = today.AddDays(-(WindowDays - 1));

            var windowStartInstant = LocalMidnight(windowStart, zone);
            var windowEndInstantExclusive = LocalMidnight(today.AddDays(1), zone);

            var anchor = _anchorStore?.Read();
            var boundaries = new List<WeeklyResetBoundary>();

            if (anchor is { } anchorInstant)
            {
                var instant = anchorInstant;

                var stepsOut = 0;
                while (instant >= windowEndInstantExclusive && stepsOut < MaxCadenceSteps)
                {
                    instant = StepLocalDays(instant, zone, -CadenceDays);
                    stepsOut++;
                }

                var stepsIn = 0;
                while (instant >= windowStartInstant && stepsIn < MaxCadenceSteps)
                {
                    var kind = instant == anchorInstant
                        ? WeeklyResetBoundaryKind.Observed
                        : WeeklyResetBoundaryKind.DerivedFromObserved;
                    boundaries.Add(new WeeklyResetBoundary(instant, kind));
                    instant = StepLocalDays(instant, zone, -CadenceDays);
                    stepsIn++;
                }

                boundaries.Reverse();
            }
            else
            {
                for (var day = windowStart; day <= today; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Monday)
                    {
                        boundaries.Add(new WeeklyResetBoundary(
                            LocalMidnight(day, zone), WeeklyResetBoundaryKind.MondayFallback));
                    }
                }
            }

            return new WeeklyResetBoundaries(windowStart, today, boundaries, UsageValueStatus.Real);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return WeeklyResetBoundaries.Unavailable;
        }
    }

    /// <summary>
    /// <paramref name="instant"/>'s own local wall-clock time, <paramref name="days"/> calendar
    /// days earlier (or later), re-resolved against <paramref name="zone"/>'s offset at that
    /// stepped wall time rather than shifted by a fixed duration — the mechanism that makes a
    /// DST transition inside the window change the boundary's UTC instant by the real elapsed
    /// time rather than a naive 7 × 24 hours (ADR-0008 D9e).
    /// </summary>
    private static DateTimeOffset StepLocalDays(DateTimeOffset instant, TimeZoneInfo zone, int days)
    {
        var steppedLocal = TimeZoneInfo.ConvertTime(instant, zone).DateTime.AddDays(days);
        return new DateTimeOffset(steppedLocal, zone.GetUtcOffset(steppedLocal));
    }

    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
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

            window = new LedgerWindow(
                today, windowStart, todayBuckets, windowBuckets, recordedDays, firstRecordedDate);
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
        int RecordedDays,
        DateOnly? FirstRecordedDate);
}
