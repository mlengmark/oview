using OView.App;

namespace OView.Tray.Presentation;

/// <summary>A plain RGB triple — not <see cref="System.Windows.Media.Color"/> or
/// <see cref="System.Drawing.Color"/> — so this mapping stays independent of which UI toolkit an
/// adapter (<see cref="OView.Tray.DetailWindow"/>'s WPF, <see cref="TrayMenu"/>'s WinForms) turns
/// it into (ADR-0009 D6 point 2).</summary>
internal readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>The three colours an adapter needs to repaint itself on a theme change.</summary>
internal readonly record struct WindowThemeColors(RgbColor Background, RgbColor Foreground, RgbColor Border);

/// <summary>
/// This skin's own theme-to-colour mapping (ADR-0009 slice 7, OVI-489; D6 point 2): nothing here
/// is shared with <see cref="OView.App.IThemeSource"/>, Core, or the Linux skin — the Linux
/// skin's own mapping (slicing table row 10) is free to disagree with every value below.
///
/// <para><see cref="ThemePreference.Unknown"/> resolves to the same colours as
/// <see cref="ThemePreference.Light"/> — a defined fallback decided here, not a guess at what
/// the OS actually prefers and not a crash. D6 point 3 only requires the seam to report an
/// absent preference rather than invent one; it leaves each skin free to pick a default
/// appearance when that happens, and light is this skin's pre-slice-7 appearance already
/// (<see cref="OView.Tray.DetailWindow"/> painted white-on-black-text before this slice, with no
/// theme awareness at all).</para>
/// </summary>
internal static class WindowThemePalette
{
    private static readonly WindowThemeColors LightColors = new(
        Background: new RgbColor(255, 255, 255),
        Foreground: new RgbColor(0, 0, 0),
        Border: new RgbColor(128, 128, 128));

    private static readonly WindowThemeColors DarkColors = new(
        Background: new RgbColor(32, 32, 32),
        Foreground: new RgbColor(240, 240, 240),
        Border: new RgbColor(90, 90, 90));

    public static WindowThemeColors Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => DarkColors,
        ThemePreference.Light => LightColors,
        ThemePreference.Unknown => LightColors,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, "Unrecognised theme preference.")
    };
}
