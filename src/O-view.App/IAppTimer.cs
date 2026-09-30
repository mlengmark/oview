namespace OView.App;

/// <summary>
/// A repeating timer, abstracted so the shell can schedule its poll loop without naming a
/// UI framework (ADR-0007 D2 point 4: "one clock, one timer, injected"). Each skin supplies
/// its own concrete timer behind this seam. No implementation ships in this slice; that is
/// slice 2's poll loop work.
/// </summary>
public interface IAppTimer : IDisposable
{
    /// <summary>Assigning re-times the timer. Implementations should ignore an unchanged value.</summary>
    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}
