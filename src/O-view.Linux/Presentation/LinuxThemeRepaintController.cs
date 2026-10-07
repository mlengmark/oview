using OView.App;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0009 slice 10's repaint decision: on construction, and again every time
/// <see cref="IThemeSource.Changed"/> fires, resolve the preference through
/// <see cref="LinuxWindowThemePalette.Resolve"/> — including <see cref="ThemePreference.Unknown"/> —
/// and hand the result to the adapter's own <c>apply</c> callback. Written independently of
/// <c>O-view.Tray.Presentation.ThemeRepaintController</c> (ADR-0009 D6 point 2 — this skin owns
/// its own mapping and structure, not a port), even though both answer the same "decision
/// logic tested against a fake, adapter not" split every other control in either skin already
/// uses. <see cref="OView.Linux.DetailWindow"/> supplies <c>apply</c> and is not unit-tested
/// itself (no interactive Linux display reachable in this environment).
///
/// <para>The menu (<see cref="LinuxTrayMenu"/>, slice 9) is an Avalonia <c>NativeMenu</c>
/// rendered by the host desktop's own native menu widget, not a toolkit control this skin paints
/// — it sets no colour anywhere today, unlike the Windows skin's WinForms
/// <c>ContextMenuStrip</c> (which slice 7 does recolour). It already follows whatever theme the
/// desktop environment applies to its native menus, so it has no themeable part for this slice
/// to wire.</para>
/// </summary>
internal sealed class LinuxThemeRepaintController : IDisposable
{
    private readonly IThemeSource _themeSource;
    private readonly Action<LinuxWindowThemeColors> _apply;

    public LinuxThemeRepaintController(IThemeSource themeSource, Action<LinuxWindowThemeColors> apply)
    {
        ArgumentNullException.ThrowIfNull(themeSource);
        ArgumentNullException.ThrowIfNull(apply);

        _themeSource = themeSource;
        _apply = apply;

        _themeSource.Changed += OnThemeChanged;
        Repaint(_themeSource.Current);
    }

    private void OnThemeChanged(object? sender, ThemePreference preference) => Repaint(preference);

    private void Repaint(ThemePreference preference) => _apply(LinuxWindowThemePalette.Resolve(preference));

    public void Dispose() => _themeSource.Changed -= OnThemeChanged;
}
