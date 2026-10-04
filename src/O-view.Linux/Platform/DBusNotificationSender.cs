using Tmds.DBus.Protocol;

namespace OView.Linux.Platform;

/// <summary>
/// Sends one <c>org.freedesktop.Notifications.Notify</c> call per invocation — the real
/// adapter behind <see cref="OView.Linux.Presentation.AlertNotificationController"/>
/// (ADR-0008 slice 11, OVI-417). Built directly on <see cref="Tmds.DBus.Protocol"/>'s
/// low-level message API (<see cref="DBusConnection.GetMessageWriter"/>/
/// <see cref="DBusConnection.CallMethodAsync(MessageBuffer)"/>), the same package and the same
/// "ask the bus directly, not a generated proxy" shape slice 8's
/// <see cref="DBusSessionBusNameWatcher"/> already established — no new dependency.
///
/// <para>The <c>Notify</c> signature is <c>susssasa{sv}i</c> (app name, replaces-id, icon,
/// summary, body, actions, hints, expire-timeout); this sender always passes an empty actions
/// array and hints dictionary and <c>-1</c> (server-default) for the timeout — action buttons
/// and persistence hints are new scope the spec supports but this slice does not need, per the
/// issue's own "Escalate when" boundary.</para>
///
/// <para><b>Not verified by execution in this environment</b> (the same boundary slice 8
/// recorded for <see cref="DBusSessionBusNameWatcher"/>): no session bus is reachable from
/// this CI runner or dev machine, so the real connect/call round trip, and whether any desktop
/// environment actually renders the resulting notification, have never been observed. Proven
/// only by <see cref="OView.Linux.Presentation.AlertNotificationController"/>'s own
/// fake-backed dispatch tests.</para>
/// </summary>
public sealed class DBusNotificationSender : IAsyncDisposable
{
    private const string AppName = "O-view";
    private const string NotifyDestination = "org.freedesktop.Notifications";
    private const string NotifyPath = "/org/freedesktop/Notifications";
    private const string NotifyInterface = "org.freedesktop.Notifications";
    private const string NotifySignature = "susssasa{sv}i";

    private readonly Lazy<DBusConnection> _connection;

    public DBusNotificationSender()
    {
        _connection = new Lazy<DBusConnection>(() => new DBusConnection(DBusAddress.Session
            ?? throw new InvalidOperationException("No session bus address is set (DBUS_SESSION_BUS_ADDRESS); cannot send a desktop notification.")));
    }

    public async Task SendAsync(string summary, string body, CancellationToken cancellationToken = default)
    {
        var connection = _connection.Value;
        await connection.ConnectAsync().ConfigureAwait(false);

        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: NotifyDestination,
            path: NotifyPath,
            @interface: NotifyInterface,
            member: "Notify",
            signature: NotifySignature,
            flags: MessageFlags.None);

        writer.WriteString(AppName);
        writer.WriteUInt32(0);
        writer.WriteString(string.Empty);
        writer.WriteString(summary);
        writer.WriteString(body);

        var actions = writer.WriteArrayStart(DBusType.String);
        writer.WriteArrayEnd(actions);

        var hints = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(hints);

        writer.WriteInt32(-1);

        var message = writer.CreateMessage();
        await connection.CallMethodAsync(message).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
