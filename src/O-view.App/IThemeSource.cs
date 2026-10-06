namespace OView.App;

/// <summary>
/// The OS's light/dark preference, as a fact, never a colour (ADR-0009 D6 point 2). Core and
/// this shell hold no colour today and must not start holding one here — each skin maps this
/// onto its own resources, and the two skins' palettes are not required to match (D6 point 2).
/// </summary>
public enum ThemePreference
{
    Light,
    Dark,

    /// <summary>
    /// The OS stated no preference O-view can read — an absent registry value, an absent
    /// desktop portal. Reported rather than guessed (D6 point 3): a skin may render a default
    /// appearance, but the seam never claims a preference nobody stated.
    /// </summary>
    Unknown
}

/// <summary>
/// The shell declares this capability; each skin implements it for its own OS, selected at
/// compile time by which skin is built, never by a runtime OS check (ADR-0009 D6 point 1) —
/// the same "shell declares, skin implements" shape as <see cref="IStartupRegistration"/> and
/// <see cref="ISingleInstanceGuard"/> (ADR-0007 D5).
/// </summary>
public interface IThemeSource
{
    /// <summary>
    /// The preference as of right now — re-read on every access rather than cached, matching
    /// the source app's own re-read-on-open behaviour (D6 point 4).
    /// </summary>
    ThemePreference Current { get; }

    /// <summary>
    /// Raised when the OS reports a live preference change (Windows' <c>WM_SETTINGCHANGE</c>,
    /// Linux's desktop-portal <c>SettingChanged</c> signal), carrying the freshly re-read
    /// value. No consumer is wired to this yet — nothing repaints from this slice (ADR-0009
    /// slicing table row 5); that is rows 6/7.
    /// </summary>
    event EventHandler<ThemePreference>? Changed;
}
