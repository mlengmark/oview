using System.IO;
using OView.App.Updates;

namespace OView.Tray.Platform;

/// <summary>
/// ADR-0010 D1's Windows half: compares the running executable's directory against the per-user
/// Inno installer's expected install location (<c>%LOCALAPPDATA%\Programs\O-view</c>, ADR-0008)
/// to decide between <see cref="InstallKind.WindowsInstaller"/> and
/// <see cref="InstallKind.WindowsPortable"/>. Ported from the source repository's
/// <c>UpdateService.CurrentInstallKind</c> with no behaviour change.
///
/// <para>The directory and executable-path lookup are injectable, the same shape as
/// <see cref="RegistryStartupRegistration"/> and <see cref="RegistryThemeSource"/>, because
/// the real install location and the real running path cannot be driven from a test — D1's
/// risk note is that detection itself is the one part of this decision no CI run can prove.
/// What the tests here prove is the comparison, not the environment.</para>
///
/// <para>The comparison is case-insensitive because this path is Windows-only and Windows
/// filesystems are.</para>
///
/// <para>No consumer constructs this yet — wiring it into a real update check is ADR-0010
/// slicing table row 4 (Windows update execution), not this slice.</para>
/// </summary>
public sealed class WindowsInstallKindSource : IInstallKindSource
{
    private readonly string _installRoot;
    private readonly Func<string?> _processPath;

    /// <summary>The per-user Inno installer's install location, ADR-0008.</summary>
    public static string DefaultInstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "O-view");

    /// <param name="installRoot">Expected install directory. Null means the real one.</param>
    /// <param name="processPath">
    /// How to find the running executable. Injectable because <see cref="Environment.ProcessPath"/>
    /// is the test host's path under a test runner, not O-view's.
    /// </param>
    public WindowsInstallKindSource(string? installRoot = null, Func<string?>? processPath = null)
    {
        _installRoot = installRoot ?? DefaultInstallRoot;
        _processPath = processPath ?? (() => Environment.ProcessPath);
    }

    public InstallKind Current
    {
        get
        {
            if (_processPath() is not { Length: > 0 } exe)
            {
                return InstallKind.WindowsPortable;
            }

            var exeDir = Path.GetDirectoryName(Path.GetFullPath(exe));

            return exeDir is not null
                && string.Equals(
                    Path.TrimEndingDirectorySeparator(exeDir),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(_installRoot)),
                    StringComparison.OrdinalIgnoreCase)
                ? InstallKind.WindowsInstaller
                : InstallKind.WindowsPortable;
        }
    }
}
