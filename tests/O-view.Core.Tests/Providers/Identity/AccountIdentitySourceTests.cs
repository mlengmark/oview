using OView.Core.Models;
using OView.Core.Providers;

namespace OView.Core.Tests.Providers.Identity;

/// <summary>
/// Proves <see cref="AccountIdentitySource"/> against the documented shape of
/// <c>~/.claude.json</c> -&gt; <c>oauthAccount</c> (ADR-0005 D7, gate G7): well-formed file ->
/// name/email/tier populated from <c>organizationType</c> alone; missing or malformed file ->
/// <see cref="AccountIdentity.Unavailable"/>; <c>seatTier</c>/<c>userRateLimitTier</c> never
/// read.
/// </summary>
public sealed class AccountIdentitySourceTests : IDisposable
{
    private readonly string _root;

    public AccountIdentitySourceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi636-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void GetIdentityReturnsUnavailableWhenNoCandidateRootHasTheFile()
    {
        var source = new AccountIdentitySource(new[] { _root });

        var identity = source.GetIdentity();

        Assert.Equal(AccountIdentity.Unavailable, identity);
    }

    [Fact]
    public void GetIdentityReturnsUnavailableWhenTheFileIsMalformedJson()
    {
        WriteClaudeJson("not json at all");

        var identity = new AccountIdentitySource(new[] { _root }).GetIdentity();

        Assert.Equal(AccountIdentity.Unavailable, identity);
    }

    [Fact]
    public void GetIdentityReturnsUnavailableWhenOauthAccountIsAbsent()
    {
        WriteClaudeJson("{ \"otherKey\": true }");

        var identity = new AccountIdentitySource(new[] { _root }).GetIdentity();

        Assert.Equal(AccountIdentity.Unavailable, identity);
    }

    [Fact]
    public void GetIdentityPopulatesNameEmailAndTierFromOrganizationTypeAlone()
    {
        // Shape confirmed against a live ~/.claude.json read during this slice (OVI-636):
        // seatTier and userRateLimitTier are both null in practice; organizationType carries
        // the real tier token.
        WriteClaudeJson("""
        {
          "oauthAccount": {
            "displayName": "Maximilian",
            "emailAddress": "maximilianclister@gmail.com",
            "organizationType": "claude_max",
            "seatTier": "this-must-never-be-read",
            "userRateLimitTier": "this-must-never-be-read-either"
          }
        }
        """);

        var identity = new AccountIdentitySource(new[] { _root }).GetIdentity();

        Assert.Equal("Maximilian", identity.DisplayName);
        Assert.Equal("maximilianclister@gmail.com", identity.EmailAddress);
        Assert.Equal("claude_max", identity.OrganizationType);
        Assert.Equal(UsageValueStatus.Real, identity.Status);
    }

    [Fact]
    public void GetIdentityLeavesTierNullWhenOrganizationTypeIsAbsentEvenIfSeatTierIsPresent()
    {
        // seatTier is never read, even when it is the only tier-shaped field in the file.
        WriteClaudeJson("""
        {
          "oauthAccount": {
            "displayName": "Maximilian",
            "emailAddress": "maximilianclister@gmail.com",
            "seatTier": "enterprise"
          }
        }
        """);

        var identity = new AccountIdentitySource(new[] { _root }).GetIdentity();

        Assert.Null(identity.OrganizationType);
    }

    [Fact]
    public void GetIdentityTriesLaterCandidateRootsWhenEarlierOnesHaveNoFile()
    {
        var secondRoot = Path.Combine(Path.GetTempPath(), "ovi636-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secondRoot);
        try
        {
            File.WriteAllText(
                Path.Combine(secondRoot, ".claude.json"),
                """{ "oauthAccount": { "displayName": "Second Root", "organizationType": "claude_pro" } }""");

            var identity = new AccountIdentitySource(new[] { _root, secondRoot }).GetIdentity();

            Assert.Equal("Second Root", identity.DisplayName);
            Assert.Equal("claude_pro", identity.OrganizationType);
        }
        finally
        {
            Directory.Delete(secondRoot, recursive: true);
        }
    }

    private void WriteClaudeJson(string contents) =>
        File.WriteAllText(Path.Combine(_root, ".claude.json"), contents);
}
