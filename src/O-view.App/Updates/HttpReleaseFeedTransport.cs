using System.Net.Http;
using System.Net.Http.Headers;

namespace OView.App.Updates;

/// <summary>
/// The one real <see cref="IReleaseFeedTransport"/> implementation: fetches
/// <c>releases/latest</c> over HTTP. Platform-neutral — Windows and Linux both use this same
/// fetch (ADR-0007 D3 names it "not a per-OS mechanism like single-instance or startup
/// registration"), unlike <c>IAppTimer</c>'s per-skin concrete implementations.
/// </summary>
public sealed class HttpReleaseFeedTransport : IReleaseFeedTransport, IDisposable
{
    public const string LatestReleaseApi =
        "https://api.github.com/repos/mlengmark/O-view/releases/latest";

    private readonly HttpClient _http;

    public HttpReleaseFeedTransport()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
            // Bounds how long the read may take, not how much it may read. A release payload
            // is tens of KB, so this is ample headroom and still caps what a hostile response
            // could make a days-long process buffer.
            MaxResponseContentBufferSize = 4 * 1024 * 1024,
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("O-view", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation)
    {
        using var response = await _http.GetAsync(LatestReleaseApi, cancellation).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);

        return new ReleaseFeedResponse(
            (int)response.StatusCode,
            Header(response, "x-ratelimit-remaining"),
            Header(response, "x-ratelimit-reset"),
            Header(response, "retry-after"),
            body);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    public void Dispose() => _http.Dispose();
}
