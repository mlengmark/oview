namespace OView.App;

/// <summary>
/// Ensures one O-view per user session. Two instances would mean two tray icons and double
/// polling against the same vendor data. The mechanism that enforces this differs per OS
/// (ADR-0007 D5) — this interface is the shell's seam onto it; <c>O-view.Tray</c> and
/// <c>O-view.Linux</c> each supply their own implementation, never a shared one.
///
/// <para>Disposing releases the claim. A guard that never acquired must still be safe to
/// dispose, because the losing instance shuts down through the same path as the winner.</para>
/// </summary>
public interface ISingleInstanceGuard : IDisposable
{
    /// <summary>
    /// True when this process is the first instance. Call once; the result is the process's
    /// identity for the rest of its life.
    /// </summary>
    bool TryAcquire();
}
