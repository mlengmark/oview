namespace OView.Linux.Presentation;

/// <summary>
/// The decision ADR-0008's "First placement" / "Last position" split leaves to this skin
/// (slice 10, OVI-408), the Linux counterpart of <c>O-view.Tray</c>'s
/// <c>DetailWindowPositionController</c> (slice 6, OVI-386) — independently implemented, not
/// shared (D1): the first time the widget is ever shown, anchor it at slice 2's
/// <see cref="DetailWindowPlacement.Compute"/> corner; every later show, restore wherever the
/// user last dragged it. Takes both sources as injected delegates — the real work-area geometry
/// and the real preference file — so this ordering rule is provable against fakes with no
/// Avalonia window and no disk I/O (<see cref="OView.Linux.DetailWindowPreferenceStore"/> owns
/// the disk half; <see cref="OView.Linux.DetailWindow"/> owns the window half).
/// </summary>
public sealed class DetailWindowPositionController
{
    private readonly Func<(double X, double Y)?> _loadLastPosition;
    private readonly Func<DetailWindowPlacementResult> _computeFirstShowPlacement;
    private readonly Action<double, double> _savePosition;

    public DetailWindowPositionController(
        Func<(double X, double Y)?> loadLastPosition,
        Func<DetailWindowPlacementResult> computeFirstShowPlacement,
        Action<double, double> savePosition)
    {
        ArgumentNullException.ThrowIfNull(loadLastPosition);
        ArgumentNullException.ThrowIfNull(computeFirstShowPlacement);
        ArgumentNullException.ThrowIfNull(savePosition);

        _loadLastPosition = loadLastPosition;
        _computeFirstShowPlacement = computeFirstShowPlacement;
        _savePosition = savePosition;
    }

    /// <summary>
    /// Where the window should open this time: the last dragged position if one was ever
    /// saved, otherwise slice 2's first-run corner. Reads the preference file on every call
    /// rather than caching it, since a show can happen long after the controller was built.
    /// </summary>
    public (double X, double Y) ResolveShowPosition()
    {
        if (_loadLastPosition() is { } last)
        {
            return last;
        }

        var placement = _computeFirstShowPlacement();
        return (placement.X, placement.Y);
    }

    /// <summary>The user finished dragging the window; remember where it landed.</summary>
    public void OnDragEnd(double x, double y) => _savePosition(x, y);
}
