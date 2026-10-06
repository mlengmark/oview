using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OView.App;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// The real OS half of ADR-0008 slice 4's status icon (OVI-371): a <see cref="NotifyIcon"/>
/// rendered from the pushed <see cref="UsageLevel"/>, re-rendered on a per-monitor DPI v2
/// change, and re-added after the shell's notification area restarts (Explorer's
/// <c>TaskbarCreated</c> broadcast). All level/DPI/activation decisions live in
/// <see cref="StatusIconController"/>, which is unit-tested against fakes; this type only owns
/// the <c>NotifyIcon</c>, the GDI icon handle, and a hidden native window to receive
/// <c>WM_DPICHANGED</c> and <c>TaskbarCreated</c> — neither of which <see cref="NotifyIcon"/>
/// has a window of its own to intercept. Not unit-tested (no interactive Windows desktop in
/// this environment — the same "not verified" boundary ADR-0008 slice 3 already recorded for
/// its own WPF message loop); proved only by this PR's documented manual run.
/// Slice 5 (OVI-376) adds the tooltip: <see cref="TooltipTextController"/> owns the same
/// "decision logic testable, adapter not" split and pushes the already-capped
/// <see cref="TooltipFormatter"/> output to <c>NotifyIcon.Text</c>. Slice 7 (OVI-391) adds
/// <see cref="ShowToast"/>: <c>NotifyIcon.ShowBalloonTip</c> is the toast surface tied to this
/// same icon (Windows has no toast API independent of a notification-area icon), so it lives
/// here rather than a new standalone adapter; <see cref="AlertToastController"/> owns the
/// "which event, what text" decision and is the only caller.
/// ADR-0009 slice 6 (OVI-480) adds the right-click menu: <see cref="TrayMenu"/> owns the real
/// <c>ContextMenuStrip</c>, assigned to this icon's <c>NotifyIcon.ContextMenuStrip</c>, and
/// reuses <see cref="ShowToast"/> as the surface for its run-at-startup failed-toggle
/// statement (D3 point 3) — the same balloon tip <see cref="AlertToastController"/> already
/// uses, not a new one.
/// ADR-0009 slice 7 (OVI-489) wires a <see cref="ThemeRepaintController"/> to
/// <see cref="TrayMenu.ApplyTheme"/> so the menu repaints on every live
/// <see cref="IThemeSource"/> change, disposed alongside everything else this type owns.
/// </summary>
internal sealed class TrayStatusIcon : IDisposable
{
    private const int WM_DPICHANGED = 0x02E0;
    private const int ToastTimeoutMs = 10_000;

    private readonly StatusIconController _controller;
    private readonly TooltipTextController _tooltip;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayMenu _menu;
    private readonly ThemeRepaintController _menuTheme;
    private readonly MessageSink _messageSink;
    private readonly int _taskbarCreatedMessage;
    private Icon? _currentIcon;
    private bool _disposed;

    public TrayStatusIcon(ISkinToShell skinToShell, Func<ShellSettings> currentSettings, IStartupRegistration startupRegistration, IThemeSource themeSource)
    {
        _controller = new StatusIconController(skinToShell, Render, Reregister);
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        _notifyIcon = new NotifyIcon { Visible = false };
        _notifyIcon.MouseClick += (_, _) => _controller.OnActivated();
        _tooltip = new TooltipTextController(text => _notifyIcon.Text = text);

        var menuController = new TrayMenuController(skinToShell, currentSettings, startupRegistration);
        _menu = new TrayMenu(menuController, ShowToast);
        _notifyIcon.ContextMenuStrip = _menu.Strip;
        _menuTheme = new ThemeRepaintController(themeSource, _menu.ApplyTheme);

        _messageSink = new MessageSink(this);

        Render(_controller.CurrentLevel, _controller.CurrentDpiScale);
        _notifyIcon.Visible = true;
    }

    /// <summary>Forwards the shell's latest snapshot: re-renders the icon for its
    /// <see cref="UsageLevel"/> and reformats <c>NotifyIcon.Text</c> from the full snapshot.
    /// Wired by the composition root to <c>UsagePollLoop.SnapshotUpdated</c>.</summary>
    public void OnSnapshotUpdated(UsageSnapshot snapshot)
    {
        _controller.OnSnapshotUpdated(snapshot.UsageLevel);
        _tooltip.OnSnapshotUpdated(snapshot);
    }

    /// <summary>Shows a toast balloon tied to this icon. Wired by the composition root as
    /// <see cref="AlertToastController"/>'s injected sink, called from
    /// <c>TrayShellToSkin.EventRaised</c>.</summary>
    public void ShowToast(string title, string body) =>
        _notifyIcon.ShowBalloonTip(ToastTimeoutMs, title, body, ToolTipIcon.Info);

    private void Render(UsageLevel level, double dpiScale)
    {
        var sizePx = StatusIconGlyphRenderer.PixelSizeForDpiScale(dpiScale);
        var pixels = StatusIconGlyphRenderer.BuildBgra32(level, sizePx);
        var newIcon = StatusIconFactory.CreateIcon(pixels, sizePx);

        var oldIcon = _currentIcon;
        _notifyIcon.Icon = newIcon;
        _currentIcon = newIcon;
        oldIcon?.Dispose();
    }

    private void Reregister()
    {
        // Toggling Visible forces NotifyIcon through a fresh Shell_NotifyIcon delete/add pair,
        // which is what makes the icon exist again after Explorer (and its notification area)
        // restarted.
        _notifyIcon.Visible = false;
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _messageSink.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null;
        _menuTheme.Dispose();
        _menu.Dispose();
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _disposed = true;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string lpString);

    /// <summary>
    /// A hidden, zero-size native window whose only job is receiving <c>WM_DPICHANGED</c> and
    /// the registered <c>TaskbarCreated</c> broadcast — <see cref="NotifyIcon"/> has no window
    /// of its own to intercept either on.
    /// </summary>
    private sealed class MessageSink : NativeWindow, IDisposable
    {
        private readonly TrayStatusIcon _owner;

        public MessageSink(TrayStatusIcon owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == _owner._taskbarCreatedMessage)
            {
                _owner._controller.OnHostRestarted();
            }
            else if (m.Msg == WM_DPICHANGED)
            {
                // WM_DPICHANGED's wParam packs the new X-axis DPI in the low word and the
                // Y-axis DPI in the high word; Windows always keeps them equal today, so only
                // the low word is read. 96 DPI is the 100% baseline StatusIconGlyphRenderer
                // scales from.
                var dpiX = (short)((long)m.WParam & 0xFFFF);
                _owner._controller.OnDpiChanged(dpiX / 96.0);
            }

            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
