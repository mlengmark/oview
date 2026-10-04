using OView.App;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// The pure decision logic behind ADR-0008 slice 9's status icon (OVI-403): which
/// <see cref="UsageLevel"/> to render, when the icon may render at all, and what activation
/// means. Structurally mirrors <c>O-view.Tray</c>'s <c>StatusIconController</c> (slice 4,
/// OVI-371) but is an independent implementation (D1) — the gating rule below replaces
/// Windows' DPI/<c>TaskbarCreated</c> concerns, which do not exist on this platform. Takes its
/// effects as injected delegates so it is fully unit-testable against fakes; <c>LinuxStatusIcon</c>
/// is the untested-by-xUnit adapter that owns the real Avalonia <c>TrayIcon</c> and supplies
/// these delegates.
/// </summary>
internal sealed class StatusIconController
{
    private readonly ISkinToShell _skinToShell;
    private readonly Action<UsageLevel> _render;
    private readonly Action _registerIcon;

    private UsageLevel _level = UsageLevel.Green;
    private bool _hostPresent;

    /// <param name="skinToShell">Where <see cref="OnActivated"/> reports the user's gesture
    /// (ADR-0008 D9b, same shell contract Windows slice 4 uses: the skin asks, the shell
    /// answers with <c>SetVisible</c>).</param>
    /// <param name="render">Called with the current level whenever it changes while a
    /// notification-area host is present, so the adapter can push a freshly rendered icon.</param>
    /// <param name="registerIcon">Called exactly once, the first time a host is observed
    /// present (initially or via <see cref="OnHostAppeared"/>), so the adapter can add the icon
    /// to that host for the first time.</param>
    public StatusIconController(ISkinToShell skinToShell, Action<UsageLevel> render, Action registerIcon)
    {
        ArgumentNullException.ThrowIfNull(skinToShell);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(registerIcon);

        _skinToShell = skinToShell;
        _render = render;
        _registerIcon = registerIcon;
    }

    /// <summary>The level last rendered, or <see cref="UsageLevel.Green"/> before the first
    /// snapshot (matching <see cref="UsageSnapshot.Unavailable"/>'s own default band).</summary>
    public UsageLevel CurrentLevel => _level;

    /// <summary>Whether a notification-area host has been observed present
    /// (ADR-0008 D6) — until this is <see langword="true"/>, the icon has never been
    /// registered and <see cref="OnSnapshotUpdated"/> does not render.</summary>
    public bool HostPresent => _hostPresent;

    /// <summary>The shell pushed a new snapshot; re-render if a host is present. While no host
    /// has been observed, the level is still tracked so the icon renders correctly the moment
    /// one appears (D6 point 4) — this is what keeps the absent case a documented advisory
    /// rather than a crash or a silently-missing icon.</summary>
    public void OnSnapshotUpdated(UsageLevel level)
    {
        _level = level;

        if (_hostPresent)
        {
            _render(_level);
        }
    }

    /// <summary>
    /// <see cref="OView.Linux.Platform.NotificationHostMonitor"/> reported whether a host was
    /// observed on the initial probe. Registers and renders the icon immediately if so; does
    /// nothing further if not — the absent case's advisory is already handled by the existing
    /// <see cref="OView.Linux.Presentation.NotificationHostAdvisoryFormatter"/>/<c>Console.Error</c>
    /// trace this slice does not duplicate.
    /// </summary>
    public void OnHostObserved(bool present)
    {
        _hostPresent = present;

        if (present)
        {
            _registerIcon();
            _render(_level);
        }
    }

    /// <summary>A previously-absent host appeared later
    /// (<see cref="OView.Linux.Platform.NotificationHostMonitor.HostAppeared"/>); register and
    /// render the icon now, without requiring a restart (D6 point 4).</summary>
    public void OnHostAppeared() => OnHostObserved(true);

    /// <summary>The user activated the icon. Calls <see cref="ISkinToShell.RequestWidget"/> and
    /// nothing else, the same shell contract Windows slice 4 uses — this skin never calls
    /// <c>SetVisible</c> on itself.</summary>
    public void OnActivated() => _skinToShell.RequestWidget(true);
}
