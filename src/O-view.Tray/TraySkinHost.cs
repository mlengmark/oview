using OView.App;
using OView.Core.Providers;

namespace OView.Tray;

/// <summary>
/// Composes the app shell (ADR-0007) with this skin (ADR-0008 slice 3, OVI-360): builds a
/// <see cref="UsagePollLoop"/> over the given provider/clock/timer and wires its
/// <see cref="UsagePollLoop.SnapshotUpdated"/> event to a fresh <see cref="TrayShellToSkin"/>'s
/// <see cref="IShellToSkin.ShowSnapshot"/>. Separated from <c>Program.Main</c> so the wiring
/// is testable without a WPF message loop or a real timer/provider — <c>internal</c> plus
/// <c>InternalsVisibleTo</c> (already declared on this project for
/// <c>O-view.Tray.Tests</c>) is the same seam-testing shape ADR-0007 slice 3's own seam
/// tests use.
/// </summary>
internal static class TraySkinHost
{
    public static (UsagePollLoop PollLoop, TrayShellToSkin Skin) Compose(
        IUsageProvider provider, IClock clock, IAppTimer timer, TimeSpan cadence)
    {
        var skin = new TrayShellToSkin();
        var pollLoop = new UsagePollLoop(provider, clock, timer, cadence);
        pollLoop.SnapshotUpdated += (_, snapshot) => skin.ShowSnapshot(snapshot);

        // The skin should not have to wait for the first tick to learn the shell exists;
        // UsagePollLoop.CurrentSnapshot already has a legitimate starting value
        // (UsageSnapshot.Unavailable before the first poll) and ShowSnapshot is specified to
        // accept that value (ADR-0007 D6).
        skin.ShowSnapshot(pollLoop.CurrentSnapshot);

        return (pollLoop, skin);
    }
}
