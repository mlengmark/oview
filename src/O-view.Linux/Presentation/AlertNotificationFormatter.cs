using System.Globalization;
using OView.App;
using OView.Core.Updates;

namespace OView.Linux.Presentation;

/// <summary>
/// Builds the freedesktop notification's summary/body text from a <see cref="UsageEvent"/>
/// (ADR-0008 slice 11, OVI-417). Every wording decision lives here, in this skin, and nowhere
/// in <c>O-view.App</c> or <c>O-view.Core</c> (ADR-0001): the shell only states which
/// <see cref="UsageEventKind"/> happened and the Core-contract data backing it — deciding
/// *that* an alert is due (a threshold band, a dedupe window) is the shell's job, not this
/// formatter's (ADR-0008 D2, ADR-0007 D2 point 6). Independently composed from
/// <c>O-view.Tray</c>'s <c>AlertToastFormatter</c> (slice 7, OVI-391) per D1/ADR-0003's
/// anti-drift rule: same decision table, this skin's own sentences, not shared code.
/// </summary>
public static class AlertNotificationFormatter
{
    public static AlertNotificationContent Format(UsageEvent usageEvent) => usageEvent.Kind switch
    {
        UsageEventKind.ThresholdCrossed => new AlertNotificationContent(
            "O-view usage alert",
            FormatThresholdCrossed(usageEvent)),

        UsageEventKind.OffPlanEntered => new AlertNotificationContent(
            "O-view: activity off the plan meter",
            FormatOffPlanEntered(usageEvent)),

        UsageEventKind.UpdateAvailable => new AlertNotificationContent(
            "O-view update available",
            "A new version of O-view can be installed."),

        UsageEventKind.InputDegraded => new AlertNotificationContent(
            "O-view: usage data issue",
            FormatInputDegraded(usageEvent)),

        _ => new AlertNotificationContent("O-view alert", string.Empty),
    };

    private static string FormatThresholdCrossed(UsageEvent usageEvent)
    {
        if (usageEvent.UsageLevel is not { } level)
        {
            return "Usage moved into a new band.";
        }

        return string.Create(CultureInfo.InvariantCulture, $"Usage moved into the {level} band.");
    }

    private static string FormatOffPlanEntered(UsageEvent usageEvent)
    {
        var divergence = usageEvent.Divergence;
        if (divergence is null)
        {
            return "Recent activity no longer tracks the plan meter.";
        }

        return divergence.PlanRisePoints is { } risePoints
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Recent activity no longer tracks the plan meter, which rose {risePoints} points.")
            : "Recent activity no longer tracks the plan meter.";
    }

    private static string FormatInputDegraded(UsageEvent usageEvent)
    {
        var health = usageEvent.Health;
        if (health is null)
        {
            return "A usage data source stopped returning data.";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{health.ProviderName} stopped returning usage data.");
    }

    /// <summary>
    /// The "Check for updates now" menu item's own report (ADR-0010 D4/D7, slicing table row
    /// 5): unlike <see cref="Format"/>'s <see cref="UsageEventKind.UpdateAvailable"/> case,
    /// this always has something to say, including the two outcomes that case never carries —
    /// up to date, and <see cref="UpdateOutcome.Unknown"/> ("could not tell") — because a menu
    /// item the user clicked on purpose must never answer with silence. Independently worded
    /// from <c>O-view.Tray</c>'s own <c>AlertToastFormatter.FormatManualCheck</c> (ADR-0003).
    /// </summary>
    public static AlertNotificationContent FormatManualCheck(UpdateCheckResult result) => result.Outcome switch
    {
        UpdateOutcome.UpToDate => new AlertNotificationContent(
            "O-view is up to date",
            "No newer version is available."),

        UpdateOutcome.UpdateAvailable => new AlertNotificationContent(
            "O-view update available",
            result.Available is { } available
                ? string.Create(CultureInfo.InvariantCulture, $"Version {available.Tag} can be installed.")
                : "A new version of O-view can be installed."),

        UpdateOutcome.RateLimited => new AlertNotificationContent(
            "O-view could not check for updates",
            "GitHub rate-limited the update check. Try again later."),

        _ => new AlertNotificationContent(
            "O-view could not check for updates",
            "Could not tell whether a newer version is available."),
    };
}

/// <summary>The summary and body of one freedesktop notification, already split so the real
/// D-Bus adapter can pass both straight to <c>org.freedesktop.Notifications.Notify</c> — no
/// further formatting decision belongs there.</summary>
public sealed record AlertNotificationContent(string Summary, string Body);
