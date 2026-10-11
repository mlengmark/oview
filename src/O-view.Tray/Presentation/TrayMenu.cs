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
///
/// <para>G7 parity slice P23 (OVI-744) completes the row set the issue's own ui-spec reference
/// names: "Update automatically" is hidden for a portable build
/// (<see cref="TrayMenuSnapshot.AutoUpdateRowVisible"/>, <c>UpdatePolicy.IsInstalledBuild</c>)
/// rather than offered and then ignored; "Weekly reset…" opens the branded dialog P24 (OVI-718)
/// built but never wired; "Quit" is renamed "Exit" to match the source's own wording; and the
/// two toggle rows (run-at-startup, auto-update) now cancel the strip's own
/// <c>Closing</c> event once so the tick is visible before the menu goes away, while every
/// action row still closes it immediately, same as before.</para>
/// </summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly TrayMenuController _controller;
    private readonly Action<string, string> _showToast;
    private readonly Func<ThemePreference> _currentTheme;
    private readonly ContextMenuStrip _strip = new();
    private readonly ToolStripMenuItem _runAtStartupItem;
    private readonly ToolStripMenuItem _autoUpdateItem;
    private readonly (int Percent, ToolStripMenuItem Item)[] _thresholdItems;

    /// <summary>Set by <see cref="OnRunAtStartupClicked"/>/<see cref="OnAutoUpdateClicked"/>
    /// just before they call the controller, and read once by <see cref="OnStripClosing"/>: the
    /// ui-spec's "toggles leave the flyout open" rule (G7 parity slice P23, OVI-744), ported to
    /// this skin's WinForms <c>ContextMenuStrip</c>, which otherwise closes on every item click
    /// regardless of <c>CheckOnClick</c>.</summary>
    private bool _keepOpenOnNextClose;

    public TrayMenu(TrayMenuController controller, Action<string, string> showToast, Func<ThemePreference> currentTheme)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(showToast);
        ArgumentNullException.ThrowIfNull(currentTheme);

        _controller = controller;
        _showToast = showToast;
        _currentTheme = currentTheme;

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

        _autoUpdateItem = new ToolStripMenuItem("Update automatically") { CheckOnClick = false };
        _autoUpdateItem.Click += (_, _) => OnAutoUpdateClicked();

        var checkForUpdatesNow = new ToolStripMenuItem("Check for updates now");
        checkForUpdatesNow.Click += (_, _) => OnCheckForUpdatesNowClicked();

        var weeklyReset = new ToolStripMenuItem("Weekly reset…");
        weeklyReset.Click += (_, _) => OnWeeklyResetClicked();

        var copyDiagnostics = new ToolStripMenuItem("Copy diagnostics");
        copyDiagnostics.Click += (_, _) => _controller.OnCopyDiagnostics();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => _controller.OnQuit();

        _strip.Items.Add(refreshNow);
        _strip.Items.Add(showUsageDetails);
        _strip.Items.Add(thresholdMenu);
        _strip.Items.Add(weeklyReset);
        _strip.Items.Add(_runAtStartupItem);
        _strip.Items.Add(_autoUpdateItem);
        _strip.Items.Add(checkForUpdatesNow);
        _strip.Items.Add(new ToolStripSeparator());
        _strip.Items.Add(copyDiagnostics);
        _strip.Items.Add(exit);

        _strip.Opening += (_, _) => Refresh();
        _strip.Closing += OnStripClosing;
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
        _autoUpdateItem.Visible = snapshot.AutoUpdateRowVisible;
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
        _keepOpenOnNextClose = true;

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

    /// <summary>Ticks the row to the setting's actual post-write value (G7 parity slice P23,
    /// OVI-744), read back from <see cref="TrayMenuController.OnSetAutoUpdate"/> rather than
    /// the requested value — a settings-store write that silently fails to persist must not
    /// leave this row showing a tick for a setting that never actually changed.</summary>
    private void OnAutoUpdateClicked()
    {
        _keepOpenOnNextClose = true;

        var requested = !_autoUpdateItem.Checked;
        var actual = _controller.OnSetAutoUpdate(requested);
        _autoUpdateItem.Checked = actual;
    }

    /// <summary>Opens the branded weekly-reset entry dialog built in gate G7 parity slice P24
    /// (OVI-718) — this slice's only job for that dialog is wiring this action row to
    /// <see cref="WeeklyResetEntryDialog.Show"/>; nothing here persists its result (P24's own
    /// remarks: no Core type exists yet for a manually entered reset to live in).</summary>
    private void OnWeeklyResetClicked() => WeeklyResetEntryDialog.Show(current: null, _currentTheme());

    /// <summary>The ui-spec's "toggles leave the flyout open" rule (G7 parity slice P23):
    /// cancels the one <see cref="ContextMenuStrip.Closing"/> that a toggle row's own click just
    /// caused, and only that one — every other close (an action item, Esc, an outside click)
    /// proceeds untouched.</summary>
    private void OnStripClosing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        if (_keepOpenOnNextClose && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
        {
            e.Cancel = true;
        }

        _keepOpenOnNextClose = false;
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
