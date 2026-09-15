namespace OView.Core.Models;

/// <summary>
/// A modelled USD figure from the Core-to-skin data contract (ADR-0001), paired with its
/// trust status. Spend is always derived from token pricing, never measured directly, so a
/// present <see cref="Value"/> carries <see cref="UsageValueStatus.Estimated"/> — never
/// <see cref="UsageValueStatus.Real"/> (ADR-0001's <c>EstimatedSpendToday</c> row). A null
/// <see cref="Value"/> means no source could price the window, and carries
/// <see cref="UsageValueStatus.Unavailable"/> — never a fabricated "$0.00".
/// </summary>
public readonly record struct EstimatedUsd(decimal? Value, UsageValueStatus Status);
