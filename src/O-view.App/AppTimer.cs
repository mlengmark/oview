namespace OView.App;

/// <summary>
/// The one real <see cref="IAppTimer"/> implementation (ADR-0007 D2 point 4: "one clock,
/// one timer, injected"), backed by <see cref="System.Threading.Timer"/> rather than any
/// UI-framework dispatcher timer — the whole point of the seam is that O-view.App names no
/// UI framework. <see cref="Elapsed"/> fires on a thread-pool thread, not whichever thread
/// constructed this instance; a caller that needs to marshal back to a UI thread does that
/// itself, outside this type.
/// </summary>
public sealed class AppTimer : IAppTimer
{
    private readonly System.Threading.Timer _timer;
    private TimeSpan _interval = Timeout.InfiniteTimeSpan;
    private bool _running;

    public AppTimer()
    {
        _timer = new System.Threading.Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler? Elapsed;

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            if (_interval == value)
            {
                return;
            }

            _interval = value;

            if (_running)
            {
                _timer.Change(_interval, _interval);
            }
        }
    }

    public void Start()
    {
        _running = true;
        _timer.Change(_interval, _interval);
    }

    public void Stop()
    {
        _running = false;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _timer.Dispose();

    private void OnTick(object? state) => Elapsed?.Invoke(this, EventArgs.Empty);
}
