using OView.App;

namespace OView.Tray.Presentation;

/// <summary>A plain RGB triple — not <see cref="System.Windows.Media.Color"/> or
/// <see cref="System.Drawing.Color"/> — so this mapping stays independent of which UI toolkit an
/// adapter (<see cref="OView.Tray.DetailWindow"/>'s WPF, <see cref="TrayMenu"/>'s WinForms) turns
/// it into (ADR-0009 D6 point 2).</summary>
internal readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>The colours an adapter needs to repaint itself on a theme change. <see cref="Accent"/>
/// and <see cref="AccentHover"/> do not vary with <see cref="ThemePreference"/> (ADR-0008
/// slicing-table slice P3, OVI-622) — the source's own measurement (<c>ui-spec.md</c> §5) picked
/// one stepped-down fill that already clears a white label against both panels, so there is
/// nothing for light/dark to pick between.</summary>
internal readonly record struct WindowThemeColors(
    RgbColor Background, RgbColor Foreground, RgbColor Border, RgbColor Accent, RgbColor AccentHover);

/// <summary>The three usage-bar bands (ADR-0008 D10b, gate G7 parity slice P9), reusing this
/// skin's own existing tray-icon hues (<see cref="StatusIconGlyphRenderer.ColorFor"/>) for visual
/// continuity between the icon and the detail window — not required to match, but there is no
/// reason for the same meter to wear two different reds. Does not vary with
/// <see cref="ThemePreference"/>, same reasoning as <see cref="WindowThemeColors.Accent"/>.</summary>
internal readonly record struct UsageBarBandColors(RgbColor Green, RgbColor Amber, RgbColor Red);

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
    /// <summary>`#BE4E29` — the source's measured accent fill (<c>ui-spec.md</c> §5): 4.87:1
    /// against white label text, 4.63:1 against the light panel, 3.34:1 against the dark one.</summary>
    private static readonly RgbColor Accent = new(0xBE, 0x4E, 0x29);

    /// <summary>`#B84A27` — the source's measured hover step: 5.19:1 against white label text,
    /// 4.93:1 against the light panel, 3.14:1 against the dark one. The source stops here
    /// deliberately — the next step down lands on exactly 3.00:1 against the dark panel and
    /// reads as no longer a button there.</summary>
    private static readonly RgbColor AccentHover = new(0xB8, 0x4A, 0x27);

    private static readonly WindowThemeColors LightColors = new(
        Background: new RgbColor(255, 255, 255),
        Foreground: new RgbColor(0, 0, 0),
        Border: new RgbColor(128, 128, 128),
        Accent: Accent,
        AccentHover: AccentHover);

    private static readonly WindowThemeColors DarkColors = new(
        Background: new RgbColor(32, 32, 32),
        Foreground: new RgbColor(240, 240, 240),
        Border: new RgbColor(90, 90, 90),
        Accent: Accent,
        AccentHover: AccentHover);

    public static WindowThemeColors Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => DarkColors,
        ThemePreference.Light => LightColors,
        ThemePreference.Unknown => LightColors,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, "Unrecognised theme preference.")
    };

    /// <summary>`#32A046`/`#EBA50F`/`#D2322D` — this skin's own usage-bar band colours
    /// (ADR-0008 D10b, gate G7 parity slice P9), matched to <see cref="StatusIconGlyphRenderer"/>'s
    /// existing green/amber/red (its BGRA32 tuples converted to RGB) rather than picked afresh.</summary>
    public static readonly UsageBarBandColors BandColors = new(
        Green: new RgbColor(0x32, 0xA0, 0x46),
        Amber: new RgbColor(0xEB, 0xA5, 0x0F),
        Red: new RgbColor(0xD2, 0x32, 0x2D));

    public static RgbColor BandColor(UsageBarBand band) => band switch
    {
        UsageBarBand.Green => BandColors.Green,
        UsageBarBand.Amber => BandColors.Amber,
        UsageBarBand.Red => BandColors.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unrecognised usage bar band."),
    };

    /// <summary>A fixed, four-colour palette for the token-kind bars (gate G7 parity slice P12) —
    /// unrelated to <see cref="BandColors"/> (those warn; these just identify) and unrelated to
    /// slice P11's own per-model colour slots (those are assigned per model, these are fixed per
    /// kind). Does not vary with <see cref="ThemePreference"/>, same reasoning as
    /// <see cref="WindowThemeColors.Accent"/>.</summary>
    private static readonly RgbColor TokenKindInput = new(0x3E, 0x7C, 0xB1);
    private static readonly RgbColor TokenKindOutput = new(0x4F, 0xA6, 0x7A);
    private static readonly RgbColor TokenKindCacheCreation = new(0x9B, 0x6B, 0xC7);
    private static readonly RgbColor TokenKindCacheRead = new(0x9A, 0x9A, 0x9A);

    public static RgbColor TokenKindColor(TokenKind kind) => kind switch
    {
        TokenKind.Input => TokenKindInput,
        TokenKind.Output => TokenKindOutput,
        TokenKind.CacheCreation => TokenKindCacheCreation,
        TokenKind.CacheRead => TokenKindCacheRead,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognised token kind."),
    };
}
