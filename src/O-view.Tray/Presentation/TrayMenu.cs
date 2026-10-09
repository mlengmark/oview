using System.Drawing;
using System.Windows.Forms;
using OView.App;
using OView.Core.Updates;

namespace OView.Tray.Presentation;

/// <summary>
/// The real <c>ContextMenuStrip</c> behind ADR-0009 slice 6 (OVI-480): the seven items D1/D2
/// requires, built once and refreshed from <see cref="TrayMenuController.BuildSnapshot"/> every
/// time the strip opens. All decisions live in <see cref="TrayMenuController"/>, unit-tested
/// against fakes; this type only owns the <c>ToolStripMenuItem</c>s and their checked state —
/// not unit-tested, the same "adapter, not decision logic" split <c>TrayStatusIcon</c> already
/// uses, and for the same reason (no interactive Windows desktop in this environment).
///
/// <para>ADR-0009 slice 7 (OVI-489) adds <see cref="ApplyTheme"/>: the composition root wires a
/// <see cref="ThemeRepaintController"/> to call it with every live <see cref="IThemeSource"/>
/// reading. This type owns converting <see cref="WindowThemePalette"/>'s plain RGB values into
/// <see cref="System.Drawing.Color"/> and painting every item in the strip, including nested
/// drop-down items (the threshold picker) — the WinForms half of the same conversion
/// <c>DetailWindow.ApplyTheme</c> does for WPF.</para>
/// </summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly TrayMenuController _controller;
    private readonly Action<string, string> _showToast;
    private readonly ContextMenuStrip _strip = new();
    private readonly ToolStripMenuItem _runAtStartupItem;
    private readonly ToolStripMenuItem _autoUpdateItem;
    private readonly (int Percent, ToolStripMenuItem Item)[] _thresholdItems;

    public TrayMenu(TrayMenuController controller, Action<string, string> showToast)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(showToast);

        _controller = controller;
        _showToast = showToast;

        var refreshNow = new ToolStripMenuItem("Refresh now");
        refreshNow.Click += (_, _) => _controller.OnRefreshNow();

        var showUsageDetails = new ToolStripMenuItem("Show usage details");
        showUsageDetails.Click += (_, _) => _controller.OnShowUsageDetails();

        var thresholdMenu = new ToolStripMenuItem("Notification threshold");
        _thresholdItems = TrayMenuController.ThresholdChoicesPercent
            .Select(percent =>
            {
                var item = new ToolStripMenuItem($"{percent}%") { CheckOnClick = false };
                item.Click += (_, _) => OnThresholdClicked(percent);
                thresholdMenu.DropDownItems.Add(item);
                return (percent, item);
            })
            .ToArray();

        _runAtStartupItem = new ToolStripMenuItem("Run at startup") { CheckOnClick = false };
        _runAtStartupItem.Click += (_, _) => OnRunAtStartupClicked();

        _autoUpdateItem = new ToolStripMenuItem("Check for updates automatically") { CheckOnClick = false };
        _autoUpdateItem.Click += (_, _) => OnAutoUpdateClicked();

        var checkForUpdatesNow = new ToolStripMenuItem("Check for updates now");
        checkForUpdatesNow.Click += (_, _) => OnCheckForUpdatesNowClicked();

        var copyDiagnostics = new ToolStripMenuItem("Copy diagnostics");
        copyDiagnostics.Click += (_, _) => _controller.OnCopyDiagnostics();

        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => _controller.OnQuit();

        _strip.Items.Add(refreshNow);
        _strip.Items.Add(showUsageDetails);
        _strip.Items.Add(thresholdMenu);
        _strip.Items.Add(_runAtStartupItem);
        _strip.Items.Add(_autoUpdateItem);
        _strip.Items.Add(checkForUpdatesNow);
        _strip.Items.Add(new ToolStripSeparator());
        _strip.Items.Add(copyDiagnostics);
        _strip.Items.Add(quit);

        _strip.Opening += (_, _) => Refresh();
    }

    public ContextMenuStrip Strip => _strip;

    /// <summary>Re-reads every item's rendered state (D3 point 1, D4 point 3) just before the
    /// strip is shown.</summary>
    private void Refresh()
    {
        var snapshot = _controller.BuildSnapshot();

        foreach (var (percent, item) in _thresholdItems)
        {
            item.Checked = percent == snapshot.ThresholdPercent;
        }

        _runAtStartupItem.Checked = snapshot.StartupEnabled;
        _autoUpdateItem.Checked = snapshot.AutoUpdateEnabled;
    }

    /// <summary>Paints the strip and every item in it, recursing into drop-down items (the
    /// threshold picker) so a live theme change reaches the whole menu, not just its top
    /// level.</summary>
    public void ApplyTheme(WindowThemeColors colors)
    {
        var background = ToColor(colors.Background);
        var foreground = ToColor(colors.Foreground);

        _strip.BackColor = background;
        _strip.ForeColor = foreground;
        ApplyThemeToItems(_strip.Items, background, foreground);
    }

    private static void ApplyThemeToItems(ToolStripItemCollection items, Color background, Color foreground)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = background;
            item.ForeColor = foreground;

            if (item is ToolStripMenuItem { HasDropDownItems: true } menuItem)
            {
                ApplyThemeToItems(menuItem.DropDownItems, background, foreground);
            }
        }
    }

    private static Color ToColor(RgbColor color) => Color.FromArgb(color.R, color.G, color.B);

    private void OnThresholdClicked(int percent) => _controller.OnSetThresholdPercent(percent);

    /// <summary>Toggles run-at-startup, renders <see cref="StartupToggleResult.Enabled"/> — the
    /// state the OS actually ended up in, never the click itself (D3 point 2) — and, on a
    /// failure, states that in this skin's own words (D3 point 3) via a balloon tip on the same
    /// tray icon the menu hangs off.</summary>
    private void OnRunAtStartupClicked()
    {
        var requested = !_runAtStartupItem.Checked;
        var result = _controller.OnToggleRunAtStartup(requested);
        _runAtStartupItem.Checked = result.Enabled;

        if (result.Failed)
        {
            _showToast(
                "Run at startup",
                result.Enabled
                    ? "O-view could not turn run at startup off. It is still set to run at startup."
                    : "O-view could not turn run at startup on. It will not run at startup.");
        }
    }

    private void OnAutoUpdateClicked()
    {
        var enabled = !_autoUpdateItem.Checked;
        _controller.OnSetAutoUpdate(enabled);
        _autoUpdateItem.Checked = enabled;
    }

    /// <summary>Runs the interactive check and reports whatever it finds via the same toast
    /// surface the run-at-startup failure statement already uses (D7: a menu item the user
    /// clicked on purpose must never answer with silence).</summary>
    private async void OnCheckForUpdatesNowClicked()
    {
        var result = await _controller.OnCheckForUpdatesNow().ConfigureAwait(true);
        var content = AlertToastFormatter.FormatManualCheck(result);
        _showToast(content.Title, content.Body);
    }

    public void Dispose() => _strip.Dispose();
}
