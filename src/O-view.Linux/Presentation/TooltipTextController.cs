using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 slice 11's wiring (OVI-417): pushes each <see cref="UsageSnapshot"/> through this
/// skin's own <see cref="TooltipFormatter"/> (slice 9 era) to the injected setter — Avalonia's
/// <c>TrayIcon.ToolTipText</c> in <see cref="LinuxStatusIcon"/>. Independently implemented from
/// <c>O-view.Tray</c>'s own <c>TooltipTextController</c> (slice 5, OVI-376) per D1: same shape,
/// no shared code, and this skin's <see cref="TooltipFormatter"/> applies its own wording and
/// no length cap (the Windows 127-char <c>NotifyIcon.Text</c> cap is a Windows API fact that
/// must not travel here — ADR-0001/ADR-0002). No formatting logic lives here; it exists only so
/// the wiring itself is unit-testable against a fake setter, the same "decision logic testable,
/// adapter not" split <see cref="StatusIconController"/> already uses.
/// </summary>
internal sealed class TooltipTextController
{
    private readonly Action<string> _setText;
    private readonly TimeZoneInfo? _displayZone;

    public TooltipTextController(Action<string> setText, TimeZoneInfo? displayZone = null)
    {
        ArgumentNullException.ThrowIfNull(setText);

        _setText = setText;
        _displayZone = displayZone;
    }

    /// <summary>The shell pushed a new snapshot; reformat and push the tooltip text.</summary>
    public void OnSnapshotUpdated(UsageSnapshot snapshot) => _setText(TooltipFormatter.Format(snapshot, _displayZone));
}
