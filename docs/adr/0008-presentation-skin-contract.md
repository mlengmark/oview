# ADR-0008: The presentation contract — four surfaces, two native skins, no shared UI layer

- **Status:** **Accepted** — the board signed this record off by merging PR #58
  itself (CONFIRMED: merged by the board account `mlengmark`,
  2026-10-02T11:02:23Z; the SHA-pinned merge card tracked on OVI-320, now
  closed). The slicing table is therefore open for decomposition. Living
  document: amend in place, as [ADR-0001](0001-core-to-skin-data-contract.md)
  does.
- **Date:** 2026-10-02 · **Amended** 2026-10-02 (D9, OVI-326; D7 note,
  OVI-324/OVI-342); 2026-10-09 (D9b, OVI-601); 2026-10-09 (**gate G7 parity** —
  D4 off-screen fallback, D9e–D9h, new D10, new D11; OVI-585, accepted by the
  board on OVI-591, card `4e515cef`; D9e–D9h transcribed by OVI-602, the rest by
  OVI-607); 2026-10-10 (slice P2 built, OVI-621 — "built" note under D11, no
  decision text changed)
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
| **Menu** — added 2026-10-05, see below | Present the items [ADR-0009](0009-control-surface-contract.md) D2 enumerates; read run-at-startup **live from the OS** every time it opens; render the state the OS reported, not the state the user requested; state a failed toggle rather than swallowing it (ADR-0009 D3) | Hold its own copy of a shell setting (ADR-0009 D4), decide what any threshold *means*, or offer a setting [ADR-0007](0007-app-shell-contract.md) D4 did not assign to the shell |

> **2026-10-05 amendment (OVI-432):** this table is now **five** surfaces, not
> four. The menu is Phase 4A's surface and its obligations are decided in
> [ADR-0009](0009-control-surface-contract.md) D1–D4; the row above is the
> summary, that record is the detail. This record's *title* still says "four
> surfaces" on purpose — it is the name every existing citation, PR and issue
> uses, and renaming a merged record to keep a count current would break more
> than it fixes. The count lives in this table, which is the thing that has to
> be right.

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

> **2026-10-09 amendment (OVI-585/591, gate G7).** The board has confirmed this
> decision rather than reopening it: the detail window stays a **draggable widget
> that remembers its last position, on both platforms**, and the source's docked
> flyout (`PopupPositioner.cs`) and docked-edge rise animation
> (`FlyoutAnimation.cs`) remain explicitly **not** parity items. D4 is not
> re-decided. One rule is added.
>
> **A remembered position is a request, not an instruction.** On every show, a
> saved position is re-validated against the screens as they are *now*. If the
> saved rectangle does not intersect any current work area — monitor unplugged,
> resolution or scaling changed, a VM resized while the app was shut — the window
> falls back to first placement and opens in the corner. It never opens where it
> cannot be seen, and it never opens half off-screen: partial intersection is
> treated as failure, not nudged back into view, because "nudge it back" is a
> second placement rule for a skin to get subtly different from the other one.
> `DetailWindowPositionController.ResolveShowPosition()` today returns a saved
> position with no screen check (CONFIRMED, both skins).
>
> **Where the rule lives.** In each skin, behind an injected
> `Func<Rect, bool> isOnScreen` predicate, so the ordering rule stays provable
> against fakes with no window and no disk — exactly the shape ADR-0008's slice 6
> already built. The predicate's *implementation* is per-OS (Windows work areas;
> Linux where the geometry can be read at all). D5 applies unchanged: both skins
> compute it, and one cross-skin test asserts they agree on the content facts for
> a given screen rectangle.
>
> **Linux keeps D4's existing honesty rule**, now covering restores too: the
> position asked for and the position granted are both logged, because a Wayland
> compositor can refuse a restore exactly as it can refuse a first placement.
> Where no geometry can be read, D4's existing "centre" answer stands — a restore
> cannot be validated against screens that cannot be enumerated, and centring is
> the one placement that is never half off-screen.
>
> **Rejected: clamping the saved position into the nearest work area.** It keeps
> the window near where the user left it, and it is two geometry implementations
> drifting against each other for a case that happens once per hardware change.
> **Rejected: storing the monitor identity beside the position** and restoring
> only on a match. It is more faithful and it needs a stable per-monitor
> identifier, which Linux does not reliably give (INFERRED) — a Windows-first
> mechanism, which is the shape D4 exists to refuse.

**2026-10-10 built (OVI-620, G7 parity slice P1).** `DetailWindowPositionController`
in both skins now takes `Func<ScreenRect, bool> isOnScreen` plus the window's
fixed width/height; `ResolveShowPosition()` builds a `ScreenRect` from the saved
point and those dimensions, calls the predicate, and only returns the saved
position when it accepts — otherwise it falls through to the exact same
`computeFirstShowPlacement()` call a never-saved position already used, which is
how "falls back to first placement" and "centre when no geometry can be read"
turn out to be the same code path rather than two. `ScreenRect` (a plain
`X, Y, Width, Height` record struct, named apart from the WPF/Avalonia `Rect`
types already in scope in both files) and the full-containment arithmetic
(`DetailWindowOnScreenCheck.IsFullyOnScreen`) are each duplicated per skin (D1),
with a cross-skin fixture holding the two to the same on/off-screen verdict for
a given rectangle (D5). The real predicate — enumerating every current monitor's
work area on Windows (`System.Windows.Forms.Screen.AllScreens`) or every current
`Screens` entry on Linux — is adapter code in each composition root, not unit
tested, same boundary as every other real-environment read in this repository;
`DetailWindowOnScreenCheck` itself is proven against fakes, including the
partial-intersection case named above. Linux's existing request-vs-granted
logging (`DetailWindow.SetVisible`) already ran on every show, restore included,
so this slice needed no change there — it was never scoped to first placement
only. No `UsageDetail`/panel-content state is touched, so D11a's render-proof
obligation does not apply to this slice; the existing offscreen render hook
(P0) is unaffected.

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

> **2026-10-05 amendment (OVI-432) — where the deferred items went.** The board
> split Phase 4 in two (option C, card `80339fe7`, OVI-423):
> **settings, the right-click menu, the threshold picker, run-at-startup and
> theme following** are Phase 4A,
> [ADR-0009](0009-control-surface-contract.md). **Packaging, the installer and
> self-update execution** stay deferred and are Phase 4B, OVI-433 — this
> record's deferral of them above still stands, and ADR-0009 does not scope
> them either. macOS (G3), a second AI source (G5) and a shared UI layer (G4,
> closed as A) remain out of scope in both.

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

**Amended 2026-10-09 (OVI-601, G7 parity slice P4) — a left-click on the icon
toggles rather than always opening, per the original `ui-spec.md` section 4
("Clicking the icon toggles").** `RequestWidget(true)` still means
"show, unconditionally" — the control-surface menu's "show usage details"
item keeps that meaning (P21 reuses this slice's rule for the menu later).
The icon's own left-click gesture needed a different meaning, so the
skin→shell seam gains a second command instead of overloading the first:

```
void ToggleWidget();   // "the user clicked the tray icon" — open if closed, close if open
```

`DetailPushCoordinator.OnIconActivated()` implements the decision, from the
`_visible` state it already tracks for rule 3 above: if visible, this closes
it (equivalent to `OnRequestWidget(false)`); if hidden, this opens it
(equivalent to `OnRequestWidget(true)`) — **unless** the hide happened less
than 400 ms ago, in which case the click is absorbed and the widget stays
closed. That grace window exists because the click that dismisses the widget
by taking its focus is the *same* click the icon then receives — without it,
a click could only ever reopen the widget it had just closed. Both skins'
`DetailWindow` now also close on Esc, calling `RequestWidget(false)` exactly
as their existing `Deactivated` handler does. On Windows, `SetVisible` now
takes the foreground explicitly (`O-view.Tray/Platform/ForegroundWindowTaker.cs`,
Win32-only, not referenced from `App`/`Core`) with an `AttachThreadInput`
fallback for when `SetForegroundWindow` is refused — a widget shown but never
actually foregrounded never raises `Deactivated`, so it would stay stuck on
screen with no way to dismiss it (Esc is now the backstop even then).

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

> **2026-10-09 amendment (OVI-585/591, gate G7).** D9d said "no history series,
> and no per-model split for today", and said plainly that both were "a real
> future widening of `UsageDetail` … a later amendment with their own slice".
> Gate G7 is that amendment. D9a–D9c are unchanged; D9d's four closures are
> **narrowed, not lifted** — see D9h below.
>
> **D9e — `UsageDetail` widens once, by four optional members, and
> `UsageSnapshot` by two.** Every new member defaults to its own `Unavailable`
> sentinel, so a shell that has not assembled it yet pushes an honest gap
> rather than a zero:
>
> ```
> UsageDetail(                           // D9a's three positional members, unchanged
>     UsageSnapshot        Snapshot,
>     UsageStatistics      Statistics,
>     ModelUsageBreakdown  Models)
> {
>     AccountIdentity       Account         { get; init; } = AccountIdentity.Unavailable;
>     DailyUsageSeries      History         { get; init; } = DailyUsageSeries.Unavailable;
>     WeeklyResetBoundaries ResetBoundaries { get; init; } = WeeklyResetBoundaries.Unavailable;
>     TokenKindTotals       TokensToday     { get; init; } = TokenKindTotals.Unavailable;
>     TokenKindTotals       Tokens31d       { get; init; } = TokenKindTotals.Unavailable;
> }
> ```
>
> ```
> AccountIdentity(
>     string?           DisplayName,        // verbatim from the vendor file, never reworded
>     string?           EmailAddress,
>     string?           OrganizationType,   // the vendor's own token, relayed verbatim
>     UsageValueStatus  Status)
>   static AccountIdentity Unavailable { get; }
> ```
>
> The tier is `oauthAccount.organizationType` and nothing else. Core relays
> the token; mapping a token to a badge word is wording, so it is the skin's,
> and an unrecognised token renders verbatim — the same rule
> `ModelUsageRow.ModelId` already carries. *Rejected: a `Tier` enum in Core.*
> An enum must decide what an unknown value becomes, and every answer to that
> is either a fabricated tier or a display string in Core.
>
> ```
> DailyUsageSeries(
>     DateOnly                        FromLocalDate,
>     DateOnly                        ToLocalDate,
>     IReadOnlyList<DailyUsagePoint>  Days,       // all 31, in date order, never sparse
>     HistoryCoverage                 Coverage,
>     UsageValueStatus                Status)
>   static DailyUsageSeries Unavailable { get; }
>
> DailyUsagePoint(
>     DateOnly    LocalDate,
>     TokenCount  OutputTokens)       // Status.Unavailable = not recorded -> blank column
> ```
>
> Three properties are load-bearing: every day in the window is present, and
> absence is a status, not an omission (`OutputTokens.Status == Unavailable`
> is "this day is not in recorded history" and renders as a blank column with
> its date label still drawn; `Status == Real` with value `0` is "a recorded,
> idle day" — a sparse list would make the skin reconstruct the missing dates,
> which is date arithmetic in a skin); the measure is output tokens, matching
> the tiles; and there is no per-model and no per-kind split per day — the
> tiles' per-model split comes from `Models`, and the kind split from
> `TokensToday`/`Tokens31d`.
>
> ```
> TokenKindTotals(
>     DateOnly             FromLocalDate,
>     DateOnly             ToLocalDate,
>     TokenKindAmount      Input,
>     TokenKindAmount      Output,
>     TokenKindAmount      CacheCreation,
>     TokenKindAmount      CacheRead,
>     TokenCount           Total,          // carried, so no skin ever sums the four
>     RateCardStamp        Rates,
>     UsageValueStatus     Status)
>   static TokenKindTotals Unavailable { get; }
>
> TokenKindAmount(TokenCount Tokens, EstimatedUsd EstimatedValue)
> ```
>
> `Total` is carried rather than derived because a skin's share text needs a
> denominator that both skins agree on. Pricing per kind is Core's; the
> *share* is the skin's.
>
> ```
> WeeklyResetBoundaries(
>     DateOnly                              FromLocalDate,
>     DateOnly                              ToLocalDate,
>     IReadOnlyList<WeeklyResetBoundary>    Boundaries,   // ascending
>     UsageValueStatus                      Status)
>   static WeeklyResetBoundaries Unavailable { get; }
>
> WeeklyResetBoundary(
>     DateTimeOffset            Instant,   // carried in the offset in force at that instant,
>     WeeklyResetBoundaryKind   Kind)      //   in the same zone the day buckets were computed in
>
> enum WeeklyResetBoundaryKind { Observed, DerivedFromObserved, MondayFallback }
> ```
>
> **Three kinds, not two.** Past boundaries are *derived* by stepping the
> cadence back from the predicted next reset, and a derivation is not an
> observation. Collapsing `DerivedFromObserved` into `Observed` would present
> a computed boundary as a recorded one, which the "never fabricate" rule
> forbids. The boundary carries its own `DateTimeOffset` so a skin can place
> it against the day columns without resolving a timezone —
> [ADR-0006](0006-local-storage-contract.md) D2's rule that the zone is always
> caller-supplied, never read inside, holds for skins too; per-boundary
> offsets are what make a DST transition inside the window representable.
>
> **`UsageSnapshot` gains exactly two members**, closing
> [ADR-0001](0001-core-to-skin-data-contract.md)'s 2026-09-21 reservation:
>
> ```
> public BoostNotice? SessionBoostNotice { get; init; }   // null = the cache did not say
> public BoostNotice? WeeklyBoostNotice  { get; init; }
> ```
>
> `BoostNotice` already carries Claude's own sentence, the percent and the end
> date, and the hover card's provenance is `DataSourceKind` + `LastIngestAt`,
> already on the snapshot. Boost notices qualify as `UsageSnapshot` members —
> and not `UsageDetail` members — because they are plan-meter facts read from
> the same vendor cache block the percentages come from, on the tooltip's
> poll path, not a 31-day ledger aggregate.
>
> **D9f — the weekly-reset "not known" state needs no new field.**
> `UsageSnapshot.WeeklyResetAt` is a `UsageInstant` with its own status, and
> the "no plan data at all" case is `DataSourceKind.Unavailable` — both
> already state what a three-valued enum would be a second way to state.
>
> **D9g — the new members ride the same push and the same cadence.** No new
> seam member, no second schedule. `ShowDetail(UsageDetail)` is unchanged in
> signature; D9b's three rules (on becoming visible, on every poll while
> visible, never while hidden) apply to the whole object.
>
> **D9h — what stays closed.** D9d's four closures, narrowed:
>
> - **`IUsageProvider` still does not change.** `UsageSnapshot` gains two
>   boost members and nothing else; no series, no kind totals, no account
>   identity.
> - **`DailyModelUsage` still does not cross the seam.** `DailyUsageSeries` is
>   a Core model type with status flags, built *from* it.
> - **The alert path still does not change.**
> - **No per-model split for *today* beyond `Models`' stated window.**
>
> **This slice (OVI-602, gate G7 parity P5) adds only the contract members
> above and their `Unavailable`-sentinel defaults.** No ledger query backs
> `History`/`ResetBoundaries`/`TokensToday`/`Tokens31d` yet (a later slice,
> [ADR-0005](0005-data-provider-contract.md)'s D6c amendment), no account
> reader backs `Account` yet (also a later slice), and no skin reads any of
> the six new members yet. Every new member therefore reads as `Unavailable`
> (or `null`, for the two boost members) on every existing `UsageDetail`/
> `UsageSnapshot` construction in this repository — a pure, additive contract
> widening with no behaviour change.

### D10 — The parity bar, and who owns each half of it — added 2026-10-09 (OVI-585/591, gate G7)

**D10a — the bar.** The detail window and the surfaces in OVI-584 §K are
**complete only when every item in OVI-584's checklist A–K is met on the
platform(s) named there, or waived by a board card.** That checklist, not this
ADR's prose, is the acceptance list; this record exists to say which layer owes
each item, because the same checklist line often has a Core half and a skin half.
The checklist, the build order that works through it, the one blocking
prerequisite and the waiver candidates are transcribed in
[`docs/parity/g7-detail-window-parity.md`](../parity/g7-detail-window-parity.md).

**D10b — the boundary, stated as a rule rather than a list.** The existing D9c
split ("the shell assembles; the skin re-buckets nothing") does not decide the
new cases, so:

> **A proportion of two figures the skin was already handed is presentation.** A
> bar's fill width, a segment's share text, and a per-week intensity ramp are the
> same computation as drawing the bar at all, and refusing it would mean Core
> computing pixel ratios.
>
> **A sum, an average, or a bucketing over vendor-derived rows is Core's.** Day
> boundaries, daily totals, window totals, per-kind totals, per-model
> aggregation, and reset-boundary derivation all cross that line.

Applied to the checklist (section letters are OVI-584's):

| Item | Core / shell | Skin |
|---|---|---|
| §C bars | the percent and its status | fill width, the 50/70 bands, every word |
| §D tiles | the four figures, coverage, unpriced set | labels, "Est.", the flip, tile geometry |
| §D colour | the per-model aggregate, ranked by 31-day tokens | the slot palette, the three-slot cap, the "Other" fold |
| §E kind bars | per-kind tokens and values, and `Total` | the share text, segment order, the view switch |
| §F columns | one output-token figure per local day, status per day | bar heights, the per-week intensity ramp, the blank-column rule |
| §F gridlines | the boundary instants and their three kinds | the fractional position inside a column, amber, dotted, drawn last |
| §G banner | `Divergence`, `ExtraUsage`, `OffPlanUsageAmount`, `LastIngestAt` | all three wordings, the provenance sentence, the link |
| §H fold | nothing | all of it — timings, curve, chevron, grow-upward geometry |
| §I cards | nothing | all of it |

**Colour order is ranked by 31-day tokens, and the ranking is Core's** —
`ModelUsageBreakdown.Rows` arrive ordered by 31-day output tokens descending, so
both skins fold the same models into "Other" without either sorting vendor data.
Which three hues, and that there is never a fourth, is the skin's (the validated
palette in the source's `docs/ui-spec.md` §2). This is the one place the source
repository put a presentation concern in Core — `ModelBreakdown.ColourOrder`
(CONFIRMED, source) — and the rebuild splits it: the **order** is data, the
**colours** are not.

### D11 — Parity is proven by renders and timing tests, or it is not proven — added 2026-10-09 (OVI-585/591, gate G7)

**D11a — the render-proof obligation.** Each skin gains an offscreen render hook
that writes every panel state to PNG **in both themes**, the rebuild's equivalent
of the source's `--popup-samples` / `--tile-samples` / `--menu-samples` /
`--dialog-samples`. **Every parity PR attaches the renders for the states it
touches**, and the reviewer compares them against renders from the source at the
same fixture. A parity PR with no renders is incomplete, the way a PR with no
tests is. Required states: ordinary; partial coverage; no data; off-plan On /
Off / Unknown; boost chip; weekly reset known and unknown; and 1 / 2 / 3 /
5-model and unpriced tiles.

This grows D7's reasoning rather than replacing it: D7 accepted a harness cost as
the price of G4 = A, and renders are that same cost for the half of the surface a
string fixture cannot see.

**D11b — eight new [ADR-0003](0003-paneltext-anti-drift-mechanism.md) fixture
families**, because every item below is a *content fact* both skins must state
identically, and a render proves only one skin at a time:

| Family | Pins |
|---|---|
| `AccountIdentityFixture` | the badge for each known `organizationType`, and verbatim rendering of an unknown one |
| `DailySeriesFixture` | the absent-day rule — blank column vs. recorded-zero bar — and that all 31 date labels render |
| `ResetBoundaryFixture` | the hover wording for each of the three boundary kinds |
| `ModelColourOrderFixture` | slot assignment at 1/2/3/4/5 models, and that a model's slot is identical on all four tiles |
| `TokenKindFixture` | kind names, display order, share wording, and the unpriced-kind case |
| `OffPlanBannerFixture` | the three extra-usage wordings and the provenance clause |
| `CoverageCaptionFixture` | `N of 31 days recorded` — days **with data**, not days with usage |
| `HoverTimingFixture` | that 400 / 3000 / 20 000 ms actually resolve **on each element**, not on the container |

`HoverTimingFixture` is the one that is not a string. The source found this exact
bug by measurement — timings set once on the control silently did not inherit to
the bar segments (CONFIRMED, source `docs/ui-spec.md` §2) — and a render cannot
show it. Its Linux half is waiver candidate **W1** in
[`docs/parity/g7-detail-window-parity.md`](../parity/g7-detail-window-parity.md).

**2026-10-10 built (OVI-621, G7 parity slice P2).** `HoverCard` exists in both
skins' own `Presentation/` — independently implemented (D1) — with the two
shapes this section specifies, `Figure` (number, muted caption beneath, optional
colour swatch) and `Text` (a sentence), each wrapped in a bordered card on the
window's own `WindowThemeColors`/`LinuxWindowThemeColors`, never the toolkit's
default tooltip chrome. `ApplyTiming` is called per element, never on a shared
container, matching the source's own finding; `HoverCardTests` in each skin's
test project proves the delays resolve independently on at least three distinct
elements and do not reach a sibling or a child through a container (the
`HoverTimingFixture` fixture, run against each skin's own timing surface rather
than as a cross-skin content-fact comparison — D11b already names it as "the one
that is not a string"). `DetailWindowRenderProof` (P0) gained
`RenderHoverCardsToFile`, rendering both shapes stacked with no live pointer and
no `ToolTip` parent (a `ToolTip`/Avalonia tip cannot be given one), proven by
`O-view.CrossSkin.Tests` in both themes for both skins.

**Windows achieves all three WPF timings** (`InitialShowDelay`/
`BetweenShowDelay`/`ShowDuration` — 400/3000/20 000 ms). **Linux achieves two of
three.** Reflecting `Avalonia.Controls.dll` 12.1.3 found `ToolTip.ShowDelay` and
`ToolTip.BetweenShowDelay` (both applied), and confirmed **no `ShowDuration`
equivalent exists anywhere in the assembly** — resolving waiver candidate W1's
"INFERRED unverified" to CONFIRMED (ADR-0002's 2026-10-10 amendment carries the
new row). This slice does not build a custom popup/timer reimplementation to
fake the missing cap; W1 itself is still an open card awaiting the board. No
`UsageDetail`/panel-content state changed, and this slice does not wire any
detail-window section (B-H) to use `HoverCard` — that is each of P8-P19's own
obligation, per this table's boundary note on slice P2.

**2026-10-10 built (OVI-622, G7 parity slice P3).** Both skins' `DetailWindow`
widens `DefaultWidth` from 320 to 400 px, matching the source's own `ui-spec.md`
§2 ("Roughly 400 px wide") — the 281 px figure is a text-row truncation budget
*inside* the window, not the window's own width (Adrian's correction, OVI-585).
`WindowThemeColors` (`O-view.Tray`) and `LinuxWindowThemeColors` (`O-view.Linux`)
each gain `Accent` (`#BE4E29`) and `AccentHover` (`#B84A27`) — the source's own
measured values (`ui-spec.md` §5), identical in both themes because the
measurement already clears both panels; D1 still applies — the two skins'
records were widened independently and only happen to agree on these bytes
because both are transcribing the same source measurement. Theme
re-read-on-open was already in place from ADR-0009 slices 7/10
(`IThemeSource.Current` re-reads on every access per D6 point 4;
`ThemeRepaintController`/`LinuxThemeRepaintController` repaint on construction
and on every live `Changed` event); this slice adds no new repaint mechanism,
only the two new colours that mechanism now carries.
`WindowThemePaletteTests`/`LinuxWindowThemePaletteTests` add a WCAG 2.1
contrast-ratio assertion: `Accent` and `AccentHover` both clear 4.5:1 against a
white label under both the light and dark theme resolution — the slicing
table's "contrast assertion on the accent against both surfaces" obligation.
`Accent`/`AccentHover` do not vary by theme today, so both checks currently
pass against the same bytes; the assertion still guards against a future
per-theme value regressing below the floor. This slice does not render the
header, bars, tiles, or
graph (P8+ build those on this themed surface) and wires `Accent` into no
dialog or menu yet — that is P21/P24's own obligation.

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
| 4 | Windows status icon: `NotifyIcon`, `UsageLevel` → icon render, per-monitor DPI v2 re-render, `TaskbarCreated` re-registration, activation → **`ISkinToShell.RequestWidget(true)`** (amended 2026-10-02, OVI-326 — the skin asks, the shell answers with `SetVisible`; it does not call `SetVisible` on itself). **Built 2026-10-03 (OVI-371): `StatusIconGlyphRenderer` (pure `UsageLevel`→BGRA32 pixel buffer, DPI-scaled size) and `StatusIconController` (pure decision logic: render on snapshot/DPI change, reregister on host restart, `RequestWidget(true)` on activation and nothing else) in `src/O-view.Tray/Presentation/`, both unit-tested against fakes/no OS handle. `StatusIconFactory` turns a pixel buffer into a real `Icon` via `Bitmap.LockBits`/`GetHicon`, covered directly (`StatusIconFactoryTests` — GDI bitmap/icon creation needs no interactive desktop, so this runs for real on CI). `TrayStatusIcon` is the untested-by-xUnit adapter: a `NotifyIcon` plus a hidden `NativeWindow` receiving `WM_DPICHANGED` and the registered `TaskbarCreated` message, wired in `Program.cs` against a `PendingSkinToShell` placeholder (no shell composition root exists yet — only `RequestWidget` is reachable; every other `ISkinToShell` member throws). `O-view.Tray.csproj` gained `UseWindowsForms=true` (`NotifyIcon`/`Icon`/`NativeWindow` all live in `System.Windows.Forms`), which makes this a board merge despite the slicing table's "agent" note below — flagged in the PR. Also fixes a pre-existing build break: slice 5b (OVI-364) added `IShellToSkin.ShowDetail` but never implemented it on `TrayShellToSkin`, so `main` did not compile from that merge until this PR. `dotnet test O-view.slnx` passes in full: 533 tests. Not verified: the running executable did not crash over an 8-second manual run in this environment, but no interactive Windows desktop was available to confirm the icon's on-screen appearance, a real DPI change, or a real Explorer restart — the same "not verified" boundary slice 3 already recorded.** | 3, 5b | Medium — first OS UI integration; verifiable here | agent (corrected to **board** — csproj change, `UseWindowsForms`) |
| 5 | Windows tooltip: wire the existing `O-view.Tray` `TooltipFormatter` to `NotifyIcon.Text`, with the 127-char cap applied in the skin. **Built 2026-10-03 (OVI-376): `TooltipTextController` in `src/O-view.Tray/Presentation/` — pure wiring, no formatting logic of its own — pushes each snapshot through the existing `TooltipFormatter.Format` (unchanged, still owning the 127-char cap via `Cap`) to an injected setter, unit-tested against a fake setter (`TooltipTextControllerTests`). `TrayStatusIcon.OnSnapshotUpdated` now takes the full `UsageSnapshot` instead of just `UsageLevel`, forwarding to both `StatusIconController` (icon) and `TooltipTextController` (`NotifyIcon.Text`); `Program.cs` updated to pass the full snapshot. No `.csproj` change — `NotifyIcon` was already in scope from slice 4's `UseWindowsForms`. `dotnet test O-view.slnx` passes in full: 536 tests. Not verified: no interactive Windows desktop in this environment to confirm the rendered tooltip text or `NotifyIcon.Text`'s practical length limit matches 127 — the same boundary slices 3/4 already recorded.** | 4 | Low — formatter and its tests already exist | agent |
| 5a | **Core (D9a):** `UsageDetail`, `ModelUsageBreakdown`, `ModelUsageRow`, and the ledger-read seam [ADR-0005](0005-data-provider-contract.md) D6c designed and nobody built — `GetStatistics(utcNow)` plus `GetModelBreakdown(utcNow)` over `UsageLedgerStore`, aggregated per model, with D1's four obligations (never throws, injected clock, read-only, no display text). Pure Core, no UI. **Built 2026-10-02 (OVI-332) as `IUsageStatisticsSource`/`LedgerUsageStatisticsSource` in `src/O-view.Core/Statistics/` — see [ADR-0005](0005-data-provider-contract.md) D6c's "as built" amendment for the `TimeZoneInfo` parameter both queries needed. No rate table exists yet, so `EstimatedUsd` is `Unavailable` everywhere this seam produces one** | — | Low–medium — arithmetic and SQL over an existing store, fully testable on both runners; the per-model aggregation is the part to test hardest | **board** (Core contract — now covered by this merged ADR, but still a Core change) |
| 5b | **Shell (D9b/D9c):** `IShellToSkin.ShowDetail(UsageDetail)`, `ISkinToShell.RequestWidget(bool)`, and the shell-side rule — assemble detail on widget-show and on each successful poll while visible, never while hidden; `UsageDetail.Unavailable` when the ledger read fails. No UI. **Built 2026-10-03 (OVI-364) as the two interface members plus `DetailPushCoordinator` in `src/O-view.App/` — a standalone collaborator (no composition root exists yet to wire it to `UsagePollLoop`/`ISkinToShell` for real; that wiring is left to whichever slice first needs a running process)** | 5a | Low–medium — shell wiring with a visibility rule worth its own tests (hidden ⇒ zero ledger reads) | **board** (seam change) |
| 6 | Windows detail window: draggable widget, content from the **last `UsageDetail`** via the existing formatters plus a per-model section, skin preference file for last position, first-run corner from slice 2, outside-click dismiss → `RequestWidget(false)`. Renders no figure the pushed detail did not carry and totals nothing itself (D9c). **Built 2026-10-04 (OVI-386): `DetailWindow` in `src/O-view.Tray/` — a borderless, draggable WPF `Window` written in plain C# (no `.xaml`; the project had none yet and a dozen bound `TextBlock`s did not need a markup compiler). `DetailWindowContentBuilder` (pure, `src/O-view.Tray/Presentation/`) renders the pushed `UsageDetail` through the existing `PanelTextFormatter`/`UsageFormatter`/`PanelStatisticsFormatter` plus a new per-model section built from `ModelUsageBreakdown`/`ModelUsageRow` — it sums nothing Core did not already sum, and distinguishes "ledger unreadable" from "read, no activity" from "subtotal, a model is unpriced" (D9a/D9c). `DetailWindowPositionController` (pure) decides first-run corner (slice 2's `DetailWindowPlacement.Compute`) vs. restoring the last dragged position; `DetailWindowPreferenceStore` persists that position to its own file (`%LOCALAPPDATA%\O-view\Tray\detail-window.json`) — a skin preference, not `ShellSettingsStore` (ADR-0007 D4) and not a Core store — following the move-aside-if-corrupt convention (OVI-236/238) rather than overwriting. `TrayShellToSkin` gained `DetailShown`/`VisibilityChanged` events (additive, same widening-by-event shape `TraySkinHost.Compose` already used for `UsagePollLoop.SnapshotUpdated`) so the composition root can drive the real window off the same calls it already recorded; every existing recorder and test is unchanged. `Program.cs` is the first slice to build a real `DetailPushCoordinator` over a real `LedgerUsageStatisticsSource` (via `StoreLifetime.CreateDefault()`) and wire `ISkinToShell.RequestWidget` to it — `PendingSkinToShell` now forwards that one member for real; every other member still throws, unchanged. Outside-click dismiss is the window's own WPF `Deactivated` event calling `RequestWidget(false)`. 24 new tests (`DetailWindowContentBuilderTests`, `DetailWindowPositionControllerTests`, `DetailWindowPreferenceStoreTests`, plus three new `TrayShellToSkinTests` cases) prove the placement-first-show-vs-restore rule, the content rules, and the preference store's corrupt-file handling against fakes/temp directories — no window is constructed in any test. `dotnet test O-view.slnx` passes in full: 559 tests. **Not verified:** no interactive Windows desktop in this environment to confirm the window actually renders, drags, or dismisses on outside-click, or that `LOCALAPPDATA` resolves and the preference file round-trips on a real machine — the same "not verified" boundary slices 3/4/5 already recorded for their own OS adapters. No csproj change was needed (`UseWPF`/`UseWindowsForms` already on from slices 3/4), so this is not a board merge on that ground.** | 2, 3, 1, **5b** | Medium–high — largest Windows slice; split per `## section` of the window if it exceeds ~500 lines | agent, or board if split is declined |
| 7 | Windows alerts: `RaiseEvent` → toast, once per raised event, no skin-side threshold logic. **Built 2026-10-04 (OVI-391): `AlertToastFormatter` (pure `UsageEvent` → title/body text, one case per `UsageEventKind`) and `AlertToastController` (pure wiring: one `showToast` call per `OnEventRaised` call, no dedupe/threshold logic of its own) in `src/O-view.Tray/Presentation/`, both unit-tested against fakes. `TrayShellToSkin` gained an `EventRaised` event, raised from `RaiseEvent` alongside its existing `LastEvent` recording — the same widening-by-event shape slice 6's `DetailShown`/`VisibilityChanged` already used. `TrayStatusIcon.ShowToast` is the real adapter (`NotifyIcon.ShowBalloonTip`, already in scope from slice 4's `UseWindowsForms` — no `.csproj` change). `Program.cs` wires `skin.EventRaised` to a new `AlertToastController` driving `statusIcon.ShowToast`. No shell logic yet calls `RaiseEvent` for real (the event-decision logic — threshold crossing, off-plan entry, dedup — is ADR-0007 D2 point 6, a separate, unbuilt slice), so this wiring has no production caller today, the same "wired, not yet driven" state slice 6's new events are in. `dotnet test O-view.slnx` passes in full: 547 tests. Not verified: no interactive Windows desktop in this environment to confirm a real balloon tip renders or that Windows routes it through Action Center as a toast — the same boundary slices 3-6 already recorded.** | 3, 1 | Low–medium | agent |
| 8 | Linux skin host: Avalonia reference, entry point, `IShellToSkin` implementation rendering nothing, **plus D6's session-bus probe and the absent-host message**, off the UI thread, with watch-for-later-registration. **Built 2026-10-04 (OVI-397): `O-view.Linux.csproj` gained `OutputType=Exe` plus `Avalonia`/`Avalonia.Desktop` 12.1.3 and `Tmds.DBus.Protocol` 0.95.1 package references — this project's first third-party dependencies, why this is a board merge regardless of line count (dependencies, packaging size). `Program.Main` builds a real (read-only, no display text) `JsonlUsageProvider` over `ClaudeDataRoots.CandidateRoots` reading `HOME`, composes it with `SystemClock`/`AppTimer` into a `UsagePollLoop` through the new `LinuxSkinHost.Compose`, and keeps the process alive via `AppBuilder.StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)` — no window, no quit path, same boundary slice 3 drew for `O-view.Tray`. `LinuxShellToSkin` is the no-op `IShellToSkin`, independently implemented from `TrayShellToSkin` (D1): it records the last snapshot/event/detail/visibility/shutdown call and renders nothing. `App` (`src/O-view.Linux/App.cs`) is a bare `Avalonia.Application` with no `.axaml`, no styles, no window. D6's probe is `NotificationHostMonitor` (`src/O-view.Linux/Platform/`) over the mockable `ISessionBusNameWatcher` seam: it probes for `org.kde.StatusNotifierWatcher` off the calling thread via `Task.Run` (point 5 — the dispatcher is never blocked on the D-Bus round trip), reports the observed true/false fact through `ProbeCompleted`, and — only when initially absent — awaits `WaitForOwnerAsync` and raises `HostAppeared` once the name appears later, without a restart (point 4). `DBusSessionBusNameWatcher` is the real implementation, built on `Tmds.DBus.Protocol`'s `DBusConnection.WatchNameOwnerAsync`/`NameOwnerWatcher`. `NotificationHostAdvisoryFormatter` (`src/O-view.Linux/Presentation/`) is this skin's own wording (D1) for what D6 point 3 requires: the absent case states the bus observation and the GNOME AppIndicator/KStatusNotifierItem extension advice, never a guess about the user's desktop; `Program.cs` traces it to `Console.Error` today since no status icon exists yet to show it against (slice 9). `LinuxSkinHostTests`/`LinuxShellToSkinTests` prove the composition seam against fakes, the same shape `TraySkinHostTests`/`TrayShellToSkinTests` used for slice 3. `NotificationHostMonitorTests` proves the probe/watch orchestration against a fake `ISessionBusNameWatcher` — including that the probe genuinely runs off the calling thread, that an initially-present host raises no watch, and that an initially-absent host is picked up later without a restart — the test doubling D6 point 5's own escalation criterion as "testable without a real D-Bus daemon." `NotificationHostAdvisoryFormatterTests` pins the wording rule from point 3. `dotnet test O-view.slnx` passes in full: 562 tests (103 in `O-view.Linux.Tests`, +15 over this slice's starting count of 88). **Not verified:** `DBusSessionBusNameWatcher`'s real connect/probe/watch round trip has never run against an actual session bus — no session bus is reachable from this environment or (per ADR-0004) the Linux CI runner's non-interactive job — so the D-Bus wiring is proven by its own fake-backed orchestration tests only, not by execution against a real bus; nor is any on-screen rendering, since this slice renders none (status icon is slice 9). The Linux CI job builds and tests `O-view.Linux`/`O-view.Linux.Tests` already (ADR-0004 option (a')); no CI change was needed for this slice.** | 3 (as reference) | **High** — first third-party UI dependency and native closure; the probe is the slice's point, not an extra | **board** (dependencies, packaging size) |
| 9 | Linux status icon: live-rendered `RenderTargetBitmap` replaced on the timer; never a themed icon name (a name cannot render a gauge — CONFIRMED in the source's spike). **Built 2026-10-04 (OVI-403): `StatusIconGlyphRenderer` (pure `UsageLevel`→BGRA32 pixel buffer, 24px, no DPI concept — no per-host scale signal exists on this platform the way Windows' per-monitor DPI v2 does) and `StatusIconController` (pure decision logic: track the level from every snapshot, but render only while a notification-area host has been observed present, register-then-render exactly once on `OnHostObserved(true)`/`OnHostAppeared`, `RequestWidget(true)` on activation and nothing else) in `src/O-view.Linux/Presentation/`, both unit-tested against fakes/no toolkit, independently implemented from `O-view.Tray`'s slice-4 pair (D1). `StatusIconFactory` turns a pixel buffer into a real Avalonia `WindowIcon` via an unmanaged-pointer `Bitmap` constructor (`PixelFormat.Bgra8888`/`AlphaFormat.Unpremul`) — unlike Windows' GDI-backed factory, this one is **not** directly unit-tested: `Avalonia.Media.Imaging.Bitmap` requires `IPlatformRenderInterface` to already be registered, which only happens for real once `Program.cs` calls `AppBuilder.UsePlatformDetect`, not in a bare xUnit process, and adding a platform-init dependency (e.g. `Avalonia.Headless`) only to make this testable would be exactly the kind of new dependency this slice was not expected to need. `LinuxStatusIcon` is the untested-by-xUnit adapter (mirroring `TrayStatusIcon`'s own boundary): it owns the real `TrayIcon`, wires `Clicked` to `StatusIconController.OnActivated`, and marshals every call onto `Dispatcher.UIThread` before touching it, since `NotificationHostMonitor`'s events and `AppTimer.Elapsed` both fire off a background thread by design. `App` gained a constructor taking the pre-built `LinuxStatusIcon` and now overrides `OnFrameworkInitializationCompleted` to call `TrayIcon.SetIcons` — the first point `Application.Current` and the render interface are guaranteed to exist; `Program.cs` wires `UsagePollLoop.SnapshotUpdated` to the icon (same shape Windows' `Program.cs` uses) and wires `NotificationHostMonitor.ProbeCompleted`/`HostAppeared` to it alongside the existing `NotificationHostAdvisoryFormatter`/`Console.Error` trace from slice 8, which this slice does not duplicate — the absent case's "why nothing is showing" is already covered there. A new minimal `PendingSkinToShell` (only `RequestWidget` wired, every other `ISkinToShell` member throws) stands in for the shell, the same role `O-view.Tray`'s own placeholder played before its slice 6 built a real composition root. No `.csproj` change: Avalonia/`Avalonia.Controls` (which contains `TrayIcon`) was already referenced from slice 8. `dotnet test O-view.slnx` passes in full: 601 tests (119 in `O-view.Linux.Tests`, +16 over slice 8's 103). **Not verified:** no interactive Linux desktop or display in this environment, so neither a real `org.kde.StatusNotifierWatcher` host, a real rendered icon in any panel, nor the activation gesture reaching `RequestWidget` has been observed on real hardware — the same boundary slices 8/3/4 already recorded for their own OS adapters. The built executable was run for 8 seconds in this (Windows) environment without crashing, the same kind of non-crash evidence slice 4 documented for its own manual run; it is not evidence the tray icon renders on Linux.** | 8, 4 | **High** — unverifiable here; ships labelled unverified | agent |
| 10 | Linux detail window + placement, logging requested *and* granted position (D4). Same `UsageDetail`-only rule as slice 6. **Built 2026-10-04 (OVI-408): `DetailWindowContentBuilder` (`src/O-view.Linux/Presentation/`) mirrors `O-view.Tray`'s slice-6 content builder in shape only — independently worded per D1/ADR-0003 (this skin's own " (est.)" marker matching `TooltipFormatter`, not the Windows skin's "~"; its own sentence shapes for the session/weekly/today/31-day lines and the per-model section note). `DetailWindowPositionController` mirrors the Windows copy's injected-delegate shape exactly (load last position / compute first-run placement / save), typed against this skin's own `DetailWindowPlacement.Compute` (slice 2). `DetailWindowPreferenceStore` (`src/O-view.Linux/`) is the Linux counterpart of the Windows preference store — same move-aside-if-corrupt, atomic-write contract (OVI-236/238), its own file (`detail-window-position.json`) under its own directory (`$XDG_CONFIG_HOME/o-view`, falling back to `~/.config/o-view`, the same XDG resolution shape `XdgAutostartRegistration.DefaultDirectory` already uses), never the Windows path and never `ShellSettingsStore` (D4). `DetailWindow` (`src/O-view.Linux/DetailWindow.cs`) is the real Avalonia `Window`: borderless (`WindowDecorations.None`), manually pointer-dragged (not `BeginMoveDrag`, which hands the whole gesture to the window manager and never reports where it ended — `DetailWindowPositionController.OnDragEnd` needs that final position to persist it), dismissed on `Deactivated` (focus loss) via `ISkinToShell.RequestWidget(false)`. `SetVisible` logs the requested position and, once shown, the position actually granted (D4 — the one requirement slice 6 did not need, since a WPF window's `Left`/`Top` are not independently negotiated by a window manager the way X11/Wayland treat `Window.Position`). Wired into `LinuxShellToSkin` (gained `DetailShown`/`VisibilityChanged` events, the same additive-event widening slice 6 used for `TrayShellToSkin`) and `Program.cs`/`App.cs`: unlike `LinuxStatusIcon`'s `TrayIcon`, an Avalonia `Window` needs the platform `AppBuilder` sets up and a working `Window.Screens` for the first-run corner, neither of which exists before `StartWithClassicDesktopLifetime` runs — so the window, its position controller, and the `DetailWindowPlacement.Compute` closure are all built inside `App.OnFrameworkInitializationCompleted`, not `Program.Main` (an asymmetry from the Windows composition root, justified by this platform's init timing, not a layering violation). `PendingSkinToShell.RequestWidget` now forwards to a real `DetailPushCoordinator` over a real `LedgerUsageStatisticsSource` (via `StoreLifetime`), the same wiring slice 6 built for Windows; every other `ISkinToShell` member still throws. No `.csproj` change: Avalonia was already referenced from slice 8. `dotnet test O-view.slnx` passes in full: 627 tests (145 in `O-view.Linux.Tests`, +26 over slice 9's 119) — `DetailWindowContentBuilder`/`DetailWindowPositionController`/`DetailWindowPreferenceStore` are unit-tested against fakes/temp directories, the same boundary slice 6 drew; `DetailWindow` itself is not unit-tested, matching `LinuxStatusIcon`'s own precedent (no toolkit construction in tests without a registered platform render interface). **Not verified:** no interactive Linux display in this environment, so neither the window rendering, the drag gesture, the window manager's actual handling of a position request, nor the granted-position log line it produces has been observed on real hardware — the same boundary slices 8/9 already recorded for their own OS adapters. Nothing deferred from this slice's scope.** | 8, 6, 2, 5b | **High** — the panel has never been observed rendering on real hardware in any form | agent |
| 11 | Linux tooltip + freedesktop notifications. **Built 2026-10-04 (OVI-417): `TooltipTextController` (`src/O-view.Linux/Presentation/`) mirrors `O-view.Tray`'s slice-5 wiring in shape only — independently implemented per D1: pure wiring, no formatting logic of its own, pushing each snapshot through this skin's own (unchanged) `TooltipFormatter.Format` to an injected setter. `LinuxStatusIcon.OnSnapshotUpdated` now also drives it, setting Avalonia's `TrayIcon.ToolTipText` inside the same UI-thread dispatch that already updates the icon, so the icon and tooltip never disagree about which snapshot they rendered; this skin applies no length cap of its own, since the Windows 127-char `NotifyIcon.Text` cap is a Windows API fact that must not travel here (ADR-0001/ADR-0002). `AlertNotificationFormatter`/`AlertNotificationController` (`src/O-view.Linux/Presentation/`) mirror `O-view.Tray`'s slice-7 toast pair in shape only — independently composed wording per D1/ADR-0003, and the same "no dedupe/threshold logic here" boundary (ADR-0007 D2 point 6): the controller sends exactly one notification per `OnEventRaised` call via an injected `Func<string, string, Task>`, discarding (not awaiting) the result, the same fire-and-forget shape `Program.cs` already used for `NotificationHostMonitor.StartAsync`. `DBusNotificationSender` (`src/O-view.Linux/Platform/`) is the real adapter: a direct `org.freedesktop.Notifications.Notify` call (signature `susssasa{sv}i`) over the existing `Tmds.DBus.Protocol` low-level message API — the same "ask the bus directly, build the message by hand" shape slice 8's `DBusSessionBusNameWatcher` already established; no new package reference. `LinuxShellToSkin` gained an `EventRaised` event, raised from `RaiseEvent` alongside its existing `LastEvent` recording — the same widening-by-event shape slice 8/10 already used for `DetailShown`/`VisibilityChanged`. `Program.cs` wires `skin.EventRaised` to the new `AlertNotificationController` driving `DBusNotificationSender.SendAsync`; no shell logic yet calls `RaiseEvent` for real (that decision logic is a separate, unbuilt slice), the same "wired, not yet driven" state slice 7 documented for its own toast wiring. 16 new tests (`TooltipTextControllerTests`, `AlertNotificationFormatterTests`, `AlertNotificationControllerTests`, plus two new `LinuxShellToSkinTests` cases) prove the tooltip wiring against a fake setter and the notification dispatch against a fake `Func<string,string,Task>` seam — no `TrayIcon`, no D-Bus connection, matching slice 9/10's own no-toolkit-construction precedent. `dotnet test O-view.slnx` passes in full: 643 tests (161 in `O-view.Linux.Tests`, +16 over slice 10's 145). **Not verified:** no interactive Linux desktop or session bus is reachable in this environment, so neither the rendered `TrayIcon` tooltip text nor a real `Notify` round trip (let alone any desktop environment's notification daemon actually displaying it) has been observed on real hardware — the same boundary slices 8/9/10 already recorded for their own OS adapters. This is the last unbuilt row in this table; ADR-0002's Linux rows stay unchanged by this slice, per this ADR's closing note.** | 8, 5, 7 | **High** — both **never observed** on real hardware | agent |

Slices 1–2 and 5a–5b need no board answer beyond this ADR, though 5a and 5b
change Core and the seam and are board *merges*. Slices 3 and 8 change project
files or dependencies and are board merges by this project's rule. Slices 9–11
should each be expected to ship with their ADR-0002 rows **unchanged**, and the
PR body should say so in "Risk / not verified" rather than implying the code
working means the platform works.
