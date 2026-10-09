namespace OView.Core.Models;

/// <summary>
/// The 31-day output-token history behind the detail window's graph (ADR-0008 D9e), read from
/// Core's local ledger. Carries no per-model or per-kind split — the tiles' per-model split
/// comes from <see cref="UsageDetail.Models"/> and the kind split from
/// <see cref="UsageDetail.TokensToday"/>/<see cref="UsageDetail.Tokens31d"/>, so a skin is
/// never handed 31 × N rows to total.
///
/// <para><see cref="Days"/> is always all 31 days, in date order, never sparse — a sparse list
/// would make a skin reconstruct the missing dates, which is date arithmetic in a skin.</para>
/// </summary>
/// <param name="FromLocalDate">The first local day the window covers.</param>
/// <param name="ToLocalDate">The last local day the window covers.</param>
/// <param name="Days">One point per local day in the window, never sparse.</param>
/// <param name="Coverage">How much of the window is covered by recorded history.</param>
/// <param name="Status">Whether this series is trustworthy at all. See
/// <see cref="ModelUsageBreakdown"/>'s remarks for the same real-empty-vs-unavailable
/// distinction.</param>
public sealed record DailyUsageSeries(
    DateOnly FromLocalDate,
    DateOnly ToLocalDate,
    IReadOnlyList<DailyUsagePoint> Days,
    HistoryCoverage Coverage,
    UsageValueStatus Status)
{
    /// <summary>
    /// The canonical "no data" series. <see cref="FromLocalDate"/> and
    /// <see cref="ToLocalDate"/> are <see cref="DateOnly.MinValue"/> here as an explicit
    /// "never" sentinel, not a fabricated window (the same pattern
    /// <see cref="ModelUsageBreakdown.Unavailable"/> uses). No skin reads these two dates for
    /// an <see cref="UsageValueStatus.Unavailable"/> series.
    /// </summary>
    public static DailyUsageSeries Unavailable { get; } = new(
        DateOnly.MinValue,
        DateOnly.MinValue,
        Array.Empty<DailyUsagePoint>(),
        new HistoryCoverage(0, 0),
        UsageValueStatus.Unavailable);
}
