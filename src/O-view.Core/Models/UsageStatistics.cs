namespace OView.Core.Models;

/// <summary>
/// The subset of the Core-to-skin data contract (ADR-0001) that <c>UsageFormatter</c>'s and
/// <c>PanelStatistics</c>'s presentation logic needs: today's and the 31-day window's output
/// tokens and estimated spend, plus how much of the window is covered by recorded history.
///
/// <para>By contract, this type carries no pre-formatted display string, no K/M token
/// abbreviation, no currency prefix, and no tile-width-derived threshold — those belong to
/// whichever skin renders this snapshot (ADR-0001, ADR-0002), never to this type.</para>
/// </summary>
public sealed record UsageStatistics(
    TokenCount OutputTokensToday,
    EstimatedUsd EstimatedSpendToday,
    TokenCount OutputTokensWindow31d,
    EstimatedUsd EstimatedValueWindow31d,
    HistoryCoverage HistoryCoverage)
{
    /// <summary>The canonical "no data" statistics — every value unavailable, not zero.</summary>
    public static UsageStatistics Unavailable { get; } = new(
        new TokenCount(null, UsageValueStatus.Unavailable),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new TokenCount(null, UsageValueStatus.Unavailable),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new HistoryCoverage(0, 0));
}
