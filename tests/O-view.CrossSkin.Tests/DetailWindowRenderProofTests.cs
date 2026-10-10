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

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
