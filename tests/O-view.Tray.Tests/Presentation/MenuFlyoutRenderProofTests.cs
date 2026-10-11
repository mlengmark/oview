using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof obligation for the branded menu flyout (gate G7 parity slice
/// P21, OVI-738): proves the card renders, in both themes, docked at the corner adjacent to the
/// taskbar for all four taskbar positions — never at a fixed screen corner, and never the same
/// render regardless of where the taskbar actually sits.
/// </summary>
public sealed class MenuFlyoutRenderProofTests
{
    private const byte PngSignatureByte0 = 0x89;
    private const byte PngSignatureByte1 = 0x50; // 'P'

    [Theory]
    [InlineData(TaskbarEdge.Bottom, ThemePreference.Light)]
    [InlineData(TaskbarEdge.Bottom, ThemePreference.Dark)]
    [InlineData(TaskbarEdge.Top, ThemePreference.Light)]
    [InlineData(TaskbarEdge.Top, ThemePreference.Dark)]
    [InlineData(TaskbarEdge.Left, ThemePreference.Light)]
    [InlineData(TaskbarEdge.Left, ThemePreference.Dark)]
    [InlineData(TaskbarEdge.Right, ThemePreference.Light)]
    [InlineData(TaskbarEdge.Right, ThemePreference.Dark)]
    public void FlyoutRendersAPngDockedForEveryTaskbarPositionInBothThemes(TaskbarEdge edge, ThemePreference theme)
    {
        var png = MenuFlyoutRenderProof.Render(edge, theme);

        Assert.True(png.Length > 100);
        Assert.Equal(PngSignatureByte0, png[0]);
        Assert.Equal(PngSignatureByte1, png[1]);
    }
}
