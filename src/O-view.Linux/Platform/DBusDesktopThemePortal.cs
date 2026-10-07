using Tmds.DBus.Protocol;

namespace OView.Linux.Platform;

/// <summary>
/// The real <see cref="IDesktopThemePortal"/>: asks the desktop portal's
/// <c>org.freedesktop.portal.Settings</c> interface for <c>org.freedesktop.appearance</c>
/// <c>color-scheme</c> directly, over the same low-level <c>Tmds.DBus.Protocol</c> message API
/// <see cref="DBusNotificationSender"/> already uses — no generated proxy, no new package
/// reference (ADR-0009 D6 point 1).
///
/// <para><b>Not verified by execution in this environment</b> (the same boundary slice 8 of
/// ADR-0008 recorded for <see cref="DBusSessionBusNameWatcher"/>, OVI-397): no session bus or
/// desktop portal is reachable from this CI runner or dev machine, so the real connect/call/
/// watch round trip has never been observed to succeed. <see cref="LinuxThemeSource"/>'s own
/// orchestration — the part this slice's tests actually exercise — is proven against a fake
/// implementing <see cref="IDesktopThemePortal"/> instead.</para>
/// </summary>
public sealed class DBusDesktopThemePortal : IDesktopThemePortal
{
    private const string PortalDestination = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string SettingsInterface = "org.freedesktop.portal.Settings";
    private const string AppearanceNamespace = "org.freedesktop.appearance";
    private const string ColorSchemeKey = "color-scheme";

    private readonly Lazy<DBusConnection> _connection;

    public DBusDesktopThemePortal()
    {
        _connection = new Lazy<DBusConnection>(() => new DBusConnection(DBusAddress.Session
            ?? throw new InvalidOperationException("No session bus address is set (DBUS_SESSION_BUS_ADDRESS); cannot read the desktop portal's theme setting.")));
    }

    public async Task<uint?> ReadColorSchemeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = _connection.Value;
            await connection.ConnectAsync().ConfigureAwait(false);

            var writer = connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: PortalDestination,
                path: PortalPath,
                @interface: SettingsInterface,
                member: "Read",
                signature: "ss",
                flags: MessageFlags.None);
            writer.WriteString(AppearanceNamespace);
            writer.WriteString(ColorSchemeKey);

            var message = writer.CreateMessage();
            var variant = await connection.CallMethodAsync(
                message,
                static (Message m, object? _) => m.GetBodyReader().ReadVariantValue(),
                null).ConfigureAwait(false);

            return TryReadColorScheme(variant, out var scheme) ? scheme : null;
        }
        catch (Exception ex) when (ex is DBusConnectionException or DBusMessageException or InvalidOperationException)
        {
            // The portal (or the bus itself) is simply not there to ask — ordinary on Linux,
            // reported as "no reading taken" rather than guessed (D6 point 3).
            return null;
        }
    }

    public async Task<IDisposable> WatchColorSchemeChangedAsync(Action<uint> onChanged, CancellationToken cancellationToken)
    {
        var connection = _connection.Value;
        await connection.ConnectAsync().ConfigureAwait(false);

        return await connection.WatchSignalAsync(
            sender: null,
            path: PortalPath,
            @interface: SettingsInterface,
            signal: "SettingChanged",
            reader: static (Message m, object? _) => ReadSettingChanged(m),
            handler: (Notification<(string Namespace, string Key, VariantValue Value)> notification) =>
            {
                if (!notification.HasValue)
                {
                    return;
                }

                var (ns, key, value) = notification.Value;
                if (ns == AppearanceNamespace && key == ColorSchemeKey && TryReadColorScheme(value, out var scheme))
                {
                    onChanged(scheme);
                }
            },
            flags: ObserverFlags.None,
            emitOnCapturedContext: false,
            state: null).ConfigureAwait(false);
    }

    private static (string Namespace, string Key, VariantValue Value) ReadSettingChanged(Message message)
    {
        var reader = message.GetBodyReader();
        var ns = reader.ReadString();
        var key = reader.ReadString();
        var value = reader.ReadVariantValue();
        return (ns, key, value);
    }

    /// <summary>
    /// <c>Settings.Read</c> and <c>SettingChanged</c> both carry the value as a variant; some
    /// portal implementations wrap it in an extra variant layer (a long-standing ambiguity in
    /// the xdg-desktop-portal spec that other desktop-portal clients work around the same way),
    /// so this unwraps once before expecting the <c>uint32</c> the spec defines for
    /// <c>color-scheme</c>. Anything else — including an absent or mistyped value — is reported
    /// as "not read", never guessed.
    /// </summary>
    private static bool TryReadColorScheme(VariantValue value, out uint scheme)
    {
        if (value.Type == VariantValueType.Variant)
        {
            value = value.GetVariantValue();
        }

        if (value.Type == VariantValueType.UInt32)
        {
            scheme = value.GetUInt32();
            return true;
        }

        scheme = default;
        return false;
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
