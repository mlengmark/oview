namespace OView.App;

/// <summary>
/// Resolves the OS-specific default directory for Core's two persisted stores (ADR-0006 D2),
/// once, at shell startup. This is the one place in O-view.App that reads an environment
/// variable or asks what OS it is running on for this purpose — Core never does either; it
/// only ever takes a directory (<see cref="OView.Core.Storage.WeeklyResetAnchorStore"/>,
/// <see cref="OView.Core.Storage.UsageLedgerStore"/>).
///
/// <para>Per-platform logic is split into internal functions over an injected environment-
/// variable lookup (the pure-rule extraction ADR-0007 D5 describes for OS mechanisms), so both
/// branches are unit-testable from any host OS without an environment variable actually being
/// set that way on the test runner.</para>
///
/// <para><b>Windows</b> — CONFIRMED: <c>%LOCALAPPDATA%\O-view\</c>, matching the source
/// repository's behaviour today.</para>
///
/// <para><b>Linux</b> — INFERRED, unverified on real hardware: <c>$XDG_DATA_HOME/O-view/</c>,
/// falling back to <c>~/.local/share/O-view/</c> when <c>XDG_DATA_HOME</c> is unset or empty.
/// macOS has no documented path (G3 is out of scope) and <see cref="ResolveDefault"/> throws
/// rather than guess one.</para>
/// </summary>
public static class StoreDirectoryResolver
{
    private const string AppDirectoryName = "O-view";

    /// <summary>
    /// Resolves the default store directory for the OS this process is actually running on.
    /// Does not create the directory — that is the caller's responsibility
    /// (<see cref="StoreLifetime"/> does it).
    /// </summary>
    public static string ResolveDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return ResolveWindows(Environment.GetEnvironmentVariable);
        }

        if (OperatingSystem.IsLinux())
        {
            return ResolveLinux(Environment.GetEnvironmentVariable);
        }

        throw new PlatformNotSupportedException(
            "O-view has a documented store directory for Windows and Linux only (G3 is " +
            "out of scope; ADR-0006 D2 names no macOS path).");
    }

    /// <summary>
    /// The Windows branch: <c>%LOCALAPPDATA%\O-view\</c>. Internal and injectable so the test
    /// project can exercise it without depending on the real environment variable.
    /// </summary>
    internal static string ResolveWindows(Func<string, string?> getEnvironmentVariable)
    {
        var localAppData = getEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrEmpty(localAppData))
        {
            throw new InvalidOperationException(
                "LOCALAPPDATA is not set; cannot resolve the O-view store directory (ADR-0006 D2).");
        }

        return Path.Combine(localAppData, AppDirectoryName);
    }

    /// <summary>
    /// The Linux branch (INFERRED, unverified on real hardware): <c>$XDG_DATA_HOME/O-view/</c>,
    /// falling back to <c>~/.local/share/O-view/</c> when <c>XDG_DATA_HOME</c> is unset or
    /// empty. Internal and injectable for the same reason as <see cref="ResolveWindows"/>.
    /// </summary>
    internal static string ResolveLinux(Func<string, string?> getEnvironmentVariable)
    {
        var xdgDataHome = getEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, AppDirectoryName);
        }

        var home = getEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
        {
            throw new InvalidOperationException(
                "Neither XDG_DATA_HOME nor HOME is set; cannot resolve the O-view store " +
                "directory (ADR-0006 D2).");
        }

        return Path.Combine(home, ".local", "share", AppDirectoryName);
    }
}
