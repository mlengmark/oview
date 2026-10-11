using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook for gate G7 parity slice P24 (OVI-718): proves both branded
/// dialogs render, in both themes, without ever calling <see cref="System.Windows.Window.ShowDialog"/> —
/// the same offscreen mechanism <c>DetailWindowRenderProofTests</c> already exercises for slice
/// P0. "The primary button names the action" (source <c>ui-spec.md</c> §5) is a design constraint
/// the render proof covers directly: the label text is asserted, not a generic "OK"/"Yes".
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
    public void PrimaryActionNamesTheActionRatherThanAGenericAnswer(string actionLabel)
    {
        // Gate G7 parity slice P24's own requirement: "the primary button names the action"
        // instead of a re-worded Yes/OK. Constructing the window requires an STA thread (every
        // WPF DependencyObject does); this never calls ShowDialog, so no real HWND is involved.
        string? renderedLabel = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                var colors = WindowThemePalette.Resolve(ThemePreference.Light);
                var dialog = new BrandedDialogWindow(colors, actionLabel, "Cancel");
                renderedLabel = dialog.ConfirmLabelText;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        Assert.Equal(actionLabel, renderedLabel);
        Assert.NotEqual("OK", renderedLabel);
        Assert.NotEqual("Yes", renderedLabel);
    }
}
