using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0009 slice 10's pure theme-to-colour mapping — the Linux skin's own, matching
/// nothing in <c>O-view.Tray.Presentation.WindowThemePalette</c> (D6 point 2).</summary>
public class LinuxWindowThemePaletteTests
{
    [Fact]
    public void LightAndDarkResolveToDifferentColours()
    {
        Assert.NotEqual(LinuxWindowThemePalette.Resolve(ThemePreference.Light), LinuxWindowThemePalette.Resolve(ThemePreference.Dark));
    }

    [Fact]
    public void UnknownResolvesToTheLightPaletteAsItsDefinedFallbackRatherThanGuessingOrThrowing()
    {
        // D6 point 3: an absent OS preference is reported as Unknown, never guessed at. This
        // skin still has to paint something, so it falls back to the same colours it used
        // before this slice existed (Light) — a decision, not an accident.
        var unknown = LinuxWindowThemePalette.Resolve(ThemePreference.Unknown);

        Assert.Equal(LinuxWindowThemePalette.Resolve(ThemePreference.Light), unknown);
    }
}
