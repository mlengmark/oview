using OView.App;
using OView.Core.Models;

namespace OView.Linux;

/// <summary>
/// The Linux skin's <see cref="IShellToSkin"/> implementation (ADR-0008 slice 8, OVI-397),
/// the Linux counterpart of <c>O-view.Tray</c>'s <c>TrayShellToSkin</c> (slice 3, OVI-360) —
/// independently implemented, not shared (D1). Proves the shell-to-skin seam reaches a real
/// Linux skin project end to end: every call the shell makes is recorded exactly as received
/// and nothing renders. The status icon, tooltip, detail window and alerts are separately
/// scoped, later slices (9-11); this type only has to outlive this one.
/// </summary>
public sealed class LinuxShellToSkin : IShellToSkin
{
    /// <summary>The most recent snapshot the shell pushed, or <see langword="null"/> before
    /// the first call.</summary>
    public UsageSnapshot? LastSnapshot { get; private set; }

    /// <summary>The most recent event the shell raised, or <see langword="null"/> before the
    /// first call.</summary>
    public UsageEvent? LastEvent { get; private set; }

    /// <summary>The most recent detail the shell pushed, or <see langword="null"/> before the
    /// first call.</summary>
    public UsageDetail? LastDetail { get; private set; }

    /// <summary>The most recent visibility the shell requested, or <see langword="null"/>
    /// before the first call.</summary>
    public bool? LastVisible { get; private set; }

    /// <summary>Whether the shell has called <see cref="Shutdown"/>.</summary>
    public bool ShutdownCalled { get; private set; }

    public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

    public void RaiseEvent(UsageEvent usageEvent) => LastEvent = usageEvent;

    public void ShowDetail(UsageDetail detail) => LastDetail = detail;

    public void SetVisible(bool visible) => LastVisible = visible;

    public void Shutdown() => ShutdownCalled = true;
}
