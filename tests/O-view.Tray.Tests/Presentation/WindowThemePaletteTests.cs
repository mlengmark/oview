using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0009 slice 7 (OVI-489)'s pure theme-to-colour mapping.</summary>
public class WindowThemePaletteTests
{
    [Fact]
    public void LightAndDarkResolveToDifferentColours()
    {
        Assert.NotEqual(WindowThemePalette.Resolve(ThemePreference.Light), WindowThemePalette.Resolve(ThemePreference.Dark));
    }

    [Fact]
    public void UnknownResolvesToTheLightPaletteAsItsDefinedFallbackRatherThanGuessingOrThrowing()
    {
        // D6 point 3: an absent OS preference is reported as Unknown, never guessed at. This
        // skin still has to paint something, so it falls back to the same colours it used
        // before this slice existed (Light) — a decision, not an accident.
        var unknown = WindowThemePalette.Resolve(ThemePreference.Unknown);

        Assert.Equal(WindowThemePalette.Resolve(ThemePreference.Light), unknown);
    }
}
