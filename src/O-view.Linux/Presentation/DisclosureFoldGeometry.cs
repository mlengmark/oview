namespace OView.Linux.Presentation;

/// <summary>Which edge of the window the fold is anchored to while it runs — decided once per
/// open, per <see cref="DisclosureFoldGeometry.DecideDirection"/>'s remarks.</summary>
public enum DisclosureFoldDirection
{
    Downward,
    Upward,
}

/// <summary>The window's top and height for one instant of the fold.</summary>
public readonly record struct DisclosureFoldFrame(double Top, double Height);

/// <summary>
/// The grow-upward geometry rule for the "Why so large?" disclosure fold (ADR-0008 D10, gate G7
/// parity slice P19, OVI-717): pure functions of injected numbers, so the no-jump,
/// no-overshoot, and grow-upward-when-clipped rules are provable with no window and no real
/// screen — same reason <see cref="DetailWindowOnScreenCheck"/> and
/// <see cref="DetailWindowPlacement"/> are pure. Duplicated in <c>O-view.Tray</c> rather than
/// shared (D1/D5); <c>O-view.CrossSkin.Tests</c> holds the two to the same decisions.
///
/// <para>On this skin, repositioning/resizing a window is only ever a <i>request</i> — an X11
/// window manager may ignore it, Wayland refuses it outright (see <c>DetailWindow</c>'s own
/// remarks on <c>Position</c>). This type still computes the one correct request; whether the
/// compositor grants it is outside anything C# here can observe.</para>
/// </summary>
public static class DisclosureFoldGeometry
{
    /// <summary>
    /// Growing downward is preferred. Only when it would push the window's bottom edge past
    /// <paramref name="workAreaBottom"/> does the fold grow upward instead, keeping the
    /// window's bottom edge fixed — the content the user was already looking at does not move,
    /// only what is above it does.
    ///
    /// <para>Decided once, from the collapsed window's current top and the height it will reach
    /// fully open — never re-decided mid-animation. A fold that started growing downward and
    /// then switched to upward partway through is exactly the kind of discontinuity the task
    /// exists to prevent.</para>
    /// </summary>
    public static DisclosureFoldDirection DecideDirection(
        double collapsedTop, double collapsedHeight, double expandedHeight, double workAreaBottom)
    {
        var bottomIfDownward = collapsedTop + expandedHeight;
        return bottomIfDownward > workAreaBottom
            ? DisclosureFoldDirection.Upward
            : DisclosureFoldDirection.Downward;
    }

    /// <summary>
    /// The window's frame for a given height mid-fold. A pure function of the height alone, so
    /// every frame of the running animation — any height between <paramref name="collapsedHeight"/>
    /// and the fully open height — lands on exactly the frame this function would compute for
    /// that height taken in isolation. That is what "no jump, no overshoot" means here: there is
    /// no stateful re-docking step able to disagree with where the content actually is.
    /// </summary>
    public static DisclosureFoldFrame FrameForHeight(
        DisclosureFoldDirection direction, double collapsedTop, double collapsedHeight, double heightNow)
    {
        if (direction == DisclosureFoldDirection.Upward)
        {
            var bottom = collapsedTop + collapsedHeight;
            return new DisclosureFoldFrame(bottom - heightNow, heightNow);
        }

        return new DisclosureFoldFrame(collapsedTop, heightNow);
    }
}
