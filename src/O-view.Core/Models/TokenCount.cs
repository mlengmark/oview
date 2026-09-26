namespace OView.Core.Models;

/// <summary>
/// A token count from the Core-to-skin data contract (ADR-0001), paired with its trust
/// status. <see cref="Value"/> is null when the status is
/// <see cref="UsageValueStatus.Unavailable"/> — never a fabricated number.
/// <para>
/// Building one with <see cref="UsageValueStatus.Unavailable"/> and a non-null
/// <see cref="Value"/> throws <see cref="ArgumentException"/> (ADR-0001, OVI-146). Both
/// properties are get-only, so a <c>with</c> expression or an object initializer cannot
/// build that pair either. <c>default</c> (<see cref="UsageValueStatus.Real"/>, null) is allowed.
/// </para>
/// </summary>
public readonly record struct TokenCount(long? Value, UsageValueStatus Status)
{
    /// <summary>The token count, or null when no source produced one.</summary>
    public long? Value { get; } = UsageValueRule.NoValueWhenUnavailable(Value, Status);

    /// <summary>The trust status of <see cref="Value"/>.</summary>
    public UsageValueStatus Status { get; } = Status;
}
