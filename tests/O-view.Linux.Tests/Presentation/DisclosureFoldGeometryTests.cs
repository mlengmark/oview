using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// Proves ADR-0008 D10's grow-upward rule for the "Why so large?" disclosure fold (gate G7
/// parity slice P19, OVI-717): downward growth is preferred, upward is the fallback exactly
/// when downward would push the window's bottom edge past the work area, and the fold's frame
/// at any height in between is a pure function of that height alone — never a stateful
/// re-docking step that could disagree with itself frame to frame (the "no jump, no overshoot"
/// requirement, stated operationally).
/// </summary>
public sealed class DisclosureFoldGeometryTests
{
    [Fact]
    public void Decides_downward_when_the_expanded_window_still_fits()
    {
        var direction = DisclosureFoldGeometry.DecideDirection(
            collapsedTop: 500, collapsedHeight: 360, expandedHeight: 420, workAreaBottom: 1040);

        Assert.Equal(DisclosureFoldDirection.Downward, direction);
    }

    [Fact]
    public void Decides_upward_when_downward_growth_would_clip_the_work_area_bottom()
    {
        // Collapsed bottom (860) is already close to the work area's own bottom (900); growing
        // downward by 60px would land at 920, past it.
        var direction = DisclosureFoldGeometry.DecideDirection(
            collapsedTop: 500, collapsedHeight: 360, expandedHeight: 420, workAreaBottom: 900);

        Assert.Equal(DisclosureFoldDirection.Upward, direction);
    }

    [Fact]
    public void A_window_whose_expanded_bottom_lands_exactly_on_the_work_area_edge_still_fits()
    {
        // Exactly flush with the work area's bottom edge is not "past" it.
        var direction = DisclosureFoldGeometry.DecideDirection(
            collapsedTop: 500, collapsedHeight: 360, expandedHeight: 420, workAreaBottom: 920);

        Assert.Equal(DisclosureFoldDirection.Downward, direction);
    }

    [Fact]
    public void Downward_growth_keeps_the_top_fixed_and_grows_the_bottom()
    {
        var frame = DisclosureFoldGeometry.FrameForHeight(
            DisclosureFoldDirection.Downward, collapsedTop: 500, collapsedHeight: 360, heightNow: 400);

        Assert.Equal(500, frame.Top);
        Assert.Equal(400, frame.Height);
    }

    [Fact]
    public void Upward_growth_keeps_the_bottom_fixed_and_moves_the_top_up()
    {
        // Collapsed bottom is 500 + 360 = 860; at height 400 the top must be 860 - 400 = 460.
        var frame = DisclosureFoldGeometry.FrameForHeight(
            DisclosureFoldDirection.Upward, collapsedTop: 500, collapsedHeight: 360, heightNow: 400);

        Assert.Equal(460, frame.Top);
        Assert.Equal(400, frame.Height);
    }

    [Fact]
    public void Upward_growth_never_overshoots_the_collapsed_bottom_at_any_intermediate_height()
    {
        const double collapsedTop = 500;
        const double collapsedHeight = 360;
        var collapsedBottom = collapsedTop + collapsedHeight;

        foreach (var heightNow in new[] { 360, 370, 390, 400, 420 })
        {
            var frame = DisclosureFoldGeometry.FrameForHeight(
                DisclosureFoldDirection.Upward, collapsedTop, collapsedHeight, heightNow);

            // The bottom edge (Top + Height) must equal the collapsed bottom at every frame —
            // any deviation is exactly the "jump" the task exists to prevent.
            Assert.Equal(collapsedBottom, frame.Top + frame.Height, precision: 9);
        }
    }

    [Fact]
    public void Frame_for_height_reproduces_the_collapsed_frame_at_the_collapsed_height()
    {
        var downward = DisclosureFoldGeometry.FrameForHeight(
            DisclosureFoldDirection.Downward, collapsedTop: 500, collapsedHeight: 360, heightNow: 360);
        var upward = DisclosureFoldGeometry.FrameForHeight(
            DisclosureFoldDirection.Upward, collapsedTop: 500, collapsedHeight: 360, heightNow: 360);

        Assert.Equal(new DisclosureFoldFrame(500, 360), downward);
        Assert.Equal(new DisclosureFoldFrame(500, 360), upward);
    }
}
