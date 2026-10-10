using System.IO;
using System.Reflection;
using System.Windows;
using OView.App;
using OView.App.Updates;
using OView.Core.Models;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Core.Statistics;
using OView.Tray.Platform;
using OView.Tray.Presentation;

namespace OView.Tray;

/// <summary>
/// The Windows skin host's entry point (ADR-0008 slice 3, OVI-360): the first executable in
/// this repository. Composes the app shell with <see cref="TrayShellToSkin"/> via
/// <see cref="TraySkinHost.Compose"/> and keeps the process alive with a windowless WPF
/// message loop — <c>UseWPF</c> is this skin's own framework choice (ADR-0007/0008 name no UI
/// framework above this project). Slice 4 (OVI-371) adds the first thing this process renders:
/// a <see cref="TrayStatusIcon"/>. Slice 5 (OVI-376) adds its tooltip, formatted from the same
/// snapshot. Slice 6 (OVI-386) adds the detail window: this is the first slice to build a real
/// <see cref="DetailPushCoordinator"/> over a real <see cref="LedgerUsageStatisticsSource"/>
/// (via <see cref="StoreLifetime"/>) and wire <see cref="ISkinToShell.RequestWidget"/> to it.
/// Slice 7 (OVI-391) adds the toast: <see cref="Presentation.AlertToastController"/> subscribes
/// to <see cref="TrayShellToSkin.EventRaised"/> and shows exactly one
/// <c>NotifyIcon.ShowBalloonTip</c> per raised event. No shell logic yet decides *that* an alert
/// is due (ADR-0007 D2 point 6 is a separate, unbuilt slice), so
/// <see cref="TrayShellToSkin.RaiseEvent"/> has no real production caller today — this wiring is
/// proven by <c>AlertToastControllerTests</c> and <c>TrayShellToSkinTests</c> against fakes.
/// ADR-0009 slice 2 (OVI-447) replaces the former local <c>PendingSkinToShell</c> stub with a
/// real <see cref="AppShell"/>, loading <see cref="ShellSettings"/> from
/// <see cref="ShellSettingsStore"/> before composing the poll loop so it starts on the loaded
/// cadence.
/// ADR-0009 slice 4 (OVI-469) implements <see cref="AppShell.Quit"/>'s shutdown order (skin,
/// then poll loop, then stores) and gives it its first production caller: this process's
/// <c>Application.SessionEnding</c> handler, below. The menu's own Quit item is still a later
/// slice (ADR-0009 slicing table row 6), so a running user session still ends this process only
/// by sign-off/shutdown or being killed from outside.
/// ADR-0009 slice 3 (OVI-457) gives <see cref="TrayShellToSkin.RaiseEvent"/> — wired since slice
/// 7 but never called in production until now — its first real caller: a
/// <see cref="UsageEventDecider"/> over the same <see cref="LedgerUsageStatisticsSource"/> the
/// detail coordinator already reads, consulted on every poll alongside it.
/// <see cref="BuildUsageProvider"/> still returns a plain
/// <see cref="OView.Core.Providers.Jsonl.JsonlUsageProvider"/>, not a <c>CompositeUsageProvider</c>,
/// so there is no real <see cref="OView.Core.Models.ProviderHealth"/> list to pass yet —
/// <see cref="UsageEventKind.InputDegraded"/> stays decided and tested but unreachable in this
/// process until that composition-root slice lands. The same is true of
/// <see cref="UsageEventKind.UpdateAvailable"/>: its fetch is ADR-0007 D3's own slice (OVI-433),
/// so <see cref="UsageEventDecider.DecideUpdateAvailable"/> has no caller here either.
/// ADR-0009 slice 6 (OVI-480) adds the menu's own first production caller of
/// <see cref="OView.Tray.Platform.RegistryStartupRegistration"/>: <c>TrayStatusIcon</c>'s new
/// <c>TrayMenu</c> reads it live every time the menu opens and applies a toggle through it
/// directly (D3), never through <see cref="ISkinToShell"/>.
/// ADR-0009 slice 7 (OVI-489) gives <see cref="OView.Tray.Platform.RegistryThemeSource"/> its
/// first production construction, shared by the detail window and the menu so both repaint from
/// the same live reading.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var directory = StoreDirectoryResolver.ResolveDefault();
        var settingsStore = new ShellSettingsStore(directory);
        var settings = settingsStore.Load();

        using var themeSource = new RegistryThemeSource();

        var (pollLoop, skin) = TraySkinHost.Compose(
            BuildUsageProvider(),
            new SystemClock(),
            new AppTimer(),
            settings.PollCadence);

        var storeLifetime = new StoreLifetime(directory);
        var statisticsSource = new LedgerUsageStatisticsSource(
            storeLifetime.UsageLedgerStore, storeLifetime.WeeklyResetAnchorStore);
        var identitySource = new AccountIdentitySource(
            ClaudeDataRoots.ClaudeCliConfigRoots(Environment.GetEnvironmentVariable("USERPROFILE")));
        var detailCoordinator = new DetailPushCoordinator(statisticsSource, identitySource, skin, new SystemClock(), TimeZoneInfo.Local);
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

        // ADR-0010 slicing table row 5 (OVI-557): ReleaseFeed's first production composition
        // in this process — HttpReleaseFeedTransport is platform-neutral (ADR-0007 D3), and
        // WindowsInstallKindSource (slice 2, OVI-511) had no consumer until this slice. Neither
        // this background cadence nor the manual menu item ever downloads or installs anything
        // (D7); only WindowsUpdateExecutor (slice 4, not called here) does that, and only from
        // an explicit, confirmed user action a later slice wires up.
        using var releaseFeedTransport = new HttpReleaseFeedTransport();
        var releaseFeed = new ReleaseFeed(releaseFeedTransport, new SystemClock());
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        using var updateCadence = new UpdateCadence(
            releaseFeed,
            new WindowsInstallKindSource(),
            currentVersion,
            () => skinToShell.Settings,
            skinToShell.RecordAnnouncedUpdateTag,
            skin.RaiseEvent,
            new AppTimer(),
            // INFERRED: no ADR names an exact background cadence for this check (D4 only says
            // "on a cadence"); once a day matches an update check's own low urgency without
            // hammering GitHub's rate limit every poll tick.
            TimeSpan.FromHours(24));

        var preferenceStore = new DetailWindowPreferenceStore(ResolvePreferenceDirectory());
        var positionController = new DetailWindowPositionController(
            preferenceStore.Load,
            () => DetailWindowPlacement.Compute(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height,
                DetailWindow.DefaultWidth,
                DetailWindow.DefaultHeight),
            (x, y) => preferenceStore.Save(x, y),
            IsOnScreen,
            DetailWindow.DefaultWidth,
            DetailWindow.DefaultHeight);

        var detailWindow = new DetailWindow(skinToShell, positionController, themeSource);
        skin.DetailShown += detailWindow.ShowDetail;
        skin.VisibilityChanged += detailWindow.SetVisible;

        using var statusIcon = new TrayStatusIcon(
            skinToShell, () => skinToShell.Settings, new RegistryStartupRegistration(), themeSource, updateCadence);
        statusIcon.OnSnapshotUpdated(pollLoop.CurrentSnapshot);
        pollLoop.SnapshotUpdated += (_, snapshot) => statusIcon.OnSnapshotUpdated(snapshot);

        var alertToast = new AlertToastController(statusIcon.ShowToast);
        skin.EventRaised += alertToast.OnEventRaised;

        // UsagePollLoop.Dispose() stops and disposes the IAppTimer it was given (ADR-0007
        // slice 2), so disposing the loop is enough — there is no separate timer to dispose.
        using (pollLoop)
        {
            // Qualified: UseWindowsForms (slice 4, for NotifyIcon) puts System.Windows.Forms.Application
            // in scope alongside System.Windows.Application. This process still runs WPF's
            // message loop, not WinForms' — the status icon needs no WinForms message pump of
            // its own.
            var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // ADR-0009 D7 (OVI-469): the Windows skin's loop-exit call — the first production
            // path that invokes ISkinToShell.Quit(). A user signing off or shutting down
            // Windows is a real "the user asked to quit" event even with no menu Quit item
            // built yet (that wiring is ADR-0009 slicing table row 6); without this, the
            // process would keep running past session end until Windows kills it outright,
            // skipping the shell's shutdown order entirely. Quit() runs the shell's ordering
            // (skin Shutdown, then poll loop, then stores); only afterwards does this skin call
            // Application.Shutdown() — the platform-loop-exit mechanism D7 says is the skin's
            // own, not the shell's.
            application.SessionEnding += (_, _) =>
            {
                skinToShell.Quit();
                application.Shutdown();
            };

            application.Run();
        }
    }

    /// <summary>
    /// The directory this skin's own detail-window position preference lives in — a sibling
    /// of the shell/Core store directory, but its own subfolder and its own file, never the
    /// shared <c>ShellSettingsStore</c> (ADR-0007 D4 reserves that for shell-owned behaviour
    /// settings, not a per-skin perceptual preference). Reads <c>LOCALAPPDATA</c> directly,
    /// matching <see cref="BuildUsageProvider"/>'s own reasoning: this skin is the one place
    /// allowed to ask the real Windows environment for anything.
    /// </summary>
    private static string ResolvePreferenceDirectory()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? throw new InvalidOperationException("LOCALAPPDATA is not set; cannot resolve this skin's preference directory.");

        return Path.Combine(localAppData, "O-view", "Tray");
    }

    /// <summary>
    /// ADR-0008 D4's 2026-10-09 amendment, slice P1: the real half of
    /// <see cref="DetailWindowPositionController"/>'s <c>isOnScreen</c> predicate. Adapter code,
    /// not unit-tested, same boundary as every other real-environment read in this file —
    /// <see cref="DetailWindowOnScreenCheck.IsFullyOnScreen"/> carries the actual decision and is
    /// proven against fakes. Reads every <see cref="System.Windows.Forms.Screen"/>'s work area,
    /// not just the primary one, since the amendment asks whether the saved rectangle intersects
    /// <i>any</i> current work area.
    /// </summary>
    private static bool IsOnScreen(Presentation.ScreenRect candidate) =>
        DetailWindowOnScreenCheck.IsFullyOnScreen(
            candidate,
            System.Windows.Forms.Screen.AllScreens
                .Select(screen => new Presentation.ScreenRect(
                    screen.WorkingArea.Left,
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height))
                .ToArray());

    /// <summary>
    /// A real, read-only <see cref="IUsageProvider"/> over the current user's Claude Desktop
    /// transcripts. Reading <c>APPDATA</c>/<c>LOCALAPPDATA</c> here, not in
    /// <c>O-view.Core</c> or <c>O-view.App</c>, matches every other per-OS environment read in
    /// this codebase (e.g. <c>StoreDirectoryResolver</c>, ADR-0007 slice 4): this skin is the
    /// one place allowed to ask the real Windows environment for anything. No data-provider
    /// composition root (chaining in <c>CompositeUsageProvider</c>, ADR-0005 D3) exists yet
    /// for the shell to reuse, so this slice wires the simplest real provider directly; a
    /// fuller chain is a separate, later composition-root slice, not this seam-proving one.
    /// </summary>
    private static IUsageProvider BuildUsageProvider()
    {
        var inputs = new ClaudeDataRootInputs(
            ClaudeHostPlatform.Windows,
            AppDataDirectory: Environment.GetEnvironmentVariable("APPDATA"),
            LocalAppDataDirectory: Environment.GetEnvironmentVariable("LOCALAPPDATA"));

        return new JsonlUsageProvider(ClaudeDataRoots.CandidateRoots(inputs));
    }
}
