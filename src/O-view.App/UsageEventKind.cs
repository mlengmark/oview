namespace OView.App;

/// <summary>
/// The four event kinds <see cref="IShellToSkin.RaiseEvent"/> can carry (ADR-0007 D6,
/// lines 198-222). The shell decides *that* one of these happened and raises it exactly
/// once per occurrence (D2 point 6); a skin decides whether and how to present it.
/// </summary>
public enum UsageEventKind
{
    /// <summary>A usage percentage crossed the alert threshold the shell is watching.</summary>
    ThresholdCrossed,

    /// <summary>Local activity diverged from what the plan meter accounts for.</summary>
    OffPlanEntered,

    /// <summary>The shell's update check (ADR-0007 D3) found a newer release.</summary>
    UpdateAvailable,

    /// <summary>A usage provider is failing or returning no data (ADR-0005 D4).</summary>
    InputDegraded,
}
