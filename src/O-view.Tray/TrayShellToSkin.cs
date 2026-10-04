using OView.App;
using OView.Core.Models;

namespace OView.Tray;

/// <summary>
/// The Windows skin's <see cref="IShellToSkin"/> implementation (ADR-0008 slice 3, OVI-360).
/// Proves the shell-to-skin seam reaches a real skin project end to end: every call the shell
/// makes is recorded exactly as received. A tooltip (slice 5) and the status icon (slice 4)
/// render from a separate <c>Presentation.TrayStatusIcon</c>/<c>TooltipTextController</c>, not
/// from this type. <see cref="ShowDetail"/> was added to fix a build break: slice 5b (OVI-364)
/// widened <see cref="IShellToSkin"/> with this member but never implemented it here, so
/// <c>main</c> did not compile — OVI-371 was the first PR to touch this file since.
///
/// <para><see cref="DetailShown"/>/<see cref="VisibilityChanged"/> (slice 6, OVI-386) let the
/// composition root drive a real <c>DetailWindow</c> off the same calls this type already
/// recorded, the same widening-by-event <c>TraySkinHost.Compose</c> already used for
/// <c>UsagePollLoop.SnapshotUpdated</c> — a documented, deliberate extension of ADR-0007 D6's
/// shape, not a new seam. Every existing recorder keeps recording exactly as before; the events
/// are additive and no-ops when nothing has subscribed.</para>
///
/// <para><see cref="EventRaised"/> (slice 7, OVI-391) lets the composition root drive a real
/// toast off the same <see cref="RaiseEvent"/> call this type already recorded — the same
/// widening-by-event <c>TraySkinHost.Compose</c> already used for
/// <c>UsagePollLoop.SnapshotUpdated</c>, a documented, deliberate extension of ADR-0007 D6's
/// shape, not a new seam. <see cref="LastEvent"/> keeps recording exactly as before; the event
/// is additive and a no-op when nothing has subscribed.</para>
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

    /// <summary>Raised after every <see cref="ShowDetail"/> call, with the exact detail
    /// pushed — the real detail window subscribes to render it (slice 6).</summary>
    public event Action<UsageDetail>? DetailShown;

    /// <summary>Raised after every <see cref="SetVisible"/> call, with the exact visibility
    /// requested — the real detail window subscribes to show/hide itself (slice 6).</summary>
    public event Action<bool>? VisibilityChanged;

    /// <summary>Raised after every <see cref="RaiseEvent"/> call, with the exact event
    /// raised — the real toast controller subscribes to render it (slice 7).</summary>
    public event Action<UsageEvent>? EventRaised;

    public void ShowSnapshot(UsageSnapshot snapshot) => LastSnapshot = snapshot;

    public void RaiseEvent(UsageEvent usageEvent)
    {
        LastEvent = usageEvent;
        EventRaised?.Invoke(usageEvent);
    }

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
