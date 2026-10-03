using System.Windows;
using OView.App;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;

namespace OView.Tray;

/// <summary>
/// The Windows skin host's entry point (ADR-0008 slice 3, OVI-360): the first executable in
/// this repository. Composes the app shell with <see cref="TrayShellToSkin"/> via
/// <see cref="TraySkinHost.Compose"/> and keeps the process alive with a windowless WPF
/// message loop — <c>UseWPF</c> is this skin's own framework choice (ADR-0007/0008 name no UI
/// framework above this project). Renders nothing: no <c>NotifyIcon</c>, no window, no
/// tooltip. Those, and the first call into <see cref="ISkinToShell"/> that would let a user
/// ask to quit, are slices 4-7 — until then this process exits only by being killed from
/// outside, which is acceptable for a slice whose entire job is proving the seam, not
/// shipping a usable tray app.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var (pollLoop, _) = TraySkinHost.Compose(
            BuildUsageProvider(),
            new SystemClock(),
            new AppTimer(),
            ShellSettings.Default.PollCadence);

        // UsagePollLoop.Dispose() stops and disposes the IAppTimer it was given (ADR-0007
        // slice 2), so disposing the loop is enough — there is no separate timer to dispose.
        using (pollLoop)
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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
}
