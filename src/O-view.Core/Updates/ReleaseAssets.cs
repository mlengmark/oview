namespace OView.Core.Updates;

/// <summary>
/// Recognises one platform's release asset by name. Handed to
/// <see cref="UpdateCheck.Evaluate"/> so the comparison stays a pure predicate and Core
/// never needs to know what OS it is running on.
///
/// <para>Ported from the source repository with no behaviour change (ADR-0007 D3, ADR-0001
/// D4). Asset names here are internal matching keys, not user-facing wording — the skin
/// still owns how an available update is announced.</para>
/// </summary>
/// <param name="Description">What this looks for, for diagnostics and messages.</param>
/// <param name="Matches">Whether a published asset name is the one this build can install.</param>
public sealed record ReleaseAssetSelector(string Description, Func<string, bool> Matches);

/// <summary>
/// The names the release workflow publishes, and how each platform recognises its own.
///
/// <para><b>One decision in two places, so it lives here once.</b> The workflow writes
/// these names and the update checker matches them; if they are restated separately they
/// will drift, and the symptom is an app that quietly stops updating.</para>
/// </summary>
public static class ReleaseAssets
{
    /// <summary>Frozen — renaming would strand every already-installed Windows build.</summary>
    public const string WindowsInstallerName = "O-view-Setup.exe";

    /// <summary>Frozen. The portable exe, offered as a manual download.</summary>
    public const string WindowsPortableName = "O-view.Tray.exe";

    /// <summary>
    /// The checksum manifest published beside every release asset. Also a frozen name — a
    /// build that verifies against it looks for this exact string, so renaming it fails
    /// every update rather than skipping the check.
    /// </summary>
    public const string ChecksumsName = "SHA256SUMS";

    /// <summary>The Windows installer asset, matched by exact name.</summary>
    public static ReleaseAssetSelector WindowsInstaller { get; } = new(
        WindowsInstallerName,
        name => string.Equals(name, WindowsInstallerName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A Debian package for one architecture, e.g. <c>o-view_0.6.0_amd64.deb</c>. Matched by
    /// shape rather than equality because the name carries the version.
    /// </summary>
    public static ReleaseAssetSelector DebianPackage(string architecture) => new(
        $"o-view_<version>_{architecture}.deb",
        name => name.StartsWith("o-view_", StringComparison.Ordinal)
                && name.EndsWith($"_{architecture}.deb", StringComparison.Ordinal));

    /// <summary>
    /// The portable Linux tarball for one runtime identifier, e.g.
    /// <c>o-view-0.6.0-linux-x64.tar.gz</c>.
    /// </summary>
    public static ReleaseAssetSelector Tarball(string runtimeIdentifier) => new(
        $"o-view-<version>-{runtimeIdentifier}.tar.gz",
        name => name.StartsWith("o-view-", StringComparison.Ordinal)
                && name.EndsWith($"-{runtimeIdentifier}.tar.gz", StringComparison.Ordinal));

    /// <summary>
    /// Matches nothing. For a build that must never install anything it finds — such a
    /// build still *checks*, so it can tell the user a newer version exists; it simply has
    /// no asset to act on.
    /// </summary>
    public static ReleaseAssetSelector None { get; } = new("(no installable asset)", static _ => false);
}
