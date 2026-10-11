namespace OView.Tray.Presentation;

/// <summary>Which edge the branded tray menu flyout's rise animation reveals from (gate G7
/// parity slice P22, OVI-743).</summary>
internal enum MenuFlyoutRiseDirection
{
    /// <summary>The ordinary case: reveals upward from the bottom edge, same as a flyout
    /// docked above a bottom or side taskbar always rises.</summary>
    Upward,

    /// <summary>A top-docked taskbar's case (the task brief's own note): reveals downward from
    /// the top edge instead.</summary>
    Downward,
}

/// <summary>
/// Maps the corner <see cref="MenuFlyoutPlacement"/> already picked to the rise direction (gate
/// G7 parity slice P22, OVI-743): <see cref="MenuFlyoutCorner.TopRight"/> is the one corner that
/// corresponds to a top-docked taskbar, so it alone reveals downward; every other corner keeps
/// the ordinary upward reveal. Framework-neutral — no WPF type loaded here.
/// </summary>
internal static class MenuFlyoutRiseGeometry
{
    public static MenuFlyoutRiseDirection ForCorner(MenuFlyoutCorner corner) =>
        corner == MenuFlyoutCorner.TopRight ? MenuFlyoutRiseDirection.Downward : MenuFlyoutRiseDirection.Upward;
}
