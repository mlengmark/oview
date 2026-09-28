# ADR-0006: What Core persists on this machine, where, and who owns each file

- **Status:** Proposed — **approved for decomposition on 2026-09-28**. The
  board signed off on this addendum as the basis for cutting Phase 2 build
  tasks, but did **not** answer board question C below (SQLite as this
  repository's first runtime dependency). That question is a dependency
  decision and a critical merge in its own right; until it is answered the
  ledger slice cannot be scoped. The record becomes *Accepted* then.
- **Date:** 2026-09-27
- **Deciders:** proposed by Adrian II the Architect; pending board decision
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`), §3's
  "stores local history" — the one Core responsibility the PDR names but
  never specifies
- **Companion records:** [ADR-0005](0005-data-provider-contract.md) (what
  fills the store), [ADR-0007](0007-app-shell-contract.md) (who owns the
  store's lifetime)
- **Evidence standard:** **CONFIRMED** = read directly against
  `mlengmark/O-view` at `897777b`. **INFERRED** = reasoned, not verified.

## Context

Core has to persist something, for one reason that is not a design
preference: **Claude Code deletes its own transcripts after roughly 30
days** (CONFIRMED — `RollupStore`'s doc comment). A 31-day usage graph
therefore cannot be served from vendor files at all, no matter how well
they are read. Either Core accumulates its own history from install date,
or the 31-day figures in
[ADR-0001](0001-core-to-skin-data-contract.md) are permanently
unavailable.

That is the whole justification for local storage existing. It is worth
stating plainly because it bounds what belongs in the store: **facts the
vendor will delete and O-view cannot recompute.** Nothing else.

### What the source repository persists — CONFIRMED at `897777b`

Two files, in `%LOCALAPPDATA%\O-view\`:

| File | Holds | Mechanism |
|---|---|---|
| `usage.db` | A per-request ledger: `request_id` as PRIMARY KEY, plus dates, model ids, and token counts. Daily (local date × model) rollups are **aggregated at query time**, not stored | SQLite via `Microsoft.Data.Sqlite`. Upserted on every ingest, never blind INSERT |
| `weekly-reset.json` | The one exact weekly-reset instant Claude Code has ever reported | Written atomically (temp file, then replace); read defensively |

Four properties of that design are load-bearing, each with a recorded
reason:

- **De-duplication and idempotency are the same mechanism.** `request_id`
  as primary key means re-ingesting the same transcript rewrites identical
  rows, and streaming duplicates of one request overwrite in file order so
  the last occurrence wins. There is no separate "have I seen this?" pass
  to get out of step with the data.
- **Local-day bucketing happens at query time, from the last timestamp** —
  because a local day straddles two UTC days and a stored `utc_date` cannot
  answer for it (CONFIRMED, cites source issue #211).
- **The ledger holds no conversation content.** Ids, dates, models, token
  counts only — a stated privacy decision (source ADR-0006), not an
  omission.
- **Losing a file costs a wait, not a week.** `WeeklyResetAnchor`'s comment
  is explicit that it deliberately replaced a predecessor
  (`WeeklyResetLog`) whose justification — an unrepeatable observation —
  does not apply to an anchor, because an anchor can simply be re-read next
  time the vendor refreshes.

Supporting files in the same namespace, all CONFIRMED present:
`CorruptBackups`, `DailyRollup`, `IngestAudit`, `RollupStoreReport`,
`StaleJournal`.

## Decision

### D1 — Core persists exactly three things, and each has a stated reason it cannot be recomputed

| Store | What it holds | Why it cannot be derived on demand | Cost if lost |
|---|---|---|---|
| **Usage ledger** | One row per vendor request: request id, timestamp, local date, model id, token counts | The vendor deletes transcripts after ~30 days (CONFIRMED) | History before the vendor's retention window, permanently |
| **Weekly-reset anchor** | The exact weekly-reset instant, once observed | The vendor's cached block reports it only while fresh; measured **43 hours stale** on the development machine (CONFIRMED) | A wait until the next vendor refresh — not a week |
| **Ingest audit** | Per-poll outcome per provider: last success, consecutive failures | It is a history of events; by definition it cannot be recomputed after the fact | The evidence trail behind [ADR-0005](0005-data-provider-contract.md)'s `ProviderHealth` |

**Rejected: persisting computed rollups.** Daily and 31-day figures are
aggregations over the ledger and are cheap to recompute; storing them adds
a second thing that can disagree with the first. The source already
aggregates at query time and records why (local-day straddling).

**Rejected: persisting anything from the Core-to-skin contract as a
cache.** A snapshot is derived state. Caching it invents a path where the
skin renders a number no provider currently stands behind — which is the
"never fabricate a number" principle failing quietly rather than loudly.

**Rejected: Core persisting user settings.** Settings are not
unrecomputable vendor facts; they are the shell's, and startup
registration is the OS's own
([ADR-0002](0002-cross-platform-capability-matrix.md) forbids a shadow copy
in Core settings). See [ADR-0007](0007-app-shell-contract.md) D4 for the
three-way ownership split.

### D2 — Core never resolves its own store path; the shell injects it

Core's storage types take a directory. They do not call
`Environment.SpecialFolder`, read environment variables, or ask what OS
they are on. The shell ([ADR-0007](0007-app-shell-contract.md)) resolves
the directory once at startup and hands it down.

Documented defaults, for the two authorized platforms only (**G3** —
macOS is out of scope and no macOS path appears in this record):

| Platform | Store directory | Basis |
|---|---|---|
| Windows | `%LOCALAPPDATA%\O-view\` | CONFIRMED — what the source uses today |
| Linux | `$XDG_DATA_HOME/O-view/`, falling back to `~/.local/share/O-view/` | **INFERRED** — the XDG Base Directory convention for application data that is not configuration and not a cache. No hardware verification of this path exists |

**Rejected: one directory for both data and settings on Linux.** XDG
separates `XDG_DATA_HOME` from `XDG_CONFIG_HOME` for a reason a
backup-conscious user cares about: the ledger is large, regenerable-only-
by-waiting data; settings are small and worth syncing. `.NET`'s
`SpecialFolder.LocalApplicationData` happens to resolve to the data
directory on Linux, which makes the correct split as cheap as the wrong one
(**INFERRED** — not verified on a real Linux desktop).

**Rejected: a single hardcoded path with no injection.** It is what makes
storage tests require a real user profile, and it is the same mistake
[ADR-0005](0005-data-provider-contract.md) D5 rejects on the input side.

### D3 — Corruption degrades to "not known yet", never to a crash and never to a guess

Carried forward from the source's stated behaviour, promoted to contract:

1. **JSON stores are written atomically** — temp file, then replace. A
   process killed mid-write leaves the previous good file, never a
   half-written one.
2. **Anything unparseable degrades to absent.** An unreadable anchor means
   the weekly reset is `unavailable` and the next vendor refresh refills
   it. It never means a plausible-looking default.
3. **A corrupt store is moved aside, not deleted**, and its replacement
   starts empty (`CorruptBackups`' role in the source). The user keeps the
   evidence; the app keeps running.
4. **Losing the store is a data-availability event, not a correctness
   event.** Every affected contract value flips to `unavailable` with a
   status flag the skin must render as an explicit gap.

### D4 — Store state is contract data, for the same reason provider health is

One new row for [ADR-0001](0001-core-to-skin-data-contract.md), matching
[ADR-0005](0005-data-provider-contract.md) D4's reasoning:

| Value | Type | Unit | Status flag | Notes |
|---|---|---|---|---|
| `HistoryStoreState` | enum {`Ok`, `Rebuilt`, `Unavailable`} | — | real | `Rebuilt` means a corrupt store was moved aside this session, so `HistoryCoverage.RecordedDays` is newly near-zero for a reason the user should be allowed to know. Core reports the state; the skin decides the wording, or says nothing |

Without this row, a rebuilt store and a fresh install are indistinguishable
to a skin, and the 31-day graph silently empties with no available
explanation.

### D5 — The ledger holds no conversation content, as a contract obligation

Ids, timestamps, local dates, model ids, and token counts. No prompt text,
no response text, no file paths from the user's projects, no titles.

This is stated as an obligation rather than inherited as a habit because
the ledger is the one place in this design where reading a vendor's
transcripts could turn into retaining their contents. A reviewer may reject
any migration that adds a text column to the ledger on the strength of this
paragraph alone.

### D6 — Retention is unbounded, deliberately

The ledger accumulates from install date and is not trimmed.

**Rejected: a 31-day rolling delete.** It would make the store exactly as
forgetful as the vendor artefact it exists to outlive, and it would break
`EstimatedValueWindow31d`'s longer-horizon sibling figures the moment
anyone wants "since install."

**The cost, stated honestly:** the store grows for the life of the install,
and no one has measured how fast. Rows are ids, dates and integers, so the
growth is small per request (**INFERRED**, not measured). Two obligations
follow: the diagnostics bundle must report the store's size on disk, and if
a real measurement later shows this is wrong, this ADR is amended in place
with the number.

## Board question C — SQLite as this repository's first runtime dependency

**The decision needed:** may the usage ledger use SQLite
(`Microsoft.Data.Sqlite`), as the source repository does?

**Still open as of 2026-09-28.** The board approved this addendum for
decomposition without answering this. The recommendation below was not
accepted by implication — the ledger slice stays unscopable until there is
an explicit yes or no.

**Why it is the board's and not mine.** This repository currently has no
third-party runtime dependency at all, and per this project's own PR rules
a `*.csproj` dependency change is a critical merge. It also has a
packaging consequence on Linux: SQLite ships a native library per runtime
identifier, which touches the `.deb`/tarball story that
[ADR-0002](0002-cross-platform-capability-matrix.md) already records as
**never verified on real hardware** (**INFERRED** — no one has built a
Linux package from this repository).

**Recommendation: yes, SQLite.** The alternative is to hand-roll the one
mechanism that carries the most incident history in the whole storage
design: `request_id` as a primary key, upserted, is simultaneously the
de-duplication rule and the idempotency rule (CONFIRMED). An append-only
JSONL store re-creates that as a compaction pass plus a separate seen-set,
which is two mechanisms that can disagree — and re-earns source issue
#211's local-day bug by making query-time aggregation expensive enough to
be tempted away from.

**Rejected alternatives:**
- **Append-only JSON/JSONL of our own.** No dependency, but see above; it
  trades one well-tested dependency for two hand-written invariants.
- **LiteDB or another embedded document store.** Still a dependency, less
  widely deployed, and it buys nothing the ledger's flat rows need.
- **In-memory only, no persistence.** Abandons the 31-day figures entirely.
  Legitimate only if the board would rather ship without them, in which
  case that should be decided outright rather than arrived at by
  disagreeing with this ADR.

**If the board says no,** slices 2 and 3 in the table below change shape
and `UsageHistorySeries` / `HistoryCoverage` / the 31-day rows stay
`unavailable` for the foreseeable future. That is a product decision, not a
technical one, which is why it is here.

## Alternatives considered

**Let each skin keep its own store.** Rejected outright — two stores of the
same facts drift, and the PDR's whole premise is one canonical data layer.
It would also put vendor-file parsing in a skin.

**Put storage in the shared shell rather than Core.** Tempting, because the
shell already owns the store's lifetime
([ADR-0007](0007-app-shell-contract.md)). Rejected: the store's *content*
is canonical usage data, and PDR §3 places that in Core. The shell owning
*when* the store opens and closes is a lifetime concern, not a data one,
and D2's injected directory is exactly the seam that keeps those separate.

**Encrypt the store.** Rejected — it implies key management, and "no
credential or token handling" is standing policy evaluated and rejected
twice already in the source repository. The privacy answer here is D5:
don't store the sensitive thing in the first place.

## Consequences

**Positive:**
- The 31-day figures in [ADR-0001](0001-core-to-skin-data-contract.md)
  become reachable, which they are not today at any provider quality.
- D2's injected directory means every storage test runs against a temp
  directory on either CI runner — relevant because this repository has no
  non-Windows CI (CONFIRMED — `CLAUDE.md`).
- D3 plus D4 make store failure a thing the product can say out loud rather
  than a graph that mysteriously empties.

**Negative:**
- Board question C is a dependency the board may not want, and the honest
  fallback is fewer features, not a cheaper store.
- Unbounded growth (D6) is accepted without a measurement. This is the
  weakest claim in this record and is labelled as such.
- Every Linux path in D2 is **INFERRED**. The first real Linux run may
  move them, which is an ADR amendment, not a surprise.

## Slicing guidance for decomposition

| # | Slice | Depends on | Risk |
|---|---|---|---|
| 1 | `WeeklyResetAnchor`-equivalent: one JSON file, atomic write, defensive read, injected directory | — | **Lowest** — one small file, no dependency, no schema. Start here |
| 2 | Ledger schema + upsert + query-time daily aggregation | Board question C | Medium — first dependency, first schema |
| 3 | Corrupt-store handling + `HistoryStoreState` (D3, D4) | 2 | Low once 2 lands |
| 4 | Ingest audit + wiring to [ADR-0005](0005-data-provider-contract.md)'s `ProviderHealth` | 2, and ADR-0005 slice 6 | Low |

Slice 1 is deliberately first and is the only slice in this ADR that needs
no board answer: it is a single JSON file with an injected path, it makes
`WeeklyResetAt`/`WeeklyResetSource` reachable, and it proves D2 and D3
before any dependency question is settled.
