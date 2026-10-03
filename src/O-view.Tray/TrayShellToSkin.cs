using OView.App;
using OView.Core.Models;

namespace OView.Tray;

/// <summary>
/// The Windows skin's <see cref="IShellToSkin"/> implementation (ADR-0008 slice 3, OVI-360).
/// Proves the shell-to-skin seam reaches a real skin project end to end: every call the shell
/// makes is recorded exactly as received. A tooltip, a detail window, a toast remain out of
/// scope (slices 5-7) and do not exist here. <see cref="ShowDetail"/> was added to fix a build
/// break: slice 5b (OVI-364) widened <see cref="IShellToSkin"/> with this member but never
/// implemented it here, so <c>main</c> did not compile — this PR (OVI-371) is the first to
/// touch this file since. The status icon itself (ADR-0008 slice 4) renders from
/// <see cref="ShowSnapshot"/>'s pushed <c>UsageLevel</c> via a separate
/// <c>Presentation.TrayStatusIcon</c>, not from this no-op recorder.
/// </summary>
public sealed class TrayShellToSkin : IShellToSkin
{
    /// <summary>The most recent snapshot the shell pushed, or <see langword="null"/> before
    /// the first call.</summary>
    public UsageSnapshot? LastSnapshot { get; private set; }

    /// <summary>The most recent event the shell raised, or <see langword="null"/> before the
    /// first call.</summary>
    public UsageEvent? LastEvent { get; private set; }

    /// <summary>The most recent visibility the shell requested, or <see langword="null"/>
    /// before the first call.</summary>
    public bool? LastVisible { get; private set; }

    /// <summary>Whether the shell has called <see cref="Shutdown"/>.</summary>
    public bool ShutdownCalled { get; private set; }

    /// <summary>The most recent detail the shell pushed, or <see langword="null"/> before the
    /// first call.</summary>
    public UsageDetail? LastDetail { get; private set; }

    public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

    public void RaiseEvent(UsageEvent usageEvent) => LastEvent = usageEvent;

    public void ShowDetail(UsageDetail detail) => LastDetail = detail;

    public void SetVisible(bool visible) => LastVisible = visible;

    public void Shutdown() => ShutdownCalled = true;
}
