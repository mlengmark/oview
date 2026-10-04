using OView.App;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 slice 11's wiring (OVI-417): sends exactly one freedesktop notification per
/// <see cref="IShellToSkin.RaiseEvent"/> call, formatted by <see cref="AlertNotificationFormatter"/>.
/// No threshold, dedupe, or debounce logic lives here — the shell already decided an alert is
/// due (ADR-0007 D2 point 6) by the time this type ever sees a <see cref="UsageEvent"/>; this
/// controller only decides how to render what it was given, the same "decision logic testable,
/// adapter not" split <see cref="StatusIconController"/> and <see cref="TooltipTextController"/>
/// already use — the same boundary <c>O-view.Tray</c>'s own <c>AlertToastController</c> (slice 7,
/// OVI-391) drew, independently implemented per D1.
///
/// <para>The injected <paramref name="send"/> delegate returns a <see cref="Task"/> because the
/// real adapter (<see cref="DBusNotificationSender"/>) is a D-Bus round trip; this controller
/// invokes it and does not await the result, the same fire-and-forget shape
/// <c>Program.cs</c> already uses for <c>NotificationHostMonitor.StartAsync</c> — a dropped
/// notification is not worth blocking the dispatch path for, and no caller here has anything to
/// do with the outcome.</para>
/// </summary>
internal sealed class AlertNotificationController
{
    private readonly Func<string, string, Task> _send;

    public AlertNotificationController(Func<string, string, Task> send)
    {
        ArgumentNullException.ThrowIfNull(send);

        _send = send;
    }

    /// <summary>The shell raised an event; send it as exactly one notification.</summary>
    public void OnEventRaised(UsageEvent usageEvent)
    {
        var content = AlertNotificationFormatter.Format(usageEvent);
        _ = _send(content.Summary, content.Body);
    }
}
