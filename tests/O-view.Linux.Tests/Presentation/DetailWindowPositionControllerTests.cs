using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// Proves ADR-0008 slice 10's (OVI-408) "First placement" / "Last position" rule against
/// fakes: no real work-area geometry, no real preference file, no window. The first show ever
/// uses the computed first-run placement; every show after a position has been saved restores
/// that saved position instead, even when the computed placement would differ.
/// </summary>
public sealed class DetailWindowPositionControllerTests
{
    private static readonly DetailWindowPlacementResult FirstShowPlacement =
        new(ScreenCorner.BottomRight, X: 900, Y: 500, MarginPx: 12, IsFallback: false);

    [Fact]
    public void ResolveShowPosition_uses_first_run_placement_when_nothing_was_ever_saved()
    {
        var controller = new DetailWindowPositionController(
            loadLastPosition: () => null,
            computeFirstShowPlacement: () => FirstShowPlacement,
            savePosition: (_, _) => throw new InvalidOperationException("not expected"));

        var (x, y) = controller.ResolveShowPosition();

        Assert.Equal(FirstShowPlacement.X, x);
        Assert.Equal(FirstShowPlacement.Y, y);
    }

    [Fact]
    public void ResolveShowPosition_restores_the_saved_position_instead_of_recomputing_first_run_placement()
    {
        var controller = new DetailWindowPositionController(
            loadLastPosition: () => (200, 150),
            computeFirstShowPlacement: () => throw new InvalidOperationException("must not recompute once a position is saved"),
            savePosition: (_, _) => throw new InvalidOperationException("not expected"));

        var (x, y) = controller.ResolveShowPosition();

        Assert.Equal(200, x);
        Assert.Equal(150, y);
    }

    [Fact]
    public void OnDragEnd_saves_the_exact_position_given()
    {
        double? savedX = null;
        double? savedY = null;
        var controller = new DetailWindowPositionController(
            loadLastPosition: () => null,
            computeFirstShowPlacement: () => FirstShowPlacement,
            savePosition: (x, y) => { savedX = x; savedY = y; });

        controller.OnDragEnd(42, 77);

        Assert.Equal(42, savedX);
        Assert.Equal(77, savedY);
    }

    [Fact]
    public void ResolveShowPosition_is_evaluated_fresh_on_every_call_rather_than_cached()
    {
        (double X, double Y)? saved = null;
        var controller = new DetailWindowPositionController(
            loadLastPosition: () => saved,
            computeFirstShowPlacement: () => FirstShowPlacement,
            savePosition: (_, _) => throw new InvalidOperationException("not expected"));

        var beforeSave = controller.ResolveShowPosition();
        saved = (10, 20);
        var afterSave = controller.ResolveShowPosition();

        Assert.Equal((FirstShowPlacement.X, FirstShowPlacement.Y), beforeSave);
        Assert.Equal((10, 20), afterSave);
    }
}
