namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own on-screen check (ADR-0008 D4's 2026-10-09 amendment, slice P1), wired as
/// primitives rather than a shared geometry type — same reason
/// <see cref="DetailWindowPlacementSkinUnderTest"/> does: the ADR rejects a shared presentation
/// type for this decision, so there is no common "screen rectangle" type to pass. Each skin keeps
/// its own <c>ScreenRect</c> and <c>DetailWindowOnScreenCheck.IsFullyOnScreen</c>; this harness
/// calls each directly through <see cref="Describe"/>, which takes the candidate rectangle and
/// every current work area as flat tuples and reports the boolean verdict as a string so
/// <see cref="ContentFact"/> can pin it.
/// </summary>
public sealed record DetailWindowOnScreenSkinUnderTest(
    string Name,
    Func<
        (double X, double Y, double Width, double Height),
        (double X, double Y, double Width, double Height)[],
        string> Describe);
