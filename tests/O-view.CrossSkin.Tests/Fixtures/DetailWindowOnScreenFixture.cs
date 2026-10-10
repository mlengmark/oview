namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the off-screen fallback rule (ADR-0008 D4's
/// 2026-10-09 amendment, slice P1). Injects one candidate rectangle and the current work areas
/// into every skin's own <c>DetailWindowOnScreenCheck.IsFullyOnScreen</c> and pins the one
/// content fact both skins must agree on: whether that candidate counts as on screen. Follows
/// <see cref="DetailWindowPlacementFixture"/>'s member-selecting shape.
/// </summary>
public sealed record DetailWindowOnScreenFixture(
    string Name,
    Func<DetailWindowOnScreenSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
