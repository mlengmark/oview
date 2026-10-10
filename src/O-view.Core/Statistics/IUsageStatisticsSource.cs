using OView.Core.Models;

namespace OView.Core.Statistics;

/// <summary>
/// Core's ledger-read seam (ADR-0005 D6c, amended by ADR-0008 D9a/OVI-326 and, for the three
/// members added here, by the 2026-10-09 gate G7 parity amendment/OVI-635): one interface over
/// <see cref="OView.Core.Storage.UsageLedgerStore"/> and
/// <see cref="OView.Core.Storage.WeeklyResetAnchorStore"/>, all answering from accumulated local
/// history rather than a live vendor read. <b>Not</b> an
/// <see cref="OView.Core.Providers.IUsageProvider"/> — it is not part of composition
/// (ADR-0005 D3) and has no <see cref="DataSourceKind"/>; each value in the result carries its
/// own status flag instead.
///
/// <para>Same four obligations as <see cref="OView.Core.Providers.IUsageProvider"/> (ADR-0005
/// D1), proven by <c>UsageStatisticsSourceContractTests</c>:</para>
/// <list type="bullet">
/// <item>Never throws — any failure yields the relevant type's <c>Unavailable</c> value, never
/// an exception.</item>
/// <item>The clock is injected — <c>utcNow</c> is the only time source; an implementation never
/// reads the system clock itself.</item>
/// <item>Read-only — an implementation opens the ledger for reading and never writes to it.</item>
/// <item>No display text — results carry contract values with status flags (ADR-0001), never a
/// pre-built sentence.</item>
/// </list>
///
/// <para><paramref name="zone"/> parameters are threaded the same way
/// <see cref="OView.Core.Storage.UsageLedgerStore.QueryDailyUsage"/> already requires: a
/// parameter the caller supplies, never <see cref="TimeZoneInfo.Local"/> read internally
/// (ADR-0006 D2) — local-day bucketing cannot happen without it, and a seam that read the
/// machine's zone itself would make every figure depend on where it runs.</para>
/// </summary>
public interface IUsageStatisticsSource
{
    /// <summary>
    /// Today's and the 31-day window's output tokens, estimated spend, and history coverage, as
    /// of <paramref name="utcNow"/> in <paramref name="zone"/>. Returns
    /// <see cref="UsageStatistics.Unavailable"/> if the ledger cannot be read.
    /// </summary>
    UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone);

    /// <summary>
    /// The per-model split behind <see cref="GetStatistics"/>'s 31-day window, pre-aggregated by
    /// Core. Returns <see cref="ModelUsageBreakdown.Unavailable"/> if the ledger cannot be read.
    /// </summary>
    ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone);

    /// <summary>
    /// The 31-day output-token history behind the detail window's graph
    /// (<see cref="OView.Core.Models.DailyUsageSeries"/>, ADR-0008 D9e). Every local day in the
    /// window is present — a day before the ledger's own first recorded day is an
    /// <see cref="UsageValueStatus.Unavailable"/> point (a gap), never a fabricated zero; a
    /// recorded, idle day is <see cref="UsageValueStatus.Real"/> with value <c>0</c>. Returns
    /// <see cref="OView.Core.Models.DailyUsageSeries.Unavailable"/> if the ledger cannot be read.
    /// </summary>
    DailyUsageSeries GetDailySeries(DateTimeOffset utcNow, TimeZoneInfo zone);

    /// <summary>
    /// The input/output/cache-creation/cache-read token totals for <paramref name="window"/>
    /// (<see cref="OView.Core.Models.TokenKindTotals"/>, ADR-0008 D9e), summed across every
    /// model. Returns <see cref="OView.Core.Models.TokenKindTotals.Unavailable"/> if the ledger
    /// cannot be read.
    /// </summary>
    TokenKindTotals GetTokenKindTotals(
        DateTimeOffset utcNow, TimeZoneInfo zone, StatisticsWindow window);

    /// <summary>
    /// Every weekly-reset gridline inside the 31-day window
    /// (<see cref="OView.Core.Models.WeeklyResetBoundaries"/>, ADR-0008 D9e), derived at query
    /// time from <see cref="OView.Core.Storage.WeeklyResetAnchorStore"/>'s single stored anchor
    /// — nothing new is persisted. With no anchor stored yet, the whole list falls back to the
    /// Monday convention; with one, the anchor's own instant inside the window is
    /// <see cref="OView.Core.Models.WeeklyResetBoundaryKind.Observed"/> and every other boundary,
    /// stepped from it by the known cadence, is
    /// <see cref="OView.Core.Models.WeeklyResetBoundaryKind.DerivedFromObserved"/>. Never
    /// <see cref="OView.Core.Models.WeeklyResetBoundaries.Unavailable"/> in normal operation —
    /// an empty or all-fallback list is still a real answer.
    /// </summary>
    WeeklyResetBoundaries GetResetBoundaries(DateTimeOffset utcNow, TimeZoneInfo zone);
}
