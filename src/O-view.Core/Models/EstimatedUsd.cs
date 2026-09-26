namespace OView.Core.Models;

/// <summary>
/// A modelled USD figure from the Core-to-skin data contract (ADR-0001), paired with its
/// trust status. Spend is always derived from token pricing, never measured directly, so a
/// present <see cref="Value"/> carries <see cref="UsageValueStatus.Estimated"/> — never
/// <see cref="UsageValueStatus.Real"/> (ADR-0001's <c>EstimatedSpendToday</c> row). A null
/// <see cref="Value"/> means no source could price the window, and carries
/// <see cref="UsageValueStatus.Unavailable"/> — never a fabricated "$0.00".
/// <para>
/// Building one with <see cref="UsageValueStatus.Unavailable"/> and a non-null
/// <see cref="Value"/> throws <see cref="ArgumentException"/> (ADR-0001, OVI-146). Both
/// properties are get-only, so a <c>with</c> expression or an object initializer cannot
/// build that pair either. <c>default</c> (<see cref="UsageValueStatus.Real"/>, null) is allowed.
/// </para>
/// </summary>
public readonly record struct EstimatedUsd(decimal? Value, UsageValueStatus Status)
{
    /// <summary>The modelled USD figure, or null when no source produced one.</summary>
    public decimal? Value { get; } = UsageValueRule.NoValueWhenUnavailable(Value, Status);

    /// <summary>The trust status of <see cref="Value"/>.</summary>
    public UsageValueStatus Status { get; } = Status;
}
