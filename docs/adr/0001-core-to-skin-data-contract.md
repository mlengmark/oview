# ADR-0001: The Core-to-skin data contract, as a living document

- **Status:** Accepted — living document; amend in place as contract fields
  are added, and update the "current codebase" column as extraction lands.
- **Date:** 2026-09-08
- **Deciders:** Adrian II the Architect, per board sign-off at gate G1
  (2026-09-08T19:53:12Z)
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`), §3.1
- **Evidence standard:** every claim below is labelled **CONFIRMED** (read
  directly against `github.com/mlengmark/O-view`, by Rae II the Analyst's
  OVI-4 verification pass, 2026-09-08, commit `897777b`) or **INFERRED**
  (reasonable, not independently re-verified for this ADR). No label is
  upgraded from the source material without independent re-verification,
  and none was performed while writing this ADR — all labels below are
  carried forward unchanged from OVI-4 and the PDR.

## Context

The target architecture ([README](../../README.md)) puts exactly one layer
in charge of usage data — Core — and requires every other layer (each
OS-specific "skin") to consume that data only through a documented
contract, never through a pre-formatted string and never by reaching into
Core's storage directly.

That boundary does not exist today in the source repository. Rae II's
OVI-4 verification pass read all four files named by the inherited audit
end to end and **confirmed** that three of them — not just the one
previously confirmed — build user-facing display strings inside
`src/O-view.Core.Models/`, the layer `O-view`'s own `CLAUDE.md` states must
be UI-agnostic ("BCL and SQLite only. No UI, no Win32."):

| File | Confirmed content | Severity |
|---|---|---|
| `TooltipFormatter.cs` | Builds the full tray tooltip string: hardcoded separator (`" · "`), literal fallback copy (`"O-view · local estimate · usage % unknown"`, `"O-view · no usage data"`), locale-sensitive time formats (`HH:mm`, `ddd HH:mm`), and `MaxLength = 127` — a **Windows `NotifyIcon` API limit** — baked into a class this layer's own rules say should not know about any specific platform's UI widget. | Extensive; carries a platform-imposed hard limit |
| `PanelText.cs` | ~805 lines, ~30 constants/methods of user-facing sentences, hover-card copy, and formatting rules for the detail panel, including a hardcoded external URL (`"https://claude.ai/settings/usage"`) and locale-sensitive date/time formats. Lines 278–295 (`BoostChip`) additionally bake a **Windows-WPF-specific 281px panel-width budget** into the string-truncation rule itself — a rendering-surface measurement from one specific UI dictating string content inside the platform-neutral layer. **This file's own header doc comment argues its centralization is deliberate**, citing issues [#55](https://github.com/mlengmark/O-view/issues/55) and [#56](https://github.com/mlengmark/O-view/issues/56) — two panels wording the same figure differently — as the reason. See [ADR-0003](0003-paneltext-anti-drift-mechanism.md) for how this rebuild replaces that guarantee without keeping the centralization. | Largest; deliberate, not accidental |
| `UsageFormatter.cs` | 43 lines. Builds `"$0.00"`-style money strings and `"1.2M"`/`"3.4K"` token abbreviations, plus the literal fallback `"unknown"`. Explicitly culture-pinned (`CultureInfo.InvariantCulture`) rather than following the host OS's locale. | Small but unambiguous |
| `PanelStatistics.cs` | 314 lines, overwhelmingly a legitimate data/computation record (rollup aggregation, pricing, model-slicing) — **not** chiefly a formatter. Exactly one presentation leak: `CoverageNote` (around line 135–136), which formats `"{RecordedDays} of {WindowDays} days recorded"` as a ready-to-display sentence rather than returning the two counts for a skin to phrase itself. | Narrow — one leaked sentence, not a fourth formatter |

This table is **CONFIRMED**, carried forward from OVI-4 without
modification. The contract below is this ADR's answer to it: the shape
Core's output must take once that presentation logic moves to each skin,
stated concretely enough that no skin has to guess a type, a unit, or
whether a value can be trusted.

## Decision

**Adopt the following table as Core's complete output contract.** For
every value Core can hand a skin, the skin is told the type, the unit, and
a status flag — and must never need to guess. The **status flag** is
always exactly one of three values, applied per the standing "never
fabricate a number" rule: **real** (measured from a vendor source),
**estimated** (derived/modelled, e.g. from token pricing or a fallback
source), or **unavailable** (no source produced a value — the skin must
render this as an explicit gap, never as zero or blank).

| Value | Type | Unit | Status flag | Notes |
|---|---|---|---|---|
| `SessionUtilizationPercent` | float64, 0–100 | percent | real / estimated / unavailable | 5-hour rolling window |
| `SessionResetAt` | timestamp, UTC, ISO-8601 | instant | real / estimated / unavailable | skin owns locale/format rendering. `estimated` **is** the "approximate" signal: Core sets it when the reset was derived from a sampling bracket wider than Core's precision threshold, and a skin marks the value approximate from this flag alone — no uncertainty width or threshold crosses the contract; amended 2026-09-25 (OVI-135, D2) — see that amendment below |
| `WeeklyUtilizationPercent` | float64, 0–100 | percent | real / estimated / unavailable | 7-day rolling window |
| `WeeklyResetAt` | timestamp, UTC, ISO-8601 | instant | real / estimated / unavailable | the reported instant (source repo ADR-0014), projected forward in whole weeks, when Core has one; else the user's entered reset — see `WeeklyResetSource`. No source path produces `estimated` for this field (the derivation was deleted by source ADR-0014); amended 2026-09-25 (OVI-135, D3) |
| `WeeklyResetSource` | enum {`CachedExact`, `UserEntered`} | — | real | provenance of the `WeeklyResetAt` value in this same snapshot — not of whether an entry exists. `Derived` retired and `UserEntered` added 2026-09-25 (OVI-135, D3); a skin must say so when it renders a `UserEntered` value (source repo issue #186) |
| `WeeklyResetUserEntry` | `{day: weekday enum Mon–Sun, localTime: time-of-day HH:mm}?` (nullable) | wall-clock, reader's local zone | real / unavailable | the weekly reset the user typed in, read off Claude's own Settings → Usage. Deliberately **not** a UTC instant: the user entered a wall-clock time, and only a wall-clock time survives a daylight-saving change. `null` with `real` = nothing entered; added 2026-09-25 (OVI-135, D3) |
| `WeeklyResetConflict` | `{reportedAtUtc: timestamp, UTC, ISO-8601}?` (nullable) | instant | real | non-null when **Core** has detected that a reported reset disproves the user's entry by more than Core's contradiction slack (a Core policy value, not a contract field). Core has already set the entry aside when this is non-null; the skin decides how, and how often, to say so; added 2026-09-25 (OVI-135, D3) |
| `UsageLevel` | enum {`Green`, `Amber`, `Red`} | — | real (or unavailable if inputs are) | threshold bands; derivation stays in Core |
| `DataSourceKind` | enum {`Live`, `Stale`, `JsonlFallback`, `Estimate`, `Unavailable`} | — | real | tells the skin which confidence tier produced the other values in this snapshot; `Stale` added 2026-09-09 (OVI-16) — authoritative data older than a per-provider freshness threshold, still trusted above `JsonlFallback`/`Estimate` but no longer current; see the OVI-16 amendment below for source behaviour and migration note |
| `AccountDisplayName` | string | — | real / unavailable | from account cache |
| `AccountEmail` | string | — | real / unavailable | from account cache |
| `AccountPlanTier` | string (vendor-defined) | — | real / unavailable | the field is `oauthAccount.organizationType`, not the emptier-looking `seatTier`/`userRateLimitTier` (source repo `CLAUDE.md` rule 8) |
| `OutputTokensToday` | int64 | tokens | real / estimated / unavailable | |
| `OutputTokensWindow31d` | int64 | tokens | real / estimated / unavailable | |
| `EstimatedSpendToday` | decimal | USD, 2dp | estimated / unavailable | spend is always modelled from token pricing — never labelled `real` |
| `EstimatedValueWindow31d` | decimal | USD, 2dp | estimated / unavailable | |
| `TokenComposition.Input/.Output/.Cache` | int64 each | tokens | real / estimated / unavailable | |
| `ModelBreakdown[]` | array of `{modelId: string, tokens: int64, spend: decimal}` | tokens, USD | real / estimated / unavailable per entry | |
| `UsageHistorySeries[]` | array of `{date: ISO-8601 date, utilizationPercent: float64 \| null}` | percent | real / estimated / unavailable per point | 31-day graph; `null` = no sample that day |
| `HistoryCoverage.RecordedDays` | int32 | days | real | replaces the leaked `PanelStatistics.CoverageNote` sentence — Core hands the two counts, the skin writes the sentence |
| `HistoryCoverage.WindowDays` | int32 | days | real | |
| `OffPlanUsageAmount` | int64 (tokens) or decimal (USD) | tokens or USD | estimated / unavailable | |
| `NotificationThresholdCrossed` | `{crossed: bool, thresholdPercent: int32}` | percent | real | Core detects the crossing event; the skin decides how (or whether) to notify |
| `LastIngestAt` | timestamp, UTC, ISO-8601 | instant | real | freshness diagnostic; not necessarily surfaced by every skin. Serves the same role as the source repo's `UsageSnapshot.CapturedAtUtc` — the capture time a skin would read to word a `Stale` value's age (OVI-16 amendment below); the freshness *threshold* itself stays provider-owned config, not a contract field |
| `SessionBoostNotice` | `BoostNotice?` (nullable; see row below) | — | real / unavailable | the promo notice, if any, for the 5-hour meter; added 2026-09-21 (OVI-82) — see that amendment below |
| `WeeklyBoostNotice` | `BoostNotice?` (nullable; see row below) | — | real / unavailable | the promo notice, if any, for the 7-day meter; added 2026-09-21 (OVI-82) |
| `BoostNotice` (nested type) | `{text: string, percent: int32?, endsOn: date, ISO-8601 (YYYY-MM-DD)?}` | — | fields inherit the parent `Session`/`WeeklyBoostNotice` field's real/unavailable status | `text` is the promo sentence, relayed verbatim, never reworded, truncated, or re-punctuated by Core; `percent`/`endsOn` are independently nullable and their absence is itself a real fact (the source sentence didn't state one), not a separate unavailable state — added 2026-09-21 (OVI-82) |
| `BoostNoticesFetchedAtUtc` | timestamp, UTC, ISO-8601 | instant | real / unavailable | when the upstream promo-flag cache was last refreshed; distinct from `LastIngestAt` (O-view's own ingest time) — this is provenance for the promo message itself, needed to word how recently it was read; added 2026-09-21 (OVI-82) |
| `UnpricedModels` | `UnpricedModels` (nested type; see row below) | — | real / unavailable | vendor model ids seen in the 31-day window that Core could not price, relayed verbatim and never reworded; added 2026-09-23 (OVI-100) — see that amendment below |
| `UnpricedModels` (nested type) | `{modelIds: string[], status}` | — | real / unavailable | an **empty** list is `real` ("Core priced everything in the window"), not `unavailable`; `unavailable` means Core could not establish the set at all. A **non-empty** list changes what the neighbouring `EstimatedValueWindow31d` means — that figure is then a priced *subtotal*, not a total, and a skin must not render it as a complete figure without saying so; added 2026-09-23 (OVI-100) |
| `TtlUnrecordedCacheWritesWindow31d` | `TokenCount` (the existing int64 + status type — no new type) | tokens | real / unavailable | cache-write tokens in the 31-day window whose TTL Core never recorded, so they could only be priced under an assumption; `0` is a **real** value meaning "nothing to qualify", not an absent one; added 2026-09-23 (OVI-100) |
| `Rates` | `RateCardStamp?` (nullable; see row below) | — | real / unavailable | which rate table priced the `Estimated*` figures **in this same snapshot** — carried beside them so a skin cannot caveat one set of figures with another set's provenance; added 2026-09-23 (OVI-100) |
| `RateCardStamp` (nested type) | `{source: RateCardSource?, asOf: date, ISO-8601 (YYYY-MM-DD)?, isStale: bool}` | — | fields inherit the parent `Rates` field's real/unavailable status | `isStale` is decided **by Core**, against the same "today" the figures beside it were built for — a skin must never re-derive it, or the caveat can end up qualifying a different day from the figures it sits under. The staleness *threshold* stays Core policy, not a contract field (same rule as `LastIngestAt`'s freshness threshold). `asOf` is a bare calendar date, never a formatted one; added 2026-09-23 (OVI-100) |
| `RateCardSource` | enum {`Bundled`, `UserFile`} | — | real | where the rate table came from — an enum, never a display label: `"bundled"`/`"user file"` are skin wording. `UserFile` is carried forward though unimplemented, because the moment it exists the panel must name it (source repo issue [#255](https://github.com/mlengmark/O-view/issues/255)); added 2026-09-23 (OVI-100) |
| `UpdateCheckOutcome` | enum {`UpToDate`, `UpdateAvailable`, `Unknown`, `RateLimited`} | — | real | what the shared update check concluded. `Unknown` ("could not tell") is never collapsed into `UpToDate`, and `RateLimited` is never collapsed into `Unknown` (source repo issue #176). Produced by the shared, platform-neutral layer, not by a skin; added 2026-09-25 (OVI-135, D4) — see that amendment below |
| `UpdateRetryAfterUtc` | timestamp, UTC, ISO-8601, nullable | instant | real / unavailable | when GitHub's rate limit lifts, for `RateLimited` only. `unavailable` = GitHub sent no usable header; a skin must then say it does not know, never invent a time. The value `RateLimitedNotice` already takes as its raw `retryAfterUtc` argument; added 2026-09-25 (OVI-135, D4) |

**What Core must never emit, by contract:** a pre-formatted display
sentence, a field separator, a locale-bound date/time string, or any
platform-imposed length or pixel limit. The 127-character tray cap
(`TooltipFormatter.MaxLength`) and the 281px panel-width budget
(`PanelText.BoostChip`) are Windows-skin-owned facts under this contract,
not Core facts — see [ADR-0003](0003-paneltext-anti-drift-mechanism.md)
for how each skin takes over that responsibility without losing the
consistency `PanelText.cs` was providing.

**What a skin owns, independently of every other skin, once this contract
is the only channel between them:** exact wording, phrasing, and
formatting of everything the user reads (including any length limits — the
127-char tooltip cap should exist only in the Windows skin); the tray/menu-
bar icon's rendering; the detail window's layout, animation, and
positioning; and every OS integration point
([ADR-0002](0002-cross-platform-capability-matrix.md)). A skin talks to
Core only through this table — never into Core's storage directly — and
Core never imports anything from a skin.

## Current codebase status — CONFIRMED, as of commit `897777b` (2026-09-07)

This section is the "living" half of this ADR: it states where the source
repository stands against the target contract *today*, not where the
target design wants it to end up. Update this section (with a dated note,
not a silent rewrite) as extraction work actually lands.

- **2026-09-10 update — `UsageFormatter.cs` and `PanelStatistics.cs`'s one
  presentation leak extracted in this repository (Kit the Builder, Phase 1
  slice 2, OVI-27).** `O-view.Core.Models` here now defines four new
  contract types instantiating rows this table already listed:
  `TokenCount` (`OutputTokensToday`, `OutputTokensWindow31d`), `EstimatedUsd`
  (`EstimatedSpendToday`, `EstimatedValueWindow31d`), and `HistoryCoverage`
  (`RecordedDays`, `WindowDays`), composed into a new `UsageStatistics`
  record — the sibling of `UsageSnapshot` for this slice of the contract,
  scoped to exactly what these two files' presentation logic needs, no more.
  `HistoryCoverage.RecordedDays`/`WindowDays` replace `PanelStatistics`'s
  leaked `CoverageNote` sentence exactly as this table already specified;
  Core computes the two counts (and `HasPartialHistory`, a comparison, not
  a display string), never the sentence. String construction — the ~180px
  panel-tile-width K/M abbreviation threshold, the `"$"` prefix, the
  `"unknown"`/`"n/a"` fallback text, and the coverage-caveat sentence
  itself — lives entirely in each skin's own `Presentation/UsageFormatter.cs`
  and `Presentation/PanelStatisticsFormatter.cs` (`O-view.Tray` and
  `O-view.Linux`, independently), tested against the source app's own
  `UsageFormatterTests.cs`/`PanelStatisticsTests.cs` reference values for
  the Windows skin (`O-view.Tray`, byte-for-byte) and independently-worded
  equivalents for `O-view.Linux` (per ADR-0003 — content facts must match,
  exact wording need not). `PanelText.cs` remains **not yet extracted**,
  per this slice's explicit scope boundary (it is a separate, later,
  higher-risk slice — issues [#55](https://github.com/mlengmark/O-view/issues/55)/[#56](https://github.com/mlengmark/O-view/issues/56)).
  See [ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s 2026-09-10 (OVI-27)
  amendment for how the golden-master harness was extended to cover this
  slice's figures.
- **2026-09-09 amendment — `Stale`/staleness-age resolved as add-now, not deferred
  (Adrian II the Architect, OVI-16, triggered by a gap Quinn found in OVI-11's
  review of the OVI-10 extraction).** The source repository has a fourth
  `DataSource` tier this table's original `DataSourceKind` (from OVI-9) did not
  carry. Re-verified directly against the source repo at the same pinned commit
  this ADR already cites, `897777b` — all claims below are **CONFIRMED** by
  fetching and, for two files, byte-diffing against copies already pulled for
  OVI-11:
  - `DataSource` (`src/O-view.Core/Models/DataSource.cs`) is a 4-value enum —
    `None`, `Estimate`, `Stale`, `Live` — not the 3 tiers this table's `Estimate`/
    `Unavailable`-flavoured original implied. `Stale`'s own doc comment:
    "Authoritative data, but older than the freshness threshold. Label with its
    age."
  - The freshness threshold is **provider-owned, not global, and has already
    changed once**: `PlanHistoryProvider.DefaultFreshness` (the primary usage
    provider, ADR-0007-equivalent) is `TimeSpan.FromMinutes(16)`, calibrated
    against 1,443 measured sampling gaps. It was 11 minutes before 2026-08-10,
    when Claude Desktop's own sampling cadence changed from 5 to 15 minutes —
    the source project measured and re-tuned the threshold rather than hardcode
    it once (source `PlanHistoryProvider.cs`, confirmed byte-identical to the
    live file at that path).
  - **Two source renderers disagree on purpose**, confirmed from two files:
    the tray tooltip (`TooltipFormatter.Format`, `src/O-view.Core/Models/TooltipFormatter.cs`)
    branches explicitly — `Live` gets no suffix, `Stale` gets
    `" (as of HH:mm)"` (local time, from `CapturedAtUtc`) or, if no capture
    time is available, the literal fallback `" (stale)"`. The detail panel
    (`PanelText.Freshness`, `src/O-view.Core/Models/PanelText.cs`, confirmed
    byte-identical to the live file) **deliberately collapses `Live` and
    `Stale` into the same text** — `"As of {age}"` either way — per its own doc
    comment: a capture-time age says how old a reading is more precisely than
    the Live/Stale split it replaces, and both tiers are authoritative either
    way. Only `Estimate` gets different wording (`"Local estimate · as of
    {age}"`). So "how `Stale` reads to the user" is not one fixed answer even
    in the source app — it is a per-surface skin decision, confirming this
    contract should carry the tier as data and leave the wording, including
    whether to word it at all, to each skin (consistent with this ADR's
    existing "what a skin owns" section).
  - `RateCard.StaleAfter` (seen in `PanelText.Caveat`) is a **separate,
    unrelated staleness concept** — pricing-rate age, not usage-data
    freshness. Noted here only so it is not confused with `DataSource.Stale`
    in future extraction work.

  **Decision: add now, not deferred.** `Stale` is added to this table's
  `DataSourceKind` enum today (row above), backed by the existing
  `LastIngestAt` field as the capture-time value a skin needs to word it —
  no new contract field required. Reasoning: this table is documented as
  Core's *complete* target contract (line "Adopt the following table as
  Core's complete output contract"), and every other not-yet-built row in it
  (`ModelBreakdown[]`, `HistoryCoverage`, `LastIngestAt` itself) is already
  carried as aspirational-until-extracted rather than omitted until some
  slice needs it. Treating `Stale` differently — leaving it out of the
  "complete" contract because no slice has reached it yet — would recreate
  exactly the silent-gap problem this ADR's discipline exists to prevent.
  Deferral was rejected for that reason, not because the freshness threshold
  or suffix wording is settled — those remain provider/skin implementation
  detail, out of scope for this contract-shape decision.

  **Migration note — OVI-10/OVI-15's shipped fields do not need to change.**
  This is a purely additive enum case: `UsageSnapshot`, `TooltipFormatter`,
  and their tests (OVI-10, OVI-15) compile and behave unchanged, since
  `TooltipFormatter.Format` branches with `if`/`==`, not an exhaustive
  `switch`, and no provider in this repository emits any `DataSourceKind`
  today (no provider has been ported yet — see below). Nothing shipped is
  reopened by this decision.

  **The real obligation this creates falls on a future slice, named
  explicitly so it isn't missed:** the slice that first ports a real usage
  provider (the `PlanHistoryProvider`/ADR-0007 equivalent — the only source
  of `Stale` in the source app) **must not merge without also teaching every
  consumer that currently branches on `DataSourceKind` to handle `Stale`
  explicitly** — starting with `O-view.Tray`'s `TooltipFormatter`, which
  today has no `Stale` branch and would silently render a stale value
  identically to a `Live` one. That silent fallthrough is fine *today* only
  because nothing can produce `Stale` yet; it becomes a violation of ADR-0002's
  mandatory data-source-labelling rule the moment a provider can. This is a
  gate on that future slice's own review (OVI-11-style), not new work for
  this ADR to schedule.

- **2026-09-09 update — `TooltipFormatter.Format` now reads `UsageValueStatus`
  (Kit the Builder, OVI-15, fixing a blocking finding from Quinn's OVI-11
  review of the OVI-10 extraction).** The initial extraction carried the
  `UsageValueStatus`/`DataSourceKind` fields this table already specifies
  but only branched on value nullness, so an `Estimated` percent or reset
  instant rendered byte-identical to a `Real` one — a regression against
  this ADR's own "never fabricate a number" status-flag rule and the source
  repo's ADR-0002. `Format` now marks any field whose `.Status` is
  `Estimated` with a `~` prefix (e.g. `7d: ~14%`, `resets ~Mon 23:00`); a
  `Real` field renders unmarked, unchanged from the OVI-4 parity string.
  Separately, the `"O-view · local estimate · usage % unknown"` fallback
  copy now fires only when `DataSourceKind` is actually `Estimate`, not
  merely because both percentages happen to be null — a `Live` snapshot
  with nothing sampled yet no longer misdescribes its own provenance. No
  contract field changed shape; this was a consumption bug in the skin, not
  a gap in this table.
- **2026-09-08 update — `TooltipFormatter.cs` extracted in this repository
  (Kit the Builder, Phase 1 slice 1, OVI-10).** `O-view.Core.Models` here now
  defines `UsageSnapshot` carrying exactly the fields this table lists that
  tooltip presentation needs — `SessionUtilizationPercent`,
  `SessionResetAt`, `WeeklyUtilizationPercent`, `WeeklyResetAt`,
  `UsageLevel`, `DataSourceKind` — each percent/instant paired with its
  `UsageValueStatus` (real/estimated/unavailable), no pre-built sentence, no
  separator, no locale-bound format, no length cap. String construction,
  including the 127-character `NotifyIcon` cap, lives entirely in
  `O-view.Tray`'s `Presentation.TooltipFormatter`, tested against the exact
  live tooltip OVI-4 observed (`5h: 57% · resets 20:59 · 7d: 14% · resets
  Mon 23:00`). This is the first field of this table with a real
  implementation on either side of the contract; every other row is still
  aspirational until its own extraction lands. `PanelText.cs`,
  `UsageFormatter.cs`, and `PanelStatistics.cs`'s `CoverageNote` remain
  **not yet extracted**, per this slice's explicit scope boundary.
- **Not yet extracted (source repository).** All four files in the table
  above still live in `src/O-view.Core.Models/` in the source repository —
  this repository's extraction does not change the source repository,
  which remains read-only reference. No skin-owned text-resource layer or
  equivalent exists there yet.
- **`PanelStatistics.cs`'s computation is already contract-shaped.** Its
  rollup aggregation, pricing, and model-slicing logic (`Build`,
  `WithDivergence`, `EstimateTotal`, `SliceByModel`) already produces
  structured data matching this table's intent — only `CoverageNote` needs
  to move.
- **The other three files need to move their string-construction wholesale**
  once each skin has a place to receive `HistoryCoverage`,
  `TokenComposition`, `ModelBreakdown[]`, etc. and turn them into text.
- **Runtime confirmation, narrow but real:** OVI-4 ran the built Windows
  head end-to-end against live data and observed a tooltip reading
  `5h: 57% · resets 20:59 · 7d: 14% · resets Mon 23:00` — a byte-for-byte
  match to what `TooltipFormatter.FormatAuthoritative` constructs from
  exactly the session/weekly percent and reset-time values this contract
  defines. This confirms the *shape* of the target contract's core fields
  is already correct against real data; it does not confirm anything about
  the detail panel, menu, notifications, or the Linux runtime, none of
  which were exercised in that pass.

- **2026-09-11 amendment (OVI-45) — `DataSourceKind.Stale` authorized to
  land in code via OVI-29; `LastIngestAt`'s code-level addition escalated,
  not yet decided (Adrian II the Architect, resolving two design questions
  Kit the Builder correctly stopped on rather than deciding solo).**

  **`DataSourceKind.Stale` — Option A authorized.** The 2026-09-09 amendment
  above decided `Stale` belongs in this table's contract; it did not land in
  `src/O-view.Core/Models/DataSourceKind.cs` — OVI-42 (which relanded this
  amendment's text against current `main` after a first attempt, PR #2,
  closed unmerged) touched only this markdown file, confirmed by
  `git show --stat` against its merge commit. Kit's OVI-29 investigation
  (comment, 2026-09-11T06:09:03Z) confirmed adding the enum member now is
  safe: neither `O-view.Tray` nor `O-view.Linux`'s `TooltipFormatter.cs`
  switches exhaustively over `DataSourceKind` (both branch with `if`/`==`,
  confirmed by grep), and no usage provider exists yet in this repository
  to silently mishandle the new case — matching this ADR's own 2026-09-09
  migration note. **Decision: add `DataSourceKind.Stale` as a small
  additive commit inside OVI-29's own PR**, immediately consumed by
  `PanelText.Freshness`'s Live/Stale-collapsing extraction (the first real
  code to exercise it) rather than landing inert in a separate
  prerequisite task. The original `human_only` confirmation asking this
  (`0c9dbf48`) expired unanswered; this amendment is the authorization
  Kit needs to proceed, in place of that expired interaction.

  **`LastIngestAt` — escalated to Chief Gary II, not decided here.** Kit's
  investigation (comment, 2026-09-11T06:09:03Z) also found `UsageSnapshot`
  (`src/O-view.Core/Models/UsageSnapshot.cs`) carries no capture-time field
  at all today — `LastIngestAt` exists only in this table (row above),
  confirmed by grep against `main`. `Freshness` needs it to word "As of
  {age}". Adding it is a shape change to `UsageSnapshot`'s positional
  record constructor, and it is not cosmetic: **17** existing `new
  UsageSnapshot(...)` call sites already ship on `main` today across
  OVI-10's `O-view.Tray.Tests` (8), the OVI-30 Linux scaffold's
  `O-view.Linux.Tests` (8), and `O-view.Core.Tests` (1) — confirmed by
  `git show origin/main:<path> | grep -c`, counted directly, not inferred.
  (OVI-25's `O-view.CrossSkin.Tests` golden-master fixture and OVI-27's
  extraction add more call sites again, but both still sit on open,
  unmerged PRs — #4 and #5 respectively, confirmed via `gh pr list` — so
  they are not yet part of the count that ships on `main`; whichever of
  OVI-25/27/29 merges last will need to reconcile against whatever the
  earlier ones landed.) This ADR's
  own table lists `LastIngestAt` as **real** (never estimated or
  unavailable) — a trailing optional parameter defaulting silently for
  those 18 pre-existing call sites would plant an unlabelled fabricated
  timestamp behind a field this contract promises is always real, which
  is the exact failure mode the "never fabricate a number" principle
  exists to catch, even in test fixtures. OVI-45's own escalation clause
  names this scenario explicitly (a `UsageSnapshot` shape change touching
  OVI-10/OVI-25/OVI-27/28) and requires escalating to Chief Gary II before
  proceeding, rather than deciding unilaterally.

  **2026-09-11, later same day — decided: Option A.** Chief Gary II's
  escalation confirmation (`aee1b24e`, re-issued as `56f845ce` after the
  original expired unanswered when a second, unrelated interaction on the
  same OVI-45 issue superseded it before it could be actioned — a platform
  quirk, not a reconsideration) recommended, and this amendment now
  authorizes, **Option A: `LastIngestAt` becomes a required positional
  field on `UsageSnapshot`, and all existing call sites are backfilled
  with an explicit value** — not an optional trailing parameter with a
  silent default. Reasoning carried from Gary's recommendation: every one
  of the 17 existing call sites is a test fixture already being touched by
  whichever slice adds this field (OVI-29 needs `LastIngestAt` for
  `Freshness` regardless of how it lands), the backfill edit is mechanical,
  and Option B would have planted an unlabelled `default(DateTimeOffset)`
  (year 1) timestamp behind a field this table has always documented as
  unconditionally real — the same failure mode the escalation was raised
  to prevent, just realized instead of avoided. Kit the Builder has
  explicit scope, via this amendment and the OVI-29 resolution comment, to
  add `DateTimeOffset LastIngestAt` to `UsageSnapshot`'s constructor and
  backfill all 17 pre-existing call sites in the same PR that adds
  `Freshness`.

- **2026-09-11 update — `DataSourceKind.Stale` landed in code, `LastIngestAt` added to
  `UsageSnapshot`, and `PanelText.cs`'s Freshness/Countdown/SessionReset/WeeklyReset/
  WeeklyResetConflict family extracted (Kit the Builder, Phase 1 slice 3.1, OVI-29,
  completing the two decisions the 2026-09-11 amendments above authorized).**
  - `src/O-view.Core/Models/DataSourceKind.cs` now defines `Stale` (5 values total), per
    Option A above.
  - `UsageSnapshot`'s positional constructor now carries a required
    `DateTimeOffset LastIngestAt`, placed immediately after `DataSourceKind`. All 17
    pre-existing call sites (`O-view.Tray.Tests`, `O-view.Linux.Tests`,
    `O-view.Core.Tests`) were backfilled with an explicit value, plus one more introduced
    by this same PR when it brought OVI-25's `O-view.CrossSkin.Tests` harness commit
    across — the reconciliation this ADR's 2026-09-11 amendment named as needed,
    resolved here since OVI-29 is the slice that merges last among OVI-25/27/29.
    `UsageSnapshot.Unavailable`'s canonical "no data" instance uses
    `DateTimeOffset.MinValue` as an explicit "never" sentinel, not a fabricated recent
    timestamp — the same pattern this type already used for `UsageLevel.Green` on that
    instance (a non-nullable field given a documented sentinel rather than a paired
    status flag). No skin reads this value for an `Unavailable` snapshot.
  - `O-view.Tray.Presentation.PanelTextFormatter` and
    `O-view.Linux.Presentation.PanelTextFormatter` each now implement `Freshness`,
    `Countdown`, `SessionReset`, `WeeklyReset`, and `WeeklyResetConflict`, independently
    worded per skin (ADR-0003). Confirmed by test: a `Stale` snapshot renders identically
    to a `Live` one at the same age, in both skins, matching the source app's confirmed
    collapsing behaviour.
  - **One implementation decision beyond what the source app decided, made at this
    slice's own discretion (not escalated — narrow and non-architectural):**
    `DataSourceKind.JsonlFallback` did not exist in the source app's 4-value enum, so its
    place in `Freshness`'s switch was undecided by any prior ADR. This slice groups it
    with `Live`/`Stale` in the age-labelled `"As of {age}"` branch, not with `Estimate`'s
    "Local estimate" framing — consistent with `TooltipFormatter`'s existing precedent,
    where `JsonlFallback` is never treated as a whole-snapshot special case, only its
    individual fields carry a per-value `~`/estimated marker via `UsageValueStatus`.
  - **Because `LastIngestAt` is required and non-nullable, the source app's "capture time
    unknown" fallback text (`CaptureTimeUnknown`, "rare — every shipped provider stamps
    one") has no reachable case in this contract and was not ported.** The source
    `Freshness`'s `age is null` branches are dead code once the field is guaranteed
    present; carrying that fallback text forward would have been unreachable, untestable
    code, not parity.
  - **Not part of this slice, unchanged:** `WeeklyResetUnknown`,
    `WeeklyResetUserSupplied`, and `WeeklyResetUserSuppliedHint` (the source app's
    separate "no weekly reset known at all" and "user-entered reset" copy) are not this
    slice's named scope (`Freshness`/`Countdown`/`SessionReset`/`WeeklyReset`/
    `WeeklyResetConflict` only) and were not extracted here.
  - The cross-skin golden-master harness (ADR-0003) gained two new fixture families,
    `FreshnessFixture` and `PanelTextResetFixture`, parallel to the existing
    `GoldenMasterFixture` and OVI-27's `UsageStatisticsFixture` rather than a change to
    either — see the ADR-0003 amendment below.
  - **Still not yet extracted, from `PanelText.cs`:** the boost promo chip/card (needs a
    new `BoostNotice` Core type and the 281px Windows panel-width budget resolved per
    ADR-0003), the usage-tile caveat/rate-card fields, the off-plan three-state banner,
    and the GitHub rate-limit notice. Each is its own differently-shaped sub-slice with
    its own new Core surface, per the OVI-29 escalation this amendment resolves.

- **2026-09-21 amendment (OVI-82) — `BoostNotice` Core type shape decided and added to
  the contract table above; the 281px budget question resolved explicitly for this
  member (Adrian II the Architect, closing the sign-off gap the OVI-29 escalation named
  for sub-slice 3, per the board's 2026-09-11T02:28Z reply on OVI-29).**

  **Source re-read, re-confirmed against the same pinned commit this ADR already cites,
  `897777b`** (verified current: `gh api repos/mlengmark/O-view/commits/main` returns
  the same SHA as of this amendment — the source repo has not moved). **CONFIRMED** by
  direct read of two files, not the file `PanelText.cs`'s own row in the table above
  already covers:
  - `BoostChip(BoostNotice notice, DateTimeOffset utcNow, TimeZoneInfo local) -> string`
    and `BoostCard(BoostNotice notice, DateTimeOffset fetchedAtUtc, TimeZoneInfo local)
    -> string` (`src/O-view.Core/Models/PanelText.cs`, lines 300 and 378) — both take a
    `BoostNotice` plus one `DateTimeOffset` (different meaning per member — "now" for the
    chip's countdown, "when the cache was fetched" for the card's attribution line — and
    a `TimeZoneInfo`).
  - `BoostNotice` itself — `public sealed record BoostNotice(string Bar, string Text, int?
    Percent, DateOnly? EndsOn)` — is **not** defined in `PanelText.cs` or anywhere under
    `O-view.Core.Models`. It lives in `src/O-view.Core/Providers/CachedUsage/BoostNotice.cs`,
    alongside `BoostNotices` (plural), the type that reads it from Claude Code's own
    `~/.claude.json` → `cachedGrowthBookFeatures.tengu_rate_limit_promo_notices` cache
    (read-only, no credential, per the file's own doc comment citing `CLAUDE.md` rule 3).
    `BoostNotice.Bar` is the meter key (`BoostNotice.SessionBar` = `"five_hour"`,
    `.WeeklyBar` = `"seven_day"` — the only two bars ever named; model-scoped keys such as
    `seven_day_opus` are recognised in the selection code but never observed populated) and
    is used only for *selecting* which notice applies to which meter
    (`BoostNotices.For(bar, today, utcNow)`) — neither `BoostChip` nor `BoostCard` reads
    `.Bar` directly.

  **Decision — the contract type, following this table's existing per-meter row pattern
  (`Session`/`WeeklyUtilizationPercent`, `Session`/`WeeklyResetAt`) rather than a single
  field needing skin-side filtering:** two new nullable fields, `SessionBoostNotice` and
  `WeeklyBoostNotice`, each an optional `BoostNotice` value — Core has already applied the
  bar-selection Core computes this from (mirroring how `UsageLevel`'s banding or
  `WeeklyResetAt`'s cached-vs-derived resolution already happens in Core, not the skin).
  `Bar` itself is dropped from the contract type — which field a notice is in already says
  which meter it is for, so carrying the source's own `Bar` string forward would be a
  redundant, easy-to-desync duplicate of the field name itself. `Text`, `Percent`, and
  `EndsOn` are carried forward with their source meanings and nullability unchanged: `Text`
  is always relayed verbatim (the source's own rule — see `BoostCard`'s doc comment, "never
  edited, summarised or re-worded" — the panel *relays* a claim it cannot itself verify,
  never asserts it); `Percent`/`EndsOn` are independently nullable because the source
  sentence's own parsing (`PromoText.Percent`/`PromoText.EndDate`, not re-verified by this
  amendment — out of scope, upstream of `BoostNotice`'s own shape) may not yield either, and
  that is itself real information (the sentence didn't state a figure or a date), not a
  reason to mark the whole notice `unavailable`.

  **`EndsOn` stays a bare date, not a timestamp, by contract — this is itself a "never let a
  platform assumption leak into Core" instance, same family as the 281px item below.** The
  source's `EndOfDayUtc` (private, `PanelText.cs`) resolves "the promo's last day" to a UTC
  instant using the *reader's* `TimeZoneInfo` — a genuinely skin-owned computation (what time
  is "midnight" is a locale/zone fact), not a Core one. Core hands the skin the calendar date
  Claude Code's sentence named and nothing else; each skin resolves what "ends" means in its
  own reader's zone, per this ADR's existing "skin owns locale/format rendering" rule (see
  the `SessionResetAt` row).

  **The 281px Windows panel-width budget — resolved, not newly decided.** This ADR's
  "Decision" section already named this exact constraint (`PanelText.BoostChip`, line
  ~84 above) as Windows-skin-owned, alongside the 127-character tooltip cap, at the time
  this ADR was first written — before any sub-slice had reached the member it constrains.
  This amendment confirms that resolution now applies concretely: **nothing about the
  281px budget, or how much of it a given phrase consumes, appears in the `BoostNotice`
  contract type or in `BoostChip`/`BoostCard`'s Core-side replacement.** Two source
  behaviours that exist *because of* the 281px budget are confirmed skin-only, not
  Core, consequences of that same rule, not new exceptions to it:
  - The **month abbreviation** (`{last:d MMM}`, "31 Aug" not "August 31") is
    locale-sensitive date formatting — already excluded from Core by this ADR's "must
    never emit ... a locale-bound date/time string" rule, independent of the width budget
    that happens to be *why* the source app chose the abbreviated form.
  - The **`BoostRemaining` weeks/days/hours decomposition** (`2w 4d 14h`) is presentation
    wording computed from `EndsOn` and "now" — the same category of skin-owned
    decomposition this ADR's contract already keeps out of Core for `Countdown`
    (OVI-29, raw `TimeSpan` in, skin words it). Core supplies `EndsOn`; the skin computes
    and words the remaining time, including deciding how many units the 281px row has
    room for.
  Each skin's own formatter is free to reproduce the source's exact 281px-driven
  behaviour, choose a different budget appropriate to its own toolkit and panel width (the
  Linux/Avalonia skin has no reason to share a WPF pixel measurement), or wrap instead of
  truncate — that choice belongs entirely to the skin, per this ADR's existing ownership
  rule, and this amendment adds no new constraint beyond confirming the existing one
  reaches this member.

  **Out of scope for this amendment, named explicitly so it isn't assumed decided:**
  - **The provider that populates `BoostNotice`** — porting `BoostNotices.TryRead`/`.Parse`/
    `.For` (the code that actually reads and selects from Claude Code's cache file) is
    separate, future provider-porting work, exactly as `UsageSnapshot`'s shape was defined
    (OVI-9/OVI-10) before `PlanHistoryProvider` was ported. This amendment defines only the
    type the formatter's input needs.
  - **Whether reading `~/.claude.json`'s `cachedGrowthBookFeatures` block is a "second AI
    system" under gate G5.** The source app already reads this file today, unaudited by
    this rebuild's own gate process — it predates this project. Whether porting that
    specific read counts as introducing a new source under G5, or is covered by the same
    audit as the rest of Claude Code's `.claude.json` (already the source for
    `AccountPlanTier` and other fields in this table), is a question for whichever future
    slice actually ports the provider to raise explicitly — not decided, one way or the
    other, by this documentation-only amendment.
  - **The real/unavailable distinction this amendment's contract type makes is stricter
    than what the source app currently does**, flagged here so a future provider port
    doesn't silently reproduce the gap: `BoostNotices.TryReadAny` catches read/parse
    failure (`IOException`, `JsonException`, `UnauthorizedAccessException`) and returns
    `null` — the same `null` a genuinely empty, successfully-read cache would produce. This
    contract's `real`/`unavailable` status flags exist so a skin can tell "Core checked and
    there is no active promo" (`real`, value absent) apart from "Core could not check"
    (`unavailable`) — a distinction the source app's own provider does not currently
    preserve. Whichever future slice ports the provider must not collapse the two the way
    `TryReadAny` does today, or this field would silently fail its own "never fabricate a
    number" obligation (rendering an unreadable cache identically to a genuinely quiet one).
  - **The harness fixture-family shape for testing `BoostChip`/`BoostCard`** is decided in
    [ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s 2026-09-21 (OVI-82) amendment, not
    here — this amendment is the Core-contract half of that sub-slice's sign-off only.

- **2026-09-21 update — `BoostChip`/`BoostCard` extracted (Kit the Builder, Phase 1 slice
  3.3, OVI-92, building against this ADR's own 2026-09-21 (OVI-82) amendment and Quinn's
  sign-off on the harness shape it authorized).**
  - `src/O-view.Core/Models/BoostNotice.cs` now defines `public sealed record
    BoostNotice(string Text, int? Percent, DateOnly? EndsOn)` — exactly the shape the OVI-82
    amendment decided, with the source's `Bar` selector key dropped as that amendment
    specified. **Not added in this slice:** `UsageSnapshot`'s `SessionBoostNotice`/
    `WeeklyBoostNotice` fields. The OVI-82 amendment's own "out of scope" section names the
    provider that would populate them as separate, future provider-porting work; without
    that provider nothing produces a `BoostNotice` to carry on a snapshot yet, and
    `LastIngestAt`'s precedent (2026-09-11 amendment above) is that a `UsageSnapshot` shape
    change needing every existing call site backfilled is its own escalation, not a
    side-effect of an unrelated slice. `BoostNoticeFixture` (below) keys directly on
    `BoostNotice`, not `UsageSnapshot`, exactly as the OVI-82 amendment's harness proposal
    specified — so this slice needed no `UsageSnapshot` change to build a working harness
    against.
  - `O-view.Tray.Presentation.PanelTextFormatter` and `O-view.Linux.Presentation.PanelTextFormatter`
    each now implement `BoostChip` and `BoostCard`, independently worded per skin (ADR-0003)
    exactly as `Freshness`/`Countdown`/etc. were (OVI-29). Both skins reproduce the source's
    weeks/days/hours countdown decomposition (`BoostRemaining`) and abbreviated-month date
    formatting as their own chosen wording, not because a width is being enforced — see the
    next bullet.
  - **The 281px Windows panel-width budget is confirmed skin-side only, and confirmed not
    implemented as an actual measure-and-truncate step in this slice.** No width or pixel
    constant of any kind appears in `BoostNotice` or in either skin's `BoostChip`/`BoostCard`
    signature — grep-confirmed. `O-view.Tray.Presentation.PanelTextFormatter.BoostChip`'s own
    doc comment states explicitly why: no `O-view.App` panel window exists yet in this
    repository (per this repository's own standing caveat, `CLAUDE.md`) to measure a rendered
    row against, so there is nothing yet to truncate or wrap to. This is not a gap against
    this ADR's contract, which only requires the budget stay out of Core — it is a gap
    against the source app's *behaviour*, named explicitly so the slice that first wires a
    real WPF panel window doesn't assume the truncate-or-wrap step already exists.
  - The cross-skin golden-master harness (ADR-0003) gained a fifth fixture family,
    `BoostNoticeFixture`/`BoostNoticeSkinUnderTest`/`BoostNoticeGoldenMasterCrossSkinTests`,
    built exactly to the shape Quinn signed off on (interaction `22dbaf90`, accepted
    2026-09-21T20:10:59Z) — see the ADR-0003 entry below for the fixture set and the local
    drift-detection confirmation.
  - **Still not yet extracted, from `PanelText.cs`:** the usage-tile caveat/rate-card fields,
    the off-plan three-state banner, and the GitHub rate-limit notice — sub-slices 4 and 5 of
    this split, per the OVI-29 escalation this and the prior amendments have been resolving
    one sub-slice at a time.

- **2026-09-22 update — `RateLimitedNotice` (the GitHub rate-limit notice) extracted (Kit
  the Builder, Phase 1 slice 3.4, OVI-98).** Redoes OVI-80/closed PR #12: that extraction
  was correct and passed review (OVI-81), but its branch was reconciled twice and the
  second reconciliation hit a real code-level conflict with the since-merged OVI-92
  (`BoostChip`/`BoostCard`). Rather than force a third reconciliation of an unreviewed
  `.cs` diff, the board closed PR #12 and this slice redoes the extraction fresh against
  main at `80d3913` (which already includes OVI-92). The design is unchanged from OVI-80/
  OVI-81 — this entry carries that reviewed design forward, not a new one.
  - **Confirms, and carries forward, OVI-80's correction to the original OVI-29 signature
    survey.** `RateLimitedNotice(DateTimeOffset? retryAfterUtc, TimeZoneInfo local) ->
    string` never took a `UsageSnapshot` — only two raw scalars the caller already holds —
    so extracting it needed **no new Core contract row and no new Core type at all**, same
    as OVI-80 established. Nothing about this signature changed between OVI-80's original
    landing and this redo.
  - `O-view.Tray.Presentation.PanelTextFormatter.RateLimitedNotice` and
    `O-view.Linux.Presentation.PanelTextFormatter.RateLimitedNotice` each state, in their
    own wording: the limit is shared by the caller's network (not their device alone), the
    retry time only when GitHub actually sent one (never fabricated — the standing
    no-fabrication rule), and a reassurance that nothing is wrong with the user's own
    connection or install — worded identically to OVI-80's original landing, since the
    reviewed wording was never in question, only the branch's mergeability. Confirmed by
    test in both skins' `Presentation/PanelTextFormatterTests.cs`.
  - Landed against the **current, post-OVI-92 shape** of both skins'
    `Presentation/PanelTextFormatter.cs` — `RateLimitedNotice` is appended after
    `BoostChip`/`BoostCard`, and each class's doc comment now lists all three sub-slices
    (3.1, 3.3, 3.4) landed so far, rather than reintroducing OVI-80's pre-BoostChip doc
    comment wording.
  - The cross-skin golden-master harness (ADR-0003) gained a sixth fixture family,
    `RateLimitedNoticeFixture`, parallel to `GoldenMasterFixture`/`UsageStatisticsFixture`/
    `FreshnessFixture`/`PanelTextResetFixture`/`BoostNoticeFixture` rather than a change to
    any of them — see the ADR-0003 amendment below.
  - **Not part of this slice, per its own explicit boundary:** `BoostChip`/`BoostCard`
    (already merged by OVI-92, untouched here), the usage-tile caveat/rate-card fields, and
    the off-plan banner remain not yet extracted.

- **2026-09-23 amendment (OVI-100) — the usage-tile caveat's rate-card and pricing-gap
  surface decided and added to the contract table above (Adrian II the Architect, closing
  the same kind of gap for sub-slice 4 that the 2026-09-21 (OVI-82) amendment closed for
  sub-slice 3, per the board's 2026-09-11T02:28Z reply on [OVI-29](/OVI/issues/OVI-29)).**

  **Source re-read, re-confirmed against the same pinned commit this ADR already cites,
  `897777b`** (verified current for this amendment: `gh api repos/mlengmark/O-view/commits/main`
  returns the same SHA — the source repo has not moved since OVI-82). Read-only, as always.

  **CONFIRMED** by direct read of three source files:

  - `PanelText.Caveat(PanelStatistics stats) -> string`
    (`src/O-view.Core/Models/PanelText.cs`, lines 406–432) reads exactly **five** members of
    `PanelStatistics` and nothing else: `CoverageNote`, `UnpricedModels`,
    `TtlUnrecordedCacheWrites`, `RatesAreStale`, and `Rates`. It joins the parts that apply
    with `" · "` and returns `""` when none do. It also calls two other members —
    `PanelText.RateAge(RateCard)` (line 443) and `UsageFormatter.Tokens(...)` (line 422).
  - `PanelStatistics` (`src/O-view.Core/Models/PanelStatistics.cs`): `UnpricedModels` is
    `IReadOnlyList<string>` (line 58), `TtlUnrecordedCacheWrites` is `long` (line 118),
    `RatesAreStale` is `bool` (line 107), `Rates` is a `RateCard` (line 97), and
    `CoverageNote` is the leaked sentence this ADR's `HistoryCoverage` row **already
    replaced** in OVI-27.
  - `RateCard`/`RateCardSource` (`src/O-view.Core/Pricing/RateCard.cs`, lines 9–112):
    `RateCard` is a record of `AsOf` (`DateOnly`), `Source` (`RateCardSource`), `Models`
    (`IReadOnlyList<ModelEntry>` — the whole rate table), and `UsInferenceMultiplier`
    (`decimal`), plus `StaleAfter` (a static 90-day `TimeSpan`), `IsStaleOn(DateOnly)`,
    `SourceLabel`, `Find(string)`, and `RatesFor(string, UsageModifiers)`. `RateCardSource`
    is `{Bundled, UserFile}`, with `UserFile` declared and documented as not implemented.

  **Decision 1 — `RateCard` itself does not go on the contract; a narrow projection of it
  does, named `RateCardStamp`.** The source's `RateCard` is a *pricing engine* — a rate
  table plus the lookup methods that price against it. A skin needs three facts out of it
  and no others: where the table came from, what date it was read, and whether Core judged
  it old enough to be worth saying so. Putting the whole type on the contract would hand
  every skin the pricing table and the lookup surface as well, which is a far larger
  boundary than the caveat needs and an open invitation for a skin to price something
  itself.

  **Rejected: reusing the source's `RateCard` name for the projection.** The name would
  survive while ~five of its eight members did not, so a future provider port would reach
  for `RateCard.RatesFor(...)` on the contract type, find nothing, and have to discover by
  reading that this `RateCard` is not that one. A different name (`RateCardStamp`) costs one
  word and removes the trap. The task that scoped this work named `RateCard` as the likely
  shape and explicitly allowed "whatever the confirmed shape turns out to be"; this is that
  deviation, recorded rather than assumed.

  **Decision 2 — `isStale` stays a Core decision, and the 90-day threshold stays out of the
  contract entirely.** This is not a style preference; the source states the reason and it is
  a correctness one. `PanelStatistics.RatesAreStale`'s own doc comment (lines 100–106) says it
  is "decided in `Build`, which is the only place that knows both the card and the reader's
  own today — so the caveat cannot be rendered against a different day from the figures it
  qualifies." A skin handed `asOf` + `staleAfter` and left to compare against its own clock
  can word a caveat for a different day from the one the figures beside it were built for.
  Core does the comparison once, against the same `today` it built the window from, and hands
  over the answer. The threshold itself (90 days) is Core policy, kept off the contract for
  the same reason the OVI-16 amendment kept the per-provider freshness threshold off it.

  **Decision 3 — `SourceLabel` does not travel; the enum does.** `RateCard.SourceLabel`
  returns `"bundled"` or `"user file"` — a display string built inside the platform-neutral
  layer, the same leak class this whole ADR exists to remove. Core hands the skin
  `RateCardSource`; each skin words it. `UserFile` is carried forward even though nothing
  emits it today, because the source's own reasoning for recording it applies unchanged:
  a user-editable pricing file whose provenance is not on screen is a fabricated-number
  vector (source repo issue #255). Dropping the member now would mean the contract silently
  loses the distinction on the day it starts to matter.

  **Decision 4 — `asOf` is a bare calendar date, not a formatted one.** The source renders
  it `{card.AsOf:d MMM yyyy}` under `CultureInfo.InvariantCulture` (line 445) — a
  locale-bound date string, already banned by this ADR's "what Core must never emit" rule,
  and the same call this ADR's 2026-09-21 amendment made for `BoostNotice.EndsOn`.

  **Decision 5 — `TtlUnrecordedCacheWritesWindow31d` reuses the existing `TokenCount` type;
  no new type is added for it.** It is a token count with a trust status, which is exactly
  what `TokenCount(long? Value, UsageValueStatus Status)` already is, and each skin's own
  `Presentation/UsageFormatter.Tokens(TokenCount)` already renders that type — so the field
  arrives at the skin already renderable, with no new formatter surface. The field name
  carries the window (`...Window31d`) to match `OutputTokensWindow31d`/
  `EstimatedValueWindow31d`, because the source's sum is scoped to the 31-day window
  specifically (`PanelStatistics.Build`, line 247) and an unscoped name would invite a skin
  to caveat the "today" tiles with a 31-day figure.

  **Decision 6 — `0` and the empty list are `real` values, not `unavailable` ones.** Both
  of these fields describe *conditions that clear*: `TtlUnrecordedCacheWritesWindow31d`
  falls to zero as the affected history ages out of the window, and `UnpricedModels` empties
  when every model in the window has a published rate. "Core checked and there is nothing to
  qualify" is a measured fact and must not render as a gap. `unavailable` on either field
  means only one thing — Core could not establish the value at all — and a skin seeing it
  must not imply the opposite by staying silent.

  **Decision 7 — a non-empty `UnpricedModels` changes what the figure beside it means, and
  the contract says so out loud.** This is the "never fabricate a number" rule biting at the
  level of *composition* rather than of a single field: `EstimatedValueWindow31d` is a
  perfectly real `estimated` decimal, and it is also not the whole window. The source app
  learned this the hard way in the other direction — one unrecognised model used to blank
  both Est. tiles entirely (`PanelStatistics` lines 54–56, 279–286), which traded an
  understated total for no total at all. The contract's answer is neither: hand over the
  subtotal *and* the exclusions, and oblige the skin to render both together.

  **`UnpricedModels` carries vendor model ids, not display names — and "unpriced" does not
  mean "unrecognised".** Two separate points, both CONFIRMED:
  - The source joins the raw ids into the sentence (`PanelText.cs` line 417), and does *not*
    route them through `ModelDisplayName.For(...)` the way `PanelStatistics.SliceByModel`
    does for its priced slices (line 273). The contract carries the id, verbatim. Mapping ids
    to display names is a concern the future `ModelBreakdown[]` port will have to settle for
    itself; pre-empting it here would decide that slice's question inside this one.
  - A model lands in `UnpricedModels` whenever `CostEstimator.EstimateUsd(...)` returns null,
    and `RateCard.RatesFor` (lines 97–111) returns null for **three** distinct causes: an
    unrecognised model id, an unrecognised modifier value, or fast mode on a model that has
    no published fast-mode row. The source's own caveat wording ("no published rate") is true
    of all three, but the contract does not currently carry *which* — and this amendment
    deliberately does not add a reason code. Distinguishing them needs the pricing/provider
    port to exist first; flagged here so a future slice raises it explicitly rather than
    discovering it.

  **What this amendment does *not* add, because the existing contract already covers it:**
  the coverage part of the caveat. `stats.CoverageNote` is the first of `Caveat`'s four
  parts, and `HistoryCoverage.RecordedDays`/`WindowDays` replaced it in OVI-27 — the sentence
  already lives in each skin's own `Presentation/PanelStatisticsFormatter.CoverageNote`. The
  rebuild's `Caveat` composes that existing skin-side sentence with the three new parts; it
  does not re-derive it, and Core gains nothing for it.

  **Composition — these fields join the existing `UsageStatistics` record, rather than a new
  parallel one.** They qualify precisely the 31-day figures `UsageStatistics` already
  carries, and the source's own reason for hanging the card off the result applies unchanged
  (`PanelStatistics.Build`, lines 242–245: "the tiles and the caveat beneath them have to
  describe the same rates"). Two build-side constraints follow from that and are stated here
  because they are contract consequences, not implementation taste:
  - They are added as `init`-only properties with defaults, **not** as new positional
    constructor parameters. `UsageStatistics`'s positional constructor is already called by
    both skins' tests and by `UsageStatisticsFixtures`; widening it would churn merged,
    reviewed code for no contract gain. This mirrors how the source's own `PanelStatistics`
    declares these same three members (lines 58, 97, 118).
  - `Rates`'s default must be an explicitly **unavailable** stamp, never a bundled stamp
    dated "now". A defaulted provenance that asserts a source and a date Core never
    established would be a fabricated number wearing a type — the precise failure this
    contract's status flags exist to prevent.

  **Out of scope for this amendment, named explicitly so it isn't assumed decided:**
  - **The provider and pricing code that populate these fields** — porting `ModelCatalog`,
    `CostEstimator`, `RateCardFeed`, and `RateCardDrift` is separate, future work, exactly as
    the OVI-82 amendment left `BoostNotices.TryRead`/`.Parse`/`.For` for a later slice. This
    amendment defines only the shape the formatter's input needs. **INFERRED, not confirmed:**
    a greenfield store in this rebuild would likely always emit `0` for
    `TtlUnrecordedCacheWritesWindow31d`, since the source's non-zero case exists only for rows
    a pre-issue-#255 build ingested. The field is on the contract regardless, because a ported
    store may inherit such rows and because a skin must be told the assumption was made, not
    left to assume it wasn't.
  - **`RateCardDrift`** — the source's value-comparison check that caught a rate row which was
    wrong on the day it was written (`RateCard.cs` lines 56–59, source issue #256). Age is
    necessary and not sufficient; `isStale` answers only the age half. Whether drift detection
    produces its own contract field is a question for the pricing port, not this amendment.
  - **`PanelText.TokenScopeCaveat`** (line 474) and **`PanelText.RateAge`** (line 443) are in
    sub-slice 4's *build* scope but need no Core surface decided here: `RateAge` renders from
    the `RateCardStamp` this amendment adds, and `TokenScopeCaveat` is a bare `const string`
    with no input at all — the same "no new Core surface" case `RateLimitedNotice` was
    (OVI-80/OVI-98). Named so the build slice does not silently drop `TokenScopeCaveat`: the
    source's own doc comment (lines 463–466) argues it must render *always*, because a caveat
    that appears only sometimes teaches a reader that its absence means full coverage — and
    here that would be false every time.
  - **The harness fixture-family shape for testing this sub-slice** is decided in
    [ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s 2026-09-23 (OVI-100) amendment, not
    here — this amendment is the Core-contract half of that sub-slice's sign-off only.

- **2026-09-25 amendment (OVI-135) — three values the skins already render had no
  contract row: the session-reset uncertainty (D2), the user-entered weekly reset and its
  conflict (D3), and the update check's retry time (D4) (Adrian II the Architect, from the
  OVI-133 documentation drift check).** Each of the three is a value a skin's
  `PanelTextFormatter` takes as input today without this table saying what it is, who
  decides it, or whether it can be trusted.

  **Source re-read, re-confirmed against the same pinned commit this ADR already cites,
  `897777b`** (verified current for this amendment: `gh api repos/mlengmark/O-view/commits/main`
  returns the same SHA). Read-only, as always. This repository was read at `origin/main`
  `a2a5556`.

  **D2 — session-reset uncertainty: decided (a). `SessionResetAt`'s `estimated` status is
  the "approximate" signal, and Core owns the threshold behind it.**

  What the code does today, all **CONFIRMED** by direct read:
  - In this repository, both skins' `PanelTextFormatter.SessionReset(DateTimeOffset?
    resetAtUtc, DateTimeOffset utcNow, TimeZoneInfo, TimeSpan? uncertainty = null)` take a
    raw uncertainty width. Each decides for itself whether to mark the time approximate
    (`~` on Windows, `(approx.)` on Linux), by comparing that width to its own
    `ApproximateThreshold = TimeSpan.FromMinutes(30)`. The constant is declared twice,
    once per skin (`src/O-view.Tray/Presentation/PanelTextFormatter.cs` line 136,
    `src/O-view.Linux/Presentation/PanelTextFormatter.cs` line 119).
  - Both skins' `TooltipFormatter` mark the **same** session-reset value from a different
    signal: `SessionResetAt.Status == Estimated` (the OVI-15 fix, 2026-09-09 update above).
    The panel and the tooltip can therefore disagree about one value today. A 45-minute
    bracket carried with status `Real` gets `~` in the panel and nothing in the tooltip. An
    `Estimated` value passed with no uncertainty gets `~` in the tooltip and nothing in the
    panel.
  - **The rebuild's threshold differs from the source's.** The source's threshold is
    **15 minutes** (`WeeklyWindow.PreciseBracket`,
    `src/O-view.Core/Providers/PlanHistory/WeeklyWindow.cs` line 50, applied by
    `PanelText.IsApproximate` at line 169). It lives in **Core**, beside the provider that
    measures the bracket (`SessionWindowStart.Uncertainty`, `ResetDetector.cs` line 15).
    A 20-minute bracket earns `~` in the source and would not earn one here. Nothing
    caught this, because the two rebuild skins carry the same wrong constant, so the
    cross-skin harness sees them agree.
  - Where the source's width comes from: `PlanHistoryProvider` sets it from the observed
    bracket (line 346). `CachedUtilizationProvider` and `UsageEngine.WithReportedResets`
    set it to `TimeSpan.Zero` when Claude reported the instant exactly (lines 97 and 1053).
    The source's `TooltipFormatter.Approx` is declared but never called (`git grep`, no call
    site), so the source tooltip never marked the session reset. Only its panel did.

  **Decision.** Core decides whether a session reset is approximate, and says so through
  the status flag this row already has:
  - `estimated` means the reset was derived from a sampling bracket **wider than** Core's
    precision threshold.
  - `real` means Claude reported the instant, or the bracket is at or under the threshold.
    Read `real` on this row as "exact to within the vendor's own sampling cadence". It
    does not mean "reported by the vendor". That is the source's own reading, and this
    note says so explicitly so no one mistakes one for the other.
  - The threshold is Core policy and stays **off** the contract, under the same rule as
    `RateCardStamp.isStale`'s 90 days and `LastIngestAt`'s freshness threshold. Its
    source value is 15 minutes. The provider port carries that value, not 30.
  - A skin reads `SessionResetAt.Status` and nothing else. The raw uncertainty width does
    not cross the contract.

  **Rejected: (b), a separate uncertainty field, with the threshold owned by Core.**
  1. It puts two trust signals on one value. That is the disagreement the current code
     already exhibits between the panel and the tooltip (above).
  2. Once Core owns the threshold, all a skin needs from the width is a yes/no answer,
     and the status flag already is that answer. A separate `isApproximate` would be a
     second status flag. Handing over the raw width instead invites each skin to
     re-derive the threshold, which the OVI-100 amendment rules out for `isStale`.
  3. No skin renders the width itself. The source never did either: its conflict copy
     dropped "between X and Y" ranges once resets were reported exactly (`PanelText.cs`
     line 196).

  If a future skin wants to show the bracket as a range, it adds its own row then, e.g.
  `SessionResetBracket`, a provenance range. It is not a second trust flag.

  **Consequence for the tooltip (INFERRED from the code above, not observed):** once a
  provider sets `estimated` for wide brackets, the rebuild's tooltip will mark those
  resets `~`, which the source tooltip never did. That is consistent with the OVI-15
  decision that every `estimated` field is marked, and is accepted, not accidental.

  **This needs a code change.** Both skins' `SessionReset` still take the raw width and
  apply a 30-minute threshold. The build slice is described in the OVI-135 closing
  comment. Until it lands, the current codebase does **not** conform to this row.

  **D3 — the user-entered weekly reset and its conflict: decided to add the rows now, not
  defer them.** Rows `WeeklyResetUserEntry` and `WeeklyResetConflict` are added above, and
  `WeeklyResetSource` is amended.

  What the source does, all **CONFIRMED** by direct read:
  - `UsageEngine.WithWeeklyReset` (`src/O-view.App/UsageEngine.cs` lines 895–945) has
    exactly **two** sources for the weekly reset: a stored reported anchor, projected
    forward in whole weeks, or else the user's entry. There is no derived path.
    `WeeklyResetAtUtc` is assigned in one place in `src/` (line 942; `PlanHistoryProvider`
    passes `null`, line 340). The derivation was deleted by source ADR-0014 (2026-08-25),
    which superseded ADR-0011 on measured evidence.
  - The conflict is decided in the platform-neutral layer, not a head: `WithWeeklyReset`
    sets `_weeklyResetConflict` when `ManualWeeklyReset.IsContradictedBy(...)` finds a
    stored anchor outside the entry's `ContradictionSlack` of 2 hours (`ManualWeeklyReset.cs`
    line 40). Both heads only render it.
  - The entry is a weekday plus a local wall-clock time (`ManualWeeklyReset(DayOfWeek Day,
    TimeOnly LocalTime)`), resolved in the user's zone, not UTC, so it survives
    daylight-saving changes (`NextAfter`, same file).
  - **A source quirk this contract deliberately does not reproduce:** the source panel
    labels the weekly reset "you set this" whenever an entry exists
    (`PopupWindow.xaml.cs` line 525, `userSupplied = _lastSettings?.WeeklyReset is not
    null`). That includes when an anchor exists, agrees within the 2-hour slack, and is
    the value actually on screen. The label can therefore sit beside a time up to two
    hours from the one the user typed. `WeeklyResetSource` labels the provenance of the
    **displayed** value instead. `WeeklyResetUserEntry` is carried separately, so a skin
    that wants to mention the entry can still do so honestly.

  **Decision.**
  - **`WeeklyResetSource` becomes {`CachedExact`, `UserEntered`}.**
  - **`WeeklyResetUserEntry`** carries the typed value. It stays wall-clock, which is a
    deliberate exception to this table's UTC-instant rule for the reason the source gives
    (daylight saving).
  - **`WeeklyResetConflict`** carries the reported instant Core found to disprove the
    entry. It is non-null only when Core detected a conflict, so the question "who
    decides a conflict exists" now has a written answer: **Core**. The 2-hour slack is
    Core policy, off the contract.
  - **`WeeklyResetAt`'s status** is `real` for both sources. A user-entered reset is read
    off Claude's own settings and is exact to the minute. **Provenance lives in
    `WeeklyResetSource`, precision lives in the status flag**, and D2 above uses the
    status flag the same way. A skin must say so when it renders a `UserEntered` value.

  **Rejected: deferring these rows along with the `WeeklyResetUserSupplied*` sub-slice.**
  Both skins already render `WeeklyResetConflict` today (OVI-29), so the input already has
  a consumer. Deferring would leave a skin consuming a value this table does not
  document, which is what this ADR's Consequences section forbids. Deferral was also
  rejected for `Stale` (2026-09-09 amendment above) for the same reason.

  **Rejected: keeping `Derived` alongside `UserEntered`.** Nothing produces it at
  `897777b`, and nothing in this repository reads `WeeklyResetSource` (grep: no match
  under `src/` or `tests/`). A dead enum member invites a provider port to rebuild a
  derivation the source deleted on measured evidence. Retiring it is documentation-only.

  **Rejected: `estimated` for a user-entered reset.** It would print `~` in the tooltip
  (OVI-15) and imply an imprecision the value does not have. It would also mix provenance
  into the precision flag.

  **This needs no code change now.** `WeeklyResetConflict(DateTimeOffset reportedUtc, …)`
  already takes exactly `WeeklyResetConflict.reportedAtUtc`, and nothing in this repository
  constructs a weekly-reset source or entry yet. These rows sit ahead of the code, like
  `BoostNotice` (OVI-82) and `RateCardStamp` (OVI-100) did.

  **Named, still not extracted:** the **`WeeklyResetUserSupplied*` sub-slice**. It extracts
  `PanelText.WeeklyResetUnknown`, `WeeklyResetUserSupplied` and
  `WeeklyResetUserSuppliedHint` (source `PanelText.cs` lines 177–183 and 221–252,
  with `WeeklyResetUnknownHint`/`WeeklyResetUnknownAction`), the copy the
  2026-09-11 (OVI-29) update above left out. It will consume `WeeklyResetSource` and
  `WeeklyResetUserEntry`. It is not scheduled by this amendment.

  **Out of scope, named so it isn't assumed decided:**
  - **Notify-once bookkeeping for a conflict.** The source persists which conflict instant
    the user has already been told about (`WeeklyResetConflictNoticed`, `UsageEngine.cs`
    lines 354–362). Whether that is Core state or a skin preference is for the slice that
    ports the entry dialog. By analogy with `NotificationThresholdCrossed` (Core detects,
    the skin decides whether to notify), the default is skin-side (INFERRED).
  - **A gap in how the source handles an unreadable entry.** `ManualWeeklyReset.Parse`
    returns `null`, meaning "not set", when the stored entry is unreadable. That collapses
    `unavailable` into "nothing entered", the same gap the OVI-82 amendment flagged for
    `BoostNotices.TryReadAny`. The port must keep the two apart.

  **D4 — who owns the update check and `retryAfterUtc`: the shared, platform-neutral layer,
  not each skin. The OVI-133 label ("INFERRED: owned by the skin") is refuted.**

  What the source does, all **CONFIRMED** by direct read:
  - **Core** holds the pure rules, with no HTTP:
    - `RateLimitResponse.IsRateLimited` (`src/O-view.Core/Updates/RateLimitResponse.cs`)
      turns the status code and GitHub's `x-ratelimit-*`/`retry-after` headers into
      `retryAfterUtc`.
    - `UpdateCheck.Evaluate` (`src/O-view.Core/Updates/UpdateCheck.cs`) produces
      `UpdateCheckResult(UpdateOutcome, AvailableUpdate?, DateTimeOffset? RetryAfterUtc)`.
  - **`O-view.App`**, the shared project both heads use, holds the fetch and the cooldown:
    `ReleaseFeed.CheckAsync` (`src/O-view.App/Updates/ReleaseFeed.cs`). Its doc comment
    says why: "Shared because the endpoint is one decision … restating that in each head is
    how the two quietly come to check different things". The cooldown is "held here rather
    than in either head" (source issue #176).
  - The heads own only what to **do** with a result: download and self-replace
    (`O-view.Tray/Updates/UpdateService.cs`) or notify only
    (`O-view.Linux/Updates/LinuxUpdateNotice.cs`). That is ADR-0002's Self-update row, and
    it is unchanged.

  **Decision.** The update check belongs to the shared layer, and its result is contract
  data, so it gets rows (`UpdateCheckOutcome` and `UpdateRetryAfterUtc`, above) rather
  than being left implicit. OVI-80/OVI-98 were right that `RateLimitedNotice` needs no new
  Core *type* to be extracted. What was missing is that its input comes from Core and
  therefore needs a row. Where the rebuild puts the HTTP fetch is for the porting slice to
  decide: `O-view.App` does not exist in this repository (`ls src`, CONFIRMED). The two
  constraints are that the pure rules stay in BCL-only Core, and that the fetch is not
  duplicated per skin. ADR-0002's Self-update row carries a matching dated note.

  **Rejected: each skin's Self-update capability owns the check.** The per-OS difference
  is in acting on an update, not in detecting one. A per-skin check is the duplication the
  source consolidated after issue #176 retried straight back into GitHub's limit.

  **This needs no code change now.** `RateLimitedNotice` already takes the raw value, and
  nothing in this repository ports the update check yet.

- **2026-09-25 update — the code now conforms to D2 (Kit the Builder, OVI-139).** The
  sentence above, "until it lands, the current codebase does **not** conform to this row",
  is superseded by this entry and left in place as the record of what was true before it.
  All **CONFIRMED** by direct read, grep and a local `dotnet test O-view.slnx` run on
  Windows:
  - Both skins' `PanelTextFormatter.SessionReset` now take the Core `UsageInstant` —
    `SessionReset(UsageInstant resetAt, DateTimeOffset utcNow, TimeZoneInfo displayZone)` —
    in place of `(DateTimeOffset? resetAtUtc, …, TimeSpan? uncertainty = null)`. Each marks
    the time approximate (`~` on Windows, ` (approx.)` on Linux) exactly when
    `Status == Estimated`. `Unavailable` renders the skin's "no reset observed" copy, even if
    a value is present; a null value is treated the same way.
  - `ApproximateThreshold` and `IsApproximate` are deleted from both skins. `grep` for
    either name under `src/` and `tests/` returns no match, and neither skin's
    `PanelTextFormatter` reads an uncertainty width.
  - The panel and the tooltip now read the same signal for this value. Each skin's test
    project pins that they agree on **whether** it is marked, for `Real`, `Estimated` and
    `Unavailable` (`SessionResetAndTheTooltipAgreeOnWhetherTheResetIsMarked`). The tests
    compare the yes/no answer, not the marker text: Linux writes `(approx.)` in the panel
    and ` (est.)` in the tooltip. Neither tooltip's marker changed.
  - **No Core change.** `UsageInstant` already existed, and nothing in this repository
    constructs an `Estimated` `SessionResetAt` yet (grep: the only `SessionResetAt` producer
    under `src/` is `UsageSnapshot.Unavailable`). The 15-minute threshold still arrives with
    the provider port, as D2 says. D2's tooltip consequence therefore stays **INFERRED**:
    nothing on screen has yet shown a provider-set `estimated` reset.
  - Not verified: the Linux skin was built and tested on Windows only, as a `net10.0`
    library. No Linux runner and no Linux hardware exercised it.

- **2026-09-25 update — `unavailable` wins over a present value, in the tooltip too (Kit
  the Builder, OVI-144).** The entry above overstated the agreement. Both tooltips still
  rendered a `SessionResetAt` or `WeeklyResetAt` that carried a value flagged
  `unavailable`, with no marker, while the panel said the reset was unknown. *(Wording
  corrected 2026-09-25, OVI-146: that clause holds for `SessionResetAt` only. The panel's
  `WeeklyReset` takes a raw `DateTimeOffset`, not a `UsageInstant`, and nothing under
  `src/` calls it — grep, CONFIRMED — so no panel surface said anything about a
  `WeeklyResetAt` flagged `unavailable`. The tooltip half of the sentence stands for both
  rows.)* Both skins'
  `TooltipFormatter` now omit either reset clause when its `Status == Unavailable`,
  whatever the value. For these two instant rows, `unavailable` means "absent": a skin
  never renders the value. **CONFIRMED** by read and a new test per skin
  (`UnavailableResetsAreOmittedEvenWhenAValueIsPresent`). No `src/` producer constructs
  such a pair today (grep), so this was not reachable on screen. No Core change, no
  marker wording change.

- **2026-09-25 amendment (OVI-146) — `unavailable` carries no value: Core makes the
  ill-formed pair impossible to build (Adrian II the Architect).** OVI-139 and OVI-144 each
  fixed one skin surface that rendered a value flagged `unavailable`. The tooltip's two
  percent clauses still can (see below). That is three fixes for one bug class, one consumer
  at a time. This amendment closes the class at its source.

  **Where the percents stand — CONFIRMED by read at `62fc3ce`.** Both skins'
  `TooltipFormatter` render the session and weekly percent on `.Value is { }` alone (Tray
  `src/O-view.Tray/Presentation/TooltipFormatter.cs` :38 and :49, Linux
  `src/O-view.Linux/Presentation/TooltipFormatter.cs` :37 and :48). A `UsagePercent(47,
  Unavailable)` would show as a bare `47%`, with no marker. The `Estimate`-tier fallback
  guards (Tray :32-33, Linux :31-32) test `.Value is null`, so the same pair would also skip
  the "usage % unknown" copy. **Not reachable today — CONFIRMED by grep:** the only `src/`
  producer of any of the four value types is `UsageSnapshot.Unavailable` /
  `UsageStatistics.Unavailable`, and both pass `null`. The first provider port
  is the first code that could build the pair.

  **Decision — option (a): Core enforces `Status == Unavailable ⇒ Value == null` for every
  status-paired value type.** Today that is `UsagePercent`, `UsageInstant`, `TokenCount`
  and `EstimatedUsd`. The rule is the contract's existing definition, stated at the top of
  this Decision section — **unavailable** means "no source produced a value" — now made
  true by construction instead of by convention. Consequences for each side:
  - **Core.** Constructing one of these types with `Unavailable` and a non-null value fails
    at once, with an `ArgumentException`, at the producer's own call site. That covers
    every route to an instance, not only the positional constructor. A `with` expression
    or an object initializer must not be able to build the pair either. `default(T)`
    already satisfies the rule (`Real` is the enum's zero value; `Value` is `null`). The
    mechanism is the builder's choice. One constraint: the check must not depend on the
    order in which a `with` expression assigns `Value` and `Status`.
  - **Skins.** Reading `.Value` alone is correct for these four types, because `Value is
    { }` now implies `Status != Unavailable`. The OVI-139/OVI-144 `Status: not Unavailable`
    guards become redundant, but they are not wrong. They may stay; removing them is not
    part of this decision. The tooltip percent clauses and the `Estimate` fallback guards
    comply once Core does, and need no skin edit.
  - **Every future status-paired type** (for example `WeeklyResetUserEntry`,
    `UnpricedModels`, `Rates` when they land) enforces the same rule at construction,
    wherever the type has a value slot that `unavailable` could leave filled. A new row in
    this table that pairs a value with the status flag inherits this rule without needing
    its own amendment.
  - **Out of scope.** The converse, `Value == null` with `Real`/`Estimated`, is **not**
    forbidden by this amendment. The tooltip renders it as `?` or omits it, which is a
    visible gap, not a fabricated number. Whether that pair is itself ill-formed is a
    separate question. Nobody has raised it with a case, and it is not decided here.

  **Rejected: option (b), a contract-wide skin rule** ("every skin treats `Unavailable` as
  absent for every status-flagged value", pinned by CrossSkin tests). It is the approach
  that has already failed three times. It relies on every consumer in every skin
  remembering one check, and on a test existing for each consumer. A missed consumer is
  found only when a provider first builds the pair, which is the first provider port and
  the moment this project can least afford a silent fabrication. It also puts
  data-integrity policy in the skin. This project's layering rule puts data meaning in
  Core and wording in the skin.

  **Rejected: Core silently normalises the pair** (drops the value, keeps `Unavailable`).
  It is safe on screen, but it hides a producer bug. A provider that reads a value and
  then decides it is untrustworthy has made a real decision. It should say so by passing
  `null`, and a test should catch it when it does not. A throw fails that provider's own
  tests, at the line that is wrong. Normalising would pass every test and hand the skin a
  value the provider never meant to send.

  **What this changes for existing tests — CONFIRMED by grep at `62fc3ce`.** Tests that
  build an `Unavailable` pair *with* a value, to prove a skin ignores it, will throw at
  construction under this rule. The OVI-144 tooltip tests
  (`UnavailableResetsAreOmittedEvenWhenAValueIsPresent`, both skins) and the OVI-139 panel
  tests (`PanelTextFormatterTests.cs` :112, both skins) are among them. Rewrite each one to
  pin the Core rule (the constructor throws). Do not delete it. What those tests
  protected, "an unavailable value is never shown", is still pinned, now at the one place
  that can guarantee it. **The implementation is a separate build task for Kit the Builder
  and has not landed.** Until it does, the current code does **not** conform to this
  amendment.

## Alternatives considered

**Leave the contract implicit, described only by whatever Core's C# types
happen to be.** Rejected — this is exactly the situation that let four
files drift into producing display text: nothing forced a reviewer to
notice a string leaving the platform-neutral layer. An explicit table that
every new field must be added to before a skin may consume it makes the
leak visible in review rather than in a file read six months later.

**Define the contract only in code (e.g. XML doc comments on a C#
interface), not in a versioned markdown document.** Rejected for this
phase — no implementation exists yet to attach doc comments to, and the
PDR's documentation standard (§7) requires the contract to exist as a
reviewable artifact before the first Core type is written, not after.
Once implementation begins, the code-level contract (interfaces, DTOs)
should be kept in sync with this table; this ADR does not preclude
generating one from the other later, only requires that this table remains
the source of truth for what the contract *means*.

## Consequences

**Positive:**
- Every future skin — including a hypothetical future OS skin the PDR's
  symmetric design already permits — has one place to read the complete,
  typed, unit-labelled, trust-labelled surface Core exposes.
- The "never fabricate a number" principle becomes machine-checkable in
  spirit: any value without a status flag is, by this contract, not a
  valid Core output.
- Extraction work (Kit the Builder's next slice) has an explicit target to
  build against, rather than reverse-engineering it from the files being
  removed.

**Negative:**
- This table must be kept current by hand as fields are added or changed;
  letting it drift from the actual Core implementation would recreate the
  same "documentation claim outran verified reality" problem this
  project's own evidence-labelling discipline exists to catch. Per the
  documentation standard ([README](../../README.md)), no skin may be
  merged that consumes a contract field not yet documented here.
- Some values (e.g. `WeeklyResetSource`, `DataSourceKind`) expose internal
  provenance detail that not every skin will choose to surface. That is a
  skin decision, not a contract gap — the contract's job is to make the
  information available and honestly labelled, not to decide how much of
  it a given UI shows.
