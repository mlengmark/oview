namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own detail-window placement entry point (ADR-0008 D5), wired as primitives rather
/// than a shared geometry type — the ADR rejects a shared presentation type for this decision, so
/// there is no common "screen rectangle" type to pass; each skin keeps its own
/// <c>DetailWindowPlacement.Compute</c> signature and this harness calls it directly.
/// <see cref="DetailWindowPlacementFixture.Render"/> formats each skin's own result into a
/// descriptive string so <see cref="ContentFact"/> can pin ADR-0008 D5's named content facts —
/// which corner, what margin, what the fallback is — the things the two skins must agree on. The
/// description also carries the computed X/Y for debugging a failure, but no fixture pins those
/// as content facts: D5 only requires agreement on corner/margin/fallback for a given rectangle.
/// </summary>
public sealed record DetailWindowPlacementSkinUnderTest(
    string Name,
    Func<double, double, double, double, double, double, double, string> Describe);
