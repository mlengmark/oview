using OView.App;

namespace OView.Linux.Platform;

/// <summary>
/// Linux's half of ADR-0009 D6: the desktop portal's <c>org.freedesktop.appearance</c>
/// <c>color-scheme</c>, read once at <see cref="StartAsync"/> and kept current from the
/// portal's <c>SettingChanged</c> signal — both off the calling thread (D6 point 4), through
/// <see cref="IDesktopThemePortal"/> so this type's own mapping/caching logic is proven against
/// a fake, the same split <see cref="DBusSessionBusNameWatcher"/>/
/// <see cref="NotificationHostMonitor"/> already established (ADR-0008 D6, OVI-397).
///
/// <para>Unlike <c>RegistryThemeSource</c> — a local, near-instant registry read repeated on
/// every <see cref="Current"/> access — the portal is reached over the session bus, an IPC
/// round trip. Re-reading it synchronously from whatever thread calls <see cref="Current"/>
/// would be exactly the UI-thread block D6 point 4 forbids, so this type reads once in the
/// background and caches the result, keeping the cache fresh from the signal subscription
/// instead of re-reading per access: <see cref="Current"/> itself never touches the bus.</para>
///
/// <para>Before <see cref="StartAsync"/> completes its first read, and whenever the portal is
/// absent or a read fails, <see cref="Current"/> is <see cref="ThemePreference.Unknown"/> —
/// never guessed (D6 point 3).</para>
///
/// <para>No consumer calls <see cref="StartAsync"/> yet (ADR-0009 slicing table row 8); wiring
/// one — and repainting from <see cref="Changed"/> — is row 10's job, the same way row 9's
/// Linux <c>Program.cs</c> already starts <see cref="NotificationHostMonitor"/> this way.</para>
/// </summary>
public sealed class LinuxThemeSource : IThemeSource, IAsyncDisposable
{
    private readonly IDesktopThemePortal _portal;
    private IDisposable? _subscription;

    private volatile ThemePreference _current = ThemePreference.Unknown;

    public event EventHandler<ThemePreference>? Changed;

    public LinuxThemeSource(IDesktopThemePortal portal)
    {
        _portal = portal;
    }

    public ThemePreference Current => _current;

    /// <summary>
    /// Performs the initial portal read and subscribes to its <c>SettingChanged</c> signal, both
    /// on a background thread — the returned <see cref="Task"/> represents that background
    /// work and is not meant to be awaited on a UI thread, the same "<c>Task.Run</c>, caller
    /// does not block on it" shape as <see cref="NotificationHostMonitor.StartAsync"/>.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Run(() => RunAsync(cancellationToken), cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var initial = await _portal.ReadColorSchemeAsync(cancellationToken).ConfigureAwait(false);
        _current = MapColorScheme(initial);

        try
        {
            _subscription = await _portal.WatchColorSchemeChangedAsync(
                scheme =>
                {
                    _current = MapColorScheme(scheme);
                    Changed?.Invoke(this, _current);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The cached value from the read above still stands; a portal that cannot be
            // watched for live changes is no worse than one that was never asked (D6 point 3 —
            // report what is known, never guess further), and this slice wires no consumer to
            // repaint from Changed yet regardless (row 10).
        }
    }

    private static ThemePreference MapColorScheme(uint? scheme) => scheme switch
    {
        1 => ThemePreference.Dark,
        2 => ThemePreference.Light,
        // 0 ("no preference") and null (absent portal/failed read) are both facts the OS
        // stated or that reading produced no fact at all — never promoted to Light (D6 point 3).
        _ => ThemePreference.Unknown
    };

    public async ValueTask DisposeAsync()
    {
        _subscription?.Dispose();
        await _portal.DisposeAsync().ConfigureAwait(false);
    }
}
