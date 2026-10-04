using Avalonia;
using Avalonia.Controls;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Linux.Platform;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux skin host's entry point, mirroring the composition shape <c>O-view.Tray</c>'s
/// <c>Program.Main</c> used for its own slices: builds a real (read-only, no display text)
/// <see cref="JsonlUsageProvider"/> over <see cref="ClaudeDataRoots.CandidateRoots"/>, composes
/// it with <see cref="SystemClock"/>/<see cref="AppTimer"/> into a <see cref="UsagePollLoop"/>
/// through <see cref="LinuxSkinHost.Compose"/>, and keeps the process alive with Avalonia's own
/// message loop (<c>ShutdownMode.OnExplicitShutdown</c> — no window, no quit path until a
/// later slice). <see cref="LinuxShellToSkin"/> is the no-op <see cref="IShellToSkin"/>: it
/// records every call and renders nothing (slice 8, OVI-397).
///
/// <para>D6's session-bus probe (<see cref="NotificationHostMonitor"/>) is started off this
/// thread via <see cref="NotificationHostMonitor.StartAsync"/> before the Avalonia message
/// loop runs, so the D-Bus round trip can never block it (D6 point 5). What it observes is
/// traced through <see cref="NotificationHostAdvisoryFormatter"/> (unchanged since slice 8) and,
/// new in slice 9 (OVI-403), also drives <see cref="LinuxStatusIcon"/>: the icon is registered
/// and starts rendering only once a host has been observed present, either on the initial
/// probe or later via <see cref="NotificationHostMonitor.HostAppeared"/>.</para>
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

        var skinToShell = new PendingSkinToShell();
        var statusIcon = new LinuxStatusIcon(skinToShell);
        statusIcon.OnSnapshotUpdated(pollLoop.CurrentSnapshot);
        pollLoop.SnapshotUpdated += (_, snapshot) => statusIcon.OnSnapshotUpdated(snapshot);

        var monitor = new NotificationHostMonitor(new DBusSessionBusNameWatcher(NotificationHostWellKnownName));
        monitor.ProbeCompleted += observed =>
        {
            Console.Error.WriteLine(NotificationHostAdvisoryFormatter.Describe(observed));
            statusIcon.OnHostObserved(observed);
        };
        monitor.HostAppeared += () =>
        {
            Console.Error.WriteLine(NotificationHostAdvisoryFormatter.Describe(observed: true));
            statusIcon.OnHostAppeared();
        };
        _ = monitor.StartAsync(CancellationToken.None);

        using (pollLoop)
        using (statusIcon)
        {
            BuildAvaloniaApp(statusIcon).StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }

        // The skin reference stays alive for the duration of the message loop above via the
        // event subscription UsagePollLoop.SnapshotUpdated holds on it; nothing further reads
        // skin.LastSnapshot in this slice (no window exists yet to do so — slice 10).
        GC.KeepAlive(skin);
    }

    private static AppBuilder BuildAvaloniaApp(LinuxStatusIcon statusIcon) =>
        AppBuilder.Configure(() => new App(statusIcon))
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

    /// <summary>
    /// Stands in for the real shell, the same role <c>O-view.Tray</c>'s own
    /// <c>PendingSkinToShell</c> played before its slice 6 built a real composition root: no
    /// detail window exists on this skin yet (slice 10), so only <see cref="RequestWidget"/> is
    /// wired, and it does nothing beyond recording the request — every other member throws so a
    /// future composition gap fails loudly instead of silently doing nothing.
    /// </summary>
    private sealed class PendingSkinToShell : ISkinToShell
    {
        public void RequestWidget(bool visible)
        {
            // No detail window/coordinator exists yet to forward this to (slice 10, OVI-403's
            // own boundary). Reaching here at all is what this slice's "done when" asks for.
        }

        public void RefreshNow() => throw NoCompositionRoot();

        public void SetThresholdPercent(int percent) => throw NoCompositionRoot();

        public void SetAutoUpdate(bool enabled) => throw NoCompositionRoot();

        public void WriteDiagnosticsBundle() => throw NoCompositionRoot();

        public void Quit() => throw NoCompositionRoot();

        private static NotSupportedException NoCompositionRoot() =>
            new("No shell composition root exists yet on this skin; only RequestWidget is wired.");
    }
}
