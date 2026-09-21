namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for <c>BoostChip</c>/<c>BoostCard</c>. Follows
/// <see cref="PanelTextResetFixture"/>'s precedent for a shared-shape, multi-member family:
/// <see cref="Render"/> closes over which <see cref="BoostNoticeSkinUnderTest"/> member
/// (<c>BoostChip</c> or <c>BoostCard</c>) and which fixed inputs a given fixture exercises,
/// rather than this type carrying two near-identical fixture/skin/test trios for one shared
/// input shape (ADR-0003's 2026-09-21 amendment, Quinn-signed-off, interaction
/// <c>22dbaf90</c>). Reuses <see cref="ContentFact"/> as-is.
/// </summary>
public sealed record BoostNoticeFixture(
    string Name,
    Func<BoostNoticeSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
