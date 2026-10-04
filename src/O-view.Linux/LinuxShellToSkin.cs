using OView.App;
using OView.Core.Models;

namespace OView.Linux;

/// <summary>
/// The Linux skin's <see cref="IShellToSkin"/> implementation (ADR-0008 slice 8, OVI-397),
/// the Linux counterpart of <c>O-view.Tray</c>'s <c>TrayShellToSkin</c> (slice 3, OVI-360) —
/// independently implemented, not shared (D1). Proves the shell-to-skin seam reaches a real
/// Linux skin project end to end: every call the shell makes is recorded exactly as received
/// and nothing renders. The status icon (slice 9) and detail window (slice 10) render from
/// separate types that subscribe to <see cref="DetailShown"/>/<see cref="VisibilityChanged"/>;
/// alerts are a separately scoped, later slice (11); this type only has to outlive this one.
///
/// <para><see cref="DetailShown"/>/<see cref="VisibilityChanged"/> (slice 10, OVI-408) let the
/// composition root drive a real <c>DetailWindow</c> off the same calls this type already
/// recorded — the same widening-by-event shape <c>O-view.Tray</c>'s own slice 6 used for
/// <c>TrayShellToSkin</c>, a documented, deliberate extension of ADR-0007 D6's shape, not a new
/// seam. Every existing recorder keeps recording exactly as before; the events are additive and
/// no-ops when nothing has subscribed.</para>
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

    /// <summary>Raised after every <see cref="ShowDetail"/> call, with the exact detail
    /// pushed — the real detail window subscribes to render it (slice 10).</summary>
    public event Action<UsageDetail>? DetailShown;

    /// <summary>Raised after every <see cref="SetVisible"/> call, with the exact visibility
    /// requested — the real detail window subscribes to show/hide itself (slice 10).</summary>
    public event Action<bool>? VisibilityChanged;

    public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

    public void RaiseEvent(UsageEvent usageEvent) => LastEvent = usageEvent;

    public void ShowDetail(UsageDetail detail)
    {
        LastDetail = detail;
        DetailShown?.Invoke(detail);
    }

    public void SetVisible(bool visible)
    {
        LastVisible = visible;
        VisibilityChanged?.Invoke(visible);
    }

    public void Shutdown() => ShutdownCalled = true;
}
