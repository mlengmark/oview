using OView.App;
using OView.CrossSkin.Tests.Fixtures;

namespace OView.CrossSkin.Tests;

/// <summary>
/// Proves ADR-0008 D11a's render-proof hook (OVI-598, slice P0) actually produces a file for
/// every state <see cref="DetailWindowFixtures.All"/> currently pins, in both
/// <see cref="ThemePreference.Light"/> and <see cref="ThemePreference.Dark"/>, for both skins.
/// This is the mechanism every later parity slice (P1-P25) reuses to prove the state it adds
/// rendered correctly in review — this slice only proves the mechanism itself works, not any
/// particular pixel (that is each later slice's own obligation, against the renders it attaches
/// to its own PR).
/// </summary>
public sealed class DetailWindowRenderProofTests : IDisposable
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 8, 21, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "oview-render-proof-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    public static IEnumerable<object[]> FixturesByTheme()
    {
        foreach (var fixture in DetailWindowFixtures.All)
        {
            yield return new object[] { fixture, ThemePreference.Light };
            yield return new object[] { fixture, ThemePreference.Dark };
        }
    }

    [Theory]
    [MemberData(nameof(FixturesByTheme))]
    public void TrayRenderProofWritesAFileForEveryStateInBothThemes(DetailWindowFixture fixture, ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-{fixture.Name}-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, UtcNow, fixture.DisplayZone);

        Assert.True(File.Exists(path), $"[{fixture.Name}/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[{fixture.Name}/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(FixturesByTheme))]
    public void LinuxRenderProofWritesAFileForEveryStateInBothThemes(DetailWindowFixture fixture, ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-{fixture.Name}-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, UtcNow, fixture.DisplayZone);

        Assert.True(File.Exists(path), $"[{fixture.Name}/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[{fixture.Name}/{theme}] wrote an empty file.");
    }

    public static IEnumerable<object[]> Themes()
    {
        yield return new object[] { ThemePreference.Light };
        yield return new object[] { ThemePreference.Dark };
    }

    /// <summary>
    /// ADR-0008 slicing-table slice P2 (OVI-621): the render-proof hook must capture a hovered
    /// state in both themes, for both skins — the hover card is the one surface a plain content
    /// render can never show (a <see cref="System.Windows.Controls.ToolTip"/>/Avalonia
    /// <c>ToolTip</c> cannot be given a parent to screenshot it inside).
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void TrayRenderProofCapturesTheHoveredStateInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-hover-cards-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderHoverCardsToFile(theme, path);

        Assert.True(File.Exists(path), $"[hover/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[hover/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void LinuxRenderProofCapturesTheHoveredStateInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-hover-cards-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderHoverCardsToFile(theme, path);

        Assert.True(File.Exists(path), $"[hover/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[hover/{theme}] wrote an empty file.");
    }

    /// <summary>
    /// ADR-0008 D11a's render-proof obligation, applied to gate G7 parity slice P12: the
    /// token-kind breakdown table's view-switch state renders in both themes, for both skins,
    /// using <see cref="DetailWindowFixtures.TokenKindTotalsPopulated"/> so the breakdown rows
    /// are non-empty.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void TrayRenderProofCapturesTheTokenKindBreakdownTableInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-token-kind-breakdown-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.TokenKindTotalsPopulated.Detail, theme, path, UtcNow,
            DetailWindowFixtures.TokenKindTotalsPopulated.DisplayZone, showTokenKindBreakdown: true);

        Assert.True(File.Exists(path), $"[token-kind-breakdown/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[token-kind-breakdown/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void LinuxRenderProofCapturesTheTokenKindBreakdownTableInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-token-kind-breakdown-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.TokenKindTotalsPopulated.Detail, theme, path, UtcNow,
            DetailWindowFixtures.TokenKindTotalsPopulated.DisplayZone, showTokenKindBreakdown: true);

        Assert.True(File.Exists(path), $"[token-kind-breakdown/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[token-kind-breakdown/{theme}] wrote an empty file.");
    }

    /// <summary>
    /// ADR-0008 D11a's render-proof obligation, applied to gate G7 parity slice P10: the
    /// statistics tiles' flipped state — a 31-day tile's per-model stacked bar — renders in both
    /// themes, for both skins, reusing <c>UnpricedModelInWindow</c>'s own populated breakdown
    /// (two rows, one of them unpriced) so the flipped render also exercises the "unpriced model
    /// excluded from the value tile's breakdown" case.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void TrayRenderProofCapturesTheFlippedStatisticsTilesInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-statistics-tiles-flipped-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.UnpricedModelInWindow.Detail, theme, path, UtcNow,
            DetailWindowFixtures.UnpricedModelInWindow.DisplayZone, flipWindowTiles: true);

        Assert.True(File.Exists(path), $"[flipped/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[flipped/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void LinuxRenderProofCapturesTheFlippedStatisticsTilesInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-statistics-tiles-flipped-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.UnpricedModelInWindow.Detail, theme, path, UtcNow,
            DetailWindowFixtures.UnpricedModelInWindow.DisplayZone, flipWindowTiles: true);

        Assert.True(File.Exists(path), $"[flipped/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[flipped/{theme}] wrote an empty file.");
    }

    /// <summary>
    /// The disabled-tile state: <c>RecordedWindowNoActivity</c>'s breakdown is real but empty,
    /// so both 31-day tiles have nothing to flip to and <c>flipWindowTiles</c> has no effect —
    /// the render proof shows the same no-glyph tiles whichever way it is asked to render them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void TrayRenderProofCapturesDisabledStatisticsTilesInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-statistics-tiles-disabled-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.RecordedWindowNoActivity.Detail, theme, path, UtcNow,
            DetailWindowFixtures.RecordedWindowNoActivity.DisplayZone, flipWindowTiles: true);

        Assert.True(File.Exists(path), $"[disabled/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[disabled/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void LinuxRenderProofCapturesDisabledStatisticsTilesInBothThemes(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-statistics-tiles-disabled-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            DetailWindowFixtures.RecordedWindowNoActivity.Detail, theme, path, UtcNow,
            DetailWindowFixtures.RecordedWindowNoActivity.DisplayZone, flipWindowTiles: true);

        Assert.True(File.Exists(path), $"[disabled/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[disabled/{theme}] wrote an empty file.");
    }

    /// <summary>
    /// OVI-688: proves OVI-686's height fix actually works, not just that a file exists —
    /// decodes the PNG's own IHDR height for <see cref="DetailWindowFixtures.ManyModelRowsOverflowNominalHeight"/>,
    /// whose 40 model rows provably push the rendered content past the window's nominal
    /// <see cref="OView.Tray.Presentation.DetailWindowRenderProof.Height"/> (360px), and asserts
    /// the PNG actually grew taller than that. Before OVI-686, the root <c>Border</c> carried an
    /// explicit <c>Height</c> that always won <c>Measure</c> over content, so this would have
    /// stayed pinned at exactly 360px regardless of how many rows the fixture added.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void TrayRenderProofGrowsTallerThanTheNominalHeightForOverflowingContent(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-many-model-rows-{theme}.png");
        var fixture = DetailWindowFixtures.ManyModelRowsOverflowNominalHeight;

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, UtcNow, fixture.DisplayZone);

        var height = PngHeight(path);
        Assert.True(
            height > (int)OView.Tray.Presentation.DetailWindowRenderProof.Height,
            $"[many-model-rows/{theme}] rendered at {height}px, not taller than the nominal " +
            $"{OView.Tray.Presentation.DetailWindowRenderProof.Height}px — the overflow fix regressed.");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void LinuxRenderProofGrowsTallerThanTheNominalHeightForOverflowingContent(ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-many-model-rows-{theme}.png");
        var fixture = DetailWindowFixtures.ManyModelRowsOverflowNominalHeight;

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, UtcNow, fixture.DisplayZone);

        var height = PngHeight(path);
        Assert.True(
            height > (int)OView.Linux.Presentation.DetailWindowRenderProof.Height,
            $"[many-model-rows/{theme}] rendered at {height}px, not taller than the nominal " +
            $"{OView.Linux.Presentation.DetailWindowRenderProof.Height}px — the overflow fix regressed.");
    }

    /// <summary>
    /// Reads the pixel height straight out of a PNG's own IHDR chunk (always the first chunk,
    /// always 13 bytes, per the PNG spec) rather than trusting anything the encoder claims —
    /// the same reason this is worth asserting on at all: <c>File.Exists</c> cannot tell a
    /// correctly sized render from one silently clipped to the window's nominal height.
    /// </summary>
    private static int PngHeight(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
    }

    public static IEnumerable<object[]> OffPlanBannerFixturesByTheme()
    {
        foreach (var fixture in OffPlanBannerFixtures.All)
        {
            yield return new object[] { fixture, ThemePreference.Light };
            yield return new object[] { fixture, ThemePreference.Dark };
        }
    }

    /// <summary>
    /// ADR-0008 D11a's render-proof obligation, applied to gate G7 parity slice P15 (OVI-691):
    /// the off-plan live banner's no-banner state and its three extra-usage wordings each render
    /// in both themes, for both skins.
    /// </summary>
    [Theory]
    [MemberData(nameof(OffPlanBannerFixturesByTheme))]
    public void TrayRenderProofCapturesTheOffPlanBannerStatesInBothThemes(OffPlanBannerFixture fixture, ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"tray-off-plan-banner-{fixture.Name}-{theme}.png");

        OView.Tray.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);

        Assert.True(File.Exists(path), $"[off-plan-banner/{fixture.Name}/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[off-plan-banner/{fixture.Name}/{theme}] wrote an empty file.");
    }

    [Theory]
    [MemberData(nameof(OffPlanBannerFixturesByTheme))]
    public void LinuxRenderProofCapturesTheOffPlanBannerStatesInBothThemes(OffPlanBannerFixture fixture, ThemePreference theme)
    {
        var path = Path.Combine(_directory, $"linux-off-plan-banner-{fixture.Name}-{theme}.png");

        OView.Linux.Presentation.DetailWindowRenderProof.RenderToFile(
            fixture.Detail, theme, path, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);

        Assert.True(File.Exists(path), $"[off-plan-banner/{fixture.Name}/{theme}] wrote no file.");
        Assert.True(new FileInfo(path).Length > 0, $"[off-plan-banner/{fixture.Name}/{theme}] wrote an empty file.");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
