namespace OView.Core.Models;

/// <summary>
/// A UTC instant from the Core-to-skin data contract (ADR-0001), paired with its trust
/// status. Core hands over ISO-8601 UTC only — a skin owns locale and format rendering,
/// never Core.
/// <para>
/// Building one with <see cref="UsageValueStatus.Unavailable"/> and a non-null
/// <see cref="Value"/> throws <see cref="ArgumentException"/> (ADR-0001, OVI-146). Both
/// properties are get-only, so a <c>with</c> expression or an object initializer cannot
/// build that pair either. <c>default</c> (<see cref="UsageValueStatus.Real"/>, null) is allowed.
/// </para>
/// </summary>
public readonly record struct UsageInstant(DateTimeOffset? Value, UsageValueStatus Status)
{
    /// <summary>The UTC instant, or null when no source produced one.</summary>
    public DateTimeOffset? Value { get; } = UsageValueRule.NoValueWhenUnavailable(Value, Status);

    /// <summary>The trust status of <see cref="Value"/>.</summary>
    public UsageValueStatus Status { get; } = Status;
}
