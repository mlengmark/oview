namespace OView.Core.Models;

/// <summary>
/// A token count from the Core-to-skin data contract (ADR-0001), paired with its trust
/// status. <see cref="Value"/> is null when the status is
/// <see cref="UsageValueStatus.Unavailable"/> — never a fabricated number.
/// </summary>
public readonly record struct TokenCount(long? Value, UsageValueStatus Status);
