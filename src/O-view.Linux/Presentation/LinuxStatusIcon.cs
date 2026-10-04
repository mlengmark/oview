using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using OView.App;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// The real Avalonia half of ADR-0008 slice 9's status icon (OVI-403): an
/// <see cref="TrayIcon"/> rendered from the pushed <see cref="UsageLevel"/>, added to the
/// notification area only once <see cref="OView.Linux.Platform.NotificationHostMonitor"/> has
/// observed a host present. All level/host-presence/activation decisions live in
/// <see cref="StatusIconController"/>, which is unit-tested against fakes; this type only owns
/// the real <see cref="TrayIcon"/> and the <see cref="WindowIcon"/>s
/// <see cref="StatusIconFactory"/> builds for it. Not unit-tested (no interactive Linux desktop
/// or display in this environment — the same "not verified" boundary ADR-0008 slices 3/4/8
/// already recorded for their own OS adapters); proved only by this slice's controller/renderer
/// tests and a manual non-crashing run.
///
/// <para><see cref="NotificationHostMonitor"/>'s events fire off a background thread (its own
/// doc: the D-Bus round trip must never touch a UI thread). <see cref="OnHostObserved"/>,
/// <see cref="OnHostAppeared"/> and <see cref="OnSnapshotUpdated"/> all marshal onto
/// <see cref="Dispatcher.UIThread"/> before touching <see cref="_trayIcon"/>, because
/// <see cref="OView.App.AppTimer"/>'s own doc places that responsibility on the caller, not the
/// timer, and <see cref="TrayIcon"/> is an Avalonia UI object.</para>
///
/// <para><see cref="TooltipTextController"/> (ADR-0008 slice 11, OVI-417) pushes
/// <c>TrayIcon.ToolTipText</c> from the same snapshot <see cref="OnSnapshotUpdated"/> already
/// forwards to <see cref="StatusIconController"/> — both run inside the same UI-thread
/// dispatch, so the icon and its tooltip never disagree about which snapshot they rendered.
/// </para>
/// </summary>
internal sealed class LinuxStatusIcon : IDisposable
{
    private readonly StatusIconController _controller;
    private readonly TooltipTextController _tooltip;
    private readonly TrayIcon _trayIcon;
    private bool _disposed;

    public LinuxStatusIcon(ISkinToShell skinToShell)
    {
        _controller = new StatusIconController(skinToShell, Render, RegisterIcon);
        _trayIcon = new TrayIcon { IsVisible = false };
        _tooltip = new TooltipTextController(text => _trayIcon.ToolTipText = text);
        _trayIcon.Clicked += (_, _) => _controller.OnActivated();
    }

    /// <summary>Forwards the shell's latest snapshot onto the UI thread.</summary>
    public void OnSnapshotUpdated(UsageSnapshot snapshot) =>
        Dispatcher.UIThread.Post(() =>
        {
            _controller.OnSnapshotUpdated(snapshot.UsageLevel);
            _tooltip.OnSnapshotUpdated(snapshot);
        });

    /// <summary>Forwards <see cref="OView.Linux.Platform.NotificationHostMonitor.ProbeCompleted"/>
    /// onto the UI thread.</summary>
    public void OnHostObserved(bool present) =>
        Dispatcher.UIThread.Post(() => _controller.OnHostObserved(present));

    /// <summary>Forwards <see cref="OView.Linux.Platform.NotificationHostMonitor.HostAppeared"/>
    /// onto the UI thread.</summary>
    public void OnHostAppeared() =>
        Dispatcher.UIThread.Post(_controller.OnHostAppeared);

    /// <summary>
    /// Attaches this icon to <paramref name="application"/>'s tray-icon list. Must run after
    /// the Avalonia platform is initialized (<c>Application.OnFrameworkInitializationCompleted</c>)
    /// — the same reason <c>App</c> is the one place this is called from.
    /// </summary>
    public void AttachTo(Application application) =>
        TrayIcon.SetIcons(application, new TrayIcons { _trayIcon });

    private void Render(UsageLevel level)
    {
        var pixels = StatusIconGlyphRenderer.BuildBgra32(level, StatusIconGlyphRenderer.IconSizePx);
        _trayIcon.Icon = StatusIconFactory.CreateIcon(pixels, StatusIconGlyphRenderer.IconSizePx);
        _trayIcon.IsVisible = true;
    }

    /// <summary>No separate "add to host" call exists in Avalonia's <see cref="TrayIcon"/> API
    /// beyond <see cref="AttachTo"/> (already done once at startup) and setting
    /// <see cref="TrayIcon.IsVisible"/>, which <see cref="Render"/> already does — this is a
    /// no-op kept so <see cref="StatusIconController"/>'s "register, then render" shape stays
    /// explicit and mirrors <c>O-view.Tray</c>'s own reregister hook.</summary>
    private static void RegisterIcon()
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _trayIcon.Dispose();
        _disposed = true;
    }
}
