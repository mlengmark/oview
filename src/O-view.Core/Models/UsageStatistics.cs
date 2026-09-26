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
    /// <summary>Model ids in the 31-day window that could not be priced (ADR-0001, OVI-100).
    /// Defaults to unavailable, never to "none": Core has not said everything was priced.</summary>
    public UnpricedModels UnpricedModels { get; init; } = UnpricedModels.Unavailable;

    /// <summary>Cache-write tokens in the 31-day window whose TTL was never recorded. <c>0</c> is
    /// a real "nothing to qualify"; defaults to unavailable (ADR-0001, OVI-100).</summary>
    public TokenCount TtlUnrecordedCacheWritesWindow31d { get; init; } = new(null, UsageValueStatus.Unavailable);

    /// <summary>Which rate table priced the estimated figures in this same snapshot. Defaults to
    /// an unavailable stamp, never a bundled one dated "now" (ADR-0001, OVI-100).</summary>
    public RateCardStamp Rates { get; init; } = RateCardStamp.Unavailable;

    /// <summary>The canonical "no data" statistics — every value unavailable, not zero.</summary>
    public static UsageStatistics Unavailable { get; } = new(
        new TokenCount(null, UsageValueStatus.Unavailable),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new TokenCount(null, UsageValueStatus.Unavailable),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new HistoryCoverage(0, 0));
}
