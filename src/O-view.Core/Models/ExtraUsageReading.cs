namespace OView.Core.Models;

/// <summary>
/// Whether extra usage is switched on for this account, as relayed from Claude Code's own
/// cache (ADR-0001, OVI-168). Lives on <see cref="UsageSnapshot.ExtraUsage"/> — an account
/// setting, not a 31-day statistic — and is nullable there: <c>null</c> means the cache did
/// not say, or said something Core does not understand.
/// </summary>
/// <param name="State">The resolved setting.</param>
/// <param name="FetchedAtUtc">
/// When Claude Code last refreshed this cached setting. <b>Required, not nullable</b>: a
/// relayed setting Core cannot stamp is one Core must not hand over at all — this is a cached
/// answer from another application that can be days old while looking current, and relaying
/// it unstamped would be a guess wearing a fact's clothes. Provenance for the setting itself,
/// not for O-view's own ingest — distinct from <see cref="UsageSnapshot.LastIngestAt"/>, the
/// same role the contract's (not yet implemented) <c>BoostNoticesFetchedAtUtc</c> field plays
/// for the promo notice.
/// </param>
public sealed record ExtraUsageReading(ExtraUsageState State, DateTimeOffset FetchedAtUtc);
