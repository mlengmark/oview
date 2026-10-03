using OView.App;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// The pure decision logic behind ADR-0008 slice 4's status icon (OVI-371): which
/// <see cref="UsageLevel"/> and DPI scale to render, and what a click means. Takes its OS
/// effects as injected delegates so it is fully unit-testable against fakes — <c>TrayStatusIcon</c>
/// is the untested-by-xUnit adapter that owns the real <c>NotifyIcon</c>, GDI icon handle and
/// native message window, and supplies these delegates.
/// </summary>
internal sealed class StatusIconController
{
    private readonly ISkinToShell _skinToShell;
    private readonly Action<UsageLevel, double> _render;
    private readonly Action _reregisterHost;

    private UsageLevel _level = UsageLevel.Green;
    private double _dpiScale = 1.0;

    /// <param name="skinToShell">Where <see cref="OnActivated"/> reports the user's gesture
    /// (ADR-0008 D9b, amended OVI-326: the skin asks, the shell answers with
    /// <c>SetVisible</c>).</param>
    /// <param name="render">Called with the current level and DPI scale whenever either
    /// changes, so the adapter can push a freshly rendered icon.</param>
    /// <param name="reregisterHost">Called when the host's notification area restarted
    /// (Windows' <c>TaskbarCreated</c>), so the adapter can re-add the icon.</param>
    public StatusIconController(ISkinToShell skinToShell, Action<UsageLevel, double> render, Action reregisterHost)
    {
        ArgumentNullException.ThrowIfNull(skinToShell);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(reregisterHost);

        _skinToShell = skinToShell;
        _render = render;
        _reregisterHost = reregisterHost;
    }

    /// <summary>The level last rendered, or <see cref="UsageLevel.Green"/> before the first
    /// snapshot (matching <see cref="UsageSnapshot.Unavailable"/>'s own default band).</summary>
    public UsageLevel CurrentLevel => _level;

    /// <summary>The DPI scale last rendered at, 1.0 (96 DPI) before the first change.</summary>
    public double CurrentDpiScale => _dpiScale;

    /// <summary>The shell pushed a new snapshot; re-render if its <see cref="UsageLevel"/>
    /// changed the band, at the scale already in effect.</summary>
    public void OnSnapshotUpdated(UsageLevel level)
    {
        _level = level;
        _render(_level, _dpiScale);
    }

    /// <summary>Windows delivered a per-monitor DPI v2 change; re-render the current level at
    /// the new scale so the icon stays legible.</summary>
    public void OnDpiChanged(double dpiScale)
    {
        _dpiScale = dpiScale;
        _render(_level, _dpiScale);
    }

    /// <summary>The host's notification area restarted (Windows' <c>TaskbarCreated</c>
    /// broadcast); re-add the icon. Does not re-render — the level and scale have not
    /// changed.</summary>
    public void OnHostRestarted() => _reregisterHost();

    /// <summary>The user activated the icon. Calls <see cref="ISkinToShell.RequestWidget"/> and
    /// nothing else (ADR-0008 D9b, amended OVI-326) — this skin never calls
    /// <c>SetVisible</c> on itself.</summary>
    public void OnActivated() => _skinToShell.RequestWidget(true);
}
