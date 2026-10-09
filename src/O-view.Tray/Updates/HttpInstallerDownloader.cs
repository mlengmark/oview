using System.IO;
using System.Net.Http;

namespace OView.Tray.Updates;

/// <summary>
/// The one real <see cref="IInstallerDownloader"/>: plain HTTP GET, same bounded buffer
/// discipline as <see cref="OView.App.Updates.HttpReleaseFeedTransport"/>. Any non-success
/// status or transport exception becomes null rather than throwing — the executor is what
/// decides whether "could not download" is a refusal, not this class.
/// </summary>
public sealed class HttpInstallerDownloader : IInstallerDownloader, IDisposable
{
    private readonly HttpClient _http;

    public HttpInstallerDownloader()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
            MaxResponseContentBufferSize = 200 * 1024 * 1024,
        };
    }

    public async Task<byte[]?> DownloadAsync(string url, CancellationToken cancellation)
    {
        try
        {
            using var response = await _http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
