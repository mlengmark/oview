using Avalonia;
using Avalonia.Controls;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Linux.Platform;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux skin host's entry point (ADR-0008 slice 8, OVI-397), mirroring the composition
/// shape <c>O-view.Tray</c>'s <c>Program.Main</c> used for slice 3 (OVI-360): builds a real
/// (read-only, no display text) <see cref="JsonlUsageProvider"/> over
/// <see cref="ClaudeDataRoots.CandidateRoots"/>, composes it with <see cref="SystemClock"/>/
/// <see cref="AppTimer"/> into a <see cref="UsagePollLoop"/> through
/// <see cref="LinuxSkinHost.Compose"/>, and keeps the process alive with Avalonia's own
/// message loop (<c>ShutdownMode.OnExplicitShutdown</c> — no window, no quit path until a
/// later slice). <see cref="LinuxShellToSkin"/> is the no-op <see cref="IShellToSkin"/>: it
/// records every call and renders nothing.
///
/// <para>D6's session-bus probe (<see cref="NotificationHostMonitor"/>) is started off this
/// thread via <see cref="NotificationHostMonitor.StartAsync"/> before the Avalonia message
/// loop runs, so the D-Bus round trip can never block it (D6 point 5). What it observes is
/// traced through <see cref="NotificationHostAdvisoryFormatter"/> — the only consumer today,
/// since no status icon exists yet to show the advisory against (slice 9).</para>
/// </summary>
internal static class Program
{
    private const string NotificationHostWellKnownName = "org.kde.StatusNotifierWatcher";

    private static void Main(string[] args)
    {
        var (pollLoop, skin) = LinuxSkinHost.Compose(
            BuildUsageProvider(),
            new SystemClock(),
            new AppTimer(),
            ShellSettings.Default.PollCadence);

        var monitor = new NotificationHostMonitor(new DBusSessionBusNameWatcher(NotificationHostWellKnownName));
        monitor.ProbeCompleted += observed => Console.Error.WriteLine(NotificationHostAdvisoryFormatter.Describe(observed));
        monitor.HostAppeared += () => Console.Error.WriteLine(NotificationHostAdvisoryFormatter.Describe(observed: true));
        _ = monitor.StartAsync(CancellationToken.None);

        using (pollLoop)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }

        // The skin reference stays alive for the duration of the message loop above via the
        // event subscription UsagePollLoop.SnapshotUpdated holds on it; nothing further reads
        // skin.LastSnapshot in this slice (no status icon or window exists yet to do so).
        GC.KeepAlive(skin);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>
    /// A real, read-only <see cref="IUsageProvider"/> over the current user's Claude Desktop
    /// transcripts. Reading <c>HOME</c> here, not in <c>O-view.Core</c> or <c>O-view.App</c>,
    /// matches <c>O-view.Tray</c>'s own reasoning: this skin is the one place allowed to ask
    /// the real Linux environment for anything.
    /// </summary>
    private static IUsageProvider BuildUsageProvider()
    {
        var inputs = new ClaudeDataRootInputs(
            ClaudeHostPlatform.Linux,
            HomeDirectory: Environment.GetEnvironmentVariable("HOME"));

        return new JsonlUsageProvider(ClaudeDataRoots.CandidateRoots(inputs));
    }
}
