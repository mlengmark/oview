using System.Globalization;
using OView.App;
using OView.Core.Updates;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the toast title/body text from a <see cref="UsageEvent"/> (ADR-0008 slice 7, OVI-391).
/// Every wording decision lives here, in the Windows skin, and nowhere in <c>O-view.App</c> or
/// <c>O-view.Core</c> (ADR-0001): the shell only states which <see cref="UsageEventKind"/>
/// happened and the Core-contract data backing it (ADR-0008 D2's "present <c>RaiseEvent</c> and
/// nothing else" — deciding *that* an alert is due, e.g. a threshold band or a dedupe window, is
/// the shell's job, not this formatter's). This class is not shared with <c>O-view.Linux</c>.
/// </summary>
public static class AlertToastFormatter
{
    public static AlertToastContent Format(UsageEvent usageEvent) => usageEvent.Kind switch
    {
        UsageEventKind.ThresholdCrossed => new AlertToastContent(
            "O-view usage alert",
            FormatThresholdCrossed(usageEvent)),

        UsageEventKind.OffPlanEntered => new AlertToastContent(
            "O-view: activity off the plan meter",
            FormatOffPlanEntered(usageEvent)),

        UsageEventKind.UpdateAvailable => new AlertToastContent(
            "O-view update available",
            "A newer version of O-view is available."),

        UsageEventKind.InputDegraded => new AlertToastContent(
            "O-view: usage data issue",
            FormatInputDegraded(usageEvent)),

        _ => new AlertToastContent("O-view alert", string.Empty),
    };

    private static string FormatThresholdCrossed(UsageEvent usageEvent)
    {
        if (usageEvent.UsageLevel is not { } level)
        {
            return "Usage crossed into a new band.";
        }

        return string.Create(CultureInfo.InvariantCulture, $"Usage crossed into {level}.");
    }

    private static string FormatOffPlanEntered(UsageEvent usageEvent)
    {
        var divergence = usageEvent.Divergence;
        if (divergence is null)
        {
            return "Local activity is diverging from the plan meter.";
        }

        return divergence.PlanRisePoints is { } risePoints
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Local activity is diverging from the plan meter, which has risen {risePoints} points.")
            : "Local activity is diverging from the plan meter.";
    }

    private static string FormatInputDegraded(UsageEvent usageEvent)
    {
        var health = usageEvent.Health;
        if (health is null)
        {
            return "A usage data source is degraded.";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{health.ProviderName} is not returning usage data.");
    }

    /// <summary>
    /// The "Check for updates now" menu item's own report (ADR-0010 D4/D7, slicing table row
    /// 5): unlike <see cref="Format"/>'s <see cref="UsageEventKind.UpdateAvailable"/> case,
    /// this always has something to say, including the two outcomes that case never carries —
    /// up to date, and <see cref="UpdateOutcome.Unknown"/> ("could not tell") — because a menu
    /// item the user clicked on purpose must never answer with silence.
    /// </summary>
    public static AlertToastContent FormatManualCheck(UpdateCheckResult result) => result.Outcome switch
    {
        UpdateOutcome.UpToDate => new AlertToastContent(
            "O-view is up to date",
            "No newer version is available."),

        UpdateOutcome.UpdateAvailable => new AlertToastContent(
            "O-view update available",
            result.Available is { } available
                ? string.Create(CultureInfo.InvariantCulture, $"Version {available.Tag} is available.")
                : "A newer version of O-view is available."),

        UpdateOutcome.RateLimited => new AlertToastContent(
            "O-view could not check for updates",
            "GitHub rate-limited the update check. Try again later."),

        _ => new AlertToastContent(
            "O-view could not check for updates",
            "Could not tell whether a newer version is available."),
    };
}

/// <summary>The title and body of one toast, already split so a skin adapter can pass both
/// straight to its OS toast API — no further formatting decision belongs there.</summary>
public sealed record AlertToastContent(string Title, string Body);
