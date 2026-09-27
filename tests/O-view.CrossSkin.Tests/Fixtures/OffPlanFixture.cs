namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003, OVI-168) for the off-plan banner's five entry
/// points. Follows <see cref="BoostNoticeFixture"/>'s precedent: <see cref="Render"/> closes
/// over which <see cref="OffPlanSkinUnderTest"/> member and which fixed inputs a given fixture
/// exercises, rather than one fixture/skin/test trio per member. Reuses <see cref="ContentFact"/>
/// as-is.
/// </summary>
public sealed record OffPlanFixture(
    string Name,
    Func<OffPlanSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
