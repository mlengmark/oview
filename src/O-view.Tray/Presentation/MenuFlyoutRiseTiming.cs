namespace OView.Tray.Presentation;

/// <summary>
/// Timing and curve for the branded tray menu flyout's own rise animation (gate G7 parity slice
/// P22, OVI-743, ADR-0009): 230 ms open / 150 ms close, both on the same curve the detail
/// window's disclosure fold uses for its own open (<see cref="DisclosureFoldTiming.Decelerate"/>,
/// P19, OVI-717) — the task brief's own instruction to reuse "the same curve as the detail
/// window's disclosure fold". Unlike the fold, open and close share this one curve rather than
/// mirroring it into a second one.
///
/// <para>Numbers only, framework-neutral — no <c>System.Windows.Media.Animation.KeySpline</c> or
/// Avalonia easing type loaded here, so this stays provable with no window involved.
/// <see cref="MenuFlyoutRiseAnimation"/> is the WPF adapter that turns these numbers into a
/// running animation. Windows only by this slice's own scope (the issue: "Windows only. Do not
/// touch Linux's native menu") — Linux keeps its native menu, unaffected.</para>
/// </summary>
public static class MenuFlyoutRiseTiming
{
    /// <summary>230 ms — opening.</summary>
    public const int OpenDurationMs = 230;

    /// <summary>150 ms — closing is quicker than opening, the same convention the disclosure
    /// fold already uses.</summary>
    public const int CloseDurationMs = 150;

    /// <summary>The curve named in the task brief — shared by both open and close.</summary>
    public static readonly (double X1, double Y1, double X2, double Y2) Curve = (0.02, 0.16, 0.20, 0.96);

    /// <summary>The last 20 dip of slide that accompanies the clip reveal.</summary>
    public const double SlideDistanceDip = 20;
}
