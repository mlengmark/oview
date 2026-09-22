namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for <c>PanelTextFormatter.RateLimitedNotice</c>
/// (OVI-98, Phase 1 slice 3.4). Kept as its own type, parallel to <see cref="GoldenMasterFixture"/>,
/// <see cref="FreshnessFixture"/>, and <see cref="BoostNoticeFixture"/> rather than a change
/// to any of them: <c>RateLimitedNotice</c> is a single fixed two-scalar shape
/// (<c>DateTimeOffset?</c>, <c>TimeZoneInfo</c>) that does not take a <see cref="OView.Core.Models.UsageSnapshot"/>
/// at all and does not share <see cref="PanelTextResetFixture"/>'s multi-member <c>Render</c>-closure
/// shape either — it is the only member this family covers. Reuses <see cref="ContentFact"/> as-is.
/// </summary>
public sealed record RateLimitedNoticeFixture(
    string Name,
    DateTimeOffset? RetryAfterUtc,
    TimeZoneInfo Local,
    IReadOnlyList<ContentFact> ContentFacts);
