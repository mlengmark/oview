namespace OView.App;

/// <summary>
/// The current time, injectable so the shell's poll cadence and window arithmetic can be
/// tested without waiting for a real clock (ADR-0007 D2 point 4: "one clock, one timer,
/// injected"). <see cref="SystemClock"/>, slice 2's (OVI-268) implementation, is the only
/// place in O-view.App that may read the system clock.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
