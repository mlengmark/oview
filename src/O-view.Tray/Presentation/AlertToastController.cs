using OView.App;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0008 slice 7's wiring (OVI-391): shows exactly one toast per
/// <see cref="IShellToSkin.RaiseEvent"/> call, formatted by <see cref="AlertToastFormatter"/>.
/// No threshold, dedupe, or debounce logic lives here — the shell already decided an alert is
/// due (ADR-0007 D2 point 6) by the time this type ever sees a <see cref="UsageEvent"/>; this
/// controller only decides how to render what it was given, the same "decision logic testable,
/// adapter not" split <see cref="StatusIconController"/> and <see cref="TooltipTextController"/>
/// already use. The injected <see cref="Action{T1,T2}"/> is the real toast adapter
/// (<c>NotifyIcon.ShowBalloonTip</c> in <see cref="TrayStatusIcon"/>) in production, and a
/// recording fake in tests.
/// </summary>
internal sealed class AlertToastController
{
    private readonly Action<string, string> _showToast;

    public AlertToastController(Action<string, string> showToast)
    {
        ArgumentNullException.ThrowIfNull(showToast);

        _showToast = showToast;
    }

    /// <summary>The shell raised an event; show it as exactly one toast.</summary>
    public void OnEventRaised(UsageEvent usageEvent)
    {
        var content = AlertToastFormatter.Format(usageEvent);
        _showToast(content.Title, content.Body);
    }
}
