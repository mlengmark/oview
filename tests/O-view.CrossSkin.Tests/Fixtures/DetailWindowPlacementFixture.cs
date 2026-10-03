namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the detail window's first-placement rule
/// (ADR-0008 D5). Injects one screen rectangle and one window size into every skin's own
/// <c>DetailWindowPlacement.Compute</c> and pins the content facts both skins must agree on:
/// which corner, what margin, what the fallback is. Follows
/// <see cref="UsageCaveatFixture"/>'s member-selecting shape.
/// </summary>
public sealed record DetailWindowPlacementFixture(
    string Name,
    double WorkLeft,
    double WorkTop,
    double WorkWidth,
    double WorkHeight,
    double WindowWidth,
    double WindowHeight,
    double MarginPx,
    Func<DetailWindowPlacementSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
