# ADR-0005: The data-provider contract — how Core reads Claude's data off this machine

- **Status:** Proposed — **approved for decomposition on 2026-09-28**. The
  board signed off on this addendum as the basis for cutting Phase 2 build
  tasks. That sign-off does not authorise any slice on its own, and it did
  **not** answer board question A or B below — the acceptance carried no
  answer to either, so both remain open and still gate the slices named
  against them. The record becomes *Accepted* when A is answered.
- **Date:** 2026-09-27
- **Deciders:** proposed by Adrian II the Architect; pending board decision
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
| Never throws | Any failure, malformed file, missing directory or permission error yields `UsageSnapshot.None` | A monitoring tool that dies on a bad poll is worse than one showing a stale number (CONFIRMED — `UsageEngine`'s own doc comment states this as the design) |
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

### D3 — Composition selects by information value; the winner keeps its own source label

`CompositeUsageProvider` is carried forward with the source's rule intact:
tier first (`Live` beats `Stale` beats `Estimate` beats `Unavailable`),
then completeness, then recency, then argument order as a final tie-break.
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
- A provider that finds no root returns `UsageSnapshot.None` with
  `DataSourceKind.Unavailable`. It never guesses a path into existence.

**Rejected: `Environment.SpecialFolder` called directly inside a
provider.** It is what makes the layout rules untestable off-target, and
the source already avoids it for exactly this reason.

## Board questions

These are not rhetorical, and none is answered by this ADR.

**Amendment, 2026-09-28.** The board approved this addendum for
decomposition without answering A or B. Silence on a recommendation is not
assent to it: A still forbids scoping the `CachedUsage` slice, and B still
forbids scoping the ledger slice. Question C needed no answer — it asked the
board to leave G5 as it is, and it is unchanged.

### Board question A — does "read-only against vendor data" permit invoking the vendor's own CLI? (proposed gate G6)

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

### Board question B — SQLite as a dependency

Deferred in full to [ADR-0006](0006-local-storage-contract.md), board
question C. Flagged here only because the `Jsonl` provider is the thing
that feeds it, and the two slices land next to each other.

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
- Board question A blocks the `CachedUsage` slice specifically. The other
  two providers can be scoped without it.

## Slicing guidance for decomposition

Lowest risk first, each independently mergeable, per PDR §8's board
question 4 recommendation:

| # | Slice | Depends on | Risk |
|---|---|---|---|
| 1 | `IUsageProvider` + `UsageSnapshot.None` + the never-throw structural test | — | Lowest — one interface, no I/O |
| 2 | `ClaudeDataRoots`-equivalent path rules, pure, injected roots, no provider yet | 1 | Low — pure functions, fully testable |
| 3 | `JsonlUsageProvider` (token counts, `Estimate`/`JsonlFallback`) | 1, 2 | Medium — real file parsing |
| 4 | `PlanHistoryProvider` | 1, 2 | Medium |
| 5 | `CachedUtilizationProvider` | 1, 2, **board question A** | Medium; gated |
| 6 | `CompositeUsageProvider` + `ProviderHealth` (D3, D4) | 3, 4 (5 if authorized) | Medium — needs ≥2 providers to be meaningful |

Slice 6's `ProviderHealth` rows must land in
[ADR-0001](0001-core-to-skin-data-contract.md) before any skin consumes
them, per that ADR's own rule.
