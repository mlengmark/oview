using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>Pins the branded menu flyout's rise-animation timing and curve (gate G7 parity slice
/// P22, OVI-743): 230 ms open / 150 ms close, both on <c>KeySpline(0.02,0.16 0.20,0.96)</c> — the
/// task brief's own numbers, the same curve <see cref="DisclosureFoldTiming.Decelerate"/> uses
/// for its own open (P19, OVI-717).</summary>
public sealed class MenuFlyoutRiseTimingTests
{
    [Fact]
    public void Open_duration_is_230ms() =>
        Assert.Equal(230, MenuFlyoutRiseTiming.OpenDurationMs);

    [Fact]
    public void Close_duration_is_150ms() =>
        Assert.Equal(150, MenuFlyoutRiseTiming.CloseDurationMs);

    [Fact]
    public void Close_is_quicker_than_open() =>
        Assert.True(MenuFlyoutRiseTiming.CloseDurationMs < MenuFlyoutRiseTiming.OpenDurationMs);

    [Fact]
    public void Curve_matches_the_task_brief() =>
        Assert.Equal((0.02, 0.16, 0.20, 0.96), MenuFlyoutRiseTiming.Curve);

    [Fact]
    public void Curve_is_the_same_curve_the_disclosure_fold_uses_for_its_own_open() =>
        Assert.Equal(DisclosureFoldTiming.Decelerate, MenuFlyoutRiseTiming.Curve);

    [Fact]
    public void Slide_distance_is_20_dip() =>
        Assert.Equal(20, MenuFlyoutRiseTiming.SlideDistanceDip);
}
