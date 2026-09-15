namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the <c>Countdown</c>/<c>SessionReset</c>/
/// <c>WeeklyReset</c>/<c>WeeklyResetConflict</c> raw-scalar family. <see cref="Render"/>
/// closes over the one <see cref="PanelTextResetSkinUnderTest"/> member this fixture
/// exercises and the fixture's own pinned inputs, rather than this type carrying four
/// optional/differently-typed input fields for members that do not share a shape. Reuses
/// <see cref="ContentFact"/> as-is.
/// </summary>
public sealed record PanelTextResetFixture(
    string Name,
    Func<PanelTextResetSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
