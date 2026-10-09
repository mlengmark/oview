using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OView.App.Updates;
using OView.Core.Updates;

namespace OView.Tray.Updates;

/// <summary>What <see cref="WindowsUpdateExecutor.RunAsync"/> did, for the caller's own logging.</summary>
public enum UpdateExecutionOutcome
{
    /// <summary>This install kind may not download or run anything (ADR-0010 D1) — nothing happened.</summary>
    NotApplicable,

    /// <summary>The feed reported no newer release, or could not be read.</summary>
    NoUpdateAvailable,

    /// <summary>The installer's <c>browser_download_url</c> is not an allowlisted GitHub host.</summary>
    UntrustedInstallerUrl,

    /// <summary>
    /// No usable checksums URL: the release published none, or its URL is not an allowlisted
    /// GitHub host. Refused the same way a missing manifest is (ADR-0010 D3) — an attacker who
    /// can replace the asset could otherwise simply point the manifest URL elsewhere.
    /// </summary>
    NoVerifiableChecksums,

    /// <summary>The manifest could not be downloaded.</summary>
    ChecksumDownloadFailed,

    /// <summary>
    /// The manifest has no usable digest for the installer asset — covers a missing file, an
    /// unparseable manifest, and one that simply never names the asset. <see cref="ChecksumFile"/>
    /// collapses all three to the same "no answer" on purpose (ADR-0010 D3).
    /// </summary>
    ChecksumNotFound,

    /// <summary>The installer could not be downloaded.</summary>
    InstallerDownloadFailed,

    /// <summary>The downloaded bytes do not match the manifest's recorded digest.</summary>
    ChecksumMismatch,

    /// <summary>Downloaded, verified, and handed off to the installer; the caller should exit.</summary>
    Launched,
}

/// <summary>
/// ADR-0010 slice 4: the Windows download-verify-launch path. Wires the shell-declared
/// <see cref="IInstallKindSource"/> selector (slice 1/2) and the shared <see cref="ReleaseFeed"/>
/// (ADR-0007 D3) together with slice 3's <see cref="ChecksumFile"/>/<see cref="ReleaseDownloadUrl"/>
/// into the one slice D3 requires verification and download to ship in.
///
/// <para><b>Fails closed at every step.</b> A non-<see cref="UpdateOutcome.UpdateAvailable"/>
/// check, an untrusted installer or checksums URL, a manifest with no usable digest for the
/// asset, a failed download, or a digest mismatch all stop here with nothing launched — never
/// "install anyway". The refusal is the deliverable (ADR-0010 D3).</para>
///
/// <para><b>No production caller constructs this against the real network yet.</b> Deciding
/// *when* to run this — the background cadence, the once-per-version notice, the "Check for
/// updates now" menu item — is ADR-0010 slicing table row 5, and it needs
/// <c>ShellSettings.AutoUpdateEnabled</c> to gate the periodic case (D7: auto-update means
/// check automatically, never install automatically), a schema change this slice does not
/// make. This class and <see cref="HttpInstallerDownloader"/>/<see cref="ShellInstallerLauncher"/>
/// are composed with real dependencies and ready for that slice to call, the same staged
/// pattern slice 2's <c>WindowsInstallKindSource</c> doc comment already used ("no consumer
/// constructs this yet").</para>
/// </summary>
public sealed class WindowsUpdateExecutor
{
    private readonly IInstallKindSource _installKindSource;
    private readonly ReleaseFeed _releaseFeed;
    private readonly IInstallerDownloader _downloader;
    private readonly IInstallerLauncher _launcher;
    private readonly Action _onLaunched;
    private readonly Architecture _architecture;

    /// <param name="onLaunched">
    /// Called once the installer has been handed off to, immediately before
    /// <see cref="RunAsync"/> returns <see cref="UpdateExecutionOutcome.Launched"/>. The real
    /// composition wires this to the shell's own shutdown order (ADR-0009 D7's
    /// <c>AppShell.Quit</c>), because the exe must not stay locked while the installer
    /// upgrades in place (ADR-0010 D6).
    /// </param>
    /// <param name="architecture">Defaults to the running process's architecture.</param>
    public WindowsUpdateExecutor(
        IInstallKindSource installKindSource,
        ReleaseFeed releaseFeed,
        IInstallerDownloader downloader,
        IInstallerLauncher launcher,
        Action onLaunched,
        Architecture? architecture = null)
    {
        ArgumentNullException.ThrowIfNull(installKindSource);
        ArgumentNullException.ThrowIfNull(releaseFeed);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(onLaunched);

        _installKindSource = installKindSource;
        _releaseFeed = releaseFeed;
        _downloader = downloader;
        _launcher = launcher;
        _onLaunched = onLaunched;
        _architecture = architecture ?? RuntimeInformation.OSArchitecture;
    }

    public async Task<UpdateExecutionOutcome> RunAsync(
        string currentVersion, CancellationToken cancellation = default)
    {
        var kind = _installKindSource.Current;
        if (!UpdatePolicy.MayDownloadAndRun(kind))
        {
            return UpdateExecutionOutcome.NotApplicable;
        }

        var asset = UpdatePolicy.DetectionAsset(kind, _architecture);
        var check = await _releaseFeed.CheckAsync(currentVersion, asset, cancellation).ConfigureAwait(false);
        if (check.Outcome != UpdateOutcome.UpdateAvailable || check.Available is not { } update)
        {
            return UpdateExecutionOutcome.NoUpdateAvailable;
        }

        if (!ReleaseDownloadUrl.IsTrusted(update.InstallerUrl))
        {
            return UpdateExecutionOutcome.UntrustedInstallerUrl;
        }

        if (update.ChecksumsUrl is not { Length: > 0 } checksumsUrl || !ReleaseDownloadUrl.IsTrusted(checksumsUrl))
        {
            return UpdateExecutionOutcome.NoVerifiableChecksums;
        }

        var manifestBytes = await _downloader.DownloadAsync(checksumsUrl, cancellation).ConfigureAwait(false);
        if (manifestBytes is null)
        {
            return UpdateExecutionOutcome.ChecksumDownloadFailed;
        }

        var recordedDigest = ChecksumFile.DigestFor(
            Encoding.UTF8.GetString(manifestBytes), ReleaseAssets.WindowsInstallerName);
        if (recordedDigest is null)
        {
            return UpdateExecutionOutcome.ChecksumNotFound;
        }

        var installerBytes = await _downloader.DownloadAsync(update.InstallerUrl, cancellation).ConfigureAwait(false);
        if (installerBytes is not { Length: > 0 })
        {
            return UpdateExecutionOutcome.InstallerDownloadFailed;
        }

        var computedDigest = Convert.ToHexString(SHA256.HashData(installerBytes));
        if (!ChecksumFile.Matches(recordedDigest, computedDigest))
        {
            return UpdateExecutionOutcome.ChecksumMismatch;
        }

        // Named from the parsed version, never update.Tag (ADR-0010 D3): ReleaseVersion.TryParse
        // truncates at the first '-' or '+', so only the bounded integers — never a raw tag's
        // traversal segments — ever reach this path.
        var fileName = ReleaseDownloadUrl.TempFileName(update.Tag)
            ?? throw new InvalidOperationException(
                "UpdateCheck.Evaluate already parsed this tag; TempFileName cannot fail here.");
        var installerPath = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllBytesAsync(installerPath, installerBytes, cancellation).ConfigureAwait(false);

        _launcher.Launch(installerPath);
        _onLaunched();
        return UpdateExecutionOutcome.Launched;
    }
}
