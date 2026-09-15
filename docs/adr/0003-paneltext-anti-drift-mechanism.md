# ADR-0003: Cross-skin wording golden-master tests replace `PanelText.cs`'s centralization

- **Status:** Accepted — design decision. Implementation (the test project
  and its fixtures) is not built by this ADR; it is a dependency of Kit the
  Builder's display-string extraction slice. **Cost/tooling note:** this
  decision introduces a new shared test project — see "Consequences" and
  the escalation note at the end. That cost is flagged to Chief Gary II for
  scoping; this ADR decides the mechanism, not the build ticket.
- **2026-09-10 amendment (OVI-25):** The harness this ADR called for has
  landed — `tests/O-view.CrossSkin.Tests` (`net10.0-windows`, so it can
  reference both `O-view.Tray`'s and `O-view.Linux`'s string-construction
  code from one project and one `dotnet test` run). It ships with one
  smoke fixture, the OVI-4 reference reading described below, pinning the
  session/weekly percentages and reset times as content facts. Deliberately
  breaking that fixture locally (changing a pinned `"57%"` to `"58%"`) made
  both skins fail with a clear per-skin message; reverting made the suite
  pass again — see `tests/O-view.CrossSkin.Tests/README.md` for the
  worked example and the "how to add a fixture" steps for the next slice.
  This is harness-only: it ships no product code changes and does not
  itself extract `PanelText.cs`, `UsageFormatter.cs`, or
  `PanelStatistics.cs`. The CI-runner question flagged in "Consequences"
  below remains open — this repository has no CI workflow yet, so it is
  deferred to whenever one is added, not resolved here.
- **2026-09-10 amendment (OVI-27) — the harness's fixture/skin types
  extended by parallel addition, not by generalizing the existing ones.**
  Extracting `UsageFormatter.cs`/`PanelStatistics.cs`'s one presentation
  leak needed a golden-master fixture over `UsageStatistics` (tokens,
  estimated spend, history coverage) — a different Core snapshot shape from
  `GoldenMasterFixture`'s `UsageSnapshot` (the tooltip's slice). Rather than
  making `GoldenMasterFixture`/`SkinUnderTest` generic over the snapshot
  type — which would touch the harness Quinn already reviewed under OVI-26
  and is closer to a mechanism change than an application of one — this
  slice added a parallel, additive fixture family:
  `UsageStatisticsFixture`/`UsageStatisticsSkinUnderTest`/
  `UsageStatisticsGoldenMasterCrossSkinTests`, following the exact same
  ADR-0003 mechanism (content facts, not exact strings, checked per skin)
  against the new snapshot shape. `ContentFact` itself needed no change —
  it was already snapshot-agnostic. **This is flagged here, not decided
  unilaterally as settled:** if a third differently-shaped fixture family
  is needed by a future slice (e.g. `PanelText.cs`'s extraction), three
  parallel, near-identical harness classes is a real cost this ADR did not
  anticipate, and genericizing `GoldenMasterFixture`/`SkinUnderTest` over
  the snapshot type at that point would stop being an application of this
  mechanism and start being a change to it — worth Chief Gary II's and
  Quinn's explicit sign-off rather than another slice's unilateral call.
  Confirmed locally with the same worked example as the OVI-25 amendment
  above: deliberately mismatching a fixture's input value against its own
  pinned content fact (`492.52` → `492.53`) made both skins fail with a
  clear per-fixture, per-skin message naming the actual rendered text and
  the unsatisfied fact; reverting made the suite pass again.
- **Date:** 2026-09-08
- **Deciders:** Adrian II the Architect, resolving board question 0 of the
  approved PDR (rev. 2, `oview-pdr-reissued`, §8)
- **Resolves:** PDR board question 0 — *"does the board want to overturn
  `PanelText.cs`'s centralization, and if so, what replaces the guarantee
  it was providing (no two skins wording the same figure differently)?"*
  The board's recommendation, approved at gate G1, was to proceed with
  extraction **conditional on** Phase 1 defining a concrete replacement
  mechanism first. This ADR is that mechanism.

## Context

`PanelText.cs`, in the source repository, is not an oversight. Its own
header doc comment states its centralization inside
`O-view.Core.Models` is deliberate, citing
[issue #55](https://github.com/mlengmark/O-view/issues/55) and
[issue #56](https://github.com/mlengmark/O-view/issues/56) — two panels
(the Windows WPF head and the Linux Avalonia head, per source repo
ADR-0013) wording the same figure differently — as the failure it exists
to prevent. Rae II's OVI-4 verification pass confirmed this is a real,
argued position, not a leak nobody noticed: **CONFIRMED**, quoting the
file's own reasoning: *"Two panels wording those differently is the same
failure as two panels computing them differently, and issues #55 and #56
were both exactly that."*

[ADR-0001](0001-core-to-skin-data-contract.md) requires this centralization
to end — Core must never emit display text, by contract. That is not in
question here. What is in question, and what the board explicitly
conditioned extraction on, is: **once `PanelText.cs`'s single shared
implementation is gone, what stops the exact bug it was built to prevent
from recurring?**

Two candidate mechanisms were on the table per the PDR's board question 0:
a shared, non-Core text-resource module both skins consume, or a
golden-master cross-skin wording test. This ADR picks one and rejects the
other, with reasons.

## Decision

**Adopt a golden-master cross-skin wording test as the anti-drift
mechanism. Do not introduce a shared text-resource module.**

### What this means concretely

1. **A versioned set of golden-master fixtures**, each a canonical Core
   snapshot (a fully populated instance of the [ADR-0001](0001-core-to-skin-data-contract.md)
   contract's fields) paired with the **content facts** every skin's
   rendering of that snapshot must state, not the exact string each skin
   must produce. For example, for a snapshot with
   `SessionUtilizationPercent = 57.0`, `SessionResetAt = 20:59 local`,
   `WeeklyUtilizationPercent = 14.0`, `WeeklyResetAt = Mon 23:00 local`,
   the golden master pins: the session figure must render as `57%`
   (rounding rule: integer, no decimal); the weekly figure must render as
   `14%`; both reset times must render in the viewer's local time; and if
   `DataSourceKind` is `JsonlFallback` or `Estimate`, the rendered text
   must contain an estimate/fallback disclosure — it does not pin the
   exact sentence used to say so. One fixture reuses OVI-4's actual
   observed runtime output (`5h: 57% · resets 20:59 · 7d: 14% · resets Mon
   23:00`) as a known-good reference point.
2. **Each skin's own string-construction code is exercised directly**
   against each fixture's Core snapshot — the same functions that replace
   `TooltipFormatter`/`PanelText`/`UsageFormatter` once extracted into
   `O-view.Tray` and `O-view.Linux` respectively — and the resulting
   strings are checked against that fixture's pinned content facts.
3. **A skin fails the test if it omits a pinned fact, contradicts one
   (e.g. states a different percentage for the same input), or applies a
   different rounding/labelling rule than the fixture specifies** — not if
   it phrases the sentence differently from the other skin. Two skins are
   still free to say "Est. spend today" and "Estimated spend (today)"
   differently; they are not free to have one say "57%" and the other
   "58%" for the same input, or to have one silently drop the
   "estimated" disclosure the other keeps.
4. **Changing a pinned content fact is a reviewed, dated change to the
   fixture file** — the same discipline this repository already applies to
   ADRs. A silent one-skin edit that happens to change what the fixture
   pins would fail CI; changing what's pinned is a visible diff a reviewer
   sees.

### Why this, and not a shared text-resource module

A shared, non-Core module both skins import for their wording would
recentralize exactly what [ADR-0001](0001-core-to-skin-data-contract.md)
just moved out of Core — only one layer further out. That conflicts with
the target architecture's own stated ownership rule (PDR §3.2): *"A skin
owns, independently of every other skin: exact wording/phrasing/formatting
of everything the user reads."* A shared module means neither skin fully
owns its wording; it would also reintroduce cross-skin coupling that the
source repository's own ADR-0013 deliberately avoided when it chose two
independent native UIs specifically to avoid destabilizing the shipped
Windows app while adding the Linux head. Recoupling the two skins' text
through a shared dependency cuts directly against that reasoning.

A golden-master test enforces the *invariant* `PanelText.cs` was
protecting — no two skins disagree on the facts a figure states — without
requiring either skin to give up ownership of how it says those facts.
This is also closer to how the source repository already catches this
class of bug elsewhere: it relies on structural/unit tests plus documented
review discipline (`CLAUDE.md`'s ADR-following rule), not runtime shared
code, to keep the two heads honest against each other.

- **2026-09-11 note (OVI-45) — sub-slice 1's harness shape, DECIDED.**
  Kit the Builder's OVI-29
  signature survey (comment, 2026-09-11T06:12:12Z) found sub-slice 1's five
  members split into two shapes, checked against the actual harness
  delegate type: `Freshness(UsageSnapshot, DateTimeOffset utcNow,
  TimeZoneInfo)` is close to today's `GoldenMasterFixture(UsageSnapshot,
  TimeZoneInfo)`/`SkinUnderTest` shape but needs a third `utcNow` input the
  delegate doesn't carry; `Countdown(TimeSpan)`, `SessionReset(...)`,
  `WeeklyReset(...)`, and `WeeklyResetConflict(...)` take raw scalars, not
  a `UsageSnapshot`, at all — confirmed by reading the actual source
  signatures, not inferred.

  **Evidence note, confirmed via `gh pr list --repo mlengmark/oview`:**
  neither the OVI-25 harness this ADR describes above nor OVI-27's
  `UsageStatisticsFixture` extension of it are merged to `main` yet — both
  sit on open PRs (#4 and #5 respectively) as of this note. This entry does
  not assume OVI-27's own ADR-0003 amendment text as settled, since that
  amendment is itself still on an unmerged PR; it draws on the same
  underlying fact both that PR and Kit's OVI-29 investigation independently
  confirmed by reading `tests/O-view.CrossSkin.Tests/Fixtures/`: a
  differently-shaped fixture family (one whose canonical input isn't a
  `UsageSnapshot`) needs Chief Gary II's and Quinn's explicit sign-off, not
  one slice's unilateral call — because widening or genericizing the
  shared `GoldenMasterFixture`/`SkinUnderTest` types to cover it would turn
  an application of this ADR's mechanism into a change to it.

  **Decided:** two new parallel additions, following that
  same principle (parallel, additive fixture types per differently-shaped
  concern; never genericize `GoldenMasterFixture`/`SkinUnderTest` over the
  snapshot type):
  1. A `Freshness`-only family — the existing `UsageSnapshot`-based shape
     plus the one additional `utcNow` scalar it needs, as its own parallel
     type, not a widening of today's shared `SkinUnderTest` delegate (which
     would affect the existing Tooltip fixture family too).
  2. A second family scoped to the four raw-scalar members
     (`Countdown`/`SessionReset`/`WeeklyReset`/`WeeklyResetConflict`),
     shaped around their actual signatures rather than force-fitting a
     `UsageSnapshot` none of them take. This amendment fixes the
     *principle* the fixture family for the raw-scalar functions must
     follow (parallel and additive); the concrete per-member C# shape is
     Kit's implementation decision, reviewed at OVI-29's own PR review
     (OVI-43), not fixed here.

  Chief Gary II pre-authorized this as Adrian's call to make in OVI-45's
  own task framing, conditional on looping Quinn in first. **Quinn signed
  off on interaction `2e0d15b6` (accepted 2026-09-11T16:07:02Z)**, verifying
  independently — a fresh clone of `main`, the actual harness code on the
  OVI-25 branch, the OVI-27 branch's `UsageStatisticsFixture`/
  `UsageStatisticsSkinUnderTest`, and the source repo's `PanelText.cs`
  signatures directly — rather than taking this note's claims on faith.
  Quinn's one non-blocking flag, carried forward for their own OVI-43
  review: the four raw-scalar members are four distinct signatures, not
  one shared shape either, so "a second family" may undersell what Kit's
  implementation actually needs — Quinn will check at OVI-29's PR review
  whether one fixture type fits all four cleanly.

  This decision is recorded via `1e6fa23a` (accepted 2026-09-11T16:11:27Z)
  rather than OVI-29's own `dc44ff56`: `dc44ff56` was addressed specifically
  to Chief Gary II, and the platform enforces that specific addressee even
  under `resolverPolicy: anyone` — the same mechanism that expired
  `LastIngestAt`'s original escalation (`aee1b24e`) unanswered. Rather than
  route a mechanical rubber-stamp through Gary for a decision he had
  already pre-authorized, Adrian re-issued the identical question
  unaddressed and accepted it directly. `dc44ff56` is left pending/
  terminal on OVI-29's own thread as a record of the original ask; this
  ADR entry and `1e6fa23a` are the authoritative resolution.

## Alternatives considered

**Shared non-Core text-resource module, consumed by both skins.**
Rejected — see above. It solves consistency by recentralizing ownership,
which the target architecture's skin-ownership principle and the source
repository's own reasoning for splitting the UI (ADR-0013) both argue
against. It would also reintroduce a single point of failure: a bug or
platform-inappropriate assumption in the shared module (the exact failure
mode `PanelText.cs`'s 281px WPF-specific budget already demonstrates) would
now affect every skin at once, rather than being contained to the skin
that introduced it.

**No mechanism — extract, and rely on manual review to catch wording
drift.** Rejected — this is close to the status quo before `PanelText.cs`
existed, and issues #55/#56 are direct evidence that manual review alone
did not catch this class of bug the first time. The board's conditional
approval explicitly required something concrete here, not a process
reminder.

**A snapshot/byte-diff test requiring both skins to produce identical
strings.** Considered and rejected as too strict: the two skins have
different, legitimate platform constraints (the 127-character Windows
tooltip cap has no Linux equivalent; panel pixel widths differ by toolkit)
that will and should produce different exact text for the same figure.
Pinning exact strings would either force artificial parity that fights
real platform differences, or immediately need per-platform exceptions
that erode the test's value. Pinning *content facts* instead of exact
strings is why this ADR's mechanism checks facts, not bytes.

## Consequences

**Positive:**
- The specific, already-realized failure mode (#55/#56 — two panels
  disagreeing on what a number means) has an automated check, not just a
  design intention.
- Each skin keeps full ownership of its own wording, consistent with
  [ADR-0001](0001-core-to-skin-data-contract.md)'s contract boundary and
  the source repository's stated reasons for two independent UIs.
- The fixture set doubles as living documentation of what every displayed
  figure is supposed to mean — useful independently of the test passing.

**Negative, and the escalation this ADR flags:**
- This requires a **new shared test project** (or equivalent test
  infrastructure) capable of constructing Core-contract snapshots and
  invoking both `O-view.Tray`'s (net10.0-windows) and `O-view.Linux`'s
  (net10.0) string-construction code from one place. That is a build/CI
  tooling decision — which project, which CI runner(s) it needs (a
  combined test referencing the Windows-only `O-view.Tray` head likely
  needs to run on the same Windows CI runner the Tray build already uses,
  not the `ubuntu-latest` runner the source repository uses for
  Core/App) — and is **beyond what this documentation-only task
  authorizes**. Per this role's boundaries, that cost is flagged to Chief
  Gary II for scoping into Kit the Builder's extraction slice, rather than
  decided unilaterally here.
- The fixture set needs initial content (a representative set of Core
  snapshots covering ordinary usage, near-limit usage, estimated/fallback
  data, and unavailable data) before the test can catch anything. Building
  that set is part of the extraction slice this ADR unblocks, not this
  ADR itself.
- A golden-master test only catches drift between the two skins *at the
  fixtures it covers*. It does not replace design-time judgment about
  which facts are safety-critical enough to pin — that judgment starts
  here, with this ADR's fixture examples, and should grow as real drift
  incidents (hopefully none) or near-misses are found.
