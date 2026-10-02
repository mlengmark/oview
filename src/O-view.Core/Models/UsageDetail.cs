namespace OView.Core.Models;

/// <summary>
/// Everything the detail window needs, pushed as one self-consistent object by
/// <c>IShellToSkin.ShowDetail</c> (ADR-0008 D9a). Carries its own <see cref="Snapshot"/> so the
/// window can never pair a percent from one poll with statistics from an earlier one (D9c) — the
/// detail window renders from one <see cref="UsageDetail"/> and nothing else.
/// </summary>
/// <param name="Snapshot">The plan-meter snapshot this detail was computed beside.</param>
/// <param name="Statistics">The existing Core type (ADR-0001), unchanged by this amendment.</param>
/// <param name="Models">The per-model split behind <see cref="Statistics"/>.</param>
public sealed record UsageDetail(
    UsageSnapshot Snapshot,
    UsageStatistics Statistics,
    ModelUsageBreakdown Models)
{
    /// <summary>The canonical "no data" detail — every member unavailable, not zero.</summary>
    public static UsageDetail Unavailable { get; } = new(
        UsageSnapshot.Unavailable,
        UsageStatistics.Unavailable,
        ModelUsageBreakdown.Unavailable);
}
