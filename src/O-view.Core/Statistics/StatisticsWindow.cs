namespace OView.Core.Statistics;

/// <summary>
/// Which window <see cref="IUsageStatisticsSource.GetTokenKindTotals"/> aggregates over
/// (ADR-0005 D6c, gate G7 parity P6). The detail window needs both totals side by side
/// (<see cref="OView.Core.Models.UsageDetail.TokensToday"/> and
/// <see cref="OView.Core.Models.UsageDetail.Tokens31d"/>), so the query takes the window as a
/// parameter rather than being called twice under two different names.
/// </summary>
public enum StatisticsWindow
{
    /// <summary>The current local day only.</summary>
    Today,

    /// <summary>The trailing 31-day window ending today, inclusive.</summary>
    ThirtyOneDays,
}
