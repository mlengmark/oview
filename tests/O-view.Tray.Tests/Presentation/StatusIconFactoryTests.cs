using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Unlike <see cref="StatusIconGlyphRendererTests"/>, this exercises the real GDI bitmap/icon
/// calls (ADR-0008 slice 4, OVI-371) — a Windows CI runner has a GDI subsystem even without an
/// interactive desktop, so this is real coverage, not a smoke test standing in for one.
/// </summary>
public class StatusIconFactoryTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void CreateIconProducesAnIconOfTheRequestedSize(int sizePx)
    {
        var pixels = StatusIconGlyphRenderer.BuildBgra32(UsageLevel.Green, sizePx);

        using var icon = StatusIconFactory.CreateIcon(pixels, sizePx);

        Assert.Equal(sizePx, icon.Width);
        Assert.Equal(sizePx, icon.Height);
    }

    [Fact]
    public void CreateIconRejectsAMismatchedBufferLength()
    {
        var pixels = StatusIconGlyphRenderer.BuildBgra32(UsageLevel.Red, 16);

        Assert.Throws<ArgumentException>(() => StatusIconFactory.CreateIcon(pixels, 32));
    }
}
