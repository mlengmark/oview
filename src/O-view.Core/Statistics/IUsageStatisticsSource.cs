using OView.Core.Models;

namespace OView.Core.Statistics;

/// <summary>
/// Core's ledger-read seam (ADR-0005 D6c, amended by ADR-0008 D9a/OVI-326): one interface over
/// <see cref="OView.Core.Storage.UsageLedgerStore"/> with two queries, both answering from
/// accumulated local history rather than a live vendor read. <b>Not</b> an
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
}
