namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the statistics tiles' coverage caption
/// (ADR-0008 D11b, gate G7 parity slice P10) — the "N of 31 days recorded" caption, pinned as
/// its own new fixture family per D11b rather than folded into <see cref="UsageCaveatFixture"/>:
/// unlike <see cref="PanelStatisticsFormatter.CoverageNote"/> (the usage-tile caveat, which hides
/// itself once the window is fully covered), this caption always states both counts. Follows
/// <see cref="UsageCaveatFixture"/>'s member-selecting shape.
/// </summary>
public sealed record CoverageCaptionFixture(
    string Name,
    Func<CoverageCaptionSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
