using OView.App;

namespace OView.Linux.Presentation;

/// <summary>A plain RGB triple — not any Avalonia <c>Color</c>/<c>IBrush</c> type — so this
/// mapping stays independent of the Avalonia adapter (<see cref="OView.Linux.DetailWindow"/>)
/// that turns it into one (ADR-0009 D6 point 2).</summary>
internal readonly record struct LinuxRgbColor(byte R, byte G, byte B);

/// <summary>The colours an adapter needs to repaint itself on a theme change. <see cref="Accent"/>
/// and <see cref="AccentHover"/> do not vary with <see cref="ThemePreference"/> (ADR-0008
/// slicing-table slice P3, OVI-622) — the source's own measurement (<c>ui-spec.md</c> §5) picked
/// one stepped-down fill that already clears a white label against both panels, so there is
/// nothing for light/dark to pick between.</summary>
internal readonly record struct LinuxWindowThemeColors(
    LinuxRgbColor Background, LinuxRgbColor Foreground, LinuxRgbColor Border, LinuxRgbColor Accent, LinuxRgbColor AccentHover);

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
    /// <summary>`#BE4E29` — the source's measured accent fill (<c>ui-spec.md</c> §5): 4.87:1
    /// against white label text, 4.63:1 against the light panel, 3.34:1 against the dark one.</summary>
    private static readonly LinuxRgbColor Accent = new(0xBE, 0x4E, 0x29);

    /// <summary>`#B84A27` — the source's measured hover step: 5.19:1 against white label text,
    /// 4.93:1 against the light panel, 3.14:1 against the dark one. The source stops here
    /// deliberately — the next step down lands on exactly 3.00:1 against the dark panel and
    /// reads as no longer a button there.</summary>
    private static readonly LinuxRgbColor AccentHover = new(0xB8, 0x4A, 0x27);

    private static readonly LinuxWindowThemeColors LightColors = new(
        Background: new LinuxRgbColor(250, 250, 250),
        Foreground: new LinuxRgbColor(20, 20, 20),
        Border: new LinuxRgbColor(160, 160, 160),
        Accent: Accent,
        AccentHover: AccentHover);

    private static readonly LinuxWindowThemeColors DarkColors = new(
        Background: new LinuxRgbColor(45, 45, 48),
        Foreground: new LinuxRgbColor(225, 225, 225),
        Border: new LinuxRgbColor(80, 80, 85),
        Accent: Accent,
        AccentHover: AccentHover);

    public static LinuxWindowThemeColors Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => DarkColors,
        ThemePreference.Light => LightColors,
        ThemePreference.Unknown => LightColors,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, "Unrecognised theme preference.")
    };
}
