using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="CoverageCaptionFixture"/>s (ADR-0003, ADR-0008 D11b, gate G7
/// parity slice P10). Each is a content fact both skins must state, not an exact sentence: both
/// counts appear, and — the D11b pin — the caption counts days Core has data <b>for</b>, not
/// days with usage, so a fully-covered window still names "31 of 31", never nothing at all (the
/// one place this family deliberately diverges from the older <see cref="UsageCaveatFixture"/>'s
/// own coverage case, which hides itself when fully covered).
/// </summary>
public static class CoverageCaptionFixtures
{
    public static readonly CoverageCaptionFixture PartialCoverageStatesBothCounts = new(
        Name: "partial-coverage-states-both-counts",
        Render: skin => skin.CoverageCaption(new HistoryCoverage(12, 31)),
        ContentFacts: new[]
        {
            ContentFact.Contains("12"),
            ContentFact.Contains("31"),
        });

    /// <summary>The D11b pin: a fully-covered window still states both counts — "31 of 31", not
    /// nothing — because this caption counts days recorded, not days with usage.</summary>
    public static readonly CoverageCaptionFixture FullCoverageStillStatesBothCountsRatherThanHidingTheCaption = new(
        Name: "full-coverage-still-states-both-counts-rather-than-hiding-the-caption",
        Render: skin => skin.CoverageCaption(new HistoryCoverage(31, 31)),
        ContentFacts: new[]
        {
            ContentFact.Contains("31"),
            new ContentFact(
                "never renders an empty caption just because the window is fully covered",
                rendered => rendered.Length > 0),
        });

    /// <summary>A window with no recorded days at all states zero, not a blank or a fabricated
    /// non-zero count — the same "never fabricate a number" rule the rest of the contract
    /// carries (ADR-0001).</summary>
    public static readonly CoverageCaptionFixture ZeroRecordedDaysStatesZeroNotNothing = new(
        Name: "zero-recorded-days-states-zero-not-nothing",
        Render: skin => skin.CoverageCaption(new HistoryCoverage(0, 31)),
        ContentFacts: new[]
        {
            ContentFact.Contains("0"),
            ContentFact.Contains("31"),
        });

    public static IReadOnlyList<CoverageCaptionFixture> All { get; } = new[]
    {
        PartialCoverageStatesBothCounts,
        FullCoverageStillStatesBothCountsRatherThanHidingTheCaption,
        ZeroRecordedDaysStatesZeroNotNothing,
    };
}
