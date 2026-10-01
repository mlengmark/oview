namespace OView.App;

/// <summary>
/// A repeating timer, abstracted so the shell can schedule its poll loop without naming a
/// UI framework (ADR-0007 D2 point 4: "one clock, one timer, injected"). Each skin supplies
/// its own concrete timer behind this seam.
///
/// <para><see cref="Elapsed"/> was added in slice 2 (OVI-268) alongside the first
/// implementation, <c>AppTimer</c>: slice 1 shipped the shape with no way to be notified of
/// a tick, which the poll loop needs. An event keeps a fake timer in tests trivial — it
/// raises <see cref="Elapsed"/> itself, on its own thread, with no real clock involved —
/// without exposing either implementation's internals.</para>
/// </summary>
public interface IAppTimer : IDisposable
{
    /// <summary>Raised each time the timer fires. Never raised after <see cref="Stop"/>
    /// until <see cref="Start"/> is called again.</summary>
    event EventHandler? Elapsed;

    /// <summary>Assigning re-times the timer. Implementations should ignore an unchanged value.</summary>
    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}
