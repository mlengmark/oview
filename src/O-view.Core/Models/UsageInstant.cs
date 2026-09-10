namespace OView.Core.Models;

/// <summary>
/// A UTC instant from the Core-to-skin data contract (ADR-0001), paired with its trust
/// status. Core hands over ISO-8601 UTC only — a skin owns locale and format rendering,
/// never Core.
/// </summary>
public readonly record struct UsageInstant(DateTimeOffset? Value, UsageValueStatus Status);
