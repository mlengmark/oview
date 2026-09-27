namespace OView.Core.Models;

/// <summary>
/// Whether work past the plan allowance bills as extra usage on this account, as Claude
/// Code's own cache reports it (ADR-0001, OVI-168). Two members only: the source app's third
/// member, <c>Unknown</c>, is <b>not</b> carried here, because "the cache did not say" is
/// already <see cref="UsageStatistics.HasCreditUsage"/>'s sibling field
/// <see cref="ExtraUsageReading"/>'s parent-nullability's job — encoding "unknown" a second
/// way inside this enum would be the ill-formed pair OVI-146 closed. A skin reads
/// <c>ExtraUsage is null</c> for "the cache did not say".
/// </summary>
public enum ExtraUsageState
{
    /// <summary>Extra usage is off, so work past the plan window does not bill beyond it.</summary>
    Disabled,

    /// <summary>Extra usage is on, so work past the plan window can bill beyond it.</summary>
    Enabled,
}
