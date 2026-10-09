namespace OView.Tray.Updates;

/// <summary>
/// Fetches one URL's bytes for <see cref="WindowsUpdateExecutor"/>: both the installer asset
/// and the <c>SHA256SUMS</c> manifest travel through this same seam, so a fake drives every
/// D3 refusal case in tests with no network and no real download.
/// </summary>
public interface IInstallerDownloader
{
    /// <summary>
    /// The bytes at <paramref name="url"/>, or null on any failure — a bad status code, a
    /// dropped connection, a timeout. Null is "could not get it", never an empty success;
    /// the caller treats it as a refusal, the same fail-closed shape as
    /// <see cref="OView.Core.Updates.ChecksumFile.DigestFor"/>.
    /// </summary>
    Task<byte[]?> DownloadAsync(string url, CancellationToken cancellation);
}
