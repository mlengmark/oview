using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="UsageStatisticsFixture"/> golden masters (ADR-0003),
/// covering the three states the contract's status flag distinguishes throughout
/// (ADR-0001): ordinary real/estimated figures, a partially-covered history window, and a
/// fully unavailable snapshot. See <c>tests/O-view.CrossSkin.Tests/README.md</c> for the
/// step-by-step guide to adding a fixture.
/// </summary>
public static class UsageStatisticsFixtures
{
    /// <summary>
    /// Ordinary usage: today's and the 31-day window's tokens and estimated spend are all
    /// present, and history coverage is complete (no caveat expected). Pins the exact K/M
    /// abbreviation and two-decimal USD figures both skins independently chose to produce
    /// for these inputs (ADR-0003's rounding-rule style of fact, not an exact sentence).
    /// </summary>
    public static readonly UsageStatisticsFixture OrdinaryUsage = new(
        Name: "ordinary-usage-figures",
        Statistics: new UsageStatistics(
            OutputTokensToday: new TokenCount(12_700_000, UsageValueStatus.Real),
            EstimatedSpendToday: new EstimatedUsd(492.52m, UsageValueStatus.Estimated),
            OutputTokensWindow31d: new TokenCount(1_500, UsageValueStatus.Real),
            EstimatedValueWindow31d: new EstimatedUsd(9.36m, UsageValueStatus.Estimated),
            HistoryCoverage: new HistoryCoverage(31, 31)),
        ContentFacts: new[]
        {
            ContentFact.Contains("12.7M"),
            ContentFact.Contains("492.52"),
            ContentFact.Contains("1.5K"),
            ContentFact.Contains("9.36"),
        });

    /// <summary>
    /// A partially-covered history window (18 of 31 days recorded) — both skins must state
    /// both counts, in whatever wording each chooses (ADR-0003).
    /// </summary>
    public static readonly UsageStatisticsFixture PartialHistoryCoverage = new(
        Name: "partial-history-coverage",
        Statistics: new UsageStatistics(
            OutputTokensToday: new TokenCount(0, UsageValueStatus.Real),
            EstimatedSpendToday: new EstimatedUsd(0m, UsageValueStatus.Estimated),
            OutputTokensWindow31d: new TokenCount(500, UsageValueStatus.Real),
            EstimatedValueWindow31d: new EstimatedUsd(1.25m, UsageValueStatus.Estimated),
            HistoryCoverage: new HistoryCoverage(18, 31)),
        ContentFacts: new[]
        {
            ContentFact.Contains("18"),
            ContentFact.Contains("31"),
        });

    /// <summary>
    /// Nothing available at all — the "never fabricate a number" rule's hardest case. Both
    /// skins must admit the gap; neither may render a "$0.00" or a bare "0" token count in
    /// its place (ADR-0001's standing product principle).
    /// </summary>
    public static readonly UsageStatisticsFixture Unavailable = new(
        Name: "unavailable-figures",
        Statistics: UsageStatistics.Unavailable,
        ContentFacts: new[]
        {
            new ContentFact(
                "never renders a fabricated $0.00 spend figure",
                rendered => !rendered.Contains("$0.00", StringComparison.Ordinal)),
            new ContentFact(
                "never renders a fabricated 0 token count",
                rendered => !rendered.Contains(" 0 tokens", StringComparison.Ordinal)),
        });

    public static IReadOnlyList<UsageStatisticsFixture> All { get; } = new[]
    {
        OrdinaryUsage,
        PartialHistoryCoverage,
        Unavailable,
    };
}
