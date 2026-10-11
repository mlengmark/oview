using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned content-fact fixture (ADR-0003) for the off-plan live banner's four entry points
/// (gate G7 parity slice P15, OVI-691): the no-banner (on-plan) state and the three extra-usage
/// wordings (On/Off/Unknown). Same shape as <see cref="DetailWindowFixture"/> — carries the full
/// <see cref="UsageDetail"/> both skins' <c>DetailWindowContentBuilder</c> and
/// <c>DetailWindowRenderProof</c> actually consume — kept as its own named type so a reviewer can
/// find the banner's own fixture set without reading every <see cref="DetailWindowFixtures"/>
/// entry for one that happens to set <see cref="UsageStatistics.Divergence"/>.
/// </summary>
public sealed record OffPlanBannerFixture(
    string Name,
    UsageDetail Detail,
    TimeZoneInfo DisplayZone,
    IReadOnlyList<ContentFact> ContentFacts);
