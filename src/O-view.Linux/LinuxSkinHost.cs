using OView.App;
using OView.Core.Providers;

namespace OView.Linux;

/// <summary>
/// Composes the app shell (ADR-0007) with this skin (ADR-0008 slice 8, OVI-397): builds a
/// <see cref="UsagePollLoop"/> over the given provider/clock/timer and wires its
/// <see cref="UsagePollLoop.SnapshotUpdated"/> event to a fresh <see cref="LinuxShellToSkin"/>'s
/// <see cref="IShellToSkin.ShowSnapshot"/>. The Linux counterpart of <c>O-view.Tray</c>'s
/// <c>TraySkinHost.Compose</c> (slice 3) — same composition shape, independently implemented
/// (D1). <c>internal</c> plus <c>InternalsVisibleTo</c> (already declared on this project for
/// <c>O-view.Linux.Tests</c>) is the same seam-testing shape slice 3's own seam tests use.
/// </summary>
internal static class LinuxSkinHost
{
    public static (UsagePollLoop PollLoop, LinuxShellToSkin Skin) Compose(
        IUsageProvider provider, IClock clock, IAppTimer timer, TimeSpan cadence)
    {
        var skin = new LinuxShellToSkin();
        var pollLoop = new UsagePollLoop(provider, clock, timer, cadence);
        pollLoop.SnapshotUpdated += (_, snapshot) => skin.ShowSnapshot(snapshot);

        // The skin should not have to wait for the first tick to learn the shell exists;
        // UsagePollLoop.CurrentSnapshot already has a legitimate starting value
        // (UsageSnapshot.Unavailable before the first poll) and ShowSnapshot is specified to
        // accept that value (ADR-0007 D6) — the same reasoning TraySkinHost.Compose used.
        skin.ShowSnapshot(pollLoop.CurrentSnapshot);

        return (pollLoop, skin);
    }
}
