namespace OView.Tray.Presentation;

/// <summary>
/// Timing and curve for the "Why so large?" disclosure fold (ADR-0008 D10, gate G7 parity slice
/// P19, OVI-717): 191 ms open / 125 ms close, both on the same curve the source's own
/// <c>Popup/FlyoutAnimation.cs</c> and <c>Popup/DisclosureAnimation.cs</c> use (reference only,
/// never built against here) — the open curve fitted by that file's own RMSE search against a
/// recorded Windows flyout trace, and its mirror used for the close, same convention. D10
/// assigns "timings, curve, chevron, grow-upward geometry" entirely to this skin; nothing here
/// reads a Core value.
///
/// <para>Numbers only, framework-neutral — no <c>System.Windows.Media.Animation.KeySpline</c> or
/// Avalonia easing type loaded here, so this stays provable with no WPF or Avalonia involved.
/// <see cref="OView.Tray.Presentation.DisclosureFold"/> is the WPF adapter that turns these
/// numbers into a running animation; <c>O-view.Linux</c> carries its own independent copy of
/// both (D1/D5).</para>
/// </summary>
public static class DisclosureFoldTiming
{
    /// <summary>191 ms — opening.</summary>
    public const int ExpandDurationMs = 191;

    /// <summary>125 ms — closing is quicker than opening; getting out of the way is not a
    /// gesture anyone waits to watch.</summary>
    public const int CollapseDurationMs = 125;

    /// <summary>The curve named in the task brief, used while opening.</summary>
    public static readonly (double X1, double Y1, double X2, double Y2) Decelerate = (0.02, 0.16, 0.20, 0.96);

    /// <summary>Its mirror, used while closing — the source's own <c>FlyoutAnimation</c> uses
    /// the same pairing for its entrance/exit, and the fold borrows the whole vocabulary.</summary>
    public static readonly (double X1, double Y1, double X2, double Y2) Accelerate = (0.80, 0.04, 0.98, 0.84);

    /// <summary>180 degrees: the chevron points back up when the fold is open.</summary>
    public const double ExpandedChevronAngleDegrees = 180;
}
