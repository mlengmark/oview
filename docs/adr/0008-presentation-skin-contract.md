# ADR-0008: The presentation contract — four surfaces, two native skins, no shared UI layer

- **Status:** **Accepted** — the board signed this record off by merging PR #58
  itself (CONFIRMED: merged by the board account `mlengmark`,
  2026-10-02T11:02:23Z; the SHA-pinned merge card tracked on OVI-320, now
  closed). The slicing table is therefore open for decomposition. Living
  document: amend in place, as [ADR-0001](0001-core-to-skin-data-contract.md)
  does.
- **Date:** 2026-10-02 · **Amended** 2026-10-02 (D9, OVI-326; D7 note, OVI-324/OVI-342)
- **Deciders:** Adrian II the Architect, signed off by the board
- **Scope:** Phase 3 — the tray/status icon, the tooltip, the detail window, the
  alerts, and wiring Phase 2's providers and app shell into each platform's own
  native window.
- **Rests on:** gate **G4 = Option A**, accepted by the board 2026-10-02T08:00:32Z
  (card `gate:G4:ui-unification:v1` on OVI-1) — **keep two native windows; no
  shared UI layer investment.** This ADR implements that answer; it does not
  re-open it.
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`) §5.2, §5.3, §6.2
- **Evidence standard:** as [ADR-0002](0002-cross-platform-capability-matrix.md).
  Every factual claim about existing behaviour is **CONFIRMED** or **INFERRED**,
  carried forward from its source. **This record upgrades no evidence label in
  ADR-0002.** Everything it decides about behaviour that does not exist yet is
  INFERRED by construction, and says so.

## Context

Phase 2 is fully landed: the data providers
([ADR-0005](0005-data-provider-contract.md)), the local stores
([ADR-0006](0006-local-storage-contract.md)) and the app shell
([ADR-0007](0007-app-shell-contract.md)) are merged, every slice in all three
slicing tables, last one PR #56 (CONFIRMED by `git log` on `main` at `2c71882`).

What exists today, CONFIRMED by reading this repository:

| Surface | What exists | What is missing |
|---|---|---|
| Tray/status icon | nothing | everything — no icon, no renderer, no OS integration |
| Tooltip | `TooltipFormatter` in **both** skins (`src/O-view.Tray/Presentation/`, `src/O-view.Linux/Presentation/`), producing text from a `UsageSnapshot`, pinned against each other by the ADR-0003 harness | nothing consumes that text; no `NotifyIcon`, no SNI tooltip |
| Detail window | `PanelTextFormatter`, `PanelStatisticsFormatter`, `UsageFormatter` in both skins — the **words** a panel would show | no window, no toolkit, no layout, no placement, no position persistence |
| Alerts | `UsageEvent`/`UsageEventKind` in the shell (`ThresholdCrossed`, `OffPlanEntered`, `UpdateAvailable`, `InputDegraded`) and `IShellToSkin.RaiseEvent` | no presenter in either skin |
| Composition | the shell: poll loop, store lifetime, settings, single-instance, startup registration, update check, diagnostics bundle | no executable entry point that builds a shell and a skin and runs them; neither skin project references a UI toolkit |

So Phase 3 is **the first phase in this repository that draws a pixel.** That is
what makes it the riskiest phase so far and why the slicing below is ordered the
way it is.

The one thing that was blocking it is now answered. ADR-0007's "Gate flag — G4
stays closed" section said the shell is deliberately drawn so that a shared
widget *could* be added if the board opened G4. The board has now closed that
question in the other direction: **Option A, keep two native windows.** The
board's own stated reasons, from the accepted card, are worth keeping in the
record because they are the live constraints on this design:

- Option B rewrites a Windows app that already works for real users, re-tests
  its animations and window placement, and adds ~100 MB of dependencies to a
  Windows build that currently has none.
- The duplication risk of Option A "already has its own safeguard" — that is
  [ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s cross-skin golden-master
  harness, which this ADR is therefore obliged to extend rather than leave at
  its Phase 1 surface area.
- There is no measured evidence yet that the duplication has caused a real
  problem.

## Decision

### D1 — Two skin implementations. No shared presentation project, and none later by the back door

`O-view.Tray` takes WPF (`net10.0-windows`, `UseWPF`). `O-view.Linux` takes
Avalonia (`net10.0`). Each owns its own window, its own toolkit, its own layout,
its own icon renderer, its own notification presenter and its own placement rule.
Both continue to reference only `O-view.Core` and `O-view.App`
([ADR-0007](0007-app-shell-contract.md) D1), never each other.

**Nothing presentational may be added to `O-view.App` by this phase.** ADR-0007's
admission rule and its `Rendering/` prohibition stay exactly as written, and
G4 = A removes the only argument that was ever offered for relaxing them. Named
concretely, because these are the specific things a builder will be tempted by:

- no shared window base class, view model, layout, geometry, palette, or
  animation;
- no `WorkAreaPlacement` and no `TrayIconGeometry` in the shell — the source
  repository put the corner rule in `OView.App.Rendering.WorkAreaPlacement`
  (CONFIRMED, source ADR-0013's 2026-08-19 follow-up), and this is a **deliberate
  divergence**: see D5;
- no third `O-view.Ui` project, however thin.

**This also means G4 does not get re-litigated slice by slice.** A builder who
believes a shared type is justified does not add it; they raise it as a new
decision, because it is a reversal of an accepted gate.

**Rejected: use Avalonia for both skins now that we are writing both windows from
scratch here anyway.** This is the strongest version of Option B and it deserves
a straight answer, because the "the Windows app already ships" argument is weaker
in *this* repository than in the source — here, neither window exists yet, so
there is no tuned WPF code to put at risk. It is still rejected, for two reasons
the board's card already names and one it does not: (a) the board decided A, and
A is what this ADR implements; (b) the ~100 MB dependency closure and the loss of
the Windows build's zero-third-party-dependency property would apply to this
repository too (CONFIRMED cost figures, source ADR-0013 §4: 25 managed
assemblies, `libSkiaSharp.so` 10.7 MB, `libHarfBuzzSharp.so` 2.7 MB); and (c) the
Windows surface is the only one this project can actually verify on real hardware
today ([ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md)), so
routing it through a toolkit whose Linux behaviour is largely *never observed*
would make the verifiable platform depend on the unverifiable one. If the board
wants this revisited, it is a G4 re-opening, not a slice.

### D2 — Four surfaces, and exactly what each one is required to state

This is the per-surface requirement side of
[ADR-0002](0002-cross-platform-capability-matrix.md), narrowed to Phase 3. Read
every "what each platform can guarantee" question against ADR-0002's table — this
ADR adds no guarantee that table does not already carry.

| Surface | Every skin must | Every skin must not |
|---|---|---|
| **Status icon** | Render `UsageLevel` as a small icon that stays legible at the platform's scaling factors; re-render on scale change; re-register if the host restarts; activation shows the widget | Invent a level, a colour meaning or a threshold of its own — `UsageLevel` is derived in Core |
| **Tooltip** | State session and weekly percent and their resets, from the snapshot, through that skin's own `TooltipFormatter`; apply its own length limit | Let one platform's limit travel to the other (the `NotifyIcon.Text` 127-char cap is a Windows fact — [ADR-0001](0001-core-to-skin-data-contract.md)) |
| **Detail window** | Be a draggable widget launched from the icon (D5); show the per-model split, the statistics, the data-source provenance, and the explanation when there is no data — all of it from the `UsageDetail` push **D9** adds, because D3's snapshot alone cannot carry them; remember its own last position as a skin preference | Read Core storage or a provider directly; hold a number the shell did not give it; sum, average or re-bucket the per-model rows it was given (D9c) |
| **Alerts** | Present `RaiseEvent` and nothing else; at most one notification per raised event | Decide *that* an alert is due. Threshold crossing, off-plan entry, update availability and input degradation are all shell decisions ([ADR-0007](0007-app-shell-contract.md) D2 point 6) |

### D3 — A skin renders the last snapshot and nothing else, and never fabricates a figure

Every one of the four surfaces draws from the most recent
`IShellToSkin.ShowSnapshot(UsageSnapshot)` and from `RaiseEvent(UsageEvent)`.
That is the entire data path into a skin
([ADR-0007](0007-app-shell-contract.md) D6). No skin constructs a provider,
touches a store, reads a file under the user's Claude directory, or makes a
network call.

> **2026-10-02 amendment (OVI-326) — corrected in place.** "Snapshot plus
> events is the entire data path" was **wrong as written**, and D2's own
> detail-window row proved it: `UsageSnapshot` carries session/weekly percent,
> their resets, `DataSourceKind`, `LastIngestAt` and `ExtraUsage` and nothing
> else (CONFIRMED by reading `src/O-view.Core/Models/UsageSnapshot.cs`), so a
> skin asked to show the statistics and the per-model split could not have got
> them through this seam at all. **D9 adds a third shell→skin push,
> `ShowDetail(UsageDetail)`.** Everything else in D3 stands unchanged, and D9
> is deliberately drawn so that it does: the skin still receives pushed,
> already-computed Core data and still never reads a store. The sentence to
> read today is *"a skin renders what the shell last pushed and nothing
> else."*

Three standing product principles become mechanical rules here, because Phase 3
is where they are most easily broken:

1. **Never fabricate a number.** Every value a surface shows carries a
   `UsageValueStatus` ([ADR-0001](0001-core-to-skin-data-contract.md)). A value
   whose status is not real is marked as estimated, or omitted with a stated
   reason — never silently rendered as if it were measured, and never
   interpolated, smoothed or extrapolated to make a graph look continuous.
2. **Unknown is a renderable state, not an error.** A missing reset time, an
   unpriced model, a degraded provider: each has a stated presentation. "Not
   known yet" is the correct output, not a blank, a zero, or a dash the user
   must interpret.
3. **Stale beats dead.** A failed poll keeps the previous snapshot on screen
   (the shell's rule, [ADR-0007](0007-app-shell-contract.md) D2 point 9); the
   skin's obligation is to make the staleness visible rather than to hide it or
   to clear the display.

**Rejected: let the detail window hold its own short history buffer** so a graph
survives a restart without the store. Rejected — that is a second copy of
unrecomputable vendor-derived data in a skin, which
[ADR-0006](0006-local-storage-contract.md) D1 and
[ADR-0007](0007-app-shell-contract.md) D4 both put in Core.

### D4 — The draggable widget is the design on both platforms, and the icon rectangle is not used even where it exists

Per PDR §5.3: the detail view is a **draggable widget on every OS**, launched
from the tray/menu-bar icon, remembering its last position.

Windows *can* anchor a panel under the icon via
`Shell_NotifyIconGetRect` (CONFIRMED, source repo). **This design does not use
it.** SNI exposes no equivalent and never will (CONFIRMED, source ADR-0013 §3:
"SNI genuinely will not say where the host drew the icon"), so anchoring would
give Windows a first-class behaviour and Linux a permanent compromise, and would
put a platform limitation into the product's shape. One consistent, explainable
interaction on both platforms is worth more than a better Windows-only one.

Consequences accepted, stated plainly because this is a product regression
against the source's Windows behaviour: the frame-measured docked flyout and its
animation curves (CONFIRMED to exist in the source at `FlyoutAnimation.cs`) are
**not** ported. Windows users of the original app would experience this as a
change, not as a port.

**First placement** (before the user has ever dragged it) is each skin's own
answer, computed from that skin's own screen geometry: a work-area corner. The
rule is duplicated in both skins by design — see D5. **Last position** is a skin
preference file, never Core's store and never the shell's settings
([ADR-0007](0007-app-shell-contract.md) D4;
[ADR-0002](0002-cross-platform-capability-matrix.md) names it "a skin
preference, not Core data").

**Inherited behaviour kept** from source ADR-0013 §3's follow-up, which was
written after the first hardware report: position is a *request* on Linux. X11
window managers may ignore it and a native Wayland compositor will refuse it
outright. So the Linux skin **logs the position it asked for and the position it
was given**, so one round trip answers "did the corner take?". Where screen
geometry cannot be read at all, centre — half off-screen reads as broken where
plainly-centred does not.

### D5 — The corner rule is duplicated on purpose, and held together by a test rather than by a shared type

The source repository moved the corner rule into shared code
(`OView.App.Rendering.WorkAreaPlacement`) precisely because "which corner and how
much margin are answers a user expects both platforms to give the same way"
(CONFIRMED, source ADR-0013 §3 follow-up). That reasoning is good, and this ADR
still rejects the mechanism, because
[ADR-0007](0007-app-shell-contract.md)'s "Alternatives considered" already
decided this exact case: geometry that decides where a user's window lands is a
presentation decision, not neutral maths, and if the skins converge on the same
answer, ADR-0003's golden-master pattern is the way to hold them together — not a
shared implementation.

So: **each skin computes its own placement; a cross-skin test asserts they agree
on the content facts** (which corner, what margin, what the fallback is for a
given screen rectangle). That test is a pure function of injected screen
geometry, so it runs on either CI runner
([ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md)).

This is the honest trade: we pay duplication and a test to avoid a shared
presentation type that G4 = A has now ruled out. It is the same trade
[ADR-0007](0007-app-shell-contract.md) D5 made for the OS mechanisms, and it is
recorded here as a cost, not as a win.

### D6 — On Linux, ask the bus, not the toolkit — and never on the UI thread

Inherited unchanged from source ADR-0013 §2, which is the most load-bearing
measured finding in the source's Linux work and is **not optional**:

1. An Avalonia `TrayIcon` reports `IsVisible = true` whether or not a host
   exists; the app's output is **identical** with and without one (CONFIRMED,
   measured in the source's spike). Trusting the toolkit therefore ships an app
   that is silently invisible on the single most likely Linux configuration.
2. The Linux skin **must** probe the session bus for
   `org.kde.StatusNotifierWatcher` — measured to return `False` with no host and
   `True` with one.
3. When absent, it tells the user **what was observed and what to do**: that no
   notification-area host was found on the session bus, that GNOME needs an
   AppIndicator/KStatusNotifierItem extension, and how to reach the panel
   meanwhile. *State the observation, never a guess about their machine.* A
   message asserting the extension is missing, when what was observed was an
   absent bus name, is as wrong as saying nothing.
4. It watches for the name appearing later and registers without a restart — a
   user may install the extension while O-view is running. This is the Linux
   equivalent of Windows' `TaskbarCreated` re-registration.
5. It **never** runs that probe synchronously on the UI thread. Measured:
   blocking the dispatcher on a D-Bus round trip deadlocks the app outright —
   this is source issue #124's first-click deadlock, and #143's segfault came
   from the neighbouring mistake (a D-Bus-thread violation).

Silently invisible is not an acceptable outcome.

### D7 — The anti-drift harness grows with the surfaces, as the price of G4 = A

The board's acceptance of Option A rests in part on the duplication having "its
own safeguard." That safeguard is
[ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s cross-skin golden-master
harness, which today pins the content facts of the tooltip, the panel text, the
statistics, the off-plan state, the caveats, the freshness line, the boost notice
and the rate-limited notice (CONFIRMED — `tests/O-view.CrossSkin.Tests/`).

Phase 3 adds two surfaces it does not yet cover: **the detail window's content
set** and **the alert content set**. Both get content-fact fixtures *before* the
second skin implements them (slice 1), for the reason ADR-0003 exists: the
fixtures are how the two skins stay in agreement about *what must be said*, while
each keeps its own wording.

**What the harness still cannot do, and must not be claimed to do**
([ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md)): it pins
facts, not pixels. It cannot tell you an icon is legible, a window landed where
it asked, a tooltip is readable, or a notification appeared. Those remain
hardware questions, and on Linux they remain **never observed**.

**2026-10-02 note (OVI-324, PR #59) — slice 1's fixtures landed, and what they
actually cover.** Recorded here because PR #59 shipped without a record in
`docs/adr/` (found by OVI-335's drift check); this entry is that record, not a new
decision. CONFIRMED by source read of `tests/O-view.CrossSkin.Tests/`:

- The two content sets exist as `DetailWindowFixture`/`DetailWindowFixtures` (four
  fixtures as PR #59 shipped them; **eight today** — see the last bullet) and
  `AlertFixture`/`AlertFixtures` (four fixtures: `ThresholdCrossedIntoRed`,
  `OffPlanEnteredWithMeasurableRise`, `UpdateAvailable`, `InputDegradedNeverSucceeded` —
  one per `UsageEventKind`).
- They are **pure data, with no `Render` and no `SkinUnderTest`**, unlike the eight
  render families ADR-0003 inventories. No presenter exists for either surface yet
  (slices 6/7/10/11), so there is nothing to render against; the cross-skin test is
  wired per fixture once a presenter lands. That shape was Chief Gary II's scoping
  decision on OVI-325.
- `DetailWindowAndAlertFixtureDataTests` adds **4 test methods**, taking
  `O-view.CrossSkin.Tests` from 9 to 13. They assert fixture-set invariants — names
  unique, every fixture pins at least one `ContentFact` (`UpdateAvailable` exempt,
  as it carries no reading) — not skin output.
- **As shipped, the detail-window fixtures took a `UsageSnapshot`, not D9's `UsageDetail`** —
  PR #59 predates D9 (same day, OVI-326), which amended the slicing table's slice 1 row to
  require `UsageDetail` plus an empty-but-`Real` breakdown, an unpriced model, and a fully
  `Unavailable` detail. **That gap is now closed:** OVI-329 (PR #63, merged 2026-10-02)
  widened `DetailWindowFixture` to carry a `UsageDetail` and grew the set to eight fixtures,
  adding `StaleDetailOldLastIngestAt`, `RecordedWindowNoActivity`, `ModelBreakdownUnavailable`,
  and `UnpricedModelInWindow` (CONFIRMED by source read at `main` `48e0c9f`). No coverage
  discrepancy remains to escalate. OVI-329 recorded nothing in `docs/adr/` itself; that
  record gap belongs to OVI-335/OVI-340's drift triage, not to this note.

Nothing in this note is verified by execution — documentation only; the 9→13 count and the
fixture shapes are from a source read of the `[Fact]` attributes and the fixture files, not
from a test run. OVI-329 added no new `[Fact]`, so 13 still stands.

### D8 — Out of scope, and staying out

- **macOS** — gate **G3**, open. No menu-bar skin, no third skin, and no type
  named or shaped to anticipate one.
- **A second AI source** — gate **G5**, open. Phase 3 renders what
  [ADR-0005](0005-data-provider-contract.md)'s Claude providers supply.
- **A shared UI layer** — gate G4, now **closed as A**. Reversing it is a new
  board decision.
- **Packaging, installer and self-update behaviour** — not Phase 3. The update
  *check* already lives in the shell
  ([ADR-0007](0007-app-shell-contract.md) D3) and Phase 3 only presents its
  result as an `UpdateAvailable` event. Acting on it — self-replace on Windows,
  notify-only under a Linux package manager, which
  [ADR-0002](0002-cross-platform-capability-matrix.md) records as a bug the
  source already shipped once — is its own phase.

**Escalation check, per this task's brief: no part of Phase 3 as scoped here
requires G3 or G5 to be opened first.**

### D9 — Detail-window data reaches a skin by a second push, `ShowDetail(UsageDetail)` — added 2026-10-02 (OVI-326)

**The gap this closes.** D2 requires the detail window to state four things: the
per-model split, the statistics, the data-source provenance, and the
explanation when there is no data. Two of those four were not reachable.
CONFIRMED by reading this repository:

| D2 requirement | Reachable through D3's seam today? |
|---|---|
| Data-source provenance | **Yes** — `DataSourceKind` + `LastIngestAt` are on `UsageSnapshot` |
| No-data explanation | **Yes** — `UsageSnapshot.Unavailable` plus each value's `UsageValueStatus` is exactly that explanation, worded by the skin |
| The statistics | **No** — `UsageStatistics` (`src/O-view.Core/Models/UsageStatistics.cs`) exists as a type, is referenced by both skins' `PanelTextFormatter`, and is wired into **no** seam. Nothing in `src/` produces one: [ADR-0005](0005-data-provider-contract.md) D6c's `GetStatistics(utcNow)` is designed and unimplemented |
| The per-model split | **No** — per-model data exists only as `OView.Core.Storage.DailyModelUsage`, a storage record of raw `long`s with no status flags, on the far side of the boundary D2 forbids a skin to cross. [ADR-0001](0001-core-to-skin-data-contract.md)'s `ModelBreakdown[]` contract row has never been built in this repository |

So D2 asked for a window that D3's contract could not feed. That is a design
defect in this record, not a builder's problem to improvise around, and the
rest of D9 fixes it.

**D9a — one new Core type, `UsageDetail`, and one new push.** The shell→skin
seam gains a third member:

```
void ShowDetail(UsageDetail detail);
```

```
UsageDetail(
    UsageSnapshot        Snapshot,     // the snapshot this detail was computed beside
    UsageStatistics      Statistics,   // the existing Core type, unchanged
    ModelUsageBreakdown  Models)
  static UsageDetail Unavailable { get; }   // Snapshot/Statistics/Models all .Unavailable
```

```
ModelUsageBreakdown(
    DateOnly                        FromLocalDate,   // the window the rows cover,
    DateOnly                        ToLocalDate,     //   stated, never implied
    IReadOnlyList<ModelUsageRow>    Rows,            // one row per model, pre-aggregated
    HistoryCoverage                 Coverage,        // how much of the window is recorded
    RateCardStamp                   Rates,           // which table priced the rows
    UsageValueStatus                Status)
  static ModelUsageBreakdown Unavailable { get; }    // Status = Unavailable, Rows empty

ModelUsageRow(
    string          ModelId,              // the vendor id, verbatim, never reworded
    int             RequestCount,
    TokenCount      InputTokens,
    TokenCount      OutputTokens,
    TokenCount      CacheCreationTokens,
    TokenCount      CacheReadTokens,
    EstimatedUsd    EstimatedSpend)       // Unavailable status = this model is unpriced
```

Three properties of that shape are load-bearing:

- **`Status = Real` with an empty `Rows` is a different fact from
  `Unavailable`** — "Core read the ledger and the window holds no recorded
  activity" versus "Core could not read the ledger". This is the same
  distinction `HasCreditUsage` and `UnpricedModels` already draw
  ([ADR-0001](0001-core-to-skin-data-contract.md), OVI-100/OVI-168), and a skin
  owes the user different words for each.
- **Rows are aggregated per model over the stated window by Core, not per
  (date × model) as `DailyModelUsage` is.** A skin is never handed 31 days ×
  N models to total up: arithmetic over vendor-derived data in a skin is how a
  fabricated number gets made, and it would be two implementations of the same
  sum, one per skin.
- **An unpriced model needs no extra flag.** `EstimatedSpend` with
  `UsageValueStatus.Unavailable` already says it, and
  `UsageStatistics.UnpricedModels` already names the window's unpriced set.
  **Rejected: an `IsPriced` flag on the row** — a second way to state one fact,
  which is the ill-formed pair ADR-0001 keeps closing (OVI-146).

**D9b — the shell is the only thing that reads the ledger, and it reads it only
while the widget is open.** The shell assembles `UsageDetail` from the last
`UsageSnapshot` plus [ADR-0005](0005-data-provider-contract.md) D6c's
ledger-read seam, and pushes `ShowDetail`:

1. when the widget becomes visible — once, immediately, built from the snapshot
   already in hand;
2. on every subsequent successful poll **while it is visible**;
3. never while it is hidden.

Rule 3 is the point. The statistics and the breakdown are SQLite aggregates
over a 31-day ledger ([ADR-0006](0006-local-storage-contract.md) D1: rollups
are computed at query time, never stored), and the icon and tooltip need none of
it. Paying for that query on every poll of a program whose window is shut
almost all the time is a cost with no reader.

This needs the shell to know the widget is open, which it currently cannot:
`SetVisible(bool)` is shell→skin, and `ISkinToShell` has no member for icon
activation (CONFIRMED by reading `src/O-view.App/ISkinToShell.cs`). Slice 4's
"activation → `SetVisible(true)`" would have had the skin calling a method on
itself. **The skin→shell seam therefore gains one command:**

```
void RequestWidget(bool visible);   // "the user activated the icon" / "dismissed it"
```

The skin reports the user's intent; the shell decides, and answers with
`SetVisible(true)` followed by `ShowDetail(...)`. That keeps ADR-0007 D6's
established shape — skin→shell members are commands, and the skin learns the
result from the next shell→skin push — and it keeps window lifecycle in the
shell where ADR-0007 D2 put it.

**This is not the skin polling the shell** (rejected by ADR-0007 D6) and not a
Core event stream (rejected above). `RequestWidget` fires on a user gesture, and
every subsequent refresh rides the shell's one existing poll cadence. There is
still exactly one schedule in the program.

**D9c — `UsageDetail` carries its own snapshot so the window cannot show a torn
reading.** The detail window renders from the last `UsageDetail` and nothing
else — never a percent from `ShowSnapshot` beside statistics from an earlier
`ShowDetail`. The alternative was an ordering rule ("the shell always pushes
`ShowSnapshot` first, from the same tick"); **rejected** — a rule about the
order of two independent calls cannot be enforced by anything a skin owns, and
it fails the first time a poll succeeds while the detail query throws. Carrying
the snapshot inside the pushed object makes one self-consistent object the only
thing the window can draw, which costs one immutable reference.

D3's "stale beats dead" rule applies unchanged: a failed poll pushes no new
detail, the window keeps the last one, and its `Snapshot.LastIngestAt` is how
the skin shows the staleness.

**D9d — nothing else widens.** Named explicitly, because "the detail window
needs more data" is an argument that will be made again:

- **`IUsageProvider` does not change.** `UsageSnapshot` stays the plan meter as
  read live from vendor artefacts. Putting ledger-derived statistics on it would
  force every provider to return data it has no source for — the exact shape
  [ADR-0005](0005-data-provider-contract.md) D6c already rejected — and would
  drag a 31-day aggregate onto the tooltip's path.
- **`DailyModelUsage` does not cross the seam.** It is a storage type; the
  Core-to-skin contract is model types with status flags.
- **The alert path does not change.** `UsageEvent`/`UsageEventKind` already
  cover D2's alert row (`InputDegraded` carries the degraded-input fact, so
  `ProviderHealth[]` needs no new transport either).
- **No history series, and no per-model split for *today*.** D2 requires
  neither; the "history graph" appears only in this record's rejected
  alternatives. Both are real future widenings of `UsageDetail` and both are a
  later amendment with their own slice, not something a builder adds in slice 6.

**Product-principle check (the escalation test in this task's brief): nothing
D9 exposes is new data.** The ledger is Core's own local store of locally
observed activity ([ADR-0006](0006-local-storage-contract.md)); every value
keeps its `UsageValueStatus`; the reads are read-only, on the local machine,
and touch no credential. No escalation required.

## Alternatives considered

**Build the Linux skin first, since it is the one nobody has ever seen work.**
Tempting on "risk first" grounds, and rejected: this repository cannot verify
Linux UI at all ([ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md);
containers have no compositor), so building it first would mean writing the
larger, harder half of the phase with no feedback and no reference
implementation to compare content facts against. Windows first gives a verified
reference for every surface, which is also what makes the cross-skin fixtures
meaningful when Linux arrives.

**One slice per platform ("the Windows skin", "the Linux skin").** Rejected —
each would be thousands of lines and unreviewable, and PRs over ~500 lines are
board-merge-critical by this project's own rule. Sliced per surface instead.

**Have the shell own "show the widget at this position."** Rejected: that is
geometry in the shell, which D1 forbids. `SetVisible(bool)` is lifecycle only
and ADR-0007 D6 already says so explicitly — "the shell says 'the user asked for
the widget,' not where or how big."

**Let a skin subscribe to a Core event stream so the detail window can
re-render mid-interval without waiting for a poll.** Rejected — ADR-0007 D6
already rejected the skin reading Core, and `RefreshNow()` is the sanctioned
answer. It will feel restrictive the first time a graph wants it; that is the
intended constraint.

**Ship Linux with a status icon only and no detail window.** Rejected on the
source's own reasoning (ADR-0013's alternatives table): the panel holds
everything the icon cannot say — the model split, the history graph, the
estimated tiles, the data-source badge, the no-data explanation. A coloured dot
with no way to see why is not the product.

## Consequences

**Positive**

- Phase 3 is scopeable today: G4's answer removes the last design question that
  was blocking it, and the slicing table below is ready for decomposition.
- ~~Everything new in this phase is new code. No Phase 2 contract changes, no
  Core change, no shell change — the seams ADR-0007 defined are used as-is,
  which is also the first real test of whether they were drawn correctly.~~
  **Corrected 2026-10-02 (OVI-326): this claim did not survive its own D2.**
  Phase 3 *is* the first real test of the Phase 2 seams, and the test found
  something: the shell→skin seam could not carry two of the four things D2
  requires the detail window to state. D9 widens it — one new Core type, one new
  member in each direction, and one unimplemented Core read seam
  ([ADR-0005](0005-data-provider-contract.md) D6c) that now has to be built.
  Three slices' worth of work the original table did not contain. Finding this
  before slice 6 rather than inside it is the cheap version of this outcome; the
  honest reading is that the "no contract change" bullet was optimism, not
  analysis.
- The draggable-widget decision gives one interaction to document, support and
  test, instead of two with a permanent asterisk on one of them.
- The Windows build keeps its zero-third-party-runtime-dependency property; the
  Avalonia closure lands only in the Linux skin, exactly as source ADR-0013 §4's
  amendment scoped it.

**Negative**

- **The panel exists twice.** This is the largest deliberate duplication in the
  project, and the board accepted it with its eyes open. D5 and D7 are the
  mitigation; they are a test and a convention, not a guarantee.
- **A Windows behaviour regression against the original app:** no docked
  flyout, no measured animation curves. Deliberate (D4), and users of the
  original will notice.
- **Most of the Linux half will ship unverified and must be labelled so.** The
  detail panel, the tooltip, notifications, theme-following and the menu
  rendering are all **never observed on real hardware**
  ([ADR-0002](0002-cross-platform-capability-matrix.md)); four fixed-never-
  retested bugs (#124, #125, #129, #143) sit in exactly the code Phase 3 writes.
  No slice below may upgrade an evidence label; only a dated hardware
  verification event can.
- **Every claim in this record about how a skin will behave is INFERRED.** None
  of it exists yet. The first slices are where it becomes CONFIRMED or gets
  amended in place.
- Slice 7 adds Avalonia and its native closure to this repository — a
  dependency and packaging-size change, and therefore a board-merge PR.

## Slicing guidance for decomposition

One slice = one PR. Ordered lowest-risk first; Windows before Linux per
"Alternatives considered". The sign-off this table waited for has landed (see
**Status**), so decomposition may proceed.

> **2026-10-02 amendment (OVI-326):** two slices inserted — **5a** and **5b**,
> the Core and shell halves of D9 — and slices 1, 4 and 6 re-scoped. Slice 6
> cannot be built without 5b, and slice 1's fixtures take a `UsageDetail`, not a
> `UsageSnapshot`. Existing slice numbers are left alone so that issues already
> filed against them stay valid.

| # | Slice | Depends on | Risk | Merge |
|---|---|---|---|---|
| 1 | Content-fact fixtures for the two new surfaces — detail-window content set and alert content set — in `tests/O-view.CrossSkin.Tests/`, in ADR-0003's existing fixture shape. Pure, no toolkit, no UI, runs on both runners. **Amended 2026-10-02 (OVI-326): the detail-window fixtures take a `UsageDetail` (D9a) — snapshot, statistics and per-model rows — not a `UsageSnapshot`. Fixture cases must include an empty-but-`Real` breakdown, an unpriced model, and a fully `Unavailable` detail** | 5a (for the types only) | **Lowest** — tests only; defines what both skins must say before either says it | agent |
| 2 | The placement rule as a pure function of injected screen geometry, implemented separately in each skin, plus the cross-skin test that they agree (D5). Still no window. **Built 2026-10-03 (OVI-356): `DetailWindowPlacement.Compute` in both `O-view.Tray` and `O-view.Linux`, independently implemented, each with its own unit tests; `tests/O-view.CrossSkin.Tests/DetailWindowPlacementGoldenMasterCrossSkinTests.cs` pins corner/margin/fallback agreement over five injected rectangles** | 1 | Lowest — arithmetic over injected rectangles | agent |
| 3 | Windows skin host: `UseWPF`, an entry point that composes shell + skin per ADR-0007, an `IShellToSkin` implementation that receives snapshots and events and renders nothing yet. Proves the seam end to end. **Built 2026-10-03 (OVI-360): `O-view.Tray.csproj` gained `OutputType=WinExe` and `UseWPF=true`. `Program.Main` builds a real (read-only, no display text) `JsonlUsageProvider` over `ClaudeDataRoots.CandidateRoots`, composes it with `SystemClock`/`AppTimer` into a `UsagePollLoop` through the new `TraySkinHost.Compose`, and keeps the process alive with a windowless WPF message loop (`ShutdownMode.OnExplicitShutdown` — no quit path exists until a later slice's `NotifyIcon`). `TrayShellToSkin` is the no-op `IShellToSkin`: it records the last snapshot/event/visibility/shutdown call and renders nothing. `UsagePollLoop` gained a `SnapshotUpdated` event (a deliberate, documented extension of ADR-0007 slice 2's shape, the same kind slice 2 made to `IAppTimer`) so a consumer can be pushed new snapshots instead of polling `CurrentSnapshot`; `TraySkinHost.Compose` wires that event to `IShellToSkin.ShowSnapshot` and pushes the pre-first-tick snapshot immediately. `TraySkinHostTests` and `TrayShellToSkinTests` prove the seam against fakes (no WPF loop, no real timer/provider): a poll tick reaches the skin's `ShowSnapshot` with the exact snapshot, a throwing poll does not, and every `IShellToSkin` member is recorded exactly as called. `dotnet test O-view.slnx` passes in full: 501 tests. Deferred, per this slice's boundaries: `NotifyIcon`/status icon (4), tooltip (5), detail window (6), alerts (7) — nothing renders here. Not verified: an actual Windows run of the built executable (no interactive Windows desktop available in this environment; `dotnet build`/`dotnet test` on the Windows CI runner is the only automated proof this slice has) and no real Claude Desktop transcript data was exercised (the provider resolves real candidate roots but this environment has none to read, so the live path degrades to `UsageSnapshot.Unavailable`, which is itself one of the states the skin seam is proven against).** | — | Low–medium — first executable in this repo; `.csproj` change | **board** (csproj) |
| 4 | Windows status icon: `NotifyIcon`, `UsageLevel` → icon render, per-monitor DPI v2 re-render, `TaskbarCreated` re-registration, activation → **`ISkinToShell.RequestWidget(true)`** (amended 2026-10-02, OVI-326 — the skin asks, the shell answers with `SetVisible`; it does not call `SetVisible` on itself) | 3, 5b | Medium — first OS UI integration; verifiable here | agent |
| 5 | Windows tooltip: wire the existing `O-view.Tray` `TooltipFormatter` to `NotifyIcon.Text`, with the 127-char cap applied in the skin | 4 | Low — formatter and its tests already exist | agent |
| 5a | **Core (D9a):** `UsageDetail`, `ModelUsageBreakdown`, `ModelUsageRow`, and the ledger-read seam [ADR-0005](0005-data-provider-contract.md) D6c designed and nobody built — `GetStatistics(utcNow)` plus `GetModelBreakdown(utcNow)` over `UsageLedgerStore`, aggregated per model, with D1's four obligations (never throws, injected clock, read-only, no display text). Pure Core, no UI. **Built 2026-10-02 (OVI-332) as `IUsageStatisticsSource`/`LedgerUsageStatisticsSource` in `src/O-view.Core/Statistics/` — see [ADR-0005](0005-data-provider-contract.md) D6c's "as built" amendment for the `TimeZoneInfo` parameter both queries needed. No rate table exists yet, so `EstimatedUsd` is `Unavailable` everywhere this seam produces one** | — | Low–medium — arithmetic and SQL over an existing store, fully testable on both runners; the per-model aggregation is the part to test hardest | **board** (Core contract — now covered by this merged ADR, but still a Core change) |
| 5b | **Shell (D9b/D9c):** `IShellToSkin.ShowDetail(UsageDetail)`, `ISkinToShell.RequestWidget(bool)`, and the shell-side rule — assemble detail on widget-show and on each successful poll while visible, never while hidden; `UsageDetail.Unavailable` when the ledger read fails. No UI. **Built 2026-10-03 (OVI-364) as the two interface members plus `DetailPushCoordinator` in `src/O-view.App/` — a standalone collaborator (no composition root exists yet to wire it to `UsagePollLoop`/`ISkinToShell` for real; that wiring is left to whichever slice first needs a running process)** | 5a | Low–medium — shell wiring with a visibility rule worth its own tests (hidden ⇒ zero ledger reads) | **board** (seam change) |
| 6 | Windows detail window: draggable widget, content from the **last `UsageDetail`** via the existing formatters plus a per-model section, skin preference file for last position, first-run corner from slice 2, outside-click dismiss → `RequestWidget(false)`. Renders no figure the pushed detail did not carry and totals nothing itself (D9c) | 2, 3, 1, **5b** | Medium–high — largest Windows slice; split per `## section` of the window if it exceeds ~500 lines | agent, or board if split is declined |
| 7 | Windows alerts: `RaiseEvent` → toast, once per raised event, no skin-side threshold logic | 3, 1 | Low–medium | agent |
| 8 | Linux skin host: Avalonia reference, entry point, `IShellToSkin` implementation rendering nothing, **plus D6's session-bus probe and the absent-host message**, off the UI thread, with watch-for-later-registration | 3 (as reference) | **High** — first third-party UI dependency and native closure; the probe is the slice's point, not an extra | **board** (dependencies, packaging size) |
| 9 | Linux status icon: live-rendered `RenderTargetBitmap` replaced on the timer; never a themed icon name (a name cannot render a gauge — CONFIRMED in the source's spike) | 8, 4 | **High** — unverifiable here; ships labelled unverified | agent |
| 10 | Linux detail window + placement, logging requested *and* granted position (D4). Same `UsageDetail`-only rule as slice 6 | 8, 6, 2, 5b | **High** — the panel has never been observed rendering on real hardware in any form | agent |
| 11 | Linux tooltip + freedesktop notifications | 8, 5, 7 | **High** — both **never observed** on real hardware | agent |

Slices 1–2 and 5a–5b need no board answer beyond this ADR, though 5a and 5b
change Core and the seam and are board *merges*. Slices 3 and 8 change project
files or dependencies and are board merges by this project's rule. Slices 9–11
should each be expected to ship with their ADR-0002 rows **unchanged**, and the
PR body should say so in "Risk / not verified" rather than implying the code
working means the platform works.
