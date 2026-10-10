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

    /// <summary>ADR-0008 slicing-table slice P3 (OVI-622): the accent fill a white dialog-button
    /// label sits on must clear WCAG's 4.5:1 text-contrast floor, per the source's own measurement
    /// (<c>ui-spec.md</c> §5). Asserted here rather than only read off the doc, so a future colour
    /// change that drifts below the floor fails a test instead of only a design review.</summary>
    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void AccentAndAccentHoverClearTheWhiteLabelContrastFloorInBothThemes(ThemePreference theme)
    {
        var colors = WindowThemePalette.Resolve(theme);
        var white = new RgbColor(255, 255, 255);

        Assert.True(
            ContrastRatio(white, colors.Accent) >= 4.5,
            $"Accent contrast against white was {ContrastRatio(white, colors.Accent):F2}:1, below the 4.5:1 floor.");
        Assert.True(
            ContrastRatio(white, colors.AccentHover) >= 4.5,
            $"AccentHover contrast against white was {ContrastRatio(white, colors.AccentHover):F2}:1, below the 4.5:1 floor.");
    }

    /// <summary>WCAG 2.1 relative-luminance contrast ratio (same formula §5's table was measured
    /// with). Duplicated per skin's own test project rather than shared (D1) — this is test code,
    /// not a Core-equivalent colour type.</summary>
    private static double ContrastRatio(RgbColor a, RgbColor b)
    {
        var lighter = Math.Max(RelativeLuminance(a), RelativeLuminance(b));
        var darker = Math.Min(RelativeLuminance(a), RelativeLuminance(b));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(RgbColor c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
