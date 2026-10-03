using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0008 slice 5's wiring (OVI-376): pushes each <see cref="UsageSnapshot"/> through the
/// existing <see cref="TooltipFormatter"/> (Phase 1 slice 1, OVI-10) and forwards the already
/// capped result to the injected setter — <c>NotifyIcon.Text</c> in <see cref="TrayStatusIcon"/>.
/// No formatting or capping logic lives here; <see cref="TooltipFormatter"/> owns both already.
/// This class exists only so the wiring itself is unit-testable against a fake setter, the same
/// "decision logic testable, adapter not" split <see cref="StatusIconController"/> already uses.
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
