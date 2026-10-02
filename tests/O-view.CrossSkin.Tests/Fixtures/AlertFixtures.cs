using OView.App;
using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="AlertFixture"/>s (ADR-0003, ADR-0008 D2, OVI-324). One per
/// <see cref="UsageEventKind"/>, covering every payload shape <see cref="UsageEvent"/> carries.
/// </summary>
public static class AlertFixtures
{
    /// <summary>A threshold crossing into the most severe band.</summary>
    public static readonly AlertFixture ThresholdCrossedIntoRed = new(
        Name: "threshold-crossed-into-red",
        Event: new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red },
        ContentFacts: new[]
        {
            new ContentFact(
                "names the band the crossing moved into",
                rendered => rendered.Contains("Red", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>Local activity diverging from the plan meter, with a measurable rise.</summary>
    public static readonly AlertFixture OffPlanEnteredWithMeasurableRise = new(
        Name: "off-plan-entered-with-measurable-rise",
        Event: new UsageEvent(UsageEventKind.OffPlanEntered)
        {
            Divergence = new DivergenceReading(
                DivergenceState.Diverging,
                new TokenCount(2_000_000, UsageValueStatus.Real),
                PlanRisePoints: 7),
        },
        ContentFacts: new[]
        {
            // Raw scalar, not a formatted abbreviation — OffPlanFixtures' own convention for
            // PlanRisePoints (ADR-0003, OVI-168): the digit survives whatever wording wraps it.
            ContentFact.Contains("7"),
        });

    /// <summary>
    /// A newer release is available. <see cref="UsageEvent"/> carries no extra payload for
    /// this kind — the shell's update check (ADR-0007 D3) only tells a skin *that* a newer
    /// release exists, not a version string or release notes — so there is no additional
    /// content fact to pin here beyond the kind itself.
    /// </summary>
    public static readonly AlertFixture UpdateAvailable = new(
        Name: "update-available",
        Event: new UsageEvent(UsageEventKind.UpdateAvailable),
        ContentFacts: Array.Empty<ContentFact>());

    /// <summary>A provider failing repeatedly, never having once succeeded.</summary>
    public static readonly AlertFixture InputDegradedNeverSucceeded = new(
        Name: "input-degraded-never-succeeded",
        Event: new UsageEvent(UsageEventKind.InputDegraded)
        {
            Health = new ProviderHealth("ccusage-cli", ProviderHealthOutcome.Failed, LastSuccessAt: null, ConsecutiveFailures: 3),
        },
        ContentFacts: new[]
        {
            ContentFact.Contains("ccusage-cli"),
        });

    public static IReadOnlyList<AlertFixture> All { get; } = new[]
    {
        ThresholdCrossedIntoRed,
        OffPlanEnteredWithMeasurableRise,
        UpdateAvailable,
        InputDegradedNeverSucceeded,
    };
}
