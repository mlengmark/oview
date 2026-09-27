using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="OffPlanFixture"/>s (ADR-0003, OVI-168). Six fixtures,
/// matching that amendment's minimum: add a new fixture as a new entry in <see cref="All"/>
/// rather than dropping one of these.
/// </summary>
public static class OffPlanFixtures
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 27, 15, 0, 0, TimeSpan.Zero);

    private static readonly TokenCount ModestTokens = new(1_200_000, UsageValueStatus.Real);

    public static readonly OffPlanFixture PlanLimitReachedExtraUsageOnStampReadToday = new(
        Name: "plan-limit-reached-extra-usage-on-stamp-read-today",
        Render: skin => skin.OffPlanTitle(
            new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-10)))
            + "\n"
            + skin.OffPlanDetail(
                new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
                new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-10)),
                UtcNow,
                TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("limit"),
            new ContentFact(
                "the detail carries a bare time-only stamp, not a date, for a same-day reading",
                rendered => rendered.Contains("14:50", StringComparison.Ordinal)
                    && !rendered.Contains("Sep", StringComparison.Ordinal)),
        });

    public static readonly OffPlanFixture PlanLimitReachedExtraUsageOffStampReadEarlierDay = new(
        Name: "plan-limit-reached-extra-usage-off-stamp-read-on-an-earlier-day",
        Render: skin => skin.OffPlanTitle(
            new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            new ExtraUsageReading(ExtraUsageState.Disabled, UtcNow.AddDays(-4)))
            + "\n"
            + skin.OffPlanDetail(
                new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
                new ExtraUsageReading(ExtraUsageState.Disabled, UtcNow.AddDays(-4)),
                UtcNow,
                TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            new ContentFact(
                "the heading says extra usage is switched off",
                rendered => rendered.Contains("off", StringComparison.OrdinalIgnoreCase)),
            new ContentFact(
                "the stamp carries the earlier date, not a bare time, when the reading is not from today",
                rendered => rendered.Contains("Sep", StringComparison.Ordinal)),
        });

    public static readonly OffPlanFixture PlanLimitReachedExtraUsageUnavailable = new(
        Name: "plan-limit-reached-extra-usage-unavailable",
        Render: skin => skin.OffPlanTitle(
            new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
            null)
            + "\n"
            + skin.OffPlanDetail(
                new DivergenceReading(DivergenceState.PlanLimitReached, ModestTokens, 0),
                null,
                UtcNow,
                TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("limit"),
            new ContentFact(
                "neither skin invents a reading: no reported-by/from-Claude-Code stamp appears",
                rendered => !rendered.Contains("read ", StringComparison.OrdinalIgnoreCase)),
        });

    public static readonly OffPlanFixture DivergingExtraUsageOnRisePresent = new(
        Name: "diverging-extra-usage-on-plan-rise-points-present",
        Render: skin => skin.OffPlanDetail(
            new DivergenceReading(DivergenceState.Diverging, ModestTokens, 3),
            new ExtraUsageReading(ExtraUsageState.Enabled, UtcNow.AddMinutes(-5)),
            UtcNow,
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("3"),
            new ContentFact(
                "names the observed output-token figure",
                rendered => rendered.Contains("1.2M", StringComparison.Ordinal)),
        });

    public static readonly OffPlanFixture NonOffPlanStateRendersNoBannerContent = new(
        Name: "non-off-plan-state-consistent-renders-no-banner-content",
        Render: skin =>
            skin.OffPlanTitle(new DivergenceReading(DivergenceState.Consistent, ModestTokens, 5), null)
            + "|"
            + skin.OffPlanDetail(
                new DivergenceReading(DivergenceState.Consistent, ModestTokens, 5), null, UtcNow, TimeZoneInfo.Utc)
            + "|"
            + skin.OffPlanNote(false)
            + "|"
            + skin.EstTodayLabel(false),
        ContentFacts: new[]
        {
            new ContentFact(
                "title and detail are empty, OffPlanNote is empty, EstTodayLabel takes its 'value' form",
                rendered => rendered == "||" + "|Est. value today"),
        });

    public static readonly OffPlanFixture RiseNotMeasurableRendersNoRiseWording = new(
        Name: "rise-not-measurable-renders-no-rise-wording",
        Render: skin => skin.OffPlanTitle(
            new DivergenceReading(DivergenceState.RiseNotMeasurable, ModestTokens, null), null)
            + "|"
            + skin.OffPlanDetail(
                new DivergenceReading(DivergenceState.RiseNotMeasurable, ModestTokens, null),
                null,
                UtcNow,
                TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            new ContentFact(
                "not off-plan, so both entry points render nothing — never a fabricated \"0 points\"",
                rendered => rendered == "|" && !rendered.Contains('0')),
        });

    public static IReadOnlyList<OffPlanFixture> All { get; } = new[]
    {
        PlanLimitReachedExtraUsageOnStampReadToday,
        PlanLimitReachedExtraUsageOffStampReadEarlierDay,
        PlanLimitReachedExtraUsageUnavailable,
        DivergingExtraUsageOnRisePresent,
        NonOffPlanStateRendersNoBannerContent,
        RiseNotMeasurableRendersNoRiseWording,
    };
}
