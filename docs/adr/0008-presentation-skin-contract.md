# ADR-0008: The presentation contract — four surfaces, two native skins, no shared UI layer

- **Status:** Proposed — awaiting explicit board sign-off (G1-style) on OVI-318.
  Not in force until that sign-off lands. No build task may be scoped from the
  slicing table before then.
- **Date:** 2026-10-02
- **Deciders:** Adrian II the Architect, pending board sign-off
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
| **Detail window** | Be a draggable widget launched from the icon (D5); show the per-model split, the statistics, the data-source provenance, and the explanation when there is no data; remember its own last position as a skin preference | Read Core storage or a provider directly; hold a number the snapshot did not give it |
| **Alerts** | Present `RaiseEvent` and nothing else; at most one notification per raised event | Decide *that* an alert is due. Threshold crossing, off-plan entry, update availability and input degradation are all shell decisions ([ADR-0007](0007-app-shell-contract.md) D2 point 6) |

### D3 — A skin renders the last snapshot and nothing else, and never fabricates a figure

Every one of the four surfaces draws from the most recent
`IShellToSkin.ShowSnapshot(UsageSnapshot)` and from `RaiseEvent(UsageEvent)`.
That is the entire data path into a skin
([ADR-0007](0007-app-shell-contract.md) D6). No skin constructs a provider,
touches a store, reads a file under the user's Claude directory, or makes a
network call.

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
- Everything new in this phase is new code. No Phase 2 contract changes, no Core
  change, no shell change — the seams ADR-0007 defined are used as-is, which is
  also the first real test of whether they were drawn correctly.
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
"Alternatives considered". **No slice may start before this ADR is signed off.**

| # | Slice | Depends on | Risk | Merge |
|---|---|---|---|---|
| 1 | Content-fact fixtures for the two new surfaces — detail-window content set and alert content set — in `tests/O-view.CrossSkin.Tests/`, in ADR-0003's existing fixture shape. Pure, no toolkit, no UI, runs on both runners | — | **Lowest** — tests only; defines what both skins must say before either says it. Start here | agent |
| 2 | The placement rule as a pure function of injected screen geometry, implemented separately in each skin, plus the cross-skin test that they agree (D5). Still no window | 1 | Lowest — arithmetic over injected rectangles | agent |
| 3 | Windows skin host: `UseWPF`, an entry point that composes shell + skin per ADR-0007, an `IShellToSkin` implementation that receives snapshots and events and renders nothing yet. Proves the seam end to end | — | Low–medium — first executable in this repo; `.csproj` change | **board** (csproj) |
| 4 | Windows status icon: `NotifyIcon`, `UsageLevel` → icon render, per-monitor DPI v2 re-render, `TaskbarCreated` re-registration, activation → `SetVisible(true)` | 3 | Medium — first OS UI integration; verifiable here | agent |
| 5 | Windows tooltip: wire the existing `O-view.Tray` `TooltipFormatter` to `NotifyIcon.Text`, with the 127-char cap applied in the skin | 4 | Low — formatter and its tests already exist | agent |
| 6 | Windows detail window: draggable widget, content from the snapshot via the existing formatters, skin preference file for last position, first-run corner from slice 2, outside-click dismiss | 2, 3, 1 | Medium–high — largest Windows slice; split per `## section` of the window if it exceeds ~500 lines | agent, or board if split is declined |
| 7 | Windows alerts: `RaiseEvent` → toast, once per raised event, no skin-side threshold logic | 3, 1 | Low–medium | agent |
| 8 | Linux skin host: Avalonia reference, entry point, `IShellToSkin` implementation rendering nothing, **plus D6's session-bus probe and the absent-host message**, off the UI thread, with watch-for-later-registration | 3 (as reference) | **High** — first third-party UI dependency and native closure; the probe is the slice's point, not an extra | **board** (dependencies, packaging size) |
| 9 | Linux status icon: live-rendered `RenderTargetBitmap` replaced on the timer; never a themed icon name (a name cannot render a gauge — CONFIRMED in the source's spike) | 8, 4 | **High** — unverifiable here; ships labelled unverified | agent |
| 10 | Linux detail window + placement, logging requested *and* granted position (D4) | 8, 6, 2 | **High** — the panel has never been observed rendering on real hardware in any form | agent |
| 11 | Linux tooltip + freedesktop notifications | 8, 5, 7 | **High** — both **never observed** on real hardware | agent |

Slices 1–2 need no board answer beyond this ADR. Slices 3 and 8 change project
files or dependencies and are board merges by this project's rule. Slices 9–11
should each be expected to ship with their ADR-0002 rows **unchanged**, and the
PR body should say so in "Risk / not verified" rather than implying the code
working means the platform works.
