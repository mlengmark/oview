namespace OView.Core.Models;

/// <summary>
/// A promo notice for one usage meter — the 5-hour session window or the 7-day weekly
/// window — relayed from Claude Code's own promo-flag cache
/// (<c>cachedGrowthBookFeatures.tengu_rate_limit_promo_notices</c>). ADR-0001's contract
/// carries one of these per meter, nullable (the future <c>SessionBoostNotice</c>/
/// <c>WeeklyBoostNotice</c> fields) — which field a notice arrives in already says which
/// meter it is for, so unlike the source app's own <c>BoostNotice</c> type, this record
/// drops the source's <c>Bar</c> selector key entirely (ADR-0001, 2026-09-21 amendment).
///
/// <para><see cref="Text"/> is always relayed verbatim — never edited, summarised, or
/// re-worded by Core or by a skin. Whether a promo applies to this account is something
/// O-view cannot itself verify (the payload is a feature-flag cache, evaluated
/// server-side), so every layer relays the claim rather than asserting it (the "never
/// fabricate a number" discipline, applied to a claim rather than a figure).
/// <see cref="Percent"/> and <see cref="EndsOn"/> are independently nullable; their absence
/// is itself a real fact the source sentence didn't state, not a separate unavailable
/// state.</para>
///
/// <para><see cref="EndsOn"/> is a bare calendar date, not a timestamp, by contract. What
/// local midnight means on the promo's last day is a skin-owned computation (ADR-0001's
/// 2026-09-21 amendment) — the same family of platform/locale fact this contract already
/// keeps out of Core everywhere else (the 127-character tooltip cap, the 281px panel-width
/// budget, every locale-bound date/time format).</para>
/// </summary>
public sealed record BoostNotice(string Text, int? Percent, DateOnly? EndsOn);
