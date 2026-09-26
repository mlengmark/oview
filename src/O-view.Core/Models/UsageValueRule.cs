namespace OView.Core.Models;

/// <summary>
/// The construction rule every status-paired value type in the Core-to-skin data contract
/// enforces (ADR-0001, OVI-146 amendment): <c>Status == Unavailable ⇒ Value == null</c>.
/// <see cref="UsageValueStatus.Unavailable"/> means "no source produced a value", so a pair
/// that carries both is a producer bug, and it fails at the producer's call site.
/// </summary>
internal static class UsageValueRule
{
    /// <summary>
    /// Returns <paramref name="value"/> unchanged, or throws <see cref="ArgumentException"/>
    /// when it is non-null and <paramref name="status"/> is <see cref="UsageValueStatus.Unavailable"/>.
    /// The converse (null with <see cref="UsageValueStatus.Real"/> or
    /// <see cref="UsageValueStatus.Estimated"/>) is deliberately not checked.
    /// </summary>
    public static T? NoValueWhenUnavailable<T>(T? value, UsageValueStatus status)
        where T : struct
    {
        if (status == UsageValueStatus.Unavailable && value is not null)
        {
            throw new ArgumentException(
                "An Unavailable value must carry a null Value (ADR-0001, OVI-146).", "Value");
        }

        return value;
    }
}
