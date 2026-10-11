using Avalonia.Controls;
using OView.App;
using OView.Core.Updates;

namespace OView.Linux.Presentation;

/// <summary>
/// The real <c>NativeMenu</c> behind ADR-0009 slice 9 (OVI-484): the same seven items D1/D2
/// requires (minus nothing yet — theme is slices 7/8, out of scope here), rebuilt from
/// <see cref="LinuxMenuController.Snapshot"/> every time the menu opens. Every decision lives in
/// <see cref="LinuxMenuController"/>, unit-tested against fakes; this type only owns the
/// <see cref="NativeMenuItem"/>s and their checked state — not unit-tested, the same
/// "adapter, not decision logic" split <see cref="LinuxStatusIcon"/> already draws, and for the
/// same reason (no interactive Linux display reachable in this environment).
///
/// <para>Independently structured from <c>O-view.Tray.Presentation.TrayMenu</c> (ADR-0008 D1):
/// this adapter owns an Avalonia <see cref="NativeMenu"/> rather than a WinForms
/// <c>ContextMenuStrip</c>, and its own item wording.</para>
///
/// <para>G7 parity slice P23 (OVI-744) completes the row set: "Update automatically" is hidden
/// for a non-package build (<see cref="LinuxMenuSnapshot.AutoUpdateRowVisible"/>,
/// <c>UpdatePolicy.IsInstalledBuild</c>); "Weekly reset…" opens the branded dialog P24
/// (OVI-718) built but never wired, via <see cref="MenuOwnerWindow"/>; "Quit" is renamed "Exit"
/// to match the source's own wording. The ui-spec's "toggles leave the flyout open" rule is
/// this surface's one unmet row requirement — <see cref="NativeMenu"/> wraps the host desktop's
/// own native menu widget, which closes on every item activation by its own design with no
/// property this type can set to change that (INFERRED: no Avalonia API exposes one); it is the
/// same already-documented boundary as this adapter's theme (above) and its "not unit-tested"
/// note, not a regression this slice introduces.</para>
/// </summary>
internal sealed class LinuxTrayMenu
{
    private readonly LinuxMenuController _controller;
    private readonly Action<string, string> _showNotification;
    private readonly MenuOwnerWindow _ownerWindow;
    private readonly Func<ThemePreference> _currentTheme;
    private readonly NativeMenu _menu = new();
    private readonly NativeMenuItem _runAtStartupItem;
    private readonly NativeMenuItem _autoUpdateItem;
    private readonly (int Percent, NativeMenuItem Item)[] _thresholdItems;

    public LinuxTrayMenu(
        LinuxMenuController controller,
        Action<string, string> showNotification,
        MenuOwnerWindow ownerWindow,
        Func<ThemePreference> currentTheme)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(showNotification);
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(currentTheme);

        _controller = controller;
        _showNotification = showNotification;
        _ownerWindow = ownerWindow;
        _currentTheme = currentTheme;

        var refreshNow = new NativeMenuItem("Refresh now");
        refreshNow.Click += (_, _) => _controller.RefreshNow();

        var showUsageDetails = new NativeMenuItem("Show usage details");
        showUsageDetails.Click += (_, _) => _controller.ShowUsageDetails();

        var thresholdSubmenu = new NativeMenu();
        _thresholdItems = LinuxMenuController.ThresholdChoicesPercent
            .Select(percent =>
            {
                var item = new NativeMenuItem($"{percent}%") { ToggleType = MenuItemToggleType.Radio };
                item.Click += (_, _) => _controller.SetThresholdPercent(percent);
                thresholdSubmenu.Add(item);
                return (percent, item);
            })
            .ToArray();
        var thresholdItem = new NativeMenuItem("Notification threshold") { Menu = thresholdSubmenu };

        _runAtStartupItem = new NativeMenuItem("Run at startup") { ToggleType = MenuItemToggleType.CheckBox };
        _runAtStartupItem.Click += (_, _) => OnRunAtStartupClicked();

        _autoUpdateItem = new NativeMenuItem("Update automatically") { ToggleType = MenuItemToggleType.CheckBox };
        _autoUpdateItem.Click += (_, _) => OnAutoUpdateClicked();

        var checkForUpdatesNow = new NativeMenuItem("Check for updates now");
        checkForUpdatesNow.Click += (_, _) => OnCheckForUpdatesNowClicked();

        var weeklyReset = new NativeMenuItem("Weekly reset…");
        weeklyReset.Click += (_, _) => OnWeeklyResetClicked();

        var copyDiagnostics = new NativeMenuItem("Copy diagnostics");
        copyDiagnostics.Click += (_, _) => _controller.CopyDiagnostics();

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => _controller.Quit();

        _menu.Add(refreshNow);
        _menu.Add(showUsageDetails);
        _menu.Add(thresholdItem);
        _menu.Add(weeklyReset);
        _menu.Add(_runAtStartupItem);
        _menu.Add(_autoUpdateItem);
        _menu.Add(checkForUpdatesNow);
        _menu.Add(new NativeMenuItemSeparator());
        _menu.Add(copyDiagnostics);
        _menu.Add(exit);

        _menu.Opening += (_, _) => Refresh();
    }

    public NativeMenu Menu => _menu;

    /// <summary>Re-reads every item's rendered state (D3 point 1, D4 point 3) just before the
    /// menu is shown — never the state it was last clicked into.</summary>
    private void Refresh()
    {
        var snapshot = _controller.Snapshot();

        foreach (var (percent, item) in _thresholdItems)
        {
            item.IsChecked = percent == snapshot.ThresholdPercent;
        }

        _runAtStartupItem.IsChecked = snapshot.StartupEnabled;
        _autoUpdateItem.IsChecked = snapshot.AutoUpdateEnabled;
        _autoUpdateItem.IsVisible = snapshot.AutoUpdateRowVisible;
    }

    /// <summary>Toggles run-at-startup and renders <see cref="StartupToggleOutcome.Enabled"/> —
    /// the state the OS actually ended up in, never the click itself (D3 point 2). On a
    /// failure, states that in this skin's own words (D3 point 3) via
    /// <see cref="_showFailureNotification"/>, the same desktop-notification surface
    /// <c>AlertNotificationController</c> already uses — not a new one.</summary>
    private void OnRunAtStartupClicked()
    {
        var requested = !_runAtStartupItem.IsChecked;
        var result = _controller.ToggleRunAtStartup(requested);
        _runAtStartupItem.IsChecked = result.Enabled;

        if (result.Failed)
        {
            _showNotification(
                "Run at startup",
                result.Enabled
                    ? "O-view could not turn run at startup off. It is still set to run at startup."
                    : "O-view could not turn run at startup on. It will not run at startup.");
        }
    }

    /// <summary>Ticks the row to the setting's actual post-write value (G7 parity slice P23,
    /// OVI-744), read back from <see cref="LinuxMenuController.SetAutoUpdate"/> rather than the
    /// requested value — the same reasoning <see cref="OnRunAtStartupClicked"/> already applies
    /// to its own toggle.</summary>
    private void OnAutoUpdateClicked()
    {
        var requested = !_autoUpdateItem.IsChecked;
        var actual = _controller.SetAutoUpdate(requested);
        _autoUpdateItem.IsChecked = actual;
    }

    /// <summary>Opens the branded weekly-reset entry dialog built in gate G7 parity slice P24
    /// (OVI-718) — this slice's only job for that dialog is wiring this action row to
    /// <see cref="WeeklyResetEntryDialog.ShowAsync"/>; nothing here persists its result (P24's
    /// own remarks: no Core type exists yet for a manually entered reset to live in). No-op if
    /// <see cref="MenuOwnerWindow.Current"/> is still unset — only possible in the instant
    /// between this menu's construction and <c>App.OnFrameworkInitializationCompleted</c>
    /// building the detail window, before Avalonia's message loop is even pumping input.</summary>
    private async void OnWeeklyResetClicked()
    {
        if (_ownerWindow.Current is { } owner)
        {
            await WeeklyResetEntryDialog.ShowAsync(owner, current: null, _currentTheme());
        }
    }

    /// <summary>Runs the interactive check and reports whatever it finds via the same
    /// notification surface the run-at-startup failure statement already uses (D7: a menu item
    /// the user clicked on purpose must never answer with silence).</summary>
    private async void OnCheckForUpdatesNowClicked()
    {
        var result = await _controller.CheckForUpdatesNow().ConfigureAwait(true);
        var content = AlertNotificationFormatter.FormatManualCheck(result);
        _showNotification(content.Summary, content.Body);
    }
}
