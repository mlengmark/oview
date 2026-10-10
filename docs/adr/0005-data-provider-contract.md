# ADR-0005: The data-provider contract — how Core reads Claude's data off this machine

- **Status:** **Accepted 2026-09-28.** The board signed off on this addendum
  as the basis for cutting Phase 2 build tasks, and on the same day answered
  board question A — gate **G6 is open** on the narrow terms below. Question B
  (SQLite) was answered in [ADR-0006](0006-local-storage-contract.md): yes.
  No question on this record is open. Acceptance still authorises no slice on
  its own; each slice in the table below is its own PR and its own review.
- **Date:** 2026-09-27 · **Amended** 2026-10-02 (D6c, OVI-326/OVI-332);
  2026-10-09 (**gate G7 parity** — D2 Cowork ingest source, two more D6c ledger
  queries, new D7 account-identity reader; OVI-585, accepted by the board on
  OVI-591, card `4e515cef`, transcribed into this record by OVI-607);
  2026-10-10 (D6c's "built" note for gate G7 parity slice P6, OVI-635)
- **Deciders:** proposed by Adrian II the Architect; accepted by the Oview board 2026-09-28
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`), §3 and
  §3.3 — the *input* half of the Core boundary, which Phase 1 did not touch
- **Companion records:** [ADR-0006](0006-local-storage-contract.md)
  (what Core persists), [ADR-0007](0007-app-shell-contract.md) (who drives
  the poll and owns the process)
- **Evidence standard:** every claim below is **CONFIRMED** (read directly
  against `mlengmark/O-view` at commit `897777b`, the commit OVI-4 verified)
  or **INFERRED**. No label is promoted from its source.

## Context

Phase 1 emptied Core of display strings. It said nothing about where Core's
numbers come from, because in this repository they come from nowhere yet:
every field in [ADR-0001](0001-core-to-skin-data-contract.md) is populated
by a test fixture. `UsageSnapshot` exists; nothing fills it.

Phase 2's first question is therefore the *input* boundary. The source
repository has a worked answer to it — nine years of incident history
compressed into `src/O-view.Core/Providers/`, which this ADR reads as
evidence rather than reinventing. What follows separates that answer into
the parts that are contract (and should be carried forward deliberately)
and the parts that are accident.

### What the source repository actually does — CONFIRMED at `897777b`

- **One interface, one method.** `IUsageProvider.GetSnapshot(DateTimeOffset
  utcNow)` returns a `UsageSnapshot`. Two properties are stated in its own
  doc comment: implementations **must never throw** (unavailable or
  malformed data yields `UsageSnapshot.None`), and **`utcNow` is injected
  rather than read from a clock**, "so staleness and reset prediction are
  testable."
- **Three provider families, not one reader.** `Providers/CachedUsage/`
  (Claude Code's own cached utilization figures),
  `Providers/Jsonl/` (Claude Code's per-turn transcript logs), and
  `Providers/PlanHistory/` (Claude Desktop's sampled plan-history file).
  These are three *different on-disk artefacts written by two different
  vendor applications*, not three tiers of one reader.
- **Composition is by information value, not list order.**
  `CompositeUsageProvider` resolves the chain `PlanHistoryProvider →
  CachedUtilizationProvider → (OAuth, deferred) → JsonlUsageProvider`, but
  its doc comment is explicit that order is a tie-break only: any `Live`
  snapshot beats any `Stale` one, stale authoritative percentages beat an
  estimate with no percentages at all, and within a tier the winner is
  "whichever describes the account most completely and most recently."
  The winning snapshot **keeps its own `Source`** so the skin can label it
  honestly.
- **Path resolution is a pure function of a search root.**
  `ClaudeDataRoots` enumerates every directory Claude Desktop might use —
  canonical (`%APPDATA%\Claude`, `~/.config/Claude`), Windows MSIX
  (`%LOCALAPPDATA%\Packages\<family>\LocalCache\Roaming\Claude`), Linux
  Snap (`~/snap/<snap>/current/.config/Claude`), Linux Flatpak
  (`~/.var/app/<app-id>/config/Claude`). Its own comment records why:
  assuming the canonical path "already cost this project one 'no usage
  data' report against a machine where Claude Desktop was open and
  working." Only one member (`Redirected()`) asks what OS this is; every
  layout rule takes an injected root and so is testable on either runner.
- **A swallowed provider failure is invisible unless someone asks.**
  `CompositeUsageProvider` carries an `Action<string>? Log` seam, added
  because the catch around each provider is otherwise perfectly silent.
  Measured in the field, per that comment: **transcript ingestion failed on
  every poll for five days** while the panel showed live percentages from a
  sibling provider and the support bundle reported `status : Ok`. Nothing
  named the failing provider, because nothing was asked to.

### The one thing in that design this ADR will not carry forward silently

`Providers/CachedUsage/ClaudeCliRefresher.cs` **starts a Claude Code
process** (`System.Diagnostics`, CONFIRMED by its imports and its
`RefreshOutcome` enum) to make Claude Code refresh its own usage cache,
because that cache "refreshes only when Claude Code fetches usage" and was
measured at **43 hours stale** on the development machine
([`WeeklyResetAnchor`](https://github.com/mlengmark/O-view/blob/897777b/src/O-view.Core/Storage/WeeklyResetAnchor.cs),
CONFIRMED).

O-view does not write the vendor's files. It causes the vendor to write
them. Whether that satisfies "**read-only against every vendor data
source**" is not a question this ADR gets to answer by choosing a code
layout — see board question A.

## Decision

### D1 — One input contract: `IUsageProvider`, carried forward as-is

Core defines exactly one input seam:

```
UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
```

with both of the source's stated obligations promoted from doc comment to
contract:

| Obligation | Rule | Why it is contract, not style |
|---|---|---|
| Never throws | Any failure, malformed file, missing directory or permission error yields `UsageSnapshot.Unavailable` — this repository's existing sentinel (`src/O-view.Core/Models/UsageSnapshot.cs`), not a new `.None` member; the source repo names the same sentinel `UsageSnapshot.None` (see above), but this repo already carries it forward under the name ADR-0001 established | A monitoring tool that dies on a bad poll is worse than one showing a stale number (CONFIRMED — `UsageEngine`'s own doc comment states this as the design) |
| Clock is injected | `utcNow` is a parameter, never read inside a provider | Staleness and reset prediction are otherwise untestable (CONFIRMED — `IUsageProvider`'s doc comment) |
| Read-only | A provider opens vendor files for reading and never writes, moves, or truncates them | Standing product principle; OVI-4 confirmed the running app writes only to its own store |
| No display text | A provider returns contract values with status flags, never a sentence | [ADR-0001](0001-core-to-skin-data-contract.md) |

**Rejected: a richer interface** (`TryGetSnapshot`, `Task<UsageSnapshot>`,
an observable stream). The single synchronous method is what makes the
never-throw rule enforceable and the composite trivially testable; the
polling loop that would consume a stream belongs to the shell
([ADR-0007](0007-app-shell-contract.md)), not to Core.

### D2 — Three named providers for the Claude source, not one "Claude reader"

Under G5 this repository reads **one vendor's data (Claude) and no other**.
But that one vendor writes three unrelated artefacts, and collapsing them
into a single reader would hide exactly the distinctions the skins must
render:

| Provider | Reads | Produces `DataSourceKind` | Notes |
|---|---|---|---|
| `CachedUtilizationProvider` | Claude Code's cached utilization block (`~/.claude.json`) | `Live`, or `Stale` when the block's own fetch timestamp is old | The only source of authoritative percentages and of the exact weekly reset instant (ADR-0006's anchor) |
| `PlanHistoryProvider` | Claude Desktop's sampled plan-history file, located via `ClaudeDataRoots` | `Live` / `Stale` | Samples roughly every 5 minutes (CONFIRMED — `CompositeUsageProvider`) |
| `JsonlUsageProvider` | Claude Code's per-turn transcript logs | `JsonlFallback` / `Estimate` | Token counts, not percentages. Claude Code deletes these after ~30 days (CONFIRMED — `RollupStore`), which is why ADR-0006 exists |

**Rejected: one `ClaudeUsageProvider` with three private strategies.** It
reads as tidier and is worse: the composite's selection rule needs to
compare snapshots *across* these artefacts, and the field incident above
happened precisely because one failing artefact was indistinguishable from
one with no data. Named providers are what make "transcript ingestion is
failing" a sentence anyone can write.

**This is not a step toward a second AI source.** `IUsageProvider` is the
pluggable input shape PDR §3.3 already describes, and the three providers
above are Claude-specific by name and by content. Gate G5 stands: no
provider for any other vendor may be designed, named, or shaped for until
that vendor has had its own audit. Nothing in this ADR is generalized
"ready for" a second source, and the shape must not be defended on those
grounds in review.

> **2026-10-09 amendment (OVI-585/591, gate G7) — a fourth Claude-family source:
> Cowork audit logs.** The rebuild reads none today (CONFIRMED — zero references
> in `src/`, against 23 files in the source repository), so the tiles, the graph
> and every per-model figure **undercount on any machine that uses Cowork**.
> Corrected against OVI-584's framing: audit logs are transcript-shaped token
> records, not a plan meter, so this is an **ingest source on
> `JsonlUsageProvider`'s path writing to the ledger**, not a fifth snapshot
> source. It reports its own provenance (`JsonlFallback`), is read-only, and is a
> Claude-family artefact — **G5 is untouched**: Cowork is the same vendor, and
> nothing here is shaped for, named for, or defended as readiness for any other
> system. Treating it as another snapshot source would put token counts on the
> tooltip's path, which D6b and [ADR-0008](0008-presentation-skin-contract.md)
> D9d both already rejected.

### D3 — Composition selects by information value; the winner keeps its own source label

`CompositeUsageProvider` is carried forward with the source's rule intact,
corrected for this repository's four-tier enum: tier first (`Live` beats
`Stale` beats `JsonlFallback` beats `Estimate`; `Unavailable` is excluded
from ranking rather than ranked last — an `Unavailable` reading carries no
information to prefer over another `Unavailable` reading), then
completeness, then recency, then argument order as a final tie-break.
The winning snapshot's `DataSourceKind` travels to the skin unchanged.

**Rejected: strict precedence by list position.** The source repository
already tried that and amended it (ADR-0002 precedence, amended by
ADR-0007, CONFIRMED in `CompositeUsageProvider`'s comment): with two
sources reporting the same meters, position meant showing a reading up to a
sampling interval old while a fresher one sat unread in the other source.

### D4 — Provider health is contract data, not a log line

**This is the one row this ADR adds to [ADR-0001](0001-core-to-skin-data-contract.md).**
The source's `Action<string>? Log` seam sends a swallowed failure to a file
the user never opens. That is what allowed five days of silently failed
ingestion behind a panel showing `status : Ok` (CONFIRMED). A log seam is
necessary and not sufficient: the skin cannot tell the user something it is
never handed.

Proposed new contract rows:

| Value | Type | Unit | Status flag | Notes |
|---|---|---|---|---|
| `ProviderHealth[]` | array of `{providerName: string, outcome: enum {Ok, NoData, Failed}, lastSuccessAt: timestamp \| null, consecutiveFailures: int32}` | — | real | One entry per provider consulted on this poll. `NoData` and `Failed` are different facts and must not be merged |
| `DegradedInputCount` | int32 | count | real | How many providers are currently `Failed`. Lets a skin decide to say something without walking the array |

Core detects and counts; **the skin decides whether and how to surface it**
— a caveat line, a tray-icon state, or nothing at all, per each skin's own
wording ([ADR-0003](0003-paneltext-anti-drift-mechanism.md)). Core emits no
sentence here, exactly as everywhere else.

`Action<string>? Log` is kept **in addition**, for diagnostics-bundle
detail. It is not the user-facing path.

### D5 — Vendor path resolution is a pure function of an injected root

Carried forward from `ClaudeDataRoots` unchanged in spirit:

- Every candidate-directory layout rule is a pure function of a search root
  passed in, so both platforms' rules are exercised by tests on either
  runner.
- Exactly one member may ask what OS this is, and it only *selects which
  layouts apply* — it never computes a path.
- Sandboxed/redirected layouts are first-class, not a fallback:
  **Windows MSIX, Linux Snap, and Linux Flatpak** are enumerated for the
  authorized platforms. macOS layouts are **out of scope (G3)** and must
  not be added speculatively.
- A provider that finds no root returns `UsageSnapshot.Unavailable` (already
  `DataSourceKind.Unavailable`). It never guesses a path into existence.

**Rejected: `Environment.SpecialFolder` called directly inside a
provider.** It is what makes the layout rules untestable off-target, and
the source already avoids it for exactly this reason.

### D7 — An account-identity reader, which is not an `IUsageProvider` — added 2026-10-09 (OVI-585/591, gate G7)

`IAccountIdentitySource.GetIdentity()` reads `~/.claude.json` → `oauthAccount`,
located through D5's injected root, and returns
[ADR-0008](0008-presentation-skin-contract.md) D9e's `AccountIdentity`. It takes
no clock (identity is not time-varying), produces no `DataSourceKind`, and is not
part of composition (D3). A missing or malformed file yields
`AccountIdentity.Unavailable`, never a guess.

**It reads only `oauthAccount`'s display name, email and `organizationType`, and
touches no token field in that file, by contract.** The standing "no credential
handling" principle is the reason, and this reader is the first thing in the
repository that opens a file which also contains credentials — so the restriction
is contract, with a test asserting the reader never surfaces any other key.

*Rejected: putting identity on `CachedUtilizationProvider`*, which already opens
that very file. It would make one provider answer two unrelated questions and
put identity on the tooltip's poll path.

The tier value itself is `oauthAccount.organizationType` and nothing else;
`seatTier` and `userRateLimitTier` are empty in the artefact (CONFIRMED in the
source repository per OVI-584's evidence table; **INFERRED** here — no live
`~/.claude.json` has been read from this repository, and the slice that builds
this owes that reading).

## Board questions

These are not rhetorical, and none is answered by this ADR.

**Amendment, 2026-09-28 (second, superseding the first).** Read this before
the question text below it. The board approved the addendum in the morning
without answering A or B; re-asked as a structured question, it answered both
the same day:

- **A — answered: allow it, as gate G6.** Gate **G6 is open**. The standing
  principle "read-only against vendor data" is hereby narrowed in writing,
  not silently excepted: O-view may run the vendor's own command so that the
  vendor refreshes its own cache, subject to the three limits stated below,
  which are now binding contract and not a recommendation. The
  `CachedUtilizationProvider` slice is unblocked.
- **B — answered in ADR-0006: yes, SQLite is allowed.** The ledger slice is
  unblocked. See that record for the terms.
- **C needed no answer** — it asked the board to leave G5 as it is, and it is
  unchanged. No second vendor source may be audited, designed for, or named.

**Gate G6, as opened (binding):** O-view may invoke a vendor's own command
only when all three hold — (i) it is the vendor's own documented,
read-shaped command; (ii) it is never passed arguments that mutate vendor
state; (iii) it is never a precondition for showing a number, so a machine
where the refresh does nothing still works. A slice that needs a fourth
freedom needs the board again, not a wider reading of these three.

### Board question A — does "read-only against vendor data" permit invoking the vendor's own CLI? (proposed gate G6) — ANSWERED 2026-09-28: yes, as G6

**The fact, CONFIRMED:** the source repository's `ClaudeCliRefresher`
starts a Claude Code process so that Claude Code will refresh its own usage
cache, because that cache is otherwise routinely stale (measured at 43
hours). O-view writes nothing; it causes the vendor to write.

**Why it needs the board and not me.** "Read-only against every vendor data
source" is a standing product principle the board has told this project not
to renegotiate, and this is a case the principle's wording does not cover.
Reading it strictly ("cause no vendor write") forbids the refresher and
accepts a materially worse product: no exact weekly reset instant on most
machines, and percentages that can be two days old. Reading it loosely
("never write vendor files ourselves; invoking a vendor's own documented
command is use, not mutation") permits it and narrows the principle to
something the code can still be held to.

**Recommendation:** adopt the narrower reading explicitly, as an amendment
to the principle rather than a silent exception, with three limits written
into it: only the vendor's own documented, read-shaped command; never with
arguments that mutate vendor state; and never as a precondition for showing
a number, so that a machine where the refresh does nothing still works.
**Do not let this be settled by a slice getting merged.** If the board
prefers the strict reading, say so before the `CachedUsage` slice is
scoped — it changes what that slice can contain and lowers what ADR-0006's
anchor can promise.

**Board answer, 2026-09-28: the narrower reading, adopted as gate G6, with
all three limits.** They are restated at the top of this section as binding
contract. The `CachedUtilizationProvider` slice may be scoped.

### Board question B — SQLite as a dependency — ANSWERED 2026-09-28: yes

Deferred in full to [ADR-0006](0006-local-storage-contract.md), board
question C, which the board answered *yes* on 2026-09-28. Flagged here only
because the `Jsonl` provider is the thing that feeds it, and the two slices
land next to each other.

### Board question C — G5's boundary, restated for the record

This ADR designs an input seam (`IUsageProvider`) that PDR §3.3 describes
as pluggable. **It is not a request to open G5, and the board should read
it as unchanged:** no second vendor may be audited, designed for, or named.
If anyone later cites D1/D2 as evidence that a second source is "already
supported," that is a misreading of this record, and this paragraph exists
so the misreading is refutable in writing.

## Alternatives considered

**Design the provider layer fresh, without reading the source
repository's.** Rejected. Three of the five rules above exist in the source
only because a specific incident forced them (the canonical-path
assumption, the position-based precedence, the silent five-day failure).
Designing fresh means re-paying for all three.

**Port the source's provider layer verbatim, comments and all.** Rejected
for one reason only: it would carry `ClaudeCliRefresher` in with everything
else and thereby settle board question A by not asking it.

**Have providers write their own freshness state.** Rejected — a provider
is read-only by D1, and freshness is either in the vendor's own artefact or
in Core's store ([ADR-0006](0006-local-storage-contract.md)).

## Consequences

**Positive:**
- Every number a skin renders will be traceable to a named provider and a
  status flag, with no path through Core that produces an unlabelled
  figure.
- `ProviderHealth` (D4) closes a CONFIRMED class of field failure — a
  wrong-but-plausible number shown for days — at the cost of two contract
  rows and no new mechanism.
- The pure-function path rules (D5) mean the Linux provider layer is
  testable on Windows CI, which matters because this repository has no
  non-Windows CI (CONFIRMED — `CLAUDE.md`).

**Negative:**
- **Nothing here is hardware-verified on Linux.** The Snap and Flatpak
  layouts are carried forward from the source's rules, which were themselves
  written against a Windows-and-Arch/KDE evidence base
  ([ADR-0002](0002-cross-platform-capability-matrix.md)). Treat every Linux
  path claim as **INFERRED** until someone reads a real redirected store.
- D2's three named providers are three slices, not one. That is more PRs
  than a single reader would have been, and deliberately so.
- Gate G6 (board question A, answered 2026-09-28) narrows a standing product
  principle rather than leaving it absolute. The cost is that "read-only"
  now needs its three limits quoted with it; the benefit is that the limits
  are written down and testable instead of implied by whatever a slice did.

## Slicing guidance for decomposition

Lowest risk first, each independently mergeable, per PDR §8's board
question 4 recommendation:

| # | Slice | Depends on | Risk |
|---|---|---|---|
| 1 | `IUsageProvider` + `UsageSnapshot.Unavailable` + the never-throw structural test — **landed** (PR #35, 2026-09-28, OVI-188) | — | Lowest — one interface, no I/O |
| 2 | `ClaudeDataRoots`-equivalent path rules, pure, injected roots, no provider yet — **landed** (PR #36, 2026-09-29, OVI-200) | 1 | Low — pure functions, fully testable |
| 3a | Transcript-line parsing and de-duplication: `TranscriptTokens`, `TranscriptRecord`, `TranscriptParser` — pure functions over strings, no I/O — **landed** (PR #38, 2026-09-29, OVI-203) | 1, 2 | Low — string fixtures only |
| 3b | `JsonlUsageProvider` proper: locates transcripts under `ClaudeDataRoots.CandidateRoots`, reads files, produces a `UsageSnapshot` shaped exactly per D6a — **landed** (PR #41, 2026-09-29, OVI-219) | 1, 2, 3a | Medium — real file parsing |
| 4 | `PlanHistoryProvider` — **landed** (PR pending, 2026-09-29, OVI-222) | 1, 2 | Medium |
| 5 | `CachedUtilizationProvider`, incl. the vendor-refresh call under G6's three limits — **landed** (PR #43, 2026-09-30, OVI-227) | 1, 2 | Medium — G6 open since 2026-09-28; no longer gated |
| 6 | `CompositeUsageProvider` + `ProviderHealth` (D3, D4) — **landed** (PR #48, 2026-09-30, OVI-243) | 3, 4, 5 | Medium — needs ≥2 providers to be meaningful |

Slice 6's `ProviderHealth` rows must land in
[ADR-0001](0001-core-to-skin-data-contract.md) before any skin consumes
them, per that ADR's own rule.

- **2026-09-28 update — slice 1 landed (Kit the Builder, OVI-188).**
  `src/O-view.Core/Providers/IUsageProvider.cs` carries D1's single method
  forward as written above. D1's four obligations are now enforced by
  `IUsageProviderContractTests` (`tests/O-view.Core.Tests/Providers/`)
  rather than by prose: never-throws is proven by a test double that throws
  from every internal path and still returns a snapshot through the seam;
  clock-injection is proven at the interface's shape (`GetSnapshot`'s only
  parameter is `utcNow`) since no implementation exists yet to scan for a
  direct clock read — each provider slice (2 onward) carries its own version
  of that check. Read-only and no-display-text are not independently
  testable at this slice (there is no I/O yet); they carry forward as
  obligations on slices 3-5's implementations instead.
  **Correction to D1's text above:** this repository already has the "no
  data" sentinel D1 describes — `UsageSnapshot.Unavailable`
  (`DataSourceKind.Unavailable`, every value status-flagged, established by
  ADR-0001 and in use across all three skins' tests). The source repository
  names the same sentinel `UsageSnapshot.None`; this ADR's D1 text originally
  carried that name forward without checking it against this repository's
  already-landed Core code. `.None` was never added — doing so would have
  given Core two names for one sentinel. The table above and the two
  `UsageSnapshot.None` references earlier in this decision have been
  corrected to `UsageSnapshot.Unavailable` to match.

- **2026-09-29 update — slice 2 landed (Kit the Builder, OVI-200).**
  `src/O-view.Core/Providers/ClaudeDataRoots.cs` carries D5 forward as pure,
  injected-input functions: `WindowsCanonical`, `WindowsMsix`,
  `LinuxCanonical`, `LinuxSnap`, and `LinuxFlatpak` each resolve one layout
  from directly-supplied root strings and vendor identifiers, returning
  `null` rather than guessing when a required input is missing.
  `CandidateRoots(ClaudeDataRootInputs)` is the one member D5 allows to be
  platform-aware; it does not detect the platform — `ClaudeDataRootInputs`
  carries an injected `ClaudeHostPlatform` value, so the member only
  *selects* which layout rules to call, never reading the real OS or
  environment. No provider exists yet and no I/O is performed anywhere in
  this file (proven by `ClaudeDataRootsTests`, which exercises every rule
  with fake roots only). `WindowsPackageFamilyName`, `LinuxSnapName`, and
  `LinuxFlatpakAppId` have no default values: this ADR's D5 text records the
  *shape* of the MSIX/Snap/Flatpak layouts, not a confirmed exact vendor
  identifier for any of them, and this slice does not fabricate one — a
  caller (slice 5's `CachedUtilizationProvider` or its host) supplies the
  real identifier when one is confirmed. macOS is out of scope (G3) and adds
  no `ClaudeHostPlatform` member. Slices 3-5 consume `CandidateRoots` to
  build real providers; none of that I/O exists in this slice.

- **2026-09-29 update — slice 3a landed (Kit the Builder, OVI-203).** Slice 3
  (the row above's original `JsonlUsageProvider`) is split into 3a and 3b:
  the source repository's `Providers/Jsonl/` is 1,949 lines across 11 files
  and depends on `OView.Core.Pricing` (`TokenSplit`, `UsageModifiers`,
  `CostEstimator`), a namespace that does not exist in this repository —
  taken whole, the slice would both exceed ~500 changed lines and silently
  decide how Core prices tokens. 3a lands only the part that needs neither:
  `src/O-view.Core/Providers/Jsonl/TranscriptTokens.cs` (a `readonly record
  struct` carrying `InputTokens`, `OutputTokens`, `CacheCreationInputTokens`,
  `CacheReadInputTokens`, and the two TTL fields), `TranscriptRecord.cs`
  (`RequestId`, `TimestampUtc`, `Model`, `Tokens` — no pricing type), and
  `TranscriptParser.cs`, a static class with `TryParseAssistantRecord`
  (string in, `TranscriptRecord` out, never throws) and `Deduplicate`
  (groups by `RequestId`, keeps the last occurrence in file order). No file
  I/O, no clock read, and no `UsageSnapshot` exist anywhere in this slice —
  proven by `TranscriptParserTests`, which exercises every rule with string
  fixtures only.
  **`CacheCreationEphemeral5mTokens` and `CacheCreationEphemeral1hTokens`
  are nullable on purpose.** `usage.cache_creation` is not present on every
  record — a record whose `cache_creation` object is missing or unreadable
  is not attributed to either TTL, and writing `0` there would fabricate a
  number this project has promised never to fabricate; the flat
  `cache_creation_input_tokens` beside it still carries the unattributed
  total. The synthetic-record marker (`<synthetic>`) is compared
  case-insensitively in exactly one place, `TranscriptParser`, rather than
  the three diverging branches the source repository carried (GitHub issue
  #57) — synthetic records are dropped, not stored at zero, because every
  one measured carries all-zero usage.
  **Deliberately deferred to whichever slice adds `OView.Core.Pricing`:**
  `usage.speed` and `usage.inference_geo`, the two published pricing
  modifiers. They are load-bearing only under a rate card, and this slice
  parses no pricing at all — not even inactive fields — rather than add a
  `Pricing` namespace speculatively. 3b (`JsonlUsageProvider` proper:
  locating transcripts under `ClaudeDataRoots.CandidateRoots`, reading
  files, producing a `UsageSnapshot`) depends on 1, 2, and this slice, and
  is not yet filed — it had an open design question of its own, about which
  `UsageSnapshot` field a token-count source populates when the type carries
  percentages. That question is **closed by the OVI-204 amendment directly
  below** (D6a): the answer is none of them.

- **2026-09-29 amendment (OVI-204) — what a JSONL-sourced snapshot actually
  populates, and where token counts reach a skin.** Briefing slice 3a
  (OVI-202) surfaced that D2's row for `JsonlUsageProvider` — "token counts,
  not percentages" — never said what the snapshot it must return therefore
  *contains*. `UsageSnapshot` carries percentages and reset instants and
  nothing else; token counts live on `UsageStatistics`
  ([ADR-0001](0001-core-to-skin-data-contract.md)). Slice 3b could not be
  briefed without closing that. The three answers below are **D6**, plus two
  corrections to text already written in this record. **D1 is untouched:**
  `IUsageProvider` keeps its single method returning one `UsageSnapshot`, so
  none of this needed board escalation.

  **D6a — A JSONL-sourced snapshot carries provenance and time, not meters.**
  `JsonlUsageProvider.GetSnapshot` returns either `UsageSnapshot.Unavailable`
  (the scan found no readable transcript record at all) or a snapshot whose
  fields are exactly:

  | Field | Value | Why |
  |---|---|---|
  | `DataSourceKind` | `JsonlFallback` | see D6b |
  | `LastIngestAt` | the `utcNow` this call was made with, once the ingest has read something | This field is *O-view's own capture time* (ADR-0001's row), not the age of the newest activity in the file |
  | `SessionUtilizationPercent`, `WeeklyUtilizationPercent` | `Unavailable`, value `null` | transcripts record tokens spent, never the plan's limit; a percentage cannot be computed from them without a plan-limit table this repository does not have |
  | `SessionResetAt`, `WeeklyResetAt` | `Unavailable`, value `null` | transcripts record no reset instant; the weekly anchor is [ADR-0006](0006-local-storage-contract.md)'s store, reached by the composite, never fabricated here |
  | `UsageLevel` | `Green` | the documented sentinel `UsageSnapshot.Unavailable` already uses for "no band was computed"; with both percentages unavailable no skin reads it |
  | `ExtraUsage` | `null` | not a fact transcripts carry |

  **An all-unavailable snapshot under a non-`Unavailable` `DataSourceKind` is
  legal, and this record states so explicitly.** `DataSourceKind` is the
  provenance of a snapshot — *which reader produced it* — and has never been a
  claim that any particular value is present; every value carries its own
  `UsageValueStatus`. That is the entire reason per-value status flags exist
  (ADR-0001). CONFIRMED by read at `cd68e17`: both skins already render such a
  snapshot correctly today — `TooltipFormatter` fires its "local estimate ·
  usage % unknown" copy only for `DataSourceKind.Estimate`, so a
  `JsonlFallback` snapshot with null percentages renders `5h: ?` with the reset
  and weekly clauses omitted, and is not mislabelled. **Slice 3b needs no skin
  change.**

  **Correction to the behaviour carried forward from the source repository.**
  The source's `JsonlUsageProvider.GetSnapshot` returns
  `new UsageSnapshot(DataSource.Estimate, null, null, null, latest)` where
  `latest` is the store's *last recorded activity* — passed into the field this
  repository calls `LastIngestAt` (CONFIRMED at `897777b`). Those are two
  different facts, and conflating them makes a skin word "as of three days ago"
  for a read that happened a second ago. This repository does not carry that
  forward. Last-recorded-activity is real and worth having; it belongs on the
  statistics seam (D6c), which is built from the ledger.

  **D6b — `JsonlUsageProvider` emits `JsonlFallback` only. No provider in Phase
  2 emits `Estimate`.** D2's cell listed both without saying which applies when,
  and `DataSourceKind`'s own doc comment says `Estimate` is "modelled from token
  pricing" — which for a *snapshot* would mean modelling a **utilization
  percentage** from token counts. That needs a plan-limit table (how large is a
  5-hour window on this plan?), and this repository has none. Inventing one is
  the "never fabricate a number" principle failing exactly where it matters. So:
  a JSONL read is a *fallback source*, never an *estimate of a meter*, and
  `JsonlFallback` is the only tier it emits.

  `Estimate` stays in the enum, dormant: ADR-0001 established it, the skins
  already carry wording for it, and removing an accepted contract value to
  express "nothing emits this yet" costs a Core contract change for no gain. It
  is reserved for a future provider that can genuinely model a meter. Slice 3b
  carries one doc-comment correction with it: `DataSourceKind`'s `Estimate`
  summary is amended to name it as reserved and unemitted, and `JsonlFallback`'s
  to say the snapshot carries provenance and capture time rather than derived
  percentages.

  **Rejected: emitting `Estimate` with the percentages still null**, on the
  reading that "estimate" describes the provider's general confidence. It makes
  the two tiers indistinguishable in the only place they are observable, and it
  lights the skins' "local estimate" copy over a snapshot that is estimating
  nothing. **Rejected: a rate-card/plan-limit table to make the percentages
  computable.** That is its own decision with its own record, it needs vendor
  figures nobody here has confirmed, and it is not required to build slice 3b.

  **D6c — `UsageStatistics` reaches a skin by a second seam, reading the ledger
  — not through `IUsageProvider`.** The two types answer different questions
  from different sources: a `UsageSnapshot` is *the plan meter now*, read live
  from vendor artefacts by a provider; `UsageStatistics` is *accumulated local
  history* (today's and the 31-day window's tokens and estimated spend), which
  by [ADR-0006](0006-local-storage-contract.md)'s whole justification can only
  come from Core's own ledger, because the vendor deletes the transcripts behind
  it after ~30 days. Core therefore defines a second read seam over the ledger:

  ```
  UsageStatistics GetStatistics(DateTimeOffset utcNow)
  ```

  with D1's same four obligations (never throws — yields
  `UsageStatistics.Unavailable`; clock injected; read-only against vendor data;
  no display text). It is **not** an `IUsageProvider`, is not part of
  composition (D3), and has no `DataSourceKind` — statistics are not tiered by
  provenance; each value carries its own status flag and the `Rates` stamp
  beside it. This is what the source repository already does (CONFIRMED at
  `897777b`: `PanelStatistics` is built from rollups queried out of
  `RollupStore`, never from `IUsageProvider`); this amendment writes the seam
  down rather than inventing one.

  > **2026-10-02 amendment (OVI-326, [ADR-0008](0008-presentation-skin-contract.md)
  > D9).** This seam is still unimplemented — nothing in `src/` produces a
  > `UsageStatistics` (CONFIRMED). Phase 3 needs it, and needs a second query
  > beside it, so D6c is extended rather than re-decided: **one interface over
  > the ledger with two queries**, `GetStatistics(utcNow)` and
  > `GetModelBreakdown(utcNow)`, both under D1's same four obligations. The
  > per-model query returns ADR-0008 D9a's `ModelUsageBreakdown` — Core-side
  > aggregation per model over a stated window, never the storage record
  > `DailyModelUsage` and never per-(date × model) rows for a skin to total.
  > Both are built in ADR-0008's slice 5a. Nothing above changes: it is still
  > not an `IUsageProvider`, still not part of composition, still has no
  > `DataSourceKind`.
  >
  > **2026-10-02, as built (OVI-332):** both queries also take a
  > `TimeZoneInfo zone` parameter — `GetStatistics(utcNow, zone)` and
  > `GetModelBreakdown(utcNow, zone)` — not shown in the signature above. Local-day
  > bucketing (today vs. the 31-day window) cannot happen without one, and this
  > repository's one other local-day aggregator, `UsageLedgerStore.QueryDailyUsage`,
  > already established that the zone is always a caller-supplied parameter, never
  > `TimeZoneInfo.Local` read internally (ADR-0006 D2). The interface is
  > `IUsageStatisticsSource` (`src/O-view.Core/Statistics/`), implemented by
  > `LedgerUsageStatisticsSource`. No rate table exists in this repository yet
  > (`RateCardSource` is reserved, nothing emits a `RateCardStamp`), so every
  > `EstimatedUsd` both queries produce is `UsageValueStatus.Unavailable` —
  > honest, not a placeholder; pricing is a later amendment with its own seam.
  >
  > **2026-10-09 amendment (OVI-585/591, gate G7) — two more ledger queries.**
  > Under D1's same four obligations and the same `(utcNow, zone)` parameter pair
  > as the existing two: `GetDailySeries(utcNow, zone)` →
  > [ADR-0008](0008-presentation-skin-contract.md) D9e's `DailyUsageSeries`, and
  > `GetTokenKindTotals(utcNow, zone, window)` → `TokenKindTotals`. Still not an
  > `IUsageProvider`, still not in composition, still no `DataSourceKind`.
  > `UsageLedgerStore.QueryDailyUsage(zone)` already returns (local date × model)
  > rows, so both queries are aggregation over data this repository already holds
  > (CONFIRMED).
  >
  > **Reset boundaries are derived, not stored.** `WeeklyResetAnchorStore` holds a
  > single anchor (`Read()` / `Save()`, CONFIRMED). The boundary list is stepped
  > back from it at query time and labelled per D9e's three kinds
  > (`Observed`, `DerivedFromObserved`, `MondayFallback`); with no anchor, the
  > list is Monday-fallback and says so. Nothing new is persisted —
  > [ADR-0006](0006-local-storage-contract.md) D1's "rollups are computed at query
  > time, never stored" covers this.
  >
  > **2026-10-10 — slice P6 landed (Kit the Builder, OVI-635).** All three ride
  > the same `IUsageStatisticsSource`/`LedgerUsageStatisticsSource` seam 5a
  > already built, under D1's same four obligations:
  > `GetDailySeries(utcNow, zone)` and `GetTokenKindTotals(utcNow, zone, window)`
  > (a new `StatisticsWindow { Today, ThirtyOneDays }` enum selects the window;
  > `IUsageProvider` is still untouched) aggregate the same
  > `UsageLedgerStore.QueryDailyUsage` rows seam 5a's two queries already read —
  > no new ledger query, no schema change. `GetResetBoundaries(utcNow, zone)`
  > takes an optional `WeeklyResetAnchorStore?` in the constructor (defaulting
  > to `null`, matching `CachedUtilizationProvider`'s own optional-store
  > pattern) and steps back from its stored anchor by local-calendar days, not
  > a fixed 168-hour duration, so a DST transition inside the 31-day window
  > changes a boundary's UTC instant by the real elapsed time rather than a
  > wrong one — covered by `LedgerUsageStatisticsSourceTests`' spring-forward
  > and fall-back fixtures, built against a synthetic `TimeZoneInfo` rather
  > than a named system zone so the fixtures never depend on the test
  > runner's own tz database. A day before the ledger's first recorded day is
  > `DailyUsagePoint.OutputTokens.Status == Unavailable` (a gap); a recorded,
  > idle day is `Real` with value `0` — proven by a dedicated test, not just
  > asserted in the doc comment. `DetailPushCoordinator.PushDetail` now
  > assembles `History`/`ResetBoundaries`/`TokensToday`/`Tokens31d` onto the
  > pushed `UsageDetail` the same way it already assembled `Statistics`/
  > `Models` (ADR-0008 D9g — no new seam member, no second schedule);
  > `Account` (D9e's fifth new member) is out of scope here (slice P7). No
  > skin reads any of the four assembled members yet.

  **Rejected: widening `IUsageProvider` to return both** (a second method, a
  tuple, or a combined record). D1's single method is accepted board contract,
  and widening it would force `PlanHistoryProvider` and
  `CachedUtilizationProvider` to return statistics they have no source for —
  every one of them answering "unavailable" forever, which is a shape that lies
  about what the seam is for. **Rejected: putting statistics on the composite.**
  The composite's job is choosing between snapshots of the same meters;
  statistics have exactly one source and nothing to choose between.

  **Correction to D3's tier order (superseded — D3's text is now current).**
  D3 originally wrote the ordering as "`Live` beats `Stale` beats `Estimate`
  beats `Unavailable`", carried over from the source's four-value enum, and
  omitted `JsonlFallback` — which this repository's enum has and the source's
  did not. D3 has since been corrected in place to read **`Live` > `Stale` >
  `JsonlFallback` > `Estimate`, with `Unavailable` excluded from ranking**
  rather than ranked last (an `Unavailable` reading carries no information to
  prefer over another `Unavailable` reading), matching the enum's own
  declaration order and ADR-0001's "still trusted above
  `JsonlFallback`/`Estimate`". The completeness tie-break that follows tier
  does the load-bearing work here: a JSONL snapshot with every meter
  unavailable never displaces one carrying real percentages at the same tier,
  and D3's rule already says so.

  **What slice 3b can now be briefed as:** a `JsonlUsageProvider` that scans
  transcripts via slice 2's `ClaudeDataRoots`, returns `UsageSnapshot`s shaped
  exactly by D6a's table, and emits `JsonlFallback` or
  `UsageSnapshot.Unavailable` and nothing else. The statistics seam (D6c) is
  **not** part of slice 3b — it depends on the ledger
  ([ADR-0006](0006-local-storage-contract.md) slice 2) and is sliced with it.

- **2026-09-29 update — slice 3b landed (Kit the Builder, OVI-219).**
  `src/O-view.Core/Providers/Jsonl/JsonlUsageProvider.cs` takes an injected
  candidate-root list — typically slice 2's `ClaudeDataRoots.CandidateRoots`
  result — and walks each root's `*.jsonl` files, most-canonical root first,
  returning as soon as `TranscriptParser.TryParseAssistantRecord` (slice 3a)
  parses one line. A hit returns exactly D6a's table: `JsonlFallback`,
  `LastIngestAt` set to the injected `utcNow` (never a file or transcript
  timestamp), both percentage/reset pairs `Unavailable`/`null`, `UsageLevel.Green`,
  `ExtraUsage` null. No readable record anywhere yields
  `UsageSnapshot.Unavailable`. No token totals are aggregated or returned —
  `UsageSnapshot` has no field for them (D6a); that is D6c's separate,
  not-yet-filed seam. Directory enumeration and every file read are wrapped in
  one `try`/`catch` for `IOException`/`UnauthorizedAccessException`/
  `SecurityException` — covering a subdirectory that disappears or denies
  access mid-walk, not only the initial existence check — so a missing root,
  an exclusively-locked file, or unparseable content all fall through to
  `Unavailable` rather than throwing, proven by `JsonlUsageProviderTests`
  (`tests/O-view.Core.Tests/Providers/Jsonl/`), including a real
  `FileShare.None`-locked file. `DataSourceKind.Estimate`'s and
  `JsonlFallback`'s doc comments carry D6b's two corrections in the same PR.
  No skin change: both skins already render an all-unavailable
  `JsonlFallback` snapshot correctly, per D6a's `TooltipFormatter` read.

- **2026-09-29 update — slice 4 landed (Kit the Builder, OVI-222).**
  `src/O-view.Core/Providers/PlanHistory/PlanHistoryProvider.cs` takes an
  injected candidate-root list — typically slice 2's
  `ClaudeDataRoots.CandidateRoots` result — and reads
  `plan-usage-history.json` under each root, most-canonical root first,
  stopping at the first root whose file parses to at least one valid sample.
  A hit returns `DataSourceKind.Live` when the newest sample's age is at most
  the freshness bound, else `Stale`; `SessionUtilizationPercent` and
  `WeeklyUtilizationPercent` carry that sample's `fh`/`sd` values as `Real`;
  `SessionResetAt`/`WeeklyResetAt` are `Unavailable`; `LastIngestAt` is the
  injected `utcNow`; `UsageLevel` is `Green`; `ExtraUsage` is `null`. No file,
  no `samples` array, or no sample that parses anywhere yields
  `UsageSnapshot.Unavailable`. Proven by `PlanHistoryProviderTests`
  (`tests/O-view.Core.Tests/Providers/PlanHistory/`): Live, Stale, boundary-
  exact freshness, multi-sample newest-wins, multi-root first-hit-wins,
  malformed-sample and missing-file `Unavailable`, and never-throws against
  both a `FileShare.None`-locked file and non-JSON content.

  **On-disk format — CONFIRMED at `897777b`**, read directly from
  `src/O-view.Core/Providers/PlanHistory/PlanHistoryFile.cs` and
  `PlanHistorySample.cs` in the source repository (read-only reference, never
  pushed to): a JSON object with a `samples` array; each element needs `t`
  (Unix epoch milliseconds, number), `org` (non-empty string), and `u.fh` /
  `u.sd` (integers, 0–100) to survive parsing, and anything else in the
  element is ignored. A malformed sample is skipped, not fatal to the file;
  a malformed or missing file yields no samples, not an exception. This
  slice ports that validation field-for-field.

  **Deliberately not carried forward — CONFIRMED present in the source at the
  same commit, out of scope for this slice.** The source's
  `PlanHistoryProvider.GetSnapshot` also runs `ResetDetector` (session-window
  boundary detection and next-reset prediction), narrows that window against
  local transcript activity via an injected store lookup, and resolves a
  preferred organization read from `~/.claude.json` to de-interleave a
  multi-org file, falling back to the file's most-recently-active org when no
  preference is supplied or matches. None of that machinery exists in this
  repository yet — no `~/.claude.json` reader, no transcript-activity lookup,
  no ADR-0006 store — and ADR-0005's D2 table itself assigns "the exact
  weekly reset instant" to `CachedUtilizationProvider` alone, not this
  provider. This slice therefore reports the two percentages the file states
  directly and nothing else: no session or weekly reset instant, and no
  per-organization filtering (equivalent to the source's own null-preference
  fallback — the file's latest sample wins regardless of which org it names).
  A future slice may reintroduce window/reset derivation here if a spec calls
  for it; this one does not invent it.

  **Correction to this ADR's own D2 table.** The table's `PlanHistoryProvider`
  row states "Samples roughly every 5 minutes (CONFIRMED — CompositeUsageProvider)".
  Reading the source at the same pinned commit (`897777b`) directly —
  `PlanHistoryProvider.DefaultFreshness`'s own doc comment — shows Claude
  Desktop's cadence changed from 5 to 15 minutes on 2026-08-10, measured
  against 1,443 real sampling gaps, and that the source's own freshness bound
  was independently re-measured and raised to 16 minutes for that reason (one
  interval at the new cadence plus a minute of slack). The D2 table's 5-minute
  figure was accurate once but predates that change; this slice's
  `DefaultFreshness` uses 16 minutes, the figure CONFIRMED current as of the
  same pinned commit this ADR cites elsewhere, and flags the table text as
  needing a correction rather than silently overriding it. No skin change:
  both skins already handle `DataSourceKind.Live`/`Stale`
  (`src/O-view.Tray/Presentation/PanelTextFormatter.cs`,
  `src/O-view.Linux/Presentation/PanelTextFormatter.cs`).

- **2026-09-30 update — slice 5 landed (Kit the Builder, OVI-227).**
  `src/O-view.Core/Providers/CachedUsage/CachedUtilization.cs` parses
  `~/.claude.json` -> `cachedUsageUtilization` (schema CONFIRMED by reading the
  source repository's `CachedUtilization.cs`/`CachedUtilizationProvider.cs`/
  `ExtraUsageStatus.cs` at `897777b`, read-only reference, never pushed to):
  `fetchedAtMs` (required — an undated block is refused wholesale),
  `utilization.five_hour`/`utilization.seven_day` (each `{utilization: 0-100,
  resets_at: ISO-8601}`, independently optional), and
  `utilization.extra_usage.is_enabled` (boolean). No escalation to Rae was
  needed — the schema was unambiguous in the source evidence.
  `src/O-view.Core/Providers/CachedUsage/CachedUtilizationProvider.cs`
  implements `IUsageProvider`: `Live` when the block's age is at most 15
  minutes, else `Stale`; a bar whose own `resets_at` is at or before `utcNow`
  is dropped rather than shown as a stale-but-plausible figure (the block is a
  cache, and can sit unrefreshed across a window boundary); both bars dropped
  yields `UsageSnapshot.Unavailable` rather than an empty non-`Unavailable`
  snapshot, so a future composite (slice 6) can still fall through to a source
  that knows something. `ExtraUsage` is populated only when `is_enabled` is a
  JSON boolean, stamped with the block's own `fetchedAtMs` (not
  `LastIngestAt`), consistent with the already-landed `ExtraUsageReading`
  contract (OVI-168). Every failure — no candidate root, missing file,
  malformed JSON, an exclusively-locked file — degrades to
  `UsageSnapshot.Unavailable`, proven by `CachedUtilizationProviderTests`
  (`tests/O-view.Core.Tests/Providers/CachedUsage/`) including a real
  `FileShare.None`-locked file.

  **Path resolution reuses `ClaudeDataRoots`, per this slice's brief, rather
  than porting the source's separate `ClaudeAccount` resolver.** The source
  keys `.claude.json` resolution off `CLAUDE_CONFIG_DIR` and the user profile
  via its own type; carrying that in would have been a second, parallel
  path-guessing scheme next to `ClaudeDataRoots` (D5), reading the environment
  a second way. Instead `ClaudeDataRoots.ClaudeCliConfigRoots(homeDirectory)`
  is a new pure function *on that same type*: `.claude.json` sits at the same
  relative location on both platforms (directly under, or under `.claude/`
  beside, the home directory), so unlike every other member of
  `ClaudeDataRoots` it takes no `ClaudeHostPlatform` at all. `CLAUDE_CONFIG_DIR`
  itself is **not** read anywhere in this repository — Core cannot read
  environment variables (D5/ADR-0006 D2); a future slice can extend this
  method with an explicit override parameter if the shell needs to inject one,
  and this record flags that as the deferral rather than a silent gap.
  `CachedUtilization.TryReadNewest` then picks the **freshest fetch across
  every candidate**, not the first that exists — carried forward from the
  source's own documented trap: Claude Code's 2026-08-24 migration to
  `~/.claude/.claude.json` left a stale-but-readable stub behind at the old
  path, and existence-first resolution picked the stub.

  **Deliberately deferred, CONFIRMED present in the source at the same
  commit, out of scope for this slice:** the source's zero-reading distrust
  window (an aged zero degrading to unavailable, sharing a threshold with
  `PlanHistoryProvider`) is not carried forward, because slice 4 did not port
  the constant it would share, and inventing a fresh one with no measurement
  behind it would itself be a fabricated number. The account-identity fields
  (`accountUuid`, and the source's richer `ExtraUsageStatus` — `userDisabled`,
  `spendLimitReached`, `disabledReason`) are not carried forward either:
  nothing in this repository's `UsageSnapshot` has a field for account
  identity, and `ExtraUsageState` (OVI-168) is already the two-member enum
  this repository settled on, with "unknown" already expressed by the parent
  `ExtraUsageReading?` being null — adding a richer record here would give
  Core two ways to say the same thing.

  `src/O-view.Core/Providers/CachedUsage/ClaudeCliRefresher.cs` implements
  gate G6 (ADR-0005, board-answered 2026-09-28) with all three limits
  structural rather than aspirational: (i) `UsageArgument` is exactly
  `"/usage"`, Claude Code's own documented slash command (CONFIRMED, source
  GitHub issue #234); (ii) it is the only entry ever added to
  `ProcessStartInfo.ArgumentList`, passed with `UseShellExecute` false — never
  through a shell, which is the source's own root-cause fix for a confirmed
  misinterpretation (Git Bash/MSYS path-translating `/usage` into a real
  prompt); (iii) proven structurally in
  `CachedUtilizationProviderTests.ConstructorTakesNoUsageCacheRefresherSoARefreshCanNeverGateAReading`
  that `CachedUtilizationProvider`'s constructor has no
  `IUsageCacheRefresher`/`ClaudeCliRefresher` parameter at all, so invoking a
  refresher is strictly the shell's (ADR-0007) polling decision, never inside
  the read path a poll depends on. `Refresh()` never throws and never reads
  the system clock (it compares the cached block's own `fetchedAtMs` before
  and after the run, needing no `utcNow`). Proven by `ClaudeCliRefresherTests`
  with every process outcome (`Refreshed`, `Unchanged`, `NotFound`,
  `TimedOut`, `Failed`) driven through an injected `ProcessRun` delegate — no
  test in this repository spawns a real `claude` process.

  **Deliberately deferred: the source's billed-invocation cost guard**
  (`BilledTranscriptGuard`/`TranscriptCostGuard`, which snapshots Claude
  Code's transcript tree before and after the spawn to detect a misrouted
  `/usage` that reached the model and was billed, measured at roughly 50K
  tokens per occurrence). This slice's boundary excludes touching
  `JsonlUsageProvider`/`TranscriptParser` beyond reuse, and that guard would
  need to read the same transcript tree those own. G6's three limits do not
  require it — they are satisfied by the argument-list-not-shell design
  above, which is the source's own confirmed root cause for the one
  misinterpretation case on record. A future slice may add the guard with its
  own review of the transcript coupling it would introduce; this one does not
  invent a narrower substitute.

  **ADR-0006 wiring.** `CachedUtilizationProvider` takes an optional
  `WeeklyResetAnchorStore` (ADR-0006, PR #40); when a call observes a
  current, not-yet-passed weekly reset instant, it is saved to that store as
  a side effect, so the exact instant survives the next stretch the vendor's
  cache spends stale (measured at 43 hours, ADR-0006). This slice only
  *writes* the anchor — nothing reads it back into a snapshot yet, per this
  slice's boundary against `CompositeUsageProvider`/`ProviderHealth` wiring
  (slice 6). No new field is added to `UsageSnapshot`;
  `WeeklyResetSource` (`CachedExact`/`UserEntered`, ADR-0001) remains
  documented-but-unimplemented, exactly as before this slice, since a source
  label for the field needs the composite that does not yet exist.

- **2026-09-30 update — slice 6 landed (Kit the Builder, OVI-243).**
  `src/O-view.Core/Providers/Composite/CompositeUsageProvider.cs` resolves the
  chain `PlanHistoryProvider` → `CachedUtilizationProvider` →
  `JsonlUsageProvider` (OAuth deferred) into one `UsageSnapshot`, taking an
  ordered `IReadOnlyList<NamedUsageProvider>` (a provider paired with the
  stable name its `ProviderHealth` entry reports under) and an optional
  `Action<string>? log`. Selection ranks every candidate not itself
  `DataSourceKind.Unavailable` by D3's rule exactly: tier first (`Live` >
  `Stale` > `JsonlFallback` > `Estimate`, per D3), then completeness (how many of
  `SessionUtilizationPercent`/`SessionResetAt`/`WeeklyUtilizationPercent`/
  `WeeklyResetAt` are not `UsageValueStatus.Unavailable`), then recency
  (`LastIngestAt`, newest first), then argument order as the final tie-break
  (earliest-declared provider wins an exact tie). The winning snapshot is
  returned exactly as its provider built it — no field is relabelled or
  merged across candidates. Proven by `CompositeUsageProviderTests`
  (`tests/O-view.Core.Tests/Providers/Composite/`) with one test per
  tie-break level plus the full tier ordering.

  `ProviderHealth` (`ProviderName`, `Outcome` — `Ok`/`NoData`/`Failed` —,
  `LastSuccessAt`, `ConsecutiveFailures`) and `DegradedInputCount` (D4) land
  in `src/O-view.Core/Models/` and are exposed as `CompositeUsageProvider`
  state — `Health`/`DegradedInputCount` — rather than on `UsageSnapshot`
  itself, the same pattern ADR-0006 D4's `HistoryStoreState` already uses for
  a store-health fact that isn't one of a snapshot's own values. `NoData`
  (provider ran fine, reported `Unavailable`) and `Failed` (provider threw,
  caught here since D1's never-throw obligation is this type's own contract
  too) are kept distinct per D4's explicit rule; `ConsecutiveFailures` counts
  only a `Failed` streak and is left unchanged by a `NoData` poll, since
  "currently has nothing to report" and "is currently broken" are different
  facts a skin needs to tell apart. `Action<string>? log` receives one line
  per swallowed provider exception, in addition to (not instead of) the
  `Health` entry — proven by a test that a throwing provider's failure is
  observable through the seam and that omitting `log` still yields
  `UsageSnapshot.Unavailable` rather than propagating. Both new rows are
  added to [ADR-0001](0001-core-to-skin-data-contract.md) in this same PR,
  per this ADR's own rule that they must land there before any skin consumes
  them — no skin wiring is part of this slice.
