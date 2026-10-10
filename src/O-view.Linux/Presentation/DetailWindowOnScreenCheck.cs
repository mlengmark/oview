namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 D4's 2026-10-09 amendment, slice P1: the pure geometry half of
/// <see cref="DetailWindowPositionController"/>'s <c>isOnScreen</c> predicate, pulled out so the
/// containment rule itself is provable against injected rectangles rather than real monitors —
/// same reason <see cref="DetailWindowPlacement.Compute"/> is a pure function of injected
/// geometry rather than reading <c>Screens</c> itself. Duplicated in <c>O-view.Tray</c> rather
/// than shared (D1/D5); <c>O-view.CrossSkin.Tests</c> holds the two to the same content facts.
/// </summary>
public static class DetailWindowOnScreenCheck
{
    /// <summary>
    /// A candidate rectangle is on screen only if some work area in <paramref name="workAreas"/>
    /// contains it <i>completely</i>. The amendment is explicit that partial intersection is a
    /// failure, not a case to nudge back into view — a window straddling two monitors, or
    /// overlapping one at the edge, counts exactly the same as a window not found at all. No
    /// work areas at all — a Wayland compositor that will not enumerate screens, same case D4's
    /// existing "centre" answer already names — is the same answer: nothing can contain it.
    /// </summary>
    public static bool IsFullyOnScreen(ScreenRect candidate, IReadOnlyList<ScreenRect> workAreas)
    {
        foreach (var workArea in workAreas)
        {
            if (candidate.X >= workArea.X &&
                candidate.Y >= workArea.Y &&
                candidate.X + candidate.Width <= workArea.X + workArea.Width &&
                candidate.Y + candidate.Height <= workArea.Y + workArea.Height)
            {
                return true;
            }
        }

        return false;
    }
}
