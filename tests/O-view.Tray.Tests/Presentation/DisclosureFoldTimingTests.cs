using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Pins the "Why so large?" disclosure fold's timing and curve (gate G7 parity slice P19,
/// OVI-717): 191 ms open / 125 ms close, both on <c>KeySpline(0.02,0.16 0.20,0.96)</c> — the
/// task brief's own numbers, carried verbatim into <see cref="DisclosureFoldTiming"/>.
/// </summary>
public sealed class DisclosureFoldTimingTests
{
    [Fact]
    public void Expand_duration_is_191ms() =>
        Assert.Equal(191, DisclosureFoldTiming.ExpandDurationMs);

    [Fact]
    public void Collapse_duration_is_125ms() =>
        Assert.Equal(125, DisclosureFoldTiming.CollapseDurationMs);

    [Fact]
    public void Collapse_is_quicker_than_expand() =>
        Assert.True(DisclosureFoldTiming.CollapseDurationMs < DisclosureFoldTiming.ExpandDurationMs);

    [Fact]
    public void Decelerate_curve_matches_the_task_brief() =>
        Assert.Equal((0.02, 0.16, 0.20, 0.96), DisclosureFoldTiming.Decelerate);

    [Fact]
    public void Accelerate_curve_is_the_decelerate_curves_mirror()
    {
        var (x1, y1, x2, y2) = DisclosureFoldTiming.Decelerate;
        var (ax1, ay1, ax2, ay2) = DisclosureFoldTiming.Accelerate;

        Assert.Equal(1 - x2, ax1, precision: 9);
        Assert.Equal(1 - y2, ay1, precision: 9);
        Assert.Equal(1 - x1, ax2, precision: 9);
        Assert.Equal(1 - y1, ay2, precision: 9);
    }

    [Fact]
    public void Expanded_chevron_angle_is_180_degrees() =>
        Assert.Equal(180, DisclosureFoldTiming.ExpandedChevronAngleDegrees);
}
