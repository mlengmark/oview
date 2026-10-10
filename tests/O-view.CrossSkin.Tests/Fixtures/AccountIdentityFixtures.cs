using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="AccountIdentityFixture"/>s (ADR-0003, ADR-0008 D9e, gate G7
/// parity slice P8, OVI-645). Add a new fixture as a new entry in <see cref="All"/>, following
/// <c>FreshnessFixtures</c>' existing pattern.
/// </summary>
public static class AccountIdentityFixtures
{
    /// <summary>An ordinary known identity — both skins must relay the name, email and tier
    /// token verbatim.</summary>
    public static readonly AccountIdentityFixture KnownIdentity = new(
        Name: "known-identity",
        Account: new AccountIdentity("Jane Doe", "jane@example.com", "claude_max", UsageValueStatus.Real),
        ContentFacts: new[]
        {
            ContentFact.Contains("Jane Doe"),
            ContentFact.Contains("jane@example.com"),
            ContentFact.Contains("claude_max"),
        });

    /// <summary>
    /// An organization type Core read but neither skin recognises — relayed verbatim, the same
    /// unrecognised-token rule <see cref="ModelUsageRow.ModelId"/> already carries, never
    /// mapped to a friendlier word and never dropped silently.
    /// </summary>
    public static readonly AccountIdentityFixture UnrecognisedTierToken = new(
        Name: "unrecognised-tier-token",
        Account: new AccountIdentity("Jane Doe", "jane@example.com", "some_future_tier", UsageValueStatus.Real),
        ContentFacts: new[]
        {
            ContentFact.Contains("some_future_tier"),
        });

    /// <summary>
    /// No identity at all — <see cref="AccountIdentity.Unavailable"/>. Neither skin may guess a
    /// name or email; each renders its own explicit "nothing to show" fallback instead (the
    /// same convention <see cref="OView.CrossSkin.Tests.Fixtures.UsageStatisticsFixture"/>'s own
    /// unavailable case already exercises for tokens/USD).
    /// </summary>
    public static readonly AccountIdentityFixture UnavailableIdentity = new(
        Name: "unavailable-identity",
        Account: AccountIdentity.Unavailable,
        ContentFacts: new[]
        {
            new ContentFact(
                "never renders a guessed email address",
                rendered => !rendered.Contains('@')),
            new ContentFact(
                "states an explicit unavailable fallback, not a blank field",
                rendered => rendered.Length > 0),
        });

    public static IReadOnlyList<AccountIdentityFixture> All { get; } = new[]
    {
        KnownIdentity,
        UnrecognisedTierToken,
        UnavailableIdentity,
    };
}
