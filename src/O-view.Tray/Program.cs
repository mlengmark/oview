using System.Windows;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;
using OView.Tray.Presentation;

namespace OView.Tray;

/// <summary>
/// The Windows skin host's entry point (ADR-0008 slice 3, OVI-360): the first executable in
/// this repository. Composes the app shell with <see cref="TrayShellToSkin"/> via
/// <see cref="TraySkinHost.Compose"/> and keeps the process alive with a windowless WPF
/// message loop — <c>UseWPF</c> is this skin's own framework choice (ADR-0007/0008 name no UI
/// framework above this project). Slice 4 (OVI-371) adds the first thing this process renders:
/// a <see cref="TrayStatusIcon"/>. Slice 5 (OVI-376) adds its tooltip, formatted from the same
/// snapshot. Slice 7 (OVI-391) adds the toast: <see cref="Presentation.AlertToastController"/>
/// subscribes to <see cref="TrayShellToSkin.EventRaised"/> and shows exactly one
/// <c>NotifyIcon.ShowBalloonTip</c> per raised event. The detail window and the first call into
/// <see cref="ISkinToShell"/> that would let a user ask to quit remain slice 6 — until then this
/// process exits only by being killed from outside, and clicking the icon calls
/// <see cref="ISkinToShell.RequestWidget"/> against a placeholder shell that shows nothing yet
/// (see <see cref="PendingSkinToShell"/>). No shell logic yet decides *that* an alert is due
/// (ADR-0007 D2 point 6 is a separate, unbuilt slice), so <see cref="TrayShellToSkin.RaiseEvent"/>
/// has no real production caller today — this wiring is proven by
/// <c>AlertToastControllerTests</c> and <c>TrayShellToSkinTests</c> against fakes, the same
/// "wired, not yet driven" state slice 6's <c>DetailShown</c>/<c>VisibilityChanged</c> events
/// are in until a shell event-decision slice exists.
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

        using var statusIcon = new TrayStatusIcon(new PendingSkinToShell());
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
    /// Stands in for the real shell until a composition-root slice wires
    /// <c>DetailPushCoordinator</c>/<c>UsagePollLoop</c> into a real <see cref="ISkinToShell"/>
    /// (slice 5b's own note: "no composition root exists yet to wire it ... that wiring is
    /// left to whichever slice first needs a running process" — this is that slice, but only
    /// for the one member the status icon calls). <see cref="RequestWidget"/> is the only
    /// member <see cref="TrayStatusIcon"/> reaches; every other member throws so a future
    /// composition gap fails loudly instead of silently doing nothing.
    /// </summary>
    private sealed class PendingSkinToShell : ISkinToShell
    {
        public void RequestWidget(bool visible)
        {
            // No shell exists yet to show the widget (slices 5b/6); the status icon's own
            // "done when" is this call happening, not anything appearing on screen.
        }

        public void RefreshNow() => throw NoCompositionRoot();

        public void SetThresholdPercent(int percent) => throw NoCompositionRoot();

        public void SetAutoUpdate(bool enabled) => throw NoCompositionRoot();

        public void WriteDiagnosticsBundle() => throw NoCompositionRoot();

        public void Quit() => throw NoCompositionRoot();

        private static NotSupportedException NoCompositionRoot() =>
            new("No shell composition root exists yet (ADR-0008 slice 4, OVI-371); only RequestWidget is wired.");
    }
}
