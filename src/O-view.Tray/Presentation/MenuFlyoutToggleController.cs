using OView.App;

namespace OView.Tray.Presentation;

/// <summary>
/// The open/close decision behind the branded menu flyout (gate G7 parity slice P21, OVI-738):
/// toggles rather than always opening, absorbing a click that arrives within the same grace
/// window the detail widget already uses rather than treating it as a fresh open — the issue's
/// own instruction is to wire "P4's existing 400 ms close-then-click rule", so
/// <see cref="GraceWindow"/> is <see cref="DetailPushCoordinator.IconClickAwayGrace"/> itself,
/// not a second constant that could drift from it.
///
/// <para>Deliberately independent of <see cref="DetailPushCoordinator"/>: that type pushes a
/// <see cref="OView.Core.Models.UsageDetail"/> through <see cref="IShellToSkin"/> and is wired to
/// <see cref="ISkinToShell.ToggleWidget"/>'s specific gesture (the tray icon's left click). The
/// menu flyout reads no shell data and adds no shell seam member (ADR-0009's own "the menu is a
/// skin surface over seams that already exist") — its open/close state is this skin's own, and
/// this controller is deliberately the smallest thing that can own it: no shell, no clock
/// dependency beyond timing the grace window, nothing about what the flyout shows (P23's job).
/// </para>
/// </summary>
internal sealed class MenuFlyoutToggleController
{
    /// <summary>Reuses the detail widget's own grace window verbatim — see the type remarks for
    /// why this is not a second, independently tuned constant.</summary>
    public static readonly TimeSpan GraceWindow = DetailPushCoordinator.IconClickAwayGrace;

    private readonly Action<bool> _setVisible;
    private readonly IClock _clock;

    private bool _visible;
    private DateTimeOffset _lastHiddenAt = DateTimeOffset.MinValue;

    public MenuFlyoutToggleController(Action<bool> setVisible, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(setVisible);
        ArgumentNullException.ThrowIfNull(clock);

        _setVisible = setVisible;
        _clock = clock;
    }

    public bool IsVisible => _visible;

    /// <summary>The flyout lost focus (auto-hide) or was otherwise dismissed without going
    /// through <see cref="OnActivated"/> — records when, so a toggle gesture arriving moments
    /// later is absorbed rather than reopening it.</summary>
    public void OnHidden()
    {
        _visible = false;
        _lastHiddenAt = _clock.UtcNow;
    }

    /// <summary>The user triggered the flyout's own open/close gesture: closes it if open; if
    /// closed, opens it unless the close happened within <see cref="GraceWindow"/> of now, in
    /// which case this click is the second half of that same dismissal.</summary>
    public void OnActivated()
    {
        if (_visible)
        {
            Hide();
            return;
        }

        if (_clock.UtcNow - _lastHiddenAt < GraceWindow)
        {
            return;
        }

        Show();
    }

    private void Show()
    {
        _visible = true;
        _setVisible(true);
    }

    private void Hide()
    {
        _visible = false;
        _lastHiddenAt = _clock.UtcNow;
        _setVisible(false);
    }
}
