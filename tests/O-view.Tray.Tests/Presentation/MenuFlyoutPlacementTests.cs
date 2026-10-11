using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Gate G7 parity slice P21 (OVI-738): proves <see cref="MenuFlyoutPlacement.Compute"/> docks to
/// the work-area corner adjacent to the taskbar for all four taskbar positions, never at a fixed
/// screen corner regardless of where the taskbar actually sits, and never off the work area it
/// was given.
/// </summary>
public sealed class MenuFlyoutPlacementTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);
    private const int CardWidth = 272;
    private const int CardHeight = 180;

    [Fact]
    public void BottomTaskbarDocksBottomRight()
    {
        var work = new PixelRect(0, 0, 1920, 1040);

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(MenuFlyoutCorner.BottomRight, result.Corner);
        Assert.Equal(work.Right - MenuFlyoutPlacement.DefaultMarginPx - CardWidth, result.Left);
        Assert.Equal(work.Bottom - MenuFlyoutPlacement.DefaultMarginPx - CardHeight, result.Top);
    }

    [Fact]
    public void TopTaskbarDocksTopRight()
    {
        var work = new PixelRect(0, 40, 1920, 1040);

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(MenuFlyoutCorner.TopRight, result.Corner);
        Assert.Equal(work.Right - MenuFlyoutPlacement.DefaultMarginPx - CardWidth, result.Left);
        Assert.Equal(work.Y + MenuFlyoutPlacement.DefaultMarginPx, result.Top);
    }

    [Fact]
    public void LeftTaskbarDocksBottomLeft()
    {
        var work = new PixelRect(48, 0, 1872, 1080);

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(MenuFlyoutCorner.BottomLeft, result.Corner);
        Assert.Equal(work.X + MenuFlyoutPlacement.DefaultMarginPx, result.Left);
        Assert.Equal(work.Bottom - MenuFlyoutPlacement.DefaultMarginPx - CardHeight, result.Top);
    }

    [Fact]
    public void RightTaskbarDocksBottomRight()
    {
        // The notification area sits at the bottom of a right-hand taskbar too (same reasoning
        // the source project's own PopupPositioner recorded), so this is deliberately the same
        // corner the bottom-taskbar case picks, not a fourth distinct corner.
        var work = new PixelRect(0, 0, 1872, 1080);

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(MenuFlyoutCorner.BottomRight, result.Corner);
        Assert.Equal(work.Right - MenuFlyoutPlacement.DefaultMarginPx - CardWidth, result.Left);
    }

    [Fact]
    public void AutoHiddenTaskbarLeavesNoInsetAndStillDocksBottomRight()
    {
        var work = Monitor;

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(MenuFlyoutCorner.BottomRight, result.Corner);
    }

    [Fact]
    public void NeverPlacedOutsideTheWorkAreaEvenWhenTheCardIsLargerThanAvailableSpace()
    {
        var work = new PixelRect(0, 0, 200, 150);

        var result = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.True(result.Left >= work.X);
        Assert.True(result.Top >= work.Y);
    }

    [Fact]
    public void CursorPositionNeverInfluencesTheDecision()
    {
        // MenuFlyoutPlacement.Compute takes no cursor/pointer parameter at all — the monitor
        // selection happens entirely in MenuFlyoutMonitorLocator, before this pure function ever
        // runs. Asserted here by construction: the only inputs below are the two rectangles and
        // the card's own size.
        var work = new PixelRect(0, 0, 1920, 1040);

        var first = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);
        var second = MenuFlyoutPlacement.Compute(Monitor, work, CardWidth, CardHeight);

        Assert.Equal(first, second);
    }
}
