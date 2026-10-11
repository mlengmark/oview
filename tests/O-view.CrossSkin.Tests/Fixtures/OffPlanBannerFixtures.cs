using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="OffPlanBannerFixture"/>s (ADR-0003, gate G7 parity slice P15,
/// OVI-691): the no-banner (on-plan) state, the diverging state (same heading regardless of the
/// extra-usage setting), and the exhausted-window state's three extra-usage wordings — On, Off
/// and Unknown (source <c>ui-spec.md</c> §"Off-plan usage"). Both skins' own
/// <c>DetailWindowContentBuilder.Build</c> read <see cref="UsageStatistics.Divergence"/> and
/// <see cref="UsageSnapshot.ExtraUsage"/> off the same <see cref="UsageDetail"/> here, so one
/// fixture set drives both skins' render proofs.
/// </summary>
public static class OffPlanBannerFixtures
{
    /// <summary>Exposed so <c>OffPlanBannerFixtureTests</c> can build each skin's content at the
    /// same instant these fixtures were pinned against.</summary>
    public static readonly DateTimeOffset UtcNow = new(2026, 10, 11, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionReset = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeeklyReset = new(2026, 10, 12, 23, 0, 0, TimeSpan.Zero); // a Monday
    private static readonly TokenCount ModestTokens = new(1_200_000, UsageValueStatus.Real);

    private static UsageSnapshot Snapshot(ExtraUsageReading? extraUsage) => new UsageSnapshot(
        DataSourceKind.Live,
        UtcNow.AddMinutes(-5),
        new UsagePercent(72, UsageValueStatus.Real),
        new UsageInstant(SessionReset, UsageValueStatus.Real),
        new UsagePercent(30, UsageValueStatus.Real),
        new UsageInstant(WeeklyReset, UsageValueStatus.Real),
        UsageLevel.Red)
    {
        ExtraUsage = extraUsage,
    };

    private static UsageDetail Detail(DivergenceReading? divergence, ExtraUsageReading? extraUsage) => new(
        Snapshot: Snapshot(extraUsage),
        Statistics: UsageStatistics.Unavailable with { Divergence = divergence },
        Models: ModelUsageBreakdown.Unavailable);

    /// <summary>
    /// No comparison could be run, and even if it could, usage is drawing from the plan as
    /// expected: the banner is absent entirely, never an empty shell above the bars.
    /// </summary>
    public static readonly OffPlanBannerFixture NoBannerOnPlan = new(
        Name: "no-banner-on-plan",
        Detail: Detail(divergence: null, extraUsage: new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-10))),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: Array.Empty<ContentFact>());

    /// <summary>
    /// Substantial local activity with a flat plan meter — the diverging heading is the same
    /// regardless of the account's extra-usage setting (source <c>ui-spec.md</c> §"Off-plan
    /// usage": "the same in all three").
    /// </summary>
    public static readonly OffPlanBannerFixture Diverging = new(
        Name: "diverging",
        Detail: Detail(
            divergence: new DivergenceReading(DivergenceState.Diverging, ModestTokens, 3),
            extraUsage: new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-5))),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "names the observed output-token figure in the detail",
                rendered => rendered.Contains("1.2M", StringComparison.Ordinal)),
        });

    /// <summary>Extra usage On: the exhausted-window heading names that further work bills.</summary>
    public static readonly OffPlanBannerFixture PlanLimitReachedExtraUsageOn = new(
        Name: "plan-limit-reached-extra-usage-on",
        Detail: Detail(
            divergence: new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            extraUsage: new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-10))),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "names the limit and that further work bills",
                rendered => rendered.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    && rendered.Contains("bills", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>Extra usage Off: the exhausted-window heading names that extra usage is switched off.</summary>
    public static readonly OffPlanBannerFixture PlanLimitReachedExtraUsageOff = new(
        Name: "plan-limit-reached-extra-usage-off",
        Detail: Detail(
            divergence: new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            extraUsage: new ExtraUsageReading(ExtraUsageState.Disabled, UtcNow.AddMinutes(-10))),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "names the limit and that extra usage is off",
                rendered => rendered.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    && rendered.Contains("off", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// Extra usage Unknown: Claude Code's cache did not say, so the heading makes no claim
    /// either way — the bare "Plan limit reached", never a guessed on/off.
    /// </summary>
    public static readonly OffPlanBannerFixture PlanLimitReachedExtraUsageUnknown = new(
        Name: "plan-limit-reached-extra-usage-unknown",
        Detail: Detail(
            divergence: new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            extraUsage: null),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "names the limit but asserts neither on nor off",
                rendered => rendered.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    && !rendered.Contains("Enabled", StringComparison.Ordinal)
                    && !rendered.Contains("Disabled", StringComparison.Ordinal)),
        });

    public static IReadOnlyList<OffPlanBannerFixture> All { get; } = new[]
    {
        NoBannerOnPlan,
        Diverging,
        PlanLimitReachedExtraUsageOn,
        PlanLimitReachedExtraUsageOff,
        PlanLimitReachedExtraUsageUnknown,
    };
}
