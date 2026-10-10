namespace OView.Tray.Presentation;

/// <summary>
/// The screen rectangle a candidate detail-window position would occupy (ADR-0008 D4's
/// 2026-10-09 amendment): the point under test plus this window's fixed size. Duplicated in
/// <c>O-view.Linux</c> rather than shared (D1/D5) — this skin's own
/// <see cref="DetailWindowPositionController"/> is the only thing that constructs one, so it
/// stays provable against fakes with no window and no real screen enumeration.
/// </summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

/// <summary>
/// The decision ADR-0008's "First placement" / "Last position" split leaves to this skin
/// (slice 6, OVI-386): the first time the widget is ever shown, anchor it at slice 2's
/// <see cref="DetailWindowPlacement.Compute"/> corner; every later show, restore wherever the
/// user last dragged it. Takes both sources as injected delegates — the real work-area geometry
/// and the real preference file — so this ordering rule is provable against fakes with no WPF
/// window and no disk I/O (<see cref="OView.Tray.DetailWindowPreferenceStore"/> owns the disk
/// half; <see cref="OView.Tray.DetailWindow"/> owns the window half).
///
/// <para>ADR-0008 D4's 2026-10-09 amendment (OVI-585/591, gate G7, slice P1): a saved position is
/// a <i>request</i>, re-validated on every show against the screens as they are now, not an
/// instruction. <paramref name="isOnScreen"/> (constructor parameter, below) decides that — kept
/// as an injected predicate rather than real screen enumeration for the same reason the other two
/// delegates are injected, and because a monitor arrangement is exactly the kind of state this
/// ordering rule must stay provable without. A saved position the predicate rejects — not found
/// at all, or only partially covered, which the predicate must treat as rejection too, never as a
/// nudge back into view — falls back to the same first-run placement a never-saved position
/// already used.</para>
/// </summary>
public sealed class DetailWindowPositionController
{
    private readonly Func<(double X, double Y)?> _loadLastPosition;
    private readonly Func<DetailWindowPlacementResult> _computeFirstShowPlacement;
    private readonly Action<double, double> _savePosition;
    private readonly Func<ScreenRect, bool> _isOnScreen;
    private readonly double _windowWidth;
    private readonly double _windowHeight;

    public DetailWindowPositionController(
        Func<(double X, double Y)?> loadLastPosition,
        Func<DetailWindowPlacementResult> computeFirstShowPlacement,
        Action<double, double> savePosition,
        Func<ScreenRect, bool> isOnScreen,
        double windowWidth,
        double windowHeight)
    {
        ArgumentNullException.ThrowIfNull(loadLastPosition);
        ArgumentNullException.ThrowIfNull(computeFirstShowPlacement);
        ArgumentNullException.ThrowIfNull(savePosition);
        ArgumentNullException.ThrowIfNull(isOnScreen);

        _loadLastPosition = loadLastPosition;
        _computeFirstShowPlacement = computeFirstShowPlacement;
        _savePosition = savePosition;
        _isOnScreen = isOnScreen;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
    }

    /// <summary>
    /// Where the window should open this time: the last dragged position, if one was ever saved
    /// and <paramref name="isOnScreen"/>'s injected predicate still accepts it against the
    /// screens as they are now; otherwise slice 2's first-run corner. Reads the preference file
    /// on every call rather than caching it, since a show can happen long after the controller
    /// was built — and the screen check runs on every call for exactly the same reason: a monitor
    /// unplugged, a resolution or scaling change, or a resized VM between two shows is the one
    /// case this rule exists for.
    /// </summary>
    public (double X, double Y) ResolveShowPosition()
    {
        if (_loadLastPosition() is { } last &&
            _isOnScreen(new ScreenRect(last.X, last.Y, _windowWidth, _windowHeight)))
        {
            return last;
        }

        var placement = _computeFirstShowPlacement();
        return (placement.X, placement.Y);
    }

    /// <summary>The user finished dragging the window; remember where it landed.</summary>
    public void OnDragEnd(double x, double y) => _savePosition(x, y);
}
