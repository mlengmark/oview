using OView.App;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0009 slice 7 (OVI-489)'s repaint decision: on construction, and again every time
/// <see cref="IThemeSource.Changed"/> fires, resolve the preference through
/// <see cref="WindowThemePalette.Resolve"/> — including <see cref="ThemePreference.Unknown"/> —
/// and hand the result to the adapter's own <c>apply</c> callback. This is the testable half of
/// "detail window and menu repaint live"; <see cref="OView.Tray.DetailWindow"/> and
/// <see cref="TrayMenu"/> only supply <c>apply</c> and are not unit-tested themselves, the same
/// "decision logic tested against fakes, adapter not" split every other control in this skin
/// already uses (no interactive Windows desktop in this environment).
/// </summary>
internal sealed class ThemeRepaintController : IDisposable
{
    private readonly IThemeSource _themeSource;
    private readonly Action<WindowThemeColors> _apply;

    public ThemeRepaintController(IThemeSource themeSource, Action<WindowThemeColors> apply)
    {
        ArgumentNullException.ThrowIfNull(themeSource);
        ArgumentNullException.ThrowIfNull(apply);

        _themeSource = themeSource;
        _apply = apply;

        _themeSource.Changed += OnThemeChanged;
        Repaint(_themeSource.Current);
    }

    private void OnThemeChanged(object? sender, ThemePreference preference) => Repaint(preference);

    private void Repaint(ThemePreference preference) => _apply(WindowThemePalette.Resolve(preference));

    public void Dispose() => _themeSource.Changed -= OnThemeChanged;
}
