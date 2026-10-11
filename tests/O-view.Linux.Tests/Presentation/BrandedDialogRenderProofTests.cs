using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook for gate G7 parity slice P24 (OVI-718), the Linux
/// counterpart of <c>O-view.Tray</c>'s own <c>BrandedDialogRenderProofTests</c> —
/// independently implemented, not shared (D1): proves both branded dialogs render, in both
/// themes, without ever calling <c>ShowDialog</c> — the same offscreen mechanism this skin's own
/// <c>DetailWindowRenderProof</c> already established for slice P0. "The primary button names
/// the action" (source <c>ui-spec.md</c> §5) is asserted directly against the label text, not
/// only against a rendered PNG's existence.
/// </summary>
public sealed class BrandedDialogRenderProofTests
{
    private const byte PngSignatureByte0 = 0x89;
    private const byte PngSignatureByte1 = 0x50; // 'P'

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void ConfirmDialogRendersAPngInBothThemes(ThemePreference theme)
    {
        var png = BrandedDialogRenderProof.RenderConfirmDialog(
            "Update available", "A new version of O-view is ready to install.", "Update now", "Not now", "", theme);

        Assert.True(png.Length > 100);
        Assert.Equal(PngSignatureByte0, png[0]);
        Assert.Equal(PngSignatureByte1, png[1]);
    }

    [Fact]
    public void ConfirmDialogWithADetailLineStillRenders()
    {
        var png = BrandedDialogRenderProof.RenderConfirmDialog(
            "Open page", "Opens Claude's usage settings in your browser.", "Open page", "Cancel",
            "claude.ai/settings/usage", ThemePreference.Light);

        Assert.True(png.Length > 100);
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void WeeklyResetDialogRendersAPngInBothThemes(ThemePreference theme)
    {
        var png = BrandedDialogRenderProof.RenderWeeklyResetDialog(current: null, theme);

        Assert.True(png.Length > 100);
        Assert.Equal(PngSignatureByte0, png[0]);
        Assert.Equal(PngSignatureByte1, png[1]);
    }

    [Fact]
    public void WeeklyResetDialogWithAnExistingEntryAndAnErrorStillRenders()
    {
        var png = BrandedDialogRenderProof.RenderWeeklyResetDialog(
            new WeeklyResetEntry(DayOfWeek.Tuesday, "09:00"), ThemePreference.Dark,
            error: "'09:00 AM' is not a 24-hour time. Enter it as HH:mm — 22:59, not 10:59 PM.");

        Assert.True(png.Length > 100);
    }

    [Theory]
    [InlineData("Update now")]
    [InlineData("Save")]
    [InlineData("Open page")]
    public void PrimaryActionLabelBuilderNamesTheActionRatherThanAGenericAnswer(string actionLabel)
    {
        // Gate G7 parity slice P24's own requirement: "the primary button names the action"
        // instead of a re-worded Yes/OK. Routed through the same AvaloniaWorkerThread every other
        // render-proof call uses — building the secondary button's Cursor still needs Avalonia's
        // platform registered, see BrandedDialogRenderProof's own remarks.
        var label = BrandedDialogRenderProof.ResolveConfirmLabelText(actionLabel, "Cancel", ThemePreference.Light);

        Assert.Equal(actionLabel, label);
        Assert.NotEqual("OK", label);
        Assert.NotEqual("Yes", label);
    }
}
