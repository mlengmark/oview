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
/// <remarks>
/// <para><see cref="Account"/>, <see cref="History"/>, <see cref="ResetBoundaries"/>,
/// <see cref="TokensToday"/> and <see cref="Tokens31d"/> widen this type once (ADR-0008 D9e,
/// gate G7). Each defaults to its own <c>Unavailable</c> sentinel, so a shell that has not
/// assembled it yet pushes an honest gap rather than a zero — no skin reads any of these five
/// members yet; that lands in a later slice.</para>
/// </remarks>
public sealed record UsageDetail(
    UsageSnapshot Snapshot,
    UsageStatistics Statistics,
    ModelUsageBreakdown Models)
{
    /// <summary>The account identity shown beside the header. Not a credential — see
    /// <see cref="AccountIdentity"/>'s remarks.</summary>
    public AccountIdentity Account { get; init; } = AccountIdentity.Unavailable;

    /// <summary>The 31-day output-token history behind the graph.</summary>
    public DailyUsageSeries History { get; init; } = DailyUsageSeries.Unavailable;

    /// <summary>The weekly-reset gridlines inside <see cref="History"/>'s window.</summary>
    public WeeklyResetBoundaries ResetBoundaries { get; init; } = WeeklyResetBoundaries.Unavailable;

    /// <summary>The input/output/cache-creation/cache-read token totals for today.</summary>
    public TokenKindTotals TokensToday { get; init; } = TokenKindTotals.Unavailable;

    /// <summary>The input/output/cache-creation/cache-read token totals for the 31-day
    /// window.</summary>
    public TokenKindTotals Tokens31d { get; init; } = TokenKindTotals.Unavailable;

    /// <summary>The canonical "no data" detail — every member unavailable, not zero.</summary>
    public static UsageDetail Unavailable { get; } = new(
        UsageSnapshot.Unavailable,
        UsageStatistics.Unavailable,
        ModelUsageBreakdown.Unavailable);
}
