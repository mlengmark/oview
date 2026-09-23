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

- **2026-09-11, later same day — sub-slice 1's two fixture families landed (Kit the
  Builder, Phase 1 slice 3.1, OVI-29), per the decision above.**
  - `FreshnessFixture`/`FreshnessSkinUnderTest`/`FreshnessFixtures`/
    `FreshnessGoldenMasterCrossSkinTests` implement item 1 of the decision above exactly
    as specified: the existing `GoldenMasterFixture` shape plus the one `utcNow` scalar,
    as its own parallel type. Verified locally, the same way OVI-25's smoke fixture was:
    deliberately changing a pinned `"Local estimate"` fact to `"Modelled estimate"` in
    `O-view.Tray`'s formatter failed the harness with a clear per-fixture message;
    reverting passed again. `GoldenMasterFixture`/`SkinUnderTest` were not touched.
  - Quinn's OVI-45 flag was confirmed correct at implementation time: the four
    raw-scalar members do not share one input shape (`Countdown(TimeSpan)`,
    `SessionReset(DateTimeOffset?, DateTimeOffset, TimeZoneInfo, TimeSpan?)`,
    `WeeklyReset(DateTimeOffset, DateTimeOffset, TimeZoneInfo)`,
    `WeeklyResetConflict(DateTimeOffset, TimeZoneInfo)`). Rather than force them into one
    fixture type's input fields (which would need every fixture to carry unused fields
    for the three members it isn't exercising) or split into four separate
    fixture/skin-under-test/test-class trios, this landed as **one `PanelTextResetFixture`
    type whose `Render` closes over the specific `PanelTextResetSkinUnderTest` member and
    inputs each fixture exercises**, plus one `PanelTextResetSkinUnderTest` record
    exposing all four skin entry points. This keeps the "one fixture type per shape"
    principle honest — no fixture is coerced into carrying a shape it doesn't have — while
    avoiding a fourfold file split for four small, related members added in the same
    slice. `GoldenMasterFixture`/`SkinUnderTest` were not widened to cover this either.
  - Both new test classes (`FreshnessGoldenMasterCrossSkinTests`,
    `PanelTextResetGoldenMasterCrossSkinTests`) follow `GoldenMasterCrossSkinTests`'
    existing "every skin satisfies every pinned content fact" structure unchanged.

- **2026-09-21 amendment (OVI-82) — sub-slice 3's harness shape, DECIDED, Quinn's
  sign-off recorded (Adrian II the Architect, closing the sign-off gap the OVI-29
  escalation named for `BoostNotice` — board reply 2026-09-11T02:28Z).**

  **The fixture families so far, for count:** `GoldenMasterFixture` (tooltip,
  `UsageSnapshot`), `UsageStatisticsFixture` (OVI-27, `UsageStatistics`),
  `FreshnessFixture` (OVI-29 item 1, `UsageSnapshot` + `utcNow`), and
  `PanelTextResetFixture` (OVI-29 item 2, four raw-scalar members sharing one fixture
  type via a member-selecting `Render`). This proposal is a fifth.

  **Signatures, confirmed by direct source read (same pinned commit, `897777b`) —
  see [ADR-0001](0001-core-to-skin-data-contract.md)'s 2026-09-21 amendment for the
  full citation:** `BoostChip(BoostNotice, DateTimeOffset utcNow, TimeZoneInfo)` and
  `BoostCard(BoostNotice, DateTimeOffset fetchedAtUtc, TimeZoneInfo)`. Unlike sub-slice
  1's raw-scalar members, both take the *same* input shape — one canonical value
  (`BoostNotice`, the new ADR-0001 type, not `UsageSnapshot`), one `DateTimeOffset`
  (different meaning per member, same type), one `TimeZoneInfo`.

  **Proposal: one new parallel family, `BoostNoticeFixture` /
  `BoostNoticeSkinUnderTest` / `BoostNoticeGoldenMasterCrossSkinTests`,** structurally
  parallel to `FreshnessFixture` (canonical value + one `DateTimeOffset` + one
  `TimeZoneInfo`) but keyed on `BoostNotice` instead of `UsageSnapshot` — not a widening
  of `FreshnessFixture` itself, which stays scoped to `UsageSnapshot`-shaped fixtures.
  Because `BoostChip` and `BoostCard` share one input shape but check different,
  differently-scoped content facts (the chip: percent/date/countdown phrasing inside a
  width-constrained row; the card: the verbatim message plus its attribution line), this
  follows `PanelTextResetFixture`'s precedent for a shared-shape, multi-member family: one
  fixture type whose `Render` closes over which member (`BoostChip` or `BoostCard`) and
  which `DateTimeOffset` role (`utcNow` vs `fetchedAtUtc`) a given fixture exercises,
  rather than two near-identical fixture/skin-under-test/test-class trios for one shared
  input shape. `GoldenMasterFixture`/`SkinUnderTest` remain untouched, per this ADR's
  standing principle.

  **Authorized.** Per this ADR's own OVI-27 and OVI-45 amendments, a differently-shaped
  fixture family needs Chief Gary II's and Quinn's explicit sign-off before a slice may
  build against it — not another slice's unilateral call, even when (as here) the new
  family closely mirrors an already-reviewed pattern. Chief Gary II's OVI-45
  pre-authorization of "Adrian decides, conditional on looping Quinn in" was treated as
  standing for this ADR's sign-off gate generally, not only for OVI-45's own instance of
  it — so the sign-off request went to Quinn directly, the same path OVI-45 used, rather
  than re-escalating to Gary for a second rubber-stamp of the same delegated authority.
  Quinn accepted the proposal as specified, without changes (Paperclip interaction
  `22dbaf90-2ce3-406a-85e6-c01247c22add`, resolved 2026-09-21T20:10:59Z) — this proposal's
  shape, as written above, is authorized for Kit's sub-slice 3 build task. No caveats or
  requested changes accompanied the sign-off.

- **2026-09-21, later same day — `BoostNoticeFixture` family landed exactly to the signed-off
  shape (Kit the Builder, Phase 1 slice 3.3, OVI-92).**
  - `Fixtures/BoostNoticeFixture.cs`, `Fixtures/BoostNoticeSkinUnderTest.cs`,
    `Fixtures/BoostNoticeFixtures.cs`, and `BoostNoticeGoldenMasterCrossSkinTests.cs` mirror
    `PanelTextResetFixture`'s member-selecting-`Render` shape exactly as proposed above: one
    fixture type whose `Render` closes over which `BoostNoticeSkinUnderTest` member
    (`BoostChip` or `BoostCard`) a given fixture exercises, and one skin-under-test record
    exposing both entry points (since, unlike the four raw-scalar members, `BoostChip` and
    `BoostCard` do share one input shape). `GoldenMasterFixture`/`SkinUnderTest` were not
    touched.
  - Five fixtures: a chip with both percent and end date (reusing the source app's own worked
    example from `PanelText.BoostChip`'s doc comment — 18 days 14 hours before a 31 Aug end
    date renders `2w 4d 14h` remaining), a chip with neither figure parsed (falls back to the
    bare "Boosted" word, pinned via a content fact that the rendering contains no digit at
    all), a chip with a percent but no end date (countdown omitted entirely), and two card
    fixtures (with and without an end date) both pinning that the notice's exact message text
    appears verbatim in the rendered card — the strongest content fact this family checks,
    directly proving `BoostCard`'s own "never edited, summarised or re-worded" rule rather
    than merely trusting the doc comment that states it.
  - **Verified locally, the same way every prior fixture family in this project was:**
    deliberately changing the Windows skin's `BoostChip` separator from `" · "` to `" :: "`
    and truncating its final countdown unit by one character made the harness fail with a
    clear per-fixture message naming the actual rendered text (`"...ends in 2w 4d 14"`,
    missing the pinned `"14h"` content fact); reverting made the suite pass again. All 119
    tests across the four project's test assemblies (`O-view.Core.Tests`,
    `O-view.Tray.Tests`, `O-view.Linux.Tests`, `O-view.CrossSkin.Tests`) pass after the
    revert, confirmed by `dotnet test O-view.slnx`.

- **2026-09-22 update — a sixth fixture family, `RateLimitedNoticeFixture`, landed (Kit
  the Builder, Phase 1 slice 3.4, OVI-98).** Redoes OVI-80/closed PR #12's fixture family
  fresh against main at `80d3913` (post-OVI-92); the design itself was reviewed and
  accepted under OVI-81 and is unchanged here — only the branch it lands on is new.
  `PanelText.cs`'s `RateLimitedNotice` (`DateTimeOffset? retryAfterUtc, TimeZoneInfo local
  -> string`) is a single fixed shape that does not take a `UsageSnapshot` at all, and —
  unlike the `BoostNotice` family — is the only member of its own shape, so there is no
  multi-member `Render`-closure to build. This lands as `RateLimitedNoticeFixture`/
  `RateLimitedNoticeSkinUnderTest`/`RateLimitedNoticeFixtures`/
  `RateLimitedNoticeGoldenMasterCrossSkinTests`, mirroring `FreshnessFixture`'s fixed-field
  shape (not `PanelTextResetFixture`'s or `BoostNoticeFixture`'s closure shape), following
  `GoldenMasterCrossSkinTests`' existing "every skin satisfies every pinned content fact"
  test structure unchanged.
  - **This reuses an already-reviewed fixture shape, so it does not trigger this ADR's
    "third differently-shaped family needs Chief Gary II's and Quinn's sign-off" rule** —
    `RateLimitedNoticeFixture` is structurally identical to `FreshnessFixture`, not a new
    shape. OVI-80's original landing reached the same conclusion; this redo carries that
    conclusion forward rather than re-litigating it.
  - Two fixtures pin: the formatted retry time when GitHub sent one, that no clock time is
    rendered when it did not (mirroring `PanelTextResetFixtures.SessionResetUnknown`'s "does
    not render a clock time" predicate), and — in both cases — that the notice reassures the
    reader their own connection/install is not at fault.
  - Confirmed locally with the same worked example as prior amendments: deliberately
    changing the `"14:30"` content fact to `"14:31"` made both skins fail with a clear
    per-fixture, per-skin message; reverting passed again. `GoldenMasterFixture`/
    `SkinUnderTest` were not touched or widened.

- **2026-09-23 amendment (OVI-100) — sub-slice 4's harness shape, PROPOSED; Quinn's
  sign-off requested, not yet recorded (Adrian II the Architect).** Mirrors the
  2026-09-21 (OVI-82) amendment's structure for sub-slice 3: the proposal is written
  down first, the sign-off outcome is recorded in its own dated entry afterwards. **No
  slice may build against this shape until that second entry exists.**

  **The fixture families so far, for count:** `GoldenMasterFixture` (tooltip,
  `UsageSnapshot`), `UsageStatisticsFixture` (OVI-27, `UsageStatistics`),
  `FreshnessFixture` (OVI-29 item 1, `UsageSnapshot` + `utcNow` + zone),
  `PanelTextResetFixture` (OVI-29 item 2, four raw-scalar members sharing one fixture type
  via a member-selecting `Render`), `BoostNoticeFixture` (OVI-82/OVI-92, `BoostNotice` +
  `DateTimeOffset` + zone, member-selecting `Render`), and `RateLimitedNoticeFixture`
  (OVI-98, a single fixed raw-scalar shape). This proposal is a seventh.

  **Signatures, confirmed by direct source read (same pinned commit, `897777b`) —
  see [ADR-0001](0001-core-to-skin-data-contract.md)'s 2026-09-23 amendment for the full
  citation:** `Caveat(PanelStatistics stats) -> string` (`PanelText.cs` line 406),
  `RateAge(RateCard card) -> string` (line 443), and `TokenScopeCaveat`, a
  `public const string` with no input at all (line 474). Under ADR-0001's 2026-09-23
  amendment these become, in each skin, `Caveat(UsageStatistics) -> string`,
  `RateAge(RateCardStamp) -> string`, and a constant.

  **The question this proposal actually has to answer, and why it is not obvious.**
  `Caveat`'s input shape is `UsageStatistics` — *exactly* the shape
  `UsageStatisticsFixture`/`UsageStatisticsSkinUnderTest` already carry
  (`Func<UsageStatistics, string> Render`). No prior sub-slice has hit that: every new
  family so far was new because its input shape was new. So the honest options are two,
  not one, and this is the first time this ADR has had to choose between them.

  **Rejected: fold `Caveat` into the existing `UsageStatisticsFixture` family.** It fits
  the type signature perfectly, and that is the whole of the argument for it. Against it:
  `UsageStatisticsSkinUnderTest` carries a *single* `Render` delegate, wired in
  `UsageStatisticsGoldenMasterCrossSkinTests` to a test-only composite of `Tokens`, `Usd`,
  and `CoverageNote`. Adding `Caveat` to that composite silently widens what its three
  merged, reviewed fixtures assert — `Unavailable`'s "never renders a fabricated $0.00"
  predicate would begin policing caveat text it was never written for — and a failure
  message would no longer name which member produced the offending string. Giving the
  family a second delegate instead means changing an already-landed family's *shape*,
  which is the move this ADR's standing principle exists to stop. Either way the cost
  lands on merged code rather than on new code, which is the wrong way round.

  **Proposal: a seventh parallel family, `UsageCaveatFixture` /
  `UsageCaveatSkinUnderTest` / `UsageCaveatFixtures` /
  `UsageCaveatGoldenMasterCrossSkinTests`,** following `BoostNoticeFixture`'s and
  `PanelTextResetFixture`'s already-reviewed member-selecting shape rather than inventing
  a new one: `UsageCaveatSkinUnderTest` exposes both entry points
  (`Func<UsageStatistics, string> Caveat` and `Func<RateCardStamp, string> RateAge`), and
  `UsageCaveatFixture.Render` closes over which one a given fixture exercises. Two entry
  points with *different* input shapes is precisely what that closure shape is for.
  `ContentFact` is reused unchanged, as every family since OVI-27 has.
  `GoldenMasterFixture`/`SkinUnderTest` — and now also `UsageStatisticsFixture`/
  `UsageStatisticsSkinUnderTest` — remain untouched, per this ADR's standing principle.

  **Why `RateAge` is pinned as its own entry point rather than only through `Caveat`.**
  `Caveat` invokes the rate line only when `isStale` is true, so a family that reached it
  solely that way would leave the source's stated rule — that the *provenance* is named
  alongside the date, because "a date says how likely the table is to have moved; a source
  says whose table it is" (`PanelText.RateAge` doc comment, source issue #255) — checked
  only incidentally. A skin could drop the source word entirely and still pass. Pinning
  `RateAge` directly makes that a first-class content fact.

  **Fixtures this family should carry (five; the build slice may add, not drop).** Each
  is a *content fact*, not an exact sentence — both skins must state the fact, in whatever
  wording each chooses (this ADR's standing rule):
  1. **No caveat at all** — full coverage, nothing unpriced, zero TTL-unrecorded writes,
     rates not stale. Pins that neither skin invents a qualifier when there is nothing to
     qualify, the mirror of the source's own "a caveat that is always on says nothing".
  2. **Unpriced models present** — pins that every excluded model id appears, *and* that
     the 31-day estimate beside it is not presented as a complete total (ADR-0001's
     2026-09-23 amendment, decision 7).
  3. **TTL-unrecorded cache writes non-zero** — pins that the count is stated and that the
     assumption behind it (priced at the shorter cache-write rate) is named, not implied.
  4. **Stale rates** — pins that both the source *and* the date reach the reader, via
     `RateAge`.
  5. **`Rates` unavailable** — the hardest case and the one most likely to be got wrong:
     pins that neither skin renders a rate-age line at all, and that neither implies the
     rates are current by simply falling silent.

  Fixtures 2–4 exercise conditions that *clear*, so each should also be represented in its
  cleared state by fixture 1 rather than only in isolation.

  **Not authorized yet.** Per this ADR's OVI-27, OVI-45 and OVI-82 amendments, a
  differently-shaped fixture family needs Quinn's explicit sign-off before a slice may
  build against it — not another slice's unilateral call. It is arguable that the OVI-98
  precedent exempts this one (the *shape* here is `BoostNoticeFixture`'s, already
  reviewed, and that amendment held that reusing a reviewed shape does not re-trigger the
  gate). This amendment does not rely on that exemption, for a specific reason: the
  genuinely new question here is not the shape but the **"why not extend an existing
  family"** choice above, which no prior amendment has had to make and which a second
  reader should rule on rather than an author. Chief Gary II's OVI-45 pre-authorization of
  "Adrian decides, conditional on looping Quinn in" is treated as standing, as it was for
  OVI-82, so the request went to Quinn directly rather than re-escalating to Gary.

  Sign-off requested from Quinn the Reviewer on 2026-09-23 (Paperclip interaction on
  [OVI-100](/OVI/issues/OVI-100)). **The outcome will be recorded in its own dated entry
  below, exactly as OVI-82's was — until that entry exists, this proposal is not
  authorized and Kit's sub-slice 4 build task must not start against it.**

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
