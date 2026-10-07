namespace OView.Linux.Platform;

/// <summary>
/// The mockable seam over the desktop portal's appearance setting (ADR-0009 D6): a single read
/// of <c>org.freedesktop.appearance</c> <c>color-scheme</c>, and a way to watch the portal's
/// <c>SettingChanged</c> signal for later changes. This is the type <see cref="LinuxThemeSource"/>
/// is tested against with a fake — no real session bus or portal is reachable from this test
/// runner or CI, the same split <see cref="ISessionBusNameWatcher"/>/
/// <see cref="NotificationHostMonitor"/> already established (ADR-0008 D6, OVI-397).
/// </summary>
public interface IDesktopThemePortal : IAsyncDisposable
{
    /// <summary>
    /// Reads the color-scheme value as the portal reports it right now — 0 (no preference), 1
    /// (prefer dark) or 2 (prefer light) per the xdg-desktop-portal Settings spec — or
    /// <see langword="null"/> if the portal is absent entirely or the read otherwise fails.
    /// Never throws for either of those ordinary cases; the caller maps a failed or absent read
    /// onto <c>ThemePreference.Unknown</c> rather than guessing (D6 point 3).
    /// </summary>
    Task<uint?> ReadColorSchemeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to the portal's <c>SettingChanged</c> signal, invoking
    /// <paramref name="onChanged"/> with the freshly reported color-scheme value whenever
    /// <c>org.freedesktop.appearance</c> <c>color-scheme</c> changes (changes to other
    /// namespaces/keys are filtered out before <paramref name="onChanged"/> is called). Returns
    /// a handle that stops the subscription when disposed.
    /// </summary>
    Task<IDisposable> WatchColorSchemeChangedAsync(Action<uint> onChanged, CancellationToken cancellationToken);
}
