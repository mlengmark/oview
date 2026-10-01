using OView.App;

namespace OView.Tray.Platform;

/// <summary>
/// Single-instance on Windows via a named <see cref="Mutex"/>. The kernel releases the name
/// however the owning process dies, including a hard kill, so none of the "name left held by
/// a dead process" failure mode that pushes Linux towards a lock file (ADR-0007 D5) arises
/// here.
///
/// <para>The name is left unprefixed (session-local) rather than <c>Global\</c>-prefixed,
/// matching <see cref="ISingleInstanceGuard"/>'s "one per user session" contract: a user
/// signed in twice (e.g. over Remote Desktop and at the console) gets one O-view per session,
/// not one per machine.</para>
/// </summary>
public sealed class MutexSingleInstanceGuard : ISingleInstanceGuard
{
    public const string DefaultName = "OView.Tray.SingleInstance";

    private readonly string _name;
    private Mutex? _mutex;

    public MutexSingleInstanceGuard(string? name = null) => _name = name ?? DefaultName;

    public bool TryAcquire()
    {
        if (_mutex is not null)
        {
            return true;   // already ours; asking twice is not a second instance
        }

        var mutex = new Mutex(initiallyOwned: true, _name, out var isFirstInstance);
        if (!isFirstInstance)
        {
            mutex.Dispose();
            return false;
        }

        _mutex = mutex;
        return true;
    }

    public void Dispose()
    {
        _mutex?.Dispose();
        _mutex = null;
    }
}
