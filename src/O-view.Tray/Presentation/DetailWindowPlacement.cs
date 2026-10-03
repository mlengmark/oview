namespace OView.Tray.Presentation;

/// <summary>
/// Which corner of the work area a freshly-opened detail window anchors to, before the user has
/// ever dragged it (ADR-0008 D5). <see cref="Centered"/> is the fallback used when the injected
/// geometry cannot support a corner placement at all.
/// </summary>
public enum ScreenCorner
{
    BottomRight,
    Centered,
}

/// <summary>
/// The placement this skin computed for a given screen rectangle: which corner, the margin
/// actually used, and the top-left point the window should open at (ADR-0008 D5).
/// </summary>
public readonly record struct DetailWindowPlacementResult(
    ScreenCorner Corner,
    double X,
    double Y,
    double MarginPx,
    bool IsFallback);

/// <summary>
/// This skin's own first-placement rule for the detail window (ADR-0008 D5): a pure function of
/// injected screen geometry, computed independently of <c>O-view.Linux</c>'s copy by design — the
/// ADR rejects a shared presentation type for this decision, and
/// <c>O-view.CrossSkin.Tests</c> holds the two copies to the same content facts instead (which
/// corner, what margin, what fallback) via a golden-master test rather than shared code.
///
/// <para>Last position (after the user has dragged it) is a skin preference file, not this
/// rule's concern — see ADR-0008's "First placement" / "Last position" split. This type computes
/// first placement only, and nothing here renders a window (that is slices 6/10).</para>
/// </summary>
public static class DetailWindowPlacement
{
    /// <summary>The gap this skin leaves between the window and the work-area edge.</summary>
    public const double DefaultMarginPx = 12;

    /// <summary>
    /// Anchors the window to the work area's bottom-right corner, <paramref name="marginPx"/>
    /// in from each edge. Falls back to <see cref="ScreenCorner.Centered"/> on the work area's
    /// own origin, with zero margin, when the injected work area or window size is degenerate
    /// (non-positive width or height) — there is no usable rectangle to anchor a corner within,
    /// and a window the caller cannot see land off-screen is worse than one pinned to a known
    /// point.
    /// </summary>
    public static DetailWindowPlacementResult Compute(
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight,
        double windowWidth,
        double windowHeight,
        double marginPx = DefaultMarginPx)
    {
        if (workWidth <= 0 || workHeight <= 0 || windowWidth <= 0 || windowHeight <= 0)
        {
            return new DetailWindowPlacementResult(ScreenCorner.Centered, workLeft, workTop, 0, true);
        }

        var x = workLeft + workWidth - windowWidth - marginPx;
        var y = workTop + workHeight - windowHeight - marginPx;

        return new DetailWindowPlacementResult(ScreenCorner.BottomRight, x, y, marginPx, false);
    }
}
