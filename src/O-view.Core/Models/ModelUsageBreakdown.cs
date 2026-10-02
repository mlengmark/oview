namespace OView.Core.Models;

/// <summary>
/// The per-model split behind a <see cref="UsageDetail"/>'s statistics (ADR-0008 D9a), read
/// from Core's local ledger and aggregated per model over a stated window — never per
/// (date × model), and never summed by a skin.
///
/// <para><see cref="Status"/> carries a distinction <see cref="Rows"/> alone cannot:
/// <see cref="UsageValueStatus.Real"/> with an empty <see cref="Rows"/> means "Core read the
/// ledger and the window holds no recorded activity"; <see cref="UsageValueStatus.Unavailable"/>
/// means "Core could not read the ledger" — the same distinction <c>HasCreditUsage</c> and
/// <c>UnpricedModels</c> already draw (ADR-0001, OVI-100/OVI-168), and a skin owes the user
/// different words for each.</para>
/// </summary>
/// <param name="FromLocalDate">The first local day the window covers, stated rather than
/// implied.</param>
/// <param name="ToLocalDate">The last local day the window covers.</param>
/// <param name="Rows">One row per model seen in the window, pre-aggregated by Core.</param>
/// <param name="Coverage">How much of the window is covered by recorded history.</param>
/// <param name="Rates">Which rate table priced <see cref="Rows"/>' estimated figures.</param>
/// <param name="Status">Whether this breakdown is trustworthy at all. See the type remarks.</param>
public sealed record ModelUsageBreakdown(
    DateOnly FromLocalDate,
    DateOnly ToLocalDate,
    IReadOnlyList<ModelUsageRow> Rows,
    HistoryCoverage Coverage,
    RateCardStamp Rates,
    UsageValueStatus Status)
{
    /// <summary>
    /// The canonical "no data" breakdown. <see cref="FromLocalDate"/> and
    /// <see cref="ToLocalDate"/> are <see cref="DateOnly.MinValue"/> here as an explicit "never"
    /// sentinel, not a fabricated window — no read ever produced this breakdown, so there is no
    /// real window to report (ADR-0001's "never fabricate a number" rule, the same pattern
    /// <see cref="UsageSnapshot.Unavailable"/> uses for <c>LastIngestAt</c>). No skin reads
    /// these two dates for an <see cref="UsageValueStatus.Unavailable"/> breakdown.
    /// </summary>
    public static ModelUsageBreakdown Unavailable { get; } = new(
        DateOnly.MinValue,
        DateOnly.MinValue,
        Array.Empty<ModelUsageRow>(),
        new HistoryCoverage(0, 0),
        RateCardStamp.Unavailable,
        UsageValueStatus.Unavailable);
}
