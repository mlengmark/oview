using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// Proves ADR-0008 slice 10's (OVI-408) "First placement" / "Last position" rule, and D4's
/// 2026-10-09 amendment (slice P1, OVI-620) atop it, against fakes: no real work-area geometry,
/// no real preference file, no window, no real screen enumeration. The first show ever uses the
/// computed first-run placement; every show after a position has been saved restores that saved
/// position instead — unless the injected <c>isOnScreen</c> predicate rejects it, in which case
/// this falls back to the same first-run placement exactly as a never-saved position would.
/// </summary>
public sealed class DetailWindowPositionControllerTests
{
    private const double WindowWidth = 320;
    private const double WindowHeight = 360;

    private static readonly DetailWindowPlacementResult FirstShowPlacement =
        new(ScreenCorner.BottomRight, X: 900, Y: 500, MarginPx: 12, IsFallback: false);

    private static DetailWindowPositionController MakeController(
        Func<(double X, double Y)?> loadLastPosition,
        Func<DetailWindowPlacementResult>? computeFirstShowPlacement = null,
        Action<double, double>? savePosition = null,
        Func<ScreenRect, bool>? isOnScreen = null) =>
        new(
            loadLastPosition,
            computeFirstShowPlacement ?? (() => FirstShowPlacement),
            savePosition ?? ((_, _) => throw new InvalidOperationException("not expected")),
            isOnScreen ?? (_ => true),
            WindowWidth,
            WindowHeight);

    [Fact]
    public void ResolveShowPosition_uses_first_run_placement_when_nothing_was_ever_saved()
    {
        var controller = MakeController(loadLastPosition: () => null);

        var (x, y) = controller.ResolveShowPosition();

        Assert.Equal(FirstShowPlacement.X, x);
        Assert.Equal(FirstShowPlacement.Y, y);
    }

    [Fact]
    public void ResolveShowPosition_restores_the_saved_position_when_isOnScreen_accepts_it()
    {
        var controller = MakeController(
            loadLastPosition: () => (200, 150),
            computeFirstShowPlacement: () => throw new InvalidOperationException("must not recompute once a position is saved and accepted"),
            isOnScreen: _ => true);

        var (x, y) = controller.ResolveShowPosition();

        Assert.Equal(200, x);
        Assert.Equal(150, y);
    }

    [Fact]
    public void ResolveShowPosition_falls_back_to_first_run_placement_when_isOnScreen_rejects_the_saved_position()
    {
        var controller = MakeController(
            loadLastPosition: () => (200, 150),
            isOnScreen: _ => false);

        var (x, y) = controller.ResolveShowPosition();

        Assert.Equal(FirstShowPlacement.X, x);
        Assert.Equal(FirstShowPlacement.Y, y);
    }

    [Fact]
    public void ResolveShowPosition_checks_a_rectangle_built_from_the_saved_point_and_this_windows_size()
    {
        ScreenRect? checkedRect = null;
        var controller = MakeController(
            loadLastPosition: () => (200, 150),
            isOnScreen: candidate =>
            {
                checkedRect = candidate;
                return true;
            });

        controller.ResolveShowPosition();

        Assert.Equal(new ScreenRect(200, 150, WindowWidth, WindowHeight), checkedRect);
    }

    [Fact]
    public void OnDragEnd_saves_the_exact_position_given()
    {
        double? savedX = null;
        double? savedY = null;
        var controller = MakeController(
            loadLastPosition: () => null,
            savePosition: (x, y) => { savedX = x; savedY = y; });

        controller.OnDragEnd(42, 77);

        Assert.Equal(42, savedX);
        Assert.Equal(77, savedY);
    }

    [Fact]
    public void ResolveShowPosition_is_evaluated_fresh_on_every_call_rather_than_cached()
    {
        (double X, double Y)? saved = null;
        var controller = MakeController(loadLastPosition: () => saved);

        var beforeSave = controller.ResolveShowPosition();
        saved = (10, 20);
        var afterSave = controller.ResolveShowPosition();

        Assert.Equal((FirstShowPlacement.X, FirstShowPlacement.Y), beforeSave);
        Assert.Equal((10, 20), afterSave);
    }
}
