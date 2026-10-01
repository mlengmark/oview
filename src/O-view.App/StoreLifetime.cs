using OView.Core.Storage;

namespace OView.App;

/// <summary>
/// Owns the process lifetime of Core's two persisted stores (ADR-0007 D4's one-owner-of-
/// persisted-state rule, matching D1's reasoning for the poll loop's single instance): one
/// <see cref="WeeklyResetAnchorStore"/> and one <see cref="UsageLedgerStore"/>, constructed
/// once and kept for as long as the shell runs. Core's store constructors take a directory and
/// never resolve their own path (ADR-0006 D2); this is the shell-side half of that contract —
/// the directory is resolved once, here, and handed down.
///
/// <para>Construction creates the directory if it does not already exist.
/// <see cref="UsageLedgerStore"/>'s own constructor also does this, but
/// <see cref="WeeklyResetAnchorStore"/> only creates it lazily on <c>Save</c>; creating it
/// upfront here means both stores can read immediately after startup without one of them
/// racing the other to create the directory first.</para>
/// </summary>
public sealed class StoreLifetime
{
    public StoreLifetime(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        Directory.CreateDirectory(directory);

        WeeklyResetAnchorStore = new WeeklyResetAnchorStore(directory);
        UsageLedgerStore = new UsageLedgerStore(directory);
    }

    /// <summary>
    /// The one <see cref="WeeklyResetAnchorStore"/> instance for this process.
    /// </summary>
    public WeeklyResetAnchorStore WeeklyResetAnchorStore { get; }

    /// <summary>
    /// The one <see cref="UsageLedgerStore"/> instance for this process.
    /// </summary>
    public UsageLedgerStore UsageLedgerStore { get; }

    /// <summary>
    /// Resolves the default store directory for the current OS (<see cref="StoreDirectoryResolver"/>)
    /// and constructs both stores against it. The composition root the shell calls once at startup.
    /// </summary>
    public static StoreLifetime CreateDefault() => new(StoreDirectoryResolver.ResolveDefault());
}
