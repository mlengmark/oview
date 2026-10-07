using Avalonia;
using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux skin's Avalonia application. Slice 8 (OVI-397) left this with no <c>.axaml</c>,
/// no styles, no window, rendering nothing. Slice 9 (OVI-403) was the first to put anything on
/// screen: the constructor takes the already-built <see cref="LinuxStatusIcon"/> (composed in
/// <c>Program.cs</c> before Avalonia's lifetime starts) and attaches it here, in
/// <see cref="OnFrameworkInitializationCompleted"/> — the first point at which
/// <c>Application.Current</c> and the platform render interface are guaranteed to exist.
///
/// <para>Slice 10 (OVI-408) adds the detail window, built here rather than in <c>Program.cs</c>
/// for the same platform-timing reason: unlike <see cref="LinuxStatusIcon"/>'s <c>TrayIcon</c>,
/// an Avalonia <c>Window</c> needs the platform set up, and its <c>Screens</c> property (needed
/// for <see cref="DetailWindowPlacement.Compute"/>'s first-run corner) does not resolve to
/// anything real before <see cref="OnFrameworkInitializationCompleted"/> runs.</para>
/// </summary>
internal sealed class App : Application
{
    private readonly LinuxStatusIcon? _statusIcon;
    private readonly LinuxShellToSkin? _skin;
    private readonly ISkinToShell? _skinToShell;
    private readonly DetailWindowPreferenceStore? _preferenceStore;
    private readonly IThemeSource? _themeSource;
    private LinuxThemeRepaintController? _themeRepaint;

    public App()
    {
    }

    public App(
        LinuxStatusIcon statusIcon,
        LinuxShellToSkin skin,
        ISkinToShell skinToShell,
        DetailWindowPreferenceStore preferenceStore,
        IThemeSource? themeSource = null)
    {
        _statusIcon = statusIcon;
        _skin = skin;
        _skinToShell = skinToShell;
        _preferenceStore = preferenceStore;
        _themeSource = themeSource;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        _statusIcon?.AttachTo(this);

        if (_skin is null || _skinToShell is null || _preferenceStore is null)
        {
            return;
        }

        DetailWindow? windowRef = null;
        var position = new DetailWindowPositionController(
            _preferenceStore.Load,
            () => DetailWindowPlacement.Compute(
                WorkArea(windowRef).X,
                WorkArea(windowRef).Y,
                WorkArea(windowRef).Width,
                WorkArea(windowRef).Height,
                DetailWindow.DefaultWidth,
                DetailWindow.DefaultHeight),
            (x, y) => _preferenceStore.Save(x, y));

        var detailWindow = new DetailWindow(_skinToShell, position);
        windowRef = detailWindow;

        _skin.DetailShown += detailWindow.ShowDetail;
        _skin.VisibilityChanged += detailWindow.SetVisible;

        if (_themeSource is not null)
        {
            _themeRepaint = new LinuxThemeRepaintController(_themeSource, detailWindow.ApplyTheme);
        }
    }

    /// <summary>The primary screen's work area, or an empty rectangle before the window (and
    /// so the platform's screen service) exists yet — <see cref="DetailWindowPlacement.Compute"/>
    /// already treats a non-positive rectangle as "geometry unusable" and falls back to
    /// <c>Centered</c>, so this never needs its own fallback.</summary>
    private static PixelRect WorkArea(DetailWindow? window) =>
        window?.Screens?.Primary?.WorkingArea ?? default;
}
