using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Gate G7 parity slice P21 (OVI-738): proves the branded menu flyout's own open/close gesture
/// toggles rather than always opening, and that it reuses the detail widget's 400 ms
/// close-then-click grace window verbatim (<see cref="DetailPushCoordinator.IconClickAwayGrace"/>) —
/// the same 399/401 ms boundary <c>DetailPushCoordinatorTests</c> already pins for that widget.
/// </summary>
public sealed class MenuFlyoutToggleControllerTests
{
    [Fact]
    public void ReusesTheDetailWidgetsOwnGraceWindowRatherThanASecondConstant()
    {
        Assert.Equal(DetailPushCoordinator.IconClickAwayGrace, MenuFlyoutToggleController.GraceWindow);
    }

    [Fact]
    public void FirstActivationOpensAClosedFlyout()
    {
        var visible = new List<bool>();
        var controller = new MenuFlyoutToggleController(visible.Add, new FakeClock(DateTimeOffset.UnixEpoch));

        controller.OnActivated();

        Assert.True(controller.IsVisible);
        Assert.Equal(new[] { true }, visible);
    }

    [Fact]
    public void ActivatingAnOpenFlyoutClosesIt()
    {
        var visible = new List<bool>();
        var controller = new MenuFlyoutToggleController(visible.Add, new FakeClock(DateTimeOffset.UnixEpoch));
        controller.OnActivated();

        controller.OnActivated();

        Assert.False(controller.IsVisible);
        Assert.Equal(new[] { true, false }, visible);
    }

    [Fact]
    public void AnActivationArriving399msAfterAFocusLossHideIsAbsorbedAndTheFlyoutStaysClosed()
    {
        var visible = new List<bool>();
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var controller = new MenuFlyoutToggleController(visible.Add, clock);
        controller.OnHidden();

        clock.UtcNow = clock.UtcNow.AddMilliseconds(399);
        controller.OnActivated();

        Assert.False(controller.IsVisible);
        Assert.Empty(visible);
    }

    [Fact]
    public void AnActivationArriving401msAfterAFocusLossHideReopensTheFlyout()
    {
        var visible = new List<bool>();
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var controller = new MenuFlyoutToggleController(visible.Add, clock);
        controller.OnHidden();

        clock.UtcNow = clock.UtcNow.AddMilliseconds(401);
        controller.OnActivated();

        Assert.True(controller.IsVisible);
        Assert.Equal(new[] { true }, visible);
    }

    [Fact]
    public void AnOpenFlyoutThatLosesFocusNeverGetsStuckOpen()
    {
        var visible = new List<bool>();
        var controller = new MenuFlyoutToggleController(visible.Add, new FakeClock(DateTimeOffset.UnixEpoch));
        controller.OnActivated();

        controller.OnHidden();

        Assert.False(controller.IsVisible);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }
}
