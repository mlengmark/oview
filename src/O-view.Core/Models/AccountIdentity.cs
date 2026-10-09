namespace OView.Core.Models;

/// <summary>
/// The account identity shown beside the detail window's header (ADR-0008 D9e). Read from
/// Claude Code's own <c>~/.claude.json</c> cache (<c>oauthAccount</c>), not a credential: only
/// the display name, email address and organization type are relayed, never a token
/// (ADR-0005 D7).
///
/// <para><see cref="DisplayName"/> and <see cref="EmailAddress"/> are relayed verbatim, never
/// reworded. <see cref="OrganizationType"/> is the vendor's own token
/// (<c>oauthAccount.organizationType</c>) relayed as-is — mapping it to a badge word is
/// wording, so it is a skin's call, and an unrecognised token renders verbatim, the same rule
/// <see cref="ModelUsageRow.ModelId"/> already carries. There is deliberately no <c>Tier</c>
/// enum here: an enum must decide what an unknown value becomes, and every answer to that is
/// either a fabricated tier or a display string in Core.</para>
/// </summary>
public sealed record AccountIdentity(
    string? DisplayName,
    string? EmailAddress,
    string? OrganizationType,
    UsageValueStatus Status)
{
    /// <summary>The canonical "no data" identity — every value unavailable, not a guess.</summary>
    public static AccountIdentity Unavailable { get; } = new(
        null,
        null,
        null,
        UsageValueStatus.Unavailable);
}
