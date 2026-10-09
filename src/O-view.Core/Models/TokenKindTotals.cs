namespace OView.Core.Models;

/// <summary>
/// The input/output/cache-creation/cache-read token totals behind the detail window's
/// token-kind bars, for either "today" or the 31-day window (ADR-0008 D9e). Read from Core's
/// local ledger and aggregated by Core — never summed by a skin.
///
/// <para><see cref="Total"/> is carried rather than derived because a skin's share text needs
/// a denominator that both skins agree on, so no skin ever sums the four kinds itself.</para>
/// </summary>
/// <param name="FromLocalDate">The first local day the window covers.</param>
/// <param name="ToLocalDate">The last local day the window covers.</param>
/// <param name="Input">Input tokens and their modelled value, summed over the window.</param>
/// <param name="Output">Output tokens and their modelled value, summed over the window.</param>
/// <param name="CacheCreation">Cache-write tokens and their modelled value, summed over the
/// window.</param>
/// <param name="CacheRead">Cache-read tokens and their modelled value, summed over the
/// window.</param>
/// <param name="Total">The four kinds' token count summed by Core, so no skin ever sums
/// them.</param>
/// <param name="Rates">Which rate table priced the four kinds' estimated values.</param>
/// <param name="Status">Whether this total is trustworthy at all. See
/// <see cref="ModelUsageBreakdown"/>'s remarks for the same real-empty-vs-unavailable
/// distinction.</param>
public sealed record TokenKindTotals(
    DateOnly FromLocalDate,
    DateOnly ToLocalDate,
    TokenKindAmount Input,
    TokenKindAmount Output,
    TokenKindAmount CacheCreation,
    TokenKindAmount CacheRead,
    TokenCount Total,
    RateCardStamp Rates,
    UsageValueStatus Status)
{
    private static TokenKindAmount UnavailableAmount { get; } = new(
        new TokenCount(null, UsageValueStatus.Unavailable),
        new EstimatedUsd(null, UsageValueStatus.Unavailable));

    /// <summary>
    /// The canonical "no data" totals. <see cref="FromLocalDate"/> and
    /// <see cref="ToLocalDate"/> are <see cref="DateOnly.MinValue"/> here as an explicit
    /// "never" sentinel, not a fabricated window (the same pattern
    /// <see cref="ModelUsageBreakdown.Unavailable"/> uses). No skin reads these two dates for
    /// an <see cref="UsageValueStatus.Unavailable"/> total.
    /// </summary>
    public static TokenKindTotals Unavailable { get; } = new(
        DateOnly.MinValue,
        DateOnly.MinValue,
        UnavailableAmount,
        UnavailableAmount,
        UnavailableAmount,
        UnavailableAmount,
        new TokenCount(null, UsageValueStatus.Unavailable),
        RateCardStamp.Unavailable,
        UsageValueStatus.Unavailable);
}
