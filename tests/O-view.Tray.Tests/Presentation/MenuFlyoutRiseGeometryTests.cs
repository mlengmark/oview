using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>Gate G7 parity slice P22 (OVI-743): a top-docked taskbar's corner reveals downward;
/// every other corner keeps the ordinary upward reveal.</summary>
public sealed class MenuFlyoutRiseGeometryTests
{
    [Fact]
    public void TopRight_reveals_downward() =>
        Assert.Equal(MenuFlyoutRiseDirection.Downward, MenuFlyoutRiseGeometry.ForCorner(MenuFlyoutCorner.TopRight));

    [Fact]
    public void BottomLeft_reveals_upward() =>
        Assert.Equal(MenuFlyoutRiseDirection.Upward, MenuFlyoutRiseGeometry.ForCorner(MenuFlyoutCorner.BottomLeft));

    [Fact]
    public void BottomRight_reveals_upward() =>
        Assert.Equal(MenuFlyoutRiseDirection.Upward, MenuFlyoutRiseGeometry.ForCorner(MenuFlyoutCorner.BottomRight));
}
