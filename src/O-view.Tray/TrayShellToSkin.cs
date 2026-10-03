using OView.App;
using OView.Core.Models;

namespace OView.Tray;

/// <summary>
/// The Windows skin's <see cref="IShellToSkin"/> implementation (ADR-0008 slice 3, OVI-360).
/// Proves the shell-to-skin seam reaches a real skin project end to end: every call the shell
/// makes is recorded exactly as received. Rendering — a <c>NotifyIcon</c>, a tooltip, a
/// detail window, a toast — is out of this slice's scope (slices 4-7) and does not exist
/// here; this type is deliberately a no-op renderer, not a stub that will be filled in later
/// in this same file.
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

    public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

    public void RaiseEvent(UsageEvent usageEvent) => LastEvent = usageEvent;

    public void SetVisible(bool visible) => LastVisible = visible;

    public void Shutdown() => ShutdownCalled = true;
}
