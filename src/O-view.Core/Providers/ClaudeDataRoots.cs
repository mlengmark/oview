namespace OView.Core.Providers;

/// <summary>
/// The platform Claude Desktop's data is being located for. Carried as an injected value,
/// never read from the real operating system inside this file — see
/// <see cref="ClaudeDataRoots.CandidateRoots"/>.
/// </summary>
public enum ClaudeHostPlatform
{
    Windows,
    Linux
}

/// <summary>
/// Every root directory and vendor identifier <see cref="ClaudeDataRoots"/> needs to
/// resolve Claude Desktop's candidate data directories for one host. Nothing here is read
/// from the environment by this type — the caller (a future provider, ADR-0005 slices 3-5,
/// or its host) supplies every value, which is what keeps every layout rule testable with
/// fakes on any runner (ADR-0005 D5).
/// </summary>
/// <param name="Platform">Which host this is. Selects which layout rules
/// <see cref="ClaudeDataRoots.CandidateRoots"/> applies — it is data, not a runtime OS
/// check.</param>
/// <param name="AppDataDirectory">Windows only: the roaming application-data directory
/// (conventionally <c>%APPDATA%</c>) canonical Claude Desktop installs write under.</param>
/// <param name="LocalAppDataDirectory">Windows only: the local application-data directory
/// (conventionally <c>%LOCALAPPDATA%</c>) an MSIX-packaged Claude Desktop install writes
/// under.</param>
/// <param name="HomeDirectory">Linux only: the user's home directory (conventionally
/// <c>$HOME</c>), the base every Linux layout below is rooted under.</param>
/// <param name="WindowsPackageFamilyName">Windows only: the MSIX package family name
/// segment of <c>Packages\&lt;family&gt;\LocalCache\Roaming\Claude</c>. No default is
/// supplied — ADR-0005 records the layout's shape, not a confirmed exact identifier, and
/// this type does not fabricate one.</param>
/// <param name="LinuxSnapName">Linux only: the Snap package name segment of
/// <c>snap/&lt;snap&gt;/current/.config/Claude</c>. No default is supplied, for the same
/// reason as <paramref name="WindowsPackageFamilyName"/>.</param>
/// <param name="LinuxFlatpakAppId">Linux only: the Flatpak application id segment of
/// <c>.var/app/&lt;app-id&gt;/config/Claude</c>. No default is supplied, for the same
/// reason as <paramref name="WindowsPackageFamilyName"/>.</param>
public sealed record ClaudeDataRootInputs(
    ClaudeHostPlatform Platform,
    string? AppDataDirectory = null,
    string? LocalAppDataDirectory = null,
    string? HomeDirectory = null,
    string? WindowsPackageFamilyName = null,
    string? LinuxSnapName = null,
    string? LinuxFlatpakAppId = null);

/// <summary>
/// Pure path-resolution rules for locating Claude Desktop's on-disk data (ADR-0005 D5),
/// carried forward in spirit from the source repository's <c>ClaudeDataRoots</c>. Every
/// layout rule is a pure function of an injected root directory — no member here reads
/// the real filesystem, an environment variable, or the real operating system.
///
/// <para><see cref="CandidateRoots"/> is the one member that is aware a
/// <see cref="ClaudeHostPlatform"/> exists at all, and it only selects which layout rules
/// apply for the platform its caller supplies; it never detects the platform itself. That
/// keeps this whole type free of the OS-conditional branching Core is not allowed to
/// contain (no <c>RuntimeInformation.IsOSPlatform</c>, no <c>OperatingSystem.IsWindows()</c>
/// call anywhere in this file) while still letting both platforms' rules run on either
/// test runner, per ADR-0005 D5's own requirement.</para>
///
/// <para>This slice resolves candidate directories only. It performs no I/O — it never
/// checks whether a candidate exists — and it is not a provider: no <see cref="OView.Core.Models.UsageSnapshot"/>
/// is produced here. Turning a resolved root into a snapshot is ADR-0005 slices 3-5.</para>
/// </summary>
public static class ClaudeDataRoots
{
    private const string ClaudeDirectoryName = "Claude";

    /// <summary>
    /// The canonical Windows layout: <c>%APPDATA%\Claude</c>. Returns <c>null</c> when
    /// <paramref name="appDataDirectory"/> is not supplied — this rule never guesses a
    /// path into existence.
    /// </summary>
    public static string? WindowsCanonical(string? appDataDirectory) =>
        string.IsNullOrEmpty(appDataDirectory)
            ? null
            : Path.Combine(appDataDirectory, ClaudeDirectoryName);

    /// <summary>
    /// The Windows MSIX-packaged layout: <c>%LOCALAPPDATA%\Packages\&lt;family&gt;\LocalCache\Roaming\Claude</c>.
    /// Recorded because assuming the canonical path alone already cost the source project
    /// one "no usage data" report against a machine where Claude Desktop was open and
    /// working (ADR-0005). Returns <c>null</c> when either input is missing.
    /// </summary>
    public static string? WindowsMsix(string? localAppDataDirectory, string? packageFamilyName) =>
        string.IsNullOrEmpty(localAppDataDirectory) || string.IsNullOrEmpty(packageFamilyName)
            ? null
            : Path.Combine(localAppDataDirectory, "Packages", packageFamilyName, "LocalCache", "Roaming", ClaudeDirectoryName);

    /// <summary>
    /// The canonical Linux layout: <c>~/.config/Claude</c>. Returns <c>null</c> when
    /// <paramref name="homeDirectory"/> is not supplied.
    /// </summary>
    public static string? LinuxCanonical(string? homeDirectory) =>
        string.IsNullOrEmpty(homeDirectory)
            ? null
            : Path.Combine(homeDirectory, ".config", ClaudeDirectoryName);

    /// <summary>
    /// The Linux Snap sandboxed layout: <c>~/snap/&lt;snap&gt;/current/.config/Claude</c>.
    /// Returns <c>null</c> when either input is missing.
    /// </summary>
    public static string? LinuxSnap(string? homeDirectory, string? snapName) =>
        string.IsNullOrEmpty(homeDirectory) || string.IsNullOrEmpty(snapName)
            ? null
            : Path.Combine(homeDirectory, "snap", snapName, "current", ".config", ClaudeDirectoryName);

    /// <summary>
    /// The Linux Flatpak sandboxed layout: <c>~/.var/app/&lt;app-id&gt;/config/Claude</c>.
    /// Returns <c>null</c> when either input is missing.
    /// </summary>
    public static string? LinuxFlatpak(string? homeDirectory, string? flatpakAppId) =>
        string.IsNullOrEmpty(homeDirectory) || string.IsNullOrEmpty(flatpakAppId)
            ? null
            : Path.Combine(homeDirectory, ".var", "app", flatpakAppId, "config", ClaudeDirectoryName);

    /// <summary>
    /// Every candidate directory Claude Desktop might use on <paramref name="inputs"/>'s
    /// platform, most-canonical first, skipping any layout whose required inputs were not
    /// supplied. macOS layouts are out of scope (gate G3) and are not enumerated by any
    /// <see cref="ClaudeHostPlatform"/> value here.
    /// </summary>
    public static IReadOnlyList<string> CandidateRoots(ClaudeDataRootInputs inputs)
    {
        var candidates = new List<string>();

        switch (inputs.Platform)
        {
            case ClaudeHostPlatform.Windows:
                AddIfResolved(candidates, WindowsCanonical(inputs.AppDataDirectory));
                AddIfResolved(candidates, WindowsMsix(inputs.LocalAppDataDirectory, inputs.WindowsPackageFamilyName));
                break;

            case ClaudeHostPlatform.Linux:
                AddIfResolved(candidates, LinuxCanonical(inputs.HomeDirectory));
                AddIfResolved(candidates, LinuxSnap(inputs.HomeDirectory, inputs.LinuxSnapName));
                AddIfResolved(candidates, LinuxFlatpak(inputs.HomeDirectory, inputs.LinuxFlatpakAppId));
                break;
        }

        return candidates;
    }

    private static void AddIfResolved(List<string> candidates, string? resolved)
    {
        if (resolved is not null)
        {
            candidates.Add(resolved);
        }
    }
}
