namespace OView.Core.Updates;

/// <summary>
/// Whether a URL from the release feed is one this app will fetch from, and the safe name to
/// save that fetch under.
///
/// <para><b>Why the feed's own answer is not enough.</b> <c>browser_download_url</c> arrives
/// inside the JSON, and the app acts on it by downloading bytes and — on Windows — executing
/// them. Nothing in <see cref="UpdateCheck"/> constrains where it points: a release whose
/// asset is named <c>O-view-Setup.exe</c> but whose URL is an attacker's host would be
/// fetched and run. That makes the URL, not just the file, a trust decision.</para>
///
/// <para>This does not replace TLS or the checksum ( <see cref="ChecksumFile"/> ); it is the
/// cheap outer check that keeps the other two pointed at GitHub. A compromised release can
/// still publish a bad asset on a legitimate host — that is what provenance attestation is
/// for, and it is not this (ADR-0010 D3).</para>
///
/// <para>Ported from the source repository's <c>O-view.Core/Updates/ReleaseDownloadUrl.cs</c>
/// (ADR-0010 D3) with no behaviour change to <see cref="IsTrusted"/>. <see cref="TempFileName"/>
/// is new here: the source built the download's temp file name at the call site in
/// <c>UpdateService</c>, interpolating the already-parsed <see cref="ReleaseVersion"/> rather
/// than the raw tag — the fix for the source's own confirmed path-traversal bug (ADR-0009
/// amendment 2026-08-18, restated in ADR-0010 D3). Moving that one line into Core alongside
/// the host check keeps both of this slice's trust decisions in one pure, tested place.</para>
/// </summary>
public static class ReleaseDownloadUrl
{
    /// <summary>
    /// Hosts GitHub serves release assets from. <c>github.com</c> issues the redirect;
    /// the object stores are where it lands.
    /// </summary>
    private static readonly string[] AllowedHosts =
    [
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    /// <summary>
    /// True only for an absolute <c>https</c> URL on a known GitHub host. Anything else —
    /// a different scheme, a bare path, a look-alike host, a userinfo trick — is false.
    /// </summary>
    public static bool IsTrusted(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return false;
        }

        // A populated userinfo section is how "https://github.com@evil.example/x" reads as
        // GitHub to a human and as evil.example to a fetcher. Uri.Host already resolves
        // that correctly, so this is belt and braces — and it costs one comparison.
        if (uri.UserInfo.Length > 0)
        {
            return false;
        }

        // Host comparison is case-insensitive because DNS is, and invariant because a host
        // is not culture-sensitive text. Exact match, never EndsWith: "notgithub.com" ends
        // with neither, but "evil-github.com" would pass a careless suffix test.
        foreach (var host in AllowedHosts)
        {
            if (string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The file name a downloaded installer is saved under, or null when <paramref name="tag"/>
    /// carries no usable version at all.
    ///
    /// <para><b>Built from the parsed version, never the raw tag.</b>
    /// <see cref="ReleaseVersion.TryParse"/> truncates at the first <c>-</c> or <c>+</c>, so a
    /// tag of <c>v9.9.9-../../../../Startup/evil</c> parses cleanly to version <c>9.9.9</c> —
    /// a tag string is attacker-controlled free text and a version is three bounded integers,
    /// and only the integers reach this name. Interpolating the tag directly, as the source
    /// repository once did, let the traversal segments ride along into
    /// <c>Path.Combine</c> and decide where a downloaded executable landed. This method makes
    /// that the only route: there is no parameter here through which the raw tag can reach a
    /// file name.</para>
    ///
    /// <para>Returns a bare file name, not a path — Core holds no temp directory, no
    /// <c>Path.GetTempPath()</c>, and no other OS-specific location; the head combines this
    /// with whatever directory it owns (ADR-0001: no platform-imposed paths in Core).</para>
    /// </summary>
    public static string? TempFileName(string? tag)
    {
        if (!ReleaseVersion.TryParse(tag, out var version))
        {
            return null;
        }

        var installer = ReleaseAssets.WindowsInstallerName;
        var extension = Path.GetExtension(installer);
        var stem = installer[..^extension.Length];
        return $"{stem}-{version}{extension}";
    }
}
