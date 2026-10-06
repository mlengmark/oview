using Avalonia.Controls;

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
/// </summary>
internal sealed class LinuxTrayMenu
{
    private readonly LinuxMenuController _controller;
    private readonly Action<string, string> _showFailureNotification;
    private readonly NativeMenu _menu = new();
    private readonly NativeMenuItem _runAtStartupItem;
    private readonly NativeMenuItem _autoUpdateItem;
    private readonly (int Percent, NativeMenuItem Item)[] _thresholdItems;

    public LinuxTrayMenu(LinuxMenuController controller, Action<string, string> showFailureNotification)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(showFailureNotification);

        _controller = controller;
        _showFailureNotification = showFailureNotification;

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

        _autoUpdateItem = new NativeMenuItem("Check for updates automatically") { ToggleType = MenuItemToggleType.CheckBox };
        _autoUpdateItem.Click += (_, _) => OnAutoUpdateClicked();

        var copyDiagnostics = new NativeMenuItem("Copy diagnostics");
        copyDiagnostics.Click += (_, _) => _controller.CopyDiagnostics();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _controller.Quit();

        _menu.Add(refreshNow);
        _menu.Add(showUsageDetails);
        _menu.Add(thresholdItem);
        _menu.Add(_runAtStartupItem);
        _menu.Add(_autoUpdateItem);
        _menu.Add(new NativeMenuItemSeparator());
        _menu.Add(copyDiagnostics);
        _menu.Add(quit);

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
            _showFailureNotification(
                "Run at startup",
                result.Enabled
                    ? "O-view could not turn run at startup off. It is still set to run at startup."
                    : "O-view could not turn run at startup on. It will not run at startup.");
        }
    }

    private void OnAutoUpdateClicked()
    {
        var enabled = !_autoUpdateItem.IsChecked;
        _controller.SetAutoUpdate(enabled);
        _autoUpdateItem.IsChecked = enabled;
    }
}
