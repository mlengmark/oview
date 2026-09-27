namespace OView.Core.Models;

/// <summary>
/// What the plan meter and local activity say about each other, for the current 5-hour
/// window (ADR-0001, OVI-168). Lives on <see cref="UsageStatistics.Divergence"/>; a
/// <c>null</c> value there means Core could not run the comparison at all.
/// </summary>
/// <param name="State">Which of the six comparison outcomes was observed.</param>
/// <param name="OutputTokensInWindow">Deduplicated output tokens observed since the window start.</param>
/// <param name="PlanRisePoints">
/// Percentage points the plan meter rose across the same window. <b>Nullable, and
/// <c>null</c> whenever no rise was measurable</b> — the source app reported <c>0</c> in that
/// case, which is a fabricated zero a skin cannot tell apart from a meter that genuinely held
/// still. Core knows which it is (<see cref="State"/> says so); this contract makes Core say
/// it rather than a skin guess it.
/// </param>
public sealed record DivergenceReading(
    DivergenceState State,
    TokenCount OutputTokensInWindow,
    int? PlanRisePoints)
{
    /// <summary>
    /// True when local work is happening that the plan meter is not accounting for. A
    /// Core-derived property of this type, not a skin test — the same rule as
    /// <see cref="UsageLevel"/>'s banding.
    /// </summary>
    public bool IsOffPlan => State is DivergenceState.Diverging or DivergenceState.PlanLimitReached;
}
