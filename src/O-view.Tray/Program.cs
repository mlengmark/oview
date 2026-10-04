using System.IO;
using System.Windows;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Core.Statistics;
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
/// (via <see cref="StoreLifetime"/>) and wire <see cref="ISkinToShell.RequestWidget"/> to it, so
/// <see cref="PendingSkinToShell"/> now forwards that one member instead of recording nothing.
/// Slice 7 (OVI-391) adds the toast: <see cref="Presentation.AlertToastController"/> subscribes
/// to <see cref="TrayShellToSkin.EventRaised"/> and shows exactly one
/// <c>NotifyIcon.ShowBalloonTip</c> per raised event. No shell logic yet decides *that* an alert
/// is due (ADR-0007 D2 point 6 is a separate, unbuilt slice), so
/// <see cref="TrayShellToSkin.RaiseEvent"/> has no real production caller today — this wiring is
/// proven by <c>AlertToastControllerTests</c> and <c>TrayShellToSkinTests</c> against fakes. The
/// first call into <see cref="ISkinToShell"/> that would let a user ask to quit remains
/// unbuilt — until then this process exits only by being killed from outside, and every
/// <see cref="ISkinToShell"/> member besides <see cref="ISkinToShell.RequestWidget"/> still
/// throws.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var (pollLoop, skin) = TraySkinHost.Compose(
            BuildUsageProvider(),
            new SystemClock(),
            new AppTimer(),
            ShellSettings.Default.PollCadence);

        var storeLifetime = StoreLifetime.CreateDefault();
        var statisticsSource = new LedgerUsageStatisticsSource(storeLifetime.UsageLedgerStore);
        var detailCoordinator = new DetailPushCoordinator(statisticsSource, skin, new SystemClock(), TimeZoneInfo.Local);
        pollLoop.SnapshotUpdated += (_, snapshot) => detailCoordinator.OnPollSucceeded(snapshot);

        var skinToShell = new PendingSkinToShell(detailCoordinator.OnRequestWidget);

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
            (x, y) => preferenceStore.Save(x, y));

        var detailWindow = new DetailWindow(skinToShell, positionController);
        skin.DetailShown += detailWindow.ShowDetail;
        skin.VisibilityChanged += detailWindow.SetVisible;

        using var statusIcon = new TrayStatusIcon(skinToShell);
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

    /// <summary>
    /// Stands in for the real shell until a later slice wires the rest of
    /// <see cref="ISkinToShell"/> into a real composition root (slice 5b's own note: "no
    /// composition root exists yet ... that wiring is left to whichever slice first needs a
    /// running process"). <see cref="RequestWidget"/> is now wired for real (slice 6, OVI-386):
    /// it forwards to <see cref="DetailPushCoordinator.OnRequestWidget"/>, which both answers
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
            new("No shell composition root exists yet (ADR-0008 slice 6, OVI-386); only RequestWidget is wired.");
    }
}
