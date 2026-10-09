namespace OView.Tray.Updates;

/// <summary>
/// Starts the verified installer as an independent process. Isolated behind an interface for
/// the same reason <c>IInstallerDownloader</c> is: <see cref="WindowsUpdateExecutor"/>'s tests
/// must prove it refuses to reach this step rather than actually spawning a process.
/// </summary>
public interface IInstallerLauncher
{
    /// <summary>Launches the installer at <paramref name="installerPath"/> in silent update mode.</summary>
    void Launch(string installerPath);
}

/// <summary>
/// The one real <see cref="IInstallerLauncher"/> (ADR-0010 D6): runs the installer silently,
/// as an independent process this one does not hold a handle to, so it is not locked when the
/// installer upgrades in place.
///
/// <para><c>/SILENT</c> shows only a progress bar — no wizard pages, and no post-install
/// "launch now" prompt, which is what keeps a normal user install from double-launching under
/// this path (ADR-0010 D6). <c>/update=1</c> is the installer's own switch (ADR-0010 slicing
/// table row 7, <c>installer/O-view.iss</c> — not built in this slice) for telling it to
/// relaunch O-view itself when it finishes, via <c>explorer.exe</c>, once that slice exists.
/// Ported from the source repository's <c>UpdateService.LaunchInstaller</c> with no behaviour
/// change: the app hands off to the installer directly; the <c>explorer.exe</c> re-parenting
/// trick is the installer's own relaunch step, not this one, because <c>explorer.exe</c> does
/// not reliably forward arbitrary command-line switches to what it opens — only a bare path,
/// which is all the installer's relaunch of the plain exe ever needed.</para>
/// </summary>
public sealed class ShellInstallerLauncher : IInstallerLauncher
{
    public void Launch(string installerPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /update=1",
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(startInfo);
    }
}
