namespace OView.App;

/// <summary>
/// The current time, injectable so the shell's poll cadence and window arithmetic can be
/// tested without waiting for a real clock (ADR-0007 D2 point 4: "one clock, one timer,
/// injected"). No implementation ships in this slice; that is slice 2's poll loop work.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
