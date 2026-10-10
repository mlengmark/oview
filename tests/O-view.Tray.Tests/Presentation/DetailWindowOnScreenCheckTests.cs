using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves ADR-0008 D4's 2026-10-09 amendment (slice P1, OVI-620): a candidate rectangle is on
/// screen only when some work area contains it completely. Partial intersection is pinned
/// explicitly, since it is the one case the amendment calls out by name as a failure rather than
/// a nudge back into view.
/// </summary>
public sealed class DetailWindowOnScreenCheckTests
{
    private static readonly ScreenRect PrimaryWorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void Fully_contained_rectangle_is_on_screen()
    {
        var candidate = new ScreenRect(100, 100, 320, 360);

        Assert.True(DetailWindowOnScreenCheck.IsFullyOnScreen(candidate, new[] { PrimaryWorkArea }));
    }

    [Fact]
    public void Rectangle_entirely_outside_every_work_area_is_not_on_screen()
    {
        var candidate = new ScreenRect(5000, 5000, 320, 360);

        Assert.False(DetailWindowOnScreenCheck.IsFullyOnScreen(candidate, new[] { PrimaryWorkArea }));
    }

    [Fact]
    public void Partially_intersecting_rectangle_is_treated_as_failure_not_nudged_into_view()
    {
        // Straddles the right edge of the only work area: half on screen, half off.
        var candidate = new ScreenRect(1800, 100, 320, 360);

        Assert.False(DetailWindowOnScreenCheck.IsFullyOnScreen(candidate, new[] { PrimaryWorkArea }));
    }

    [Fact]
    public void Rectangle_fully_contained_in_a_secondary_monitor_is_on_screen()
    {
        var secondaryWorkArea = new ScreenRect(1920, 0, 1280, 1024);
        var candidate = new ScreenRect(2000, 50, 320, 360);

        Assert.True(DetailWindowOnScreenCheck.IsFullyOnScreen(candidate, new[] { PrimaryWorkArea, secondaryWorkArea }));
    }

    [Fact]
    public void No_work_areas_at_all_means_nothing_is_on_screen()
    {
        var candidate = new ScreenRect(0, 0, 320, 360);

        Assert.False(DetailWindowOnScreenCheck.IsFullyOnScreen(candidate, Array.Empty<ScreenRect>()));
    }
}
