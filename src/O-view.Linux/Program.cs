using Avalonia;
using Avalonia.Controls;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Core.Statistics;
using OView.Linux.Platform;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux skin host's entry point, mirroring the composition shape <c>O-view.Tray</c>'s
/// <c>Program.Main</c> used for its own slices: builds a real (read-only, no display text)
/// <see cref="JsonlUsageProvider"/> over <see cref="ClaudeDataRoots.CandidateRoots"/>, composes
/// it with <see cref="SystemClock"/>/<see cref="AppTimer"/> into a <see cref="UsagePollLoop"/>
/// through <see cref="LinuxSkinHost.Compose"/>, and keeps the process alive with Avalonia's own
/// message loop (<c>ShutdownMode.OnExplicitShutdown</c> — no quit path until a later slice).
///
/// <para>D6's session-bus probe (<see cref="NotificationHostMonitor"/>) is started off this
/// thread via <see cref="NotificationHostMonitor.StartAsync"/> before the Avalonia message
/// loop runs, so the D-Bus round trip can never block it (D6 point 5). What it observes is
/// traced through <see cref="NotificationHostAdvisoryFormatter"/> (unchanged since slice 8) and,
/// since slice 9 (OVI-403), also drives <see cref="LinuxStatusIcon"/>: the icon is registered
/// and starts rendering only once a host has been observed present, either on the initial
/// probe or later via <see cref="NotificationHostMonitor.HostAppeared"/>.</para>
///
/// <para>Slice 10 (OVI-408) adds the first thing besides the status icon this process renders:
/// a <see cref="DetailWindow"/>. This is the first slice to build a real
/// <see cref="DetailPushCoordinator"/> over a real <see cref="LedgerUsageStatisticsSource"/>
/// (via <see cref="StoreLifetime"/>) and wire <see cref="ISkinToShell.RequestWidget"/> to it, so
/// <see cref="PendingSkinToShell"/> now forwards that one member instead of doing nothing beyond
/// recording the request. Every other <see cref="ISkinToShell"/> member still throws so a future
/// composition gap fails loudly instead of silently doing nothing.</para>
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

        var storeLifetime = StoreLifetime.CreateDefault();
        var statisticsSource = new LedgerUsageStatisticsSource(storeLifetime.UsageLedgerStore);
        var detailCoordinator = new DetailPushCoordinator(statisticsSource, skin, new SystemClock(), TimeZoneInfo.Local);
        pollLoop.SnapshotUpdated += (_, snapshot) => detailCoordinator.OnPollSucceeded(snapshot);

        var skinToShell = new PendingSkinToShell(detailCoordinator.OnRequestWidget);
        var preferenceStore = new DetailWindowPreferenceStore(DetailWindowPreferenceStore.DefaultDirectory);

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
            BuildAvaloniaApp(statusIcon, skin, skinToShell, preferenceStore)
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
    }

    /// <summary>
    /// The detail window (slice 10) is built inside <see cref="App.OnFrameworkInitializationCompleted"/>,
    /// not here — unlike <see cref="LinuxStatusIcon"/>'s <c>TrayIcon</c>, constructing an
    /// Avalonia <c>Window</c> needs the platform <see cref="AppBuilder.Configure{TApp}"/> sets up,
    /// which does not happen until <c>StartWithClassicDesktopLifetime</c> runs; it is also the
    /// first point at which <c>Window.Screens</c> (needed for <see cref="DetailWindowPlacement.Compute"/>'s
    /// first-run corner) resolves to anything real. <paramref name="skin"/>, <paramref name="skinToShell"/>
    /// and <paramref name="preferenceStore"/> carry no Avalonia dependency and are safe to build here.
    /// </summary>
    private static AppBuilder BuildAvaloniaApp(
        LinuxStatusIcon statusIcon, LinuxShellToSkin skin, ISkinToShell skinToShell, DetailWindowPreferenceStore preferenceStore) =>
        AppBuilder.Configure(() => new App(statusIcon, skin, skinToShell, preferenceStore))
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
    /// <c>PendingSkinToShell</c> played before its slice 6 built a real composition root.
    /// <see cref="RequestWidget"/> is now wired for real (slice 10, OVI-408): it forwards to
    /// <see cref="DetailPushCoordinator.OnRequestWidget"/>, which both answers
    /// <see cref="IShellToSkin.SetVisible"/> and pushes a detail on becoming visible. Every
    /// other member still throws so a future composition gap fails loudly instead of silently
    /// doing nothing.
    /// </summary>
    private sealed class PendingSkinToShell : ISkinToShell
    {
        private readonly Action<bool> _onRequestWidget;

        public PendingSkinToShell(Action<bool> onRequestWidget)
        {
            _onRequestWidget = onRequestWidget;
        }

        public void RequestWidget(bool visible) => _onRequestWidget(visible);

        public void RefreshNow() => throw NoCompositionRoot();

        public void SetThresholdPercent(int percent) => throw NoCompositionRoot();

        public void SetAutoUpdate(bool enabled) => throw NoCompositionRoot();

        public void WriteDiagnosticsBundle() => throw NoCompositionRoot();

        public void Quit() => throw NoCompositionRoot();

        private static NotSupportedException NoCompositionRoot() =>
            new("No shell composition root exists yet on this skin; only RequestWidget is wired.");
    }
}
