using Tmds.DBus.Protocol;

namespace OView.Linux.Platform;

/// <summary>
/// The real <see cref="ISessionBusNameWatcher"/>: asks the session bus itself whether a
/// well-known name has an owner, never the UI toolkit (ADR-0008 D6 point 1 — an Avalonia
/// <c>TrayIcon</c> reports <c>IsVisible = true</c> whether or not a host exists, so this type
/// never asks one). Built on <see cref="DBusConnection.WatchNameOwnerAsync"/>, which performs
/// its own round trip to resolve the current owner and can be awaited again for the next
/// owner-appeared transition — exactly D6 point 4's "watch for the name appearing later"
/// without a restart.
///
/// <para><b>Not verified by execution in this environment</b> (ADR-0008 slice 8, OVI-397):
/// no session bus is reachable from this CI runner or dev machine, so the real connect/probe/
/// watch round trip has never been observed to succeed. <see cref="NotificationHostMonitor"/>'s
/// own orchestration — the part this slice's tests actually exercise — is proven against a
/// fake implementing <see cref="ISessionBusNameWatcher"/> instead.</para>
/// </summary>
public sealed class DBusSessionBusNameWatcher : ISessionBusNameWatcher
{
    private readonly string _wellKnownName;
    private readonly Lazy<DBusConnection> _connection;
    private NameOwnerWatcher? _ownerWatcher;

    public DBusSessionBusNameWatcher(string wellKnownName)
    {
        _wellKnownName = wellKnownName;
        _connection = new Lazy<DBusConnection>(() => new DBusConnection(DBusAddress.Session
            ?? throw new InvalidOperationException("No session bus address is set (DBUS_SESSION_BUS_ADDRESS); cannot probe for a notification-area host.")));
    }

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        var connection = _connection.Value;
        await connection.ConnectAsync().ConfigureAwait(false);

        _ownerWatcher = await connection.WatchNameOwnerAsync(_wellKnownName).ConfigureAwait(false);
        return _ownerWatcher.GetCurrentOwner() is not null;
    }

    public Task WaitForOwnerAsync(CancellationToken cancellationToken)
    {
        if (_ownerWatcher is null)
        {
            throw new InvalidOperationException($"{nameof(ProbeAsync)} must complete before {nameof(WaitForOwnerAsync)} is called.");
        }

        return _ownerWatcher.WaitForOwnerAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _ownerWatcher?.Dispose();

        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
