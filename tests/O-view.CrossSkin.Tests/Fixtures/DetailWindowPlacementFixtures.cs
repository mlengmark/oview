namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="DetailWindowPlacementFixture"/>s (ADR-0008 D5). Each pins
/// which corner, what margin, and what the fallback is for one injected rectangle — not the exact
/// coordinates, which are an implementation detail of the arithmetic rather than a content fact
/// the two skins are required to phrase identically.
/// </summary>
public static class DetailWindowPlacementFixtures
{
    public static readonly DetailWindowPlacementFixture PrimaryMonitorAnchorsBottomRight = new(
        Name: "primary-monitor-anchors-bottom-right",
        WorkLeft: 0, WorkTop: 0, WorkWidth: 1920, WorkHeight: 1080,
        WindowWidth: 320, WindowHeight: 400, MarginPx: 8,
        Render: skin => skin.Describe(0, 0, 1920, 1080, 320, 400, 8),
        ContentFacts: new[]
        {
            ContentFact.Contains("corner=BottomRight"),
            ContentFact.Contains("margin=8"),
            ContentFact.Contains("fallback=False"),
        });

    public static readonly DetailWindowPlacementFixture SecondaryMonitorWithNonZeroOriginStillAnchorsBottomRight = new(
        Name: "secondary-monitor-with-non-zero-origin-still-anchors-bottom-right",
        WorkLeft: -1920, WorkTop: 40, WorkWidth: 1920, WorkHeight: 1040,
        WindowWidth: 300, WindowHeight: 500, MarginPx: 12,
        Render: skin => skin.Describe(-1920, 40, 1920, 1040, 300, 500, 12),
        ContentFacts: new[]
        {
            ContentFact.Contains("corner=BottomRight"),
            ContentFact.Contains("margin=12"),
            ContentFact.Contains("fallback=False"),
        });

    public static readonly DetailWindowPlacementFixture ZeroHeightWorkAreaFallsBackToCentered = new(
        Name: "zero-height-work-area-falls-back-to-centered",
        WorkLeft: 100, WorkTop: 50, WorkWidth: 1920, WorkHeight: 0,
        WindowWidth: 300, WindowHeight: 400, MarginPx: 8,
        Render: skin => skin.Describe(100, 50, 1920, 0, 300, 400, 8),
        ContentFacts: new[]
        {
            ContentFact.Contains("corner=Centered"),
            ContentFact.Contains("margin=0"),
            ContentFact.Contains("fallback=True"),
        });

    public static readonly DetailWindowPlacementFixture NegativeWorkWidthFallsBackToCentered = new(
        Name: "negative-work-width-falls-back-to-centered",
        WorkLeft: 0, WorkTop: 0, WorkWidth: -10, WorkHeight: 1080,
        WindowWidth: 300, WindowHeight: 400, MarginPx: 8,
        Render: skin => skin.Describe(0, 0, -10, 1080, 300, 400, 8),
        ContentFacts: new[]
        {
            ContentFact.Contains("corner=Centered"),
            ContentFact.Contains("fallback=True"),
        });

    public static readonly DetailWindowPlacementFixture DegenerateWindowSizeFallsBackToCentered = new(
        Name: "degenerate-window-size-falls-back-to-centered",
        WorkLeft: 0, WorkTop: 0, WorkWidth: 1920, WorkHeight: 1080,
        WindowWidth: 0, WindowHeight: 400, MarginPx: 8,
        Render: skin => skin.Describe(0, 0, 1920, 1080, 0, 400, 8),
        ContentFacts: new[]
        {
            ContentFact.Contains("corner=Centered"),
            ContentFact.Contains("fallback=True"),
        });

    public static IReadOnlyList<DetailWindowPlacementFixture> All { get; } = new[]
    {
        PrimaryMonitorAnchorsBottomRight,
        SecondaryMonitorWithNonZeroOriginStillAnchorsBottomRight,
        ZeroHeightWorkAreaFallsBackToCentered,
        NegativeWorkWidthFallsBackToCentered,
        DegenerateWindowSizeFallsBackToCentered,
    };
}
