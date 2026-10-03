using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>This skin's own detail-window placement rule (ADR-0008 D5); cross-skin agreement lives in the golden-master harness.</summary>
public class DetailWindowPlacementTests
{
    [Fact]
    public void AnchorsToTheBottomRightCornerInsetByTheMargin()
    {
        var result = DetailWindowPlacement.Compute(
            workLeft: 0, workTop: 0, workWidth: 1920, workHeight: 1080,
            windowWidth: 320, windowHeight: 400, marginPx: 8);

        Assert.Equal(ScreenCorner.BottomRight, result.Corner);
        Assert.False(result.IsFallback);
        Assert.Equal(8, result.MarginPx);
        Assert.Equal(1592, result.X);
        Assert.Equal(672, result.Y);
    }

    [Fact]
    public void AccountsForAWorkAreaWithANonZeroOrigin()
    {
        var result = DetailWindowPlacement.Compute(
            workLeft: -1920, workTop: 40, workWidth: 1920, workHeight: 1040,
            windowWidth: 300, windowHeight: 500, marginPx: 12);

        Assert.Equal(ScreenCorner.BottomRight, result.Corner);
        Assert.Equal(-312, result.X);
        Assert.Equal(568, result.Y);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-10, 1080)]
    public void FallsBackToCenteredWhenTheWorkAreaIsDegenerate(double workWidth, double workHeight)
    {
        var result = DetailWindowPlacement.Compute(
            workLeft: 100, workTop: 50, workWidth: workWidth, workHeight: workHeight,
            windowWidth: 300, windowHeight: 400);

        Assert.Equal(ScreenCorner.Centered, result.Corner);
        Assert.True(result.IsFallback);
        Assert.Equal(0, result.MarginPx);
        Assert.Equal(100, result.X);
        Assert.Equal(50, result.Y);
    }

    [Fact]
    public void FallsBackToCenteredWhenTheWindowSizeIsDegenerate()
    {
        var result = DetailWindowPlacement.Compute(
            workLeft: 0, workTop: 0, workWidth: 1920, workHeight: 1080,
            windowWidth: 0, windowHeight: 400);

        Assert.Equal(ScreenCorner.Centered, result.Corner);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public void UsesTheDefaultMarginWhenNoneIsGiven()
    {
        var result = DetailWindowPlacement.Compute(
            workLeft: 0, workTop: 0, workWidth: 1920, workHeight: 1080,
            windowWidth: 320, windowHeight: 400);

        Assert.Equal(DetailWindowPlacement.DefaultMarginPx, result.MarginPx);
    }
}
