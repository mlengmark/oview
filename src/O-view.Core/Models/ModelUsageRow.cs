namespace OView.Core.Models;

/// <summary>
/// One model's pre-aggregated totals over a <see cref="ModelUsageBreakdown"/>'s stated window
/// (ADR-0008 D9a). Core sums these over the whole window itself — a skin is never handed
/// (date × model) rows to total, which would be a second implementation of the same sum, one
/// per skin.
/// </summary>
/// <param name="ModelId">The vendor id, verbatim, never reworded or display-named.</param>
/// <param name="RequestCount">How many requests this model served in the window.</param>
/// <param name="InputTokens">Input tokens, summed over the window.</param>
/// <param name="OutputTokens">Output tokens, summed over the window.</param>
/// <param name="CacheCreationTokens">Cache-write tokens, summed over the window — the flat
/// total, not split by TTL (ADR-0001's <c>TtlUnrecordedCacheWritesWindow31d</c> carries the
/// TTL-unattributed subset at the statistics level, not here).</param>
/// <param name="CacheReadTokens">Cache-read tokens, summed over the window.</param>
/// <param name="EstimatedSpend">The modelled USD value of this model's window, or
/// <see cref="UsageValueStatus.Unavailable"/> when this model could not be priced — the "unpriced
/// model" case D9a names explicitly, carrying no separate flag (ADR-0001, OVI-146).</param>
public sealed record ModelUsageRow(
    string ModelId,
    int RequestCount,
    TokenCount InputTokens,
    TokenCount OutputTokens,
    TokenCount CacheCreationTokens,
    TokenCount CacheReadTokens,
    EstimatedUsd EstimatedSpend);
