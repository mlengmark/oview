namespace OView.Tray.Presentation;

/// <summary>A rectangle in device pixels — a monitor's full bounds or its work area, both of
/// which Win32 reports only in pixels (gate G7 parity slice P21, OVI-738).</summary>
internal readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>Which corner of the work area the menu flyout ended up docked to.</summary>
internal enum MenuFlyoutCorner
{
    TopRight,
    BottomLeft,
    BottomRight,
}

internal readonly record struct MenuFlyoutPlacementResult(int Left, int Top, MenuFlyoutCorner Corner);

/// <summary>
/// Where the branded menu flyout opens (gate G7 parity slice P21, OVI-738, ADR-0009): a fixed
/// corner of the work area adjacent to the taskbar, the same placement model
/// <see cref="OView.Tray.Presentation.DetailWindowPlacement"/> already uses for the detail
/// widget's first placement (<c>docs/parity/g7-detail-window-parity.md</c>'s "On P21 and D4"
/// note) — not a reversal of ADR-0008 D4, which keeps the detail widget itself a dragged,
/// remembered position. <b>Never follows the pointer and never reads
/// <c>Shell_NotifyIconGetRect</c></b> — the cursor only selects which monitor's rectangles this
/// type is handed (<see cref="OView.Tray.Platform.MenuFlyoutMonitorLocator"/>'s job); this type
/// never asks the OS for anything itself, which is what keeps it provable against plain structs.
///
/// <para>Neither Windows nor any other platform will say where the taskbar sits, so the edge is
/// inferred from where <paramref name="work"/> is inset relative to <paramref name="monitor"/> in
/// <see cref="Compute"/> below — a top or left inset names that edge; a bottom or right inset (or
/// an auto-hidden taskbar, which leaves no inset at all) falls to the bottom-right corner, which
/// is also the Windows 11 default and where this app's own notification icon already lives on a
/// right-hand taskbar.</para>
///
/// <para>Windows-only by this slice's own scope (the issue that added this type: "Linux is out of
/// scope — P21 is Windows-only per the doc"), so unlike <see cref="DetailWindowPlacement"/> there
/// is deliberately no Linux counterpart to hold this to a cross-skin golden-master test yet.</para>
/// </summary>
internal static class MenuFlyoutPlacement
{
    /// <summary>The gap this skin leaves between the flyout and the work-area edges, in device
    /// pixels — matching <see cref="DetailWindowPlacement.DefaultMarginPx"/>'s own figure so the
    /// two docked surfaces this skin now has read as using one consistent gap.</summary>
    public const int DefaultMarginPx = 12;

    /// <summary>
    /// Top-left corner for a flyout of <paramref name="widthPx"/> x <paramref name="heightPx"/>,
    /// plus the corner it docked to. Falls back to the flyout's minimum-size corner (via
    /// <see cref="Clamp"/>) when the flyout is larger than the work area allows, rather than
    /// returning a rectangle no one asked for.
    /// </summary>
    public static MenuFlyoutPlacementResult Compute(
        PixelRect monitor, PixelRect work, int widthPx, int heightPx, int marginPx = DefaultMarginPx)
    {
        int left, top;
        MenuFlyoutCorner corner;

        if (work.Y > monitor.Y)
        {
            // Taskbar along the top: dock below it, flush with the work area's right edge.
            corner = MenuFlyoutCorner.TopRight;
            left = work.Right - marginPx - widthPx;
            top = work.Y + marginPx;
        }
        else if (work.X > monitor.X)
        {
            // Taskbar down the left: dock beside it, flush with the work area's bottom edge.
            corner = MenuFlyoutCorner.BottomLeft;
            left = work.X + marginPx;
            top = work.Bottom - marginPx - heightPx;
        }
        else
        {
            // Bottom, right, or auto-hidden: the Windows 11 default and where a right-hand
            // taskbar's own notification area already sits.
            corner = MenuFlyoutCorner.BottomRight;
            left = work.Right - marginPx - widthPx;
            top = work.Bottom - marginPx - heightPx;
        }

        return new MenuFlyoutPlacementResult(
            Clamp(left, work.X + marginPx, work.Right - marginPx - widthPx),
            Clamp(top, work.Y + marginPx, work.Bottom - marginPx - heightPx),
            corner);
    }

    /// <summary>Clamp that yields to <paramref name="min"/> when the range inverts — the flyout
    /// is taller or wider than the work area allows, same as <see cref="DetailWindowPlacement"/>'s
    /// own degenerate-input rule: a surface the caller cannot see land off-screen is worse than
    /// one pinned to a known point.</summary>
    private static int Clamp(int value, int min, int max) =>
        max < min ? min : Math.Max(min, Math.Min(max, value));
}
