using System.Windows;
using OView.App;
using OView.Tray.Platform;

namespace OView.Tray.Presentation;

/// <summary>
/// The real branded tray menu flyout window (gate G7 parity slice P21, OVI-738, ADR-0009).
/// Chrome and positioning only — the row list is placeholder content until slice P23 wires real
/// menu actions into it.
///
/// <para><b>Positioning.</b> Docks to the work-area corner next to the taskbar
/// (<see cref="MenuFlyoutPlacement"/>), on the monitor the cursor is currently over
/// (<see cref="MenuFlyoutMonitorLocator"/>) — never at the pointer itself, and never via
/// <c>Shell_NotifyIconGetRect</c> (ADR-0008 D4; the issue's "On P21 and D4" note). Converts
/// between device pixels and WPF's DIPs using that monitor's own effective DPI scale, the same
/// division <c>PopupPositioner</c>-shaped code needs whenever a secondary monitor runs at a
/// different scale than the primary.</para>
///
/// <para><b>Open/close.</b> <see cref="Toggle"/> routes through
/// <see cref="MenuFlyoutToggleController"/>, which reuses the detail widget's own 400 ms
/// close-then-click grace window (<see cref="DetailPushCoordinator.IconClickAwayGrace"/>) rather
/// than a second, independently tuned constant. <see cref="Window.Deactivated"/> is this window's
/// own auto-hide — losing focus (an outside click, Alt-Tab, anything) reports
/// <see cref="MenuFlyoutToggleController.OnHidden"/> before hiding, the same ordering
/// <c>DetailWindow</c> already uses for its own focus-loss dismiss.</para>
///
/// <para>Not unit-tested (no interactive Windows desktop in this environment — the same boundary
/// every other real <see cref="Window"/> in this skin already records). Every decision this
/// window merely carries out is pulled into <see cref="MenuFlyoutPlacement"/> and
/// <see cref="MenuFlyoutToggleController"/>, both proven against plain structs/fakes with no
/// window involved.</para>
/// </summary>
internal sealed class MenuFlyoutWindow : Window
{
    private readonly MenuFlyoutToggleController _toggle;
    private readonly string _version;
    private readonly IReadOnlyList<string> _rowLabels;
    private ThemePreference _theme;
    private bool _suppressDeactivatedReport;

    public MenuFlyoutWindow(string version, IReadOnlyList<string> rowLabels, ThemePreference theme, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(rowLabels);
        ArgumentNullException.ThrowIfNull(clock);

        _version = version;
        _rowLabels = rowLabels;
        _theme = theme;
        _toggle = new MenuFlyoutToggleController(SetVisible, clock);

        Width = MenuFlyoutChrome.CardWidth;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Topmost = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Content = MenuFlyoutChrome.Build(WindowThemePalette.Resolve(theme), _version, _rowLabels).Root;

        Deactivated += OnDeactivated;
    }

    /// <summary>Applies a live theme change (ADR-0009 slice 7's own pattern): repaint by
    /// rebuilding the content, since this chrome has no partial-repaint path yet and the card is
    /// cheap to rebuild.</summary>
    public void ApplyTheme(ThemePreference theme)
    {
        _theme = theme;
        Content = MenuFlyoutChrome.Build(WindowThemePalette.Resolve(theme), _version, _rowLabels).Root;
    }

    /// <summary>The flyout's own open/close gesture (not the tray icon's left-click toggle,
    /// which stays mapped to the detail widget) — opens if closed, closes if open, absorbing a
    /// click within the grace window of the flyout's own last auto-hide.</summary>
    public void Toggle() => _toggle.OnActivated();

    private void SetVisible(bool visible)
    {
        if (visible)
        {
            PositionAtDockedCorner();
            Show();
            Activate();
        }
        else
        {
            _suppressDeactivatedReport = true;
            Hide();
            _suppressDeactivatedReport = false;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suppressDeactivatedReport)
        {
            return;
        }

        _toggle.OnHidden();
        Hide();
    }

    /// <summary>Docks to the corner adjacent to the taskbar on whichever monitor the cursor is
    /// over right now (<see cref="MenuFlyoutMonitorLocator.Locate"/> reads it) — the cursor's
    /// only role is picking the monitor; <see cref="MenuFlyoutPlacement.Compute"/> never sees it.</summary>
    private void PositionAtDockedCorner()
    {
        var (monitor, work, scale) = MenuFlyoutMonitorLocator.Locate();

        var heightDip = MenuFlyoutChrome.ContentHeight(_rowLabels.Count);
        var widthPx = (int)Math.Round(MenuFlyoutChrome.CardWidth * scale);
        var heightPx = (int)Math.Round(heightDip * scale);

        var placement = MenuFlyoutPlacement.Compute(monitor, work, widthPx, heightPx);

        Left = placement.Left / scale;
        Top = placement.Top / scale;
    }
}
