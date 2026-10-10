namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="DetailWindowOnScreenFixture"/>s (ADR-0008 D4's 2026-10-09
/// amendment). Each pins whether a given candidate rectangle counts as on screen against a given
/// set of current work areas — the one fact both skins' independently-implemented
/// <c>DetailWindowOnScreenCheck</c> must agree on, per D5.
/// </summary>
public static class DetailWindowOnScreenFixtures
{
    private static readonly (double X, double Y, double Width, double Height) PrimaryWorkArea =
        (0, 0, 1920, 1080);

    public static readonly DetailWindowOnScreenFixture FullyContainedCandidateIsOnScreen = new(
        Name: "fully-contained-candidate-is-on-screen",
        Render: skin => skin.Describe((100, 100, 320, 360), new[] { PrimaryWorkArea }),
        ContentFacts: new[] { ContentFact.Contains("on-screen=True") });

    public static readonly DetailWindowOnScreenFixture CandidateFullyOffEveryWorkAreaFallsBack = new(
        Name: "candidate-fully-off-every-work-area-falls-back",
        Render: skin => skin.Describe((5000, 5000, 320, 360), new[] { PrimaryWorkArea }),
        ContentFacts: new[] { ContentFact.Contains("on-screen=False") });

    public static readonly DetailWindowOnScreenFixture PartiallyIntersectingCandidateFallsBack = new(
        Name: "partially-intersecting-candidate-falls-back",
        // Straddles the work area's right edge: half on screen, half off — the amendment is
        // explicit this counts as failure, not as a case to nudge back into view.
        Render: skin => skin.Describe((1800, 100, 320, 360), new[] { PrimaryWorkArea }),
        ContentFacts: new[] { ContentFact.Contains("on-screen=False") });

    public static readonly DetailWindowOnScreenFixture CandidateOnASecondaryMonitorIsOnScreen = new(
        Name: "candidate-on-a-secondary-monitor-is-on-screen",
        Render: skin => skin.Describe(
            (2000, 50, 320, 360),
            new[] { PrimaryWorkArea, (1920, 0, 1280, 1024) }),
        ContentFacts: new[] { ContentFact.Contains("on-screen=True") });

    public static readonly DetailWindowOnScreenFixture NoWorkAreasAtAllFallsBack = new(
        Name: "no-work-areas-at-all-falls-back",
        Render: skin => skin.Describe((0, 0, 320, 360), Array.Empty<(double, double, double, double)>()),
        ContentFacts: new[] { ContentFact.Contains("on-screen=False") });

    public static IReadOnlyList<DetailWindowOnScreenFixture> All { get; } = new[]
    {
        FullyContainedCandidateIsOnScreen,
        CandidateFullyOffEveryWorkAreaFallsBack,
        PartiallyIntersectingCandidateFallsBack,
        CandidateOnASecondaryMonitorIsOnScreen,
        NoWorkAreasAtAllFallsBack,
    };
}
