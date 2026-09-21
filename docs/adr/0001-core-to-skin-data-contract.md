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
| `SessionResetAt` | timestamp, UTC, ISO-8601 | instant | real / estimated / unavailable | skin owns locale/format rendering |
| `WeeklyUtilizationPercent` | float64, 0–100 | percent | real / estimated / unavailable | 7-day rolling window |
| `WeeklyResetAt` | timestamp, UTC, ISO-8601 | instant | real / estimated / unavailable | sourced from cached exact reset (source repo ADR-0014) when present, else derived — see `WeeklyResetSource` |
| `WeeklyResetSource` | enum {`CachedExact`, `Derived`} | — | real | transparency flag distinguishing the two reset-detection paths (source repo ADR-0007/0014) |
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
