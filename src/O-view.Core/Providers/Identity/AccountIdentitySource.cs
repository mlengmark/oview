using System.Text.Json;
using OView.Core.Models;

namespace OView.Core.Providers;

/// <summary>
/// Reads the detail window's account block from Claude Code's own <c>~/.claude.json</c>
/// cache, <c>oauthAccount</c> (ADR-0005 D7, ADR-0008 D9e, gate G7). Not a credential reader:
/// by contract it opens only <see cref="AccountIdentity.DisplayName"/>'s,
/// <see cref="AccountIdentity.EmailAddress"/>'s and <see cref="AccountIdentity.OrganizationType"/>'s
/// three keys — <c>displayName</c>, <c>emailAddress</c> and <c>organizationType</c> — and no other
/// key in the file, token fields included.
///
/// <para><b>The tier is <c>organizationType</c> and nothing else.</b> The same block also carries
/// <c>seatTier</c> and <c>userRateLimitTier</c>; this reader never looks at either, because a live
/// read of the artefact (OVI-636) confirms OVI-584's evidence table: both are <c>null</c> in
/// practice, while <c>organizationType</c> carries the real badge value (e.g. <c>"claude_max"</c>).
/// </para>
/// </summary>
public sealed class AccountIdentitySource : IAccountIdentitySource
{
    private const string FileName = ".claude.json";
    private const string PropertyName = "oauthAccount";

    private readonly IReadOnlyList<string> _candidateRoots;

    /// <param name="candidateRoots">
    /// The <c>.claude.json</c> search roots — typically
    /// <see cref="ClaudeDataRoots.ClaudeCliConfigRoots"/>'s result for the current host.
    /// Injected rather than resolved here, so this type never reads the real operating system
    /// or environment (ADR-0005 D5).
    /// </param>
    public AccountIdentitySource(IReadOnlyList<string> candidateRoots)
    {
        _candidateRoots = candidateRoots;
    }

    /// <summary>
    /// Returns the identity from the first candidate root that carries a usable
    /// <c>oauthAccount</c> object, in <paramref name="candidateRoots"/>'s own order (most-canonical
    /// first) — or <see cref="AccountIdentity.Unavailable"/> when none has one. Every failure — a
    /// missing directory, an unreadable file, a permission error, malformed JSON, an absent or
    /// non-object <c>oauthAccount</c> — is silently equivalent to "no identity here"; none of them
    /// ever surfaces as an exception.
    /// </summary>
    public AccountIdentity GetIdentity()
    {
        foreach (var root in _candidateRoots)
        {
            var identity = SafeReadFrom(Path.Combine(root, FileName));
            if (identity is not null)
            {
                return identity;
            }
        }

        return AccountIdentity.Unavailable;
    }

    /// <summary>
    /// Parses <c>oauthAccount</c> out of a <c>.claude.json</c> document, or null when the key is
    /// absent or not an object. Reads exactly three string properties and nothing else in the
    /// object; a missing or non-string property reads as <c>null</c>, never a guess.
    /// </summary>
    internal static AccountIdentity? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty(PropertyName, out var account) ||
            account.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AccountIdentity(
            ReadString(account, "displayName"),
            ReadString(account, "emailAddress"),
            ReadString(account, "organizationType"),
            UsageValueStatus.Real);
    }

    private static string? ReadString(JsonElement account, string propertyName) =>
        account.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// One file: null when it is missing, unreadable, or malformed — all three are "this
    /// candidate cannot answer", and the caller's next candidate can. Opened with
    /// <see cref="FileShare.ReadWrite"/> because Claude Code may rewrite this file while it is
    /// being read, the same reason <c>CachedUtilization</c> opens its copy that way.
    /// </summary>
    private static AccountIdentity? SafeReadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
