using OView.App;

namespace OView.Linux.Presentation;

/// <summary>A plain RGB triple — not any Avalonia <c>Color</c>/<c>IBrush</c> type — so this
/// mapping stays independent of the Avalonia adapter (<see cref="OView.Linux.DetailWindow"/>)
/// that turns it into one (ADR-0009 D6 point 2).</summary>
internal readonly record struct LinuxRgbColor(byte R, byte G, byte B);

/// <summary>The three colours an adapter needs to repaint itself on a theme change.</summary>
internal readonly record struct LinuxWindowThemeColors(LinuxRgbColor Background, LinuxRgbColor Foreground, LinuxRgbColor Border);

/// <summary>
/// This skin's own theme-to-colour mapping (ADR-0009 slice 10; D6 point 2): nothing here is
/// shared with <see cref="OView.App.IThemeSource"/>, Core, or <c>O-view.Tray</c>'s own
/// <c>WindowThemePalette</c> — the slicing table says this row's mapping must match nothing in
/// the Windows skin, and the values below were picked independently rather than copied.
///
/// <para><see cref="ThemePreference.Unknown"/> resolves to the same colours as
/// <see cref="ThemePreference.Light"/> — a defined fallback decided here, not a guess at what
/// the desktop portal actually prefers and not a crash (D6 point 3). Light is this skin's
/// pre-slice-10 appearance already (<see cref="OView.Linux.DetailWindow"/> painted a plain white
/// background with no theme awareness at all before this slice).</para>
/// </summary>
internal static class LinuxWindowThemePalette
{
    private static readonly LinuxWindowThemeColors LightColors = new(
        Background: new LinuxRgbColor(250, 250, 250),
        Foreground: new LinuxRgbColor(20, 20, 20),
        Border: new LinuxRgbColor(160, 160, 160));

    private static readonly LinuxWindowThemeColors DarkColors = new(
        Background: new LinuxRgbColor(45, 45, 48),
        Foreground: new LinuxRgbColor(225, 225, 225),
        Border: new LinuxRgbColor(80, 80, 85));

    public static LinuxWindowThemeColors Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => DarkColors,
        ThemePreference.Light => LightColors,
        ThemePreference.Unknown => LightColors,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, "Unrecognised theme preference.")
    };
}
