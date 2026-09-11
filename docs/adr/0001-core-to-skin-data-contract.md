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
