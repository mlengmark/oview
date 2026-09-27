namespace OView.Core.Models;

/// <summary>
/// What the plan meter and local activity say about each other, for the current 5-hour
/// window (ADR-0001, OVI-168). Six states, carried across unmerged: the last three are three
/// different ways of saying "cannot tell", and the source app needed all three separately
/// (its issue #268) — collapsing any pair back into one loses a distinction a skin needs.
/// No member is a display label; wording each state is entirely a skin's call.
/// </summary>
public enum DivergenceState
{
    /// <summary>Not enough activity in the window to expect visible meter movement.</summary>
    InsufficientActivity,

    /// <summary>Activity and meter movement are consistent — usage is drawing from the plan.</summary>
    Consistent,

    /// <summary>Substantial activity with a flat meter — usage is not drawing from the plan window.</summary>
    Diverging,

    /// <summary>The plan window is exhausted, so further usage necessarily bills elsewhere.</summary>
    PlanLimitReached,

    /// <summary>
    /// The meter has stopped reporting, so a flat series is evidence of nothing. Distinct
    /// from <see cref="InsufficientActivity"/>, which means the opposite problem: there the
    /// meter is live and the work is too small to move it.
    /// </summary>
    MeterNotReporting,

    /// <summary>
    /// The window holds one meter sample, so there is no rise to measure yet. The meter
    /// <i>is</i> reporting and the activity <i>is</i> substantial — what is missing is a
    /// second point to subtract the first from.
    /// </summary>
    RiseNotMeasurable,
}
