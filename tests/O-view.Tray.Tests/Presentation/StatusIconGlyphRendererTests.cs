using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0008 slice 4 (OVI-371)'s pure pixel logic: level-to-colour and DPI-to-size, pinned without any GDI handle.</summary>
public class StatusIconGlyphRendererTests
{
    [Theory]
    [InlineData(1.0, 16)]
    [InlineData(1.5, 24)]
    [InlineData(2.0, 32)]
    [InlineData(2.5, 40)]
    public void PixelSizeScalesWithDpi(double dpiScale, int expectedSizePx)
    {
        Assert.Equal(expectedSizePx, StatusIconGlyphRenderer.PixelSizeForDpiScale(dpiScale));
    }

    [Fact]
    public void PixelSizeNeverGoesBelowTheLogicalSize()
    {
        Assert.Equal(StatusIconGlyphRenderer.LogicalSizePx, StatusIconGlyphRenderer.PixelSizeForDpiScale(0.5));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void PixelSizeRejectsNonPositiveScale(double dpiScale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusIconGlyphRenderer.PixelSizeForDpiScale(dpiScale));
    }

    [Fact]
    public void EveryUsageLevelHasADistinctColor()
    {
        var green = StatusIconGlyphRenderer.ColorFor(UsageLevel.Green);
        var amber = StatusIconGlyphRenderer.ColorFor(UsageLevel.Amber);
        var red = StatusIconGlyphRenderer.ColorFor(UsageLevel.Red);

        Assert.NotEqual(green, amber);
        Assert.NotEqual(amber, red);
        Assert.NotEqual(green, red);
    }

    [Fact]
    public void ColorForRejectsAnUndefinedLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusIconGlyphRenderer.ColorFor((UsageLevel)99));
    }

    [Theory]
    [InlineData(UsageLevel.Green)]
    [InlineData(UsageLevel.Amber)]
    [InlineData(UsageLevel.Red)]
    public void BuildBgra32ProducesTheRightBufferLength(UsageLevel level)
    {
        var pixels = StatusIconGlyphRenderer.BuildBgra32(level, 16);

        Assert.Equal(16 * 16 * 4, pixels.Length);
    }

    [Fact]
    public void CenterPixelIsFullyOpaqueInTheLevelsColor()
    {
        const int size = 16;
        var pixels = StatusIconGlyphRenderer.BuildBgra32(UsageLevel.Amber, size);
        var (b, g, r, a) = StatusIconGlyphRenderer.ColorFor(UsageLevel.Amber);

        var centerIndex = (((size / 2) * size) + (size / 2)) * 4;

        Assert.Equal(b, pixels[centerIndex + 0]);
        Assert.Equal(g, pixels[centerIndex + 1]);
        Assert.Equal(r, pixels[centerIndex + 2]);
        Assert.Equal(a, pixels[centerIndex + 3]);
    }

    [Fact]
    public void CornerPixelIsFullyTransparent()
    {
        const int size = 16;
        var pixels = StatusIconGlyphRenderer.BuildBgra32(UsageLevel.Red, size);

        Assert.Equal(0, pixels[3]);        // top-left pixel's alpha byte
        Assert.Equal(0, pixels[^1]);        // bottom-right pixel's alpha byte
    }

    [Fact]
    public void BuildBgra32RejectsNonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusIconGlyphRenderer.BuildBgra32(UsageLevel.Green, 0));
    }
}
