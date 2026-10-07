using OView.App.Updates;

namespace OView.Linux.Platform;

/// <summary>
/// ADR-0010 D1's Linux half: whether the running executable sits under the dpkg-owned install
/// location (<c>/usr/lib/o-view</c>, CONFIRMED as the source repository's convention) decides
/// between <see cref="InstallKind.LinuxPackage"/> and <see cref="InstallKind.LinuxTarball"/>.
/// The source repository's <c>Program.DetectInstallKind</c> used a raw
/// <c>string.StartsWith(prefix)</c>, which would misclassify a sibling directory such as
/// <c>/usr/lib/o-view-extra</c> as the dpkg install; this port adds the directory-boundary
/// check that comparison needs. An apt install must never self-update — overwriting files the
/// package manager owns is silently reverted by the next <c>apt upgrade</c> (D1, D4).
///
/// <para>The dpkg-owned path and the executable-path lookup are injectable, the same shape as
/// <see cref="XdgAutostartRegistration"/>, because the real install location and the real
/// running path cannot be driven from a test — D1's risk note is that detection itself is the
/// one part of this decision no CI run can prove. What the tests here prove is the comparison,
/// not the environment.</para>
///
/// <para>The comparison is ordinal, not case-insensitive: unlike Windows, Linux filesystems are
/// case-sensitive.</para>
///
/// <para>No consumer constructs this yet — wiring it into the Linux update notice is ADR-0010
/// slicing table row 5 (Linux update notice), not this slice.</para>
/// </summary>
public sealed class LinuxInstallKindSource : IInstallKindSource
{
    /// <summary>The dpkg package's install location, owned by dpkg once installed.</summary>
    public const string DefaultDpkgInstallPath = "/usr/lib/o-view";

    private readonly string _dpkgInstallPath;
    private readonly Func<string?> _processPath;

    /// <param name="dpkgInstallPath">Dpkg-owned install directory. Null means the real one.</param>
    /// <param name="processPath">
    /// How to find the running executable. Injectable because <see cref="Environment.ProcessPath"/>
    /// is the test host's path under a test runner, not O-view's.
    /// </param>
    public LinuxInstallKindSource(string? dpkgInstallPath = null, Func<string?>? processPath = null)
    {
        _dpkgInstallPath = dpkgInstallPath ?? DefaultDpkgInstallPath;
        _processPath = processPath ?? (() => Environment.ProcessPath);
    }

    public InstallKind Current
    {
        get
        {
            if (_processPath() is not { Length: > 0 } exe)
            {
                return InstallKind.LinuxTarball;
            }

            // A directory-boundary check, not a raw StartsWith: "/usr/lib/o-view" must not
            // match a sibling such as "/usr/lib/o-view-extra" that merely shares the prefix.
            var root = _dpkgInstallPath.TrimEnd('/');

            return exe == root || exe.StartsWith(root + "/", StringComparison.Ordinal)
                ? InstallKind.LinuxPackage
                : InstallKind.LinuxTarball;
        }
    }
}
