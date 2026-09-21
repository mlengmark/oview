using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for <c>PanelTextFormatter.Freshness</c>.
/// Kept as its own type, parallel to <see cref="GoldenMasterFixture"/> rather than a change
/// to it: <c>Freshness</c> needs a third <c>UtcNow</c> input <see cref="SkinUnderTest"/>'s
/// delegate does not carry (the tooltip's <c>Format</c> only ever renders absolute instants,
/// never a relative "as of" age) — widening that shared delegate would touch the tooltip
/// fixture family for a parameter it never needs (OVI-45's decision). Reuses
/// <see cref="ContentFact"/> as-is — that type is already snapshot-agnostic.
/// </summary>
public sealed record FreshnessFixture(
    string Name,
    UsageSnapshot Snapshot,
    DateTimeOffset UtcNow,
    TimeZoneInfo DisplayZone,
    IReadOnlyList<ContentFact> ContentFacts);
