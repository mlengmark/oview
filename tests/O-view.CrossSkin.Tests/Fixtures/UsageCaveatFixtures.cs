using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="UsageCaveatFixture"/>s (ADR-0003, OVI-165). Each is a content
/// fact both skins must state, not an exact sentence.
/// </summary>
public static class UsageCaveatFixtures
{
    private static readonly RateCardStamp FreshRates = new(RateCardSource.Bundled, new DateOnly(2026, 6, 24), false);
    private static readonly RateCardStamp StaleRates = new(RateCardSource.Bundled, new DateOnly(2026, 6, 24), true);

    private static readonly ContentFact RendersNothing = new("renders nothing at all", rendered => rendered.Length == 0);

    private static readonly ContentFact NamesNoRateSourceOrDate = new(
        "names no rate source or date",
        rendered => !rendered.Contains("bundled") && !rendered.Contains("2026"));

    /// <summary>Everything Core can establish, and nothing to qualify.</summary>
    private static UsageStatistics Clean() => new(
        new TokenCount(1_000, UsageValueStatus.Real),
        new EstimatedUsd(1m, UsageValueStatus.Estimated),
        new TokenCount(30_000, UsageValueStatus.Real),
        new EstimatedUsd(30m, UsageValueStatus.Estimated),
        new HistoryCoverage(31, 31))
    {
        UnpricedModels = UnpricedModels.None,
        TtlUnrecordedCacheWritesWindow31d = new TokenCount(0, UsageValueStatus.Real),
        Rates = FreshRates,
    };

    public static readonly UsageCaveatFixture NoCaveatWhenThereIsNothingToQualify = new(
        Name: "no-caveat-when-there-is-nothing-to-qualify",
        Render: skin => skin.Caveat(Clean()),
        ContentFacts: new[] { RendersNothing });

    public static readonly UsageCaveatFixture UnpricedModelsAreNamedAndTheEstimateIsNotACompleteTotal = new(
        Name: "unpriced-models-are-named-and-the-estimate-is-not-a-complete-total",
        Render: skin => skin.Caveat(Clean() with
        {
            UnpricedModels = new UnpricedModels(new[] { "claude-x-9", "claude-y-2" }),
        }),
        ContentFacts: new[]
        {
            ContentFact.Contains("claude-x-9"),
            ContentFact.Contains("claude-y-2"),
            ContentFact.Contains("exclude"),
            ContentFact.Contains("no published rate"),
        });

    public static readonly UsageCaveatFixture TtlUnrecordedCacheWritesAreCountedAndTheAssumptionIsNamed = new(
        Name: "ttl-unrecorded-cache-writes-are-counted-and-the-assumption-is-named",
        Render: skin => skin.Caveat(Clean() with
        {
            TtlUnrecordedCacheWritesWindow31d = new TokenCount(12_000, UsageValueStatus.Real),
        }),
        ContentFacts: new[]
        {
            ContentFact.Contains("12.0K"),
            ContentFact.Contains("5-minute"),
        });

    public static readonly UsageCaveatFixture StaleRatesStateTheSourceAndTheDate = new(
        Name: "stale-rates-state-the-source-and-the-date",
        Render: skin => skin.RateAge(StaleRates),
        ContentFacts: new[]
        {
            ContentFact.Contains("bundled"),
            ContentFact.Contains("24"),
            ContentFact.Contains("Jun"),
            ContentFact.Contains("2026"),
        });

    public static readonly UsageCaveatFixture StaleRatesReachTheCaveat = new(
        Name: "stale-rates-reach-the-caveat",
        Render: skin => skin.Caveat(Clean() with { Rates = StaleRates }),
        ContentFacts: new[]
        {
            ContentFact.Contains("bundled"),
            ContentFact.Contains("2026"),
        });

    public static readonly UsageCaveatFixture FreshRatesAreNotMentioned = new(
        Name: "fresh-rates-are-not-mentioned",
        Render: skin => skin.Caveat(Clean()),
        ContentFacts: new[] { NamesNoRateSourceOrDate });

    public static readonly UsageCaveatFixture UnavailableRatesRenderNoRateAgeLine = new(
        Name: "unavailable-rates-render-no-rate-age-line",
        Render: skin => skin.RateAge(RateCardStamp.Unavailable),
        ContentFacts: new[] { RendersNothing });

    public static readonly UsageCaveatFixture UnavailableRatesAreNotImpliedCurrentBySilence = new(
        Name: "unavailable-rates-are-not-implied-current-by-silence",
        Render: skin => skin.Caveat(Clean() with { Rates = RateCardStamp.Unavailable }),
        ContentFacts: new[]
        {
            ContentFact.Contains("unknown"),
            NamesNoRateSourceOrDate,
        });

    public static readonly UsageCaveatFixture UnavailableUnpricedModelsAreNotImpliedNoneBySilence = new(
        Name: "unavailable-unpriced-models-are-not-implied-none-by-silence",
        Render: skin => skin.Caveat(Clean() with { UnpricedModels = UnpricedModels.Unavailable }),
        ContentFacts: new[] { ContentFact.Contains("unknown") });

    public static readonly UsageCaveatFixture UnavailableTtlCountIsNotImpliedZeroBySilence = new(
        Name: "unavailable-ttl-count-is-not-implied-zero-by-silence",
        Render: skin => skin.Caveat(Clean() with
        {
            TtlUnrecordedCacheWritesWindow31d = new TokenCount(null, UsageValueStatus.Unavailable),
        }),
        ContentFacts: new[] { ContentFact.Contains("unknown") });

    public static IReadOnlyList<UsageCaveatFixture> All { get; } = new[]
    {
        NoCaveatWhenThereIsNothingToQualify,
        UnpricedModelsAreNamedAndTheEstimateIsNotACompleteTotal,
        TtlUnrecordedCacheWritesAreCountedAndTheAssumptionIsNamed,
        StaleRatesStateTheSourceAndTheDate,
        StaleRatesReachTheCaveat,
        FreshRatesAreNotMentioned,
        UnavailableRatesRenderNoRateAgeLine,
        UnavailableRatesAreNotImpliedCurrentBySilence,
        UnavailableUnpricedModelsAreNotImpliedNoneBySilence,
        UnavailableTtlCountIsNotImpliedZeroBySilence,
    };
}
