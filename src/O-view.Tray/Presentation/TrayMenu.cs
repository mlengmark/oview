using System.Windows.Forms;

namespace OView.Tray.Presentation;

/// <summary>
/// The real <c>ContextMenuStrip</c> behind ADR-0009 slice 6 (OVI-480): the seven items D1/D2
/// requires, built once and refreshed from <see cref="TrayMenuController.BuildSnapshot"/> every
/// time the strip opens. All decisions live in <see cref="TrayMenuController"/>, unit-tested
/// against fakes; this type only owns the <c>ToolStripMenuItem</c>s and their checked state —
/// not unit-tested, the same "adapter, not decision logic" split <c>TrayStatusIcon</c> already
/// uses, and for the same reason (no interactive Windows desktop in this environment).
/// </summary>
internal sealed class TrayMenu : IDisposable
{
    private readonly TrayMenuController _controller;
    private readonly Action<string, string> _showFailureToast;
    private readonly ContextMenuStrip _strip = new();
    private readonly ToolStripMenuItem _runAtStartupItem;
    private readonly ToolStripMenuItem _autoUpdateItem;
    private readonly (int Percent, ToolStripMenuItem Item)[] _thresholdItems;

    public TrayMenu(TrayMenuController controller, Action<string, string> showFailureToast)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(showFailureToast);

        _controller = controller;
        _showFailureToast = showFailureToast;

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

        var copyDiagnostics = new ToolStripMenuItem("Copy diagnostics");
        copyDiagnostics.Click += (_, _) => _controller.OnCopyDiagnostics();

        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => _controller.OnQuit();

        _strip.Items.Add(refreshNow);
        _strip.Items.Add(showUsageDetails);
        _strip.Items.Add(thresholdMenu);
        _strip.Items.Add(_runAtStartupItem);
        _strip.Items.Add(_autoUpdateItem);
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
            _showFailureToast(
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

    public void Dispose() => _strip.Dispose();
}
