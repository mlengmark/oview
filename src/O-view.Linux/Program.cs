using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OView.App;
using OView.Core.Models;
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
/// (via <see cref="StoreLifetime"/>) and wire <see cref="ISkinToShell.RequestWidget"/> to it.</para>
///
/// <para>Slice 11 (OVI-417) wires <see cref="LinuxShellToSkin.EventRaised"/> to a real
/// <see cref="AlertNotificationController"/> over <see cref="DBusNotificationSender"/> — the
/// same shape <see cref="LinuxStatusIcon"/>'s snapshot wiring already uses, and the same
/// "no dedupe/threshold logic here" boundary <c>O-view.Tray</c>'s slice 7 drew for its own
/// toast wiring. <see cref="LinuxStatusIcon"/>'s tooltip (also slice 11) needs no separate
/// wiring here: it is pushed from inside <see cref="LinuxStatusIcon.OnSnapshotUpdated"/>,
/// already called on every poll tick below.</para>
///
/// <para>ADR-0009 slice 2 (OVI-447) replaces the former local <c>PendingSkinToShell</c> stub
/// with a real <see cref="AppShell"/>, loading <see cref="ShellSettings"/> from
/// <see cref="ShellSettingsStore"/> before composing the poll loop so it starts on the loaded
/// cadence. ADR-0009 slice 4 (OVI-469) implements <see cref="AppShell.Quit"/>'s shutdown order
/// (skin, then poll loop, then stores), but this process wires no production caller for it —
/// the Linux half of the loop-exit call is its own, separate slice (ADR-0009 slicing table row
/// 9), so this process still exits only by being killed from outside.</para>
///
/// <para>ADR-0009 slice 3 (OVI-457) gives <see cref="LinuxShellToSkin.RaiseEvent"/> — wired
/// since slice 11 but never called in production until now — its first real caller: a
/// <see cref="UsageEventDecider"/> over the same <see cref="LedgerUsageStatisticsSource"/> the
/// detail coordinator already reads, consulted on every poll alongside it.
/// <see cref="BuildUsageProvider"/> still returns a plain
/// <see cref="OView.Core.Providers.Jsonl.JsonlUsageProvider"/>, not a <c>CompositeUsageProvider</c>,
/// so there is no real <see cref="OView.Core.Models.ProviderHealth"/> list to pass yet —
/// <see cref="UsageEventKind.InputDegraded"/> stays decided and tested but unreachable in this
/// process until that composition-root slice lands. The same is true of
/// <see cref="UsageEventKind.UpdateAvailable"/>: its fetch is ADR-0007 D3's own slice (OVI-433),
/// so <see cref="UsageEventDecider.DecideUpdateAvailable"/> has no caller here either.</para>
/// </summary>
internal static class Program
{
    private const string NotificationHostWellKnownName = "org.kde.StatusNotifierWatcher";

    private static void Main(string[] args)
    {
        var directory = StoreDirectoryResolver.ResolveDefault();
        var settingsStore = new ShellSettingsStore(directory);
        var settings = settingsStore.Load();

        var (pollLoop, skin) = LinuxSkinHost.Compose(
            BuildUsageProvider(),
            new SystemClock(),
            new AppTimer(),
            settings.PollCadence);

        var storeLifetime = new StoreLifetime(directory);
        var statisticsSource = new LedgerUsageStatisticsSource(storeLifetime.UsageLedgerStore);
        var detailCoordinator = new DetailPushCoordinator(statisticsSource, skin, new SystemClock(), TimeZoneInfo.Local);
        pollLoop.SnapshotUpdated += (_, snapshot) => detailCoordinator.OnPollSucceeded(snapshot);

        var diagnosticsWriter = new DiagnosticsBundleWriter(directory, new SystemClock());
        var skinToShell = new AppShell(settingsStore, settings, pollLoop, detailCoordinator, diagnosticsWriter, skin, storeLifetime);

        // ADR-0009 slice 3 (OVI-457): no CompositeUsageProvider is composed in this process yet
        // (see BuildUsageProvider below), so there is no real ProviderHealth list — an empty one
        // is the honest answer, not a fabricated "all healthy", and InputDegraded simply never
        // fires here until that composition-root slice lands.
        var eventDecider = new UsageEventDecider(statisticsSource, new SystemClock(), TimeZoneInfo.Local);
        pollLoop.SnapshotUpdated += (_, snapshot) =>
        {
            foreach (var usageEvent in eventDecider.OnPollSucceeded(snapshot, Array.Empty<ProviderHealth>(), skinToShell.Settings))
            {
                skin.RaiseEvent(usageEvent);
            }
        };

        var preferenceStore = new DetailWindowPreferenceStore(DetailWindowPreferenceStore.DefaultDirectory);

        var notificationSender = new DBusNotificationSender();

        var statusIcon = new LinuxStatusIcon(
            skinToShell,
            () => skinToShell.Settings,
            new XdgAutostartRegistration(),
            (summary, body) => { _ = notificationSender.SendAsync(summary, body); });
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

        var notificationController = new AlertNotificationController(
            (summary, body) => notificationSender.SendAsync(summary, body));
        skin.EventRaised += notificationController.OnEventRaised;

        // ADR-0009 D7 (OVI-484): the Linux half of the loop-exit call. SIGTERM is this
        // platform's session/service-manager termination signal — the nearest equivalent of
        // the Windows skin's WM_QUERYENDSESSION-driven Application.SessionEnding (slice 4,
        // OVI-469), since Avalonia's classic-desktop lifetime raises no "session ending" event
        // of its own on Linux. Cancelling the default handling and running the shell's own
        // ordering first (Quit(), then the platform loop's own exit call) keeps this on the
        // same ordering every other quit path uses, including the menu's (slice 9's own Quit
        // item, wired through LinuxTrayMenu above). Not exercised by this process's own tests —
        // no signal-sending harness runs against this process here — but it calls nothing that
        // is not already tested: AppShell.Quit() (slice 4) and IClassicDesktopStyleApplicationLifetime.Shutdown()
        // (an Avalonia framework member, not this repository's code).
        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                skinToShell.Quit();
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });
        });

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
}
