# ADR-0007: The app shell — a third layer between Core and the skins, and what it may not contain

- **Status:** **Accepted 2026-09-28**, with no open question of its own. The
  board signed off on this addendum as the basis for cutting Phase 2 build
  tasks and left gate G4 closed, as this record asked. Acceptance authorises
  no individual slice; each is its own PR and review. Answers one question
  [ADR-0001](0001-core-to-skin-data-contract.md) explicitly deferred to
  "the porting slice"; carries one gate flag (G4, closed).
- **Date:** 2026-09-27
- **Deciders:** proposed by Adrian II the Architect; accepted by the Oview board 2026-09-28
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`), §3.2 and
  §5.4 — and closes ADR-0001's 2026-09-25 open point, "where the rebuild
  puts the HTTP fetch is for the porting slice to decide"
- **Companion records:** [ADR-0005](0005-data-provider-contract.md),
  [ADR-0006](0006-local-storage-contract.md)
- **Evidence standard:** **CONFIRMED** = read directly against
  `mlengmark/O-view` at `897777b`. **INFERRED** = reasoned, not verified.

## Context

The PDR draws two boxes: Core, and a skin per OS. The source repository
ships **three** projects on that axis, not two —
`O-view.Core`, `O-view.App`, and then `O-view.Tray` / `O-view.Linux`
(CONFIRMED). `O-view.App` does not exist in this repository yet
(CONFIRMED — ADR-0001's own note, `ls src`).

Two already-merged decisions in this repository have quietly been leaning
on it:

- [ADR-0002](0002-cross-platform-capability-matrix.md)'s 2026-09-25
  amendment (OVI-135) decided the update *check* is shared, not per-skin.
- [ADR-0001](0001-core-to-skin-data-contract.md)'s matching entry decided
  the pure rules stay in BCL-only Core and the fetch "is not duplicated per
  skin" — but explicitly left the fetch's home to a later slice, because
  there was no shared project to name.

So the third layer is not a new idea to be justified from scratch; it is an
existing commitment that has never been given a name, a boundary, or a rule
about what may not go in it. That last part is what this ADR is mostly
about, because the source's own version of this layer is where its cleanest
rule leaks.

### What the source's shared layer actually holds — CONFIRMED at `897777b`

`UsageEngine`'s doc comment describes itself as "the app, minus its face,"
and claims a precise boundary: it owns provider composition, the polling
loop and cadence, the store and reset-log lifecycle, threshold and off-plan
notification *decisions*, settings, and the update-check schedule — and it
does "not draw anything, decide how a notification looks, or touch the
network. It says *what* happened and lets a head decide what that looks
like."

That description is accurate about `UsageEngine`. It is **not** accurate
about the project `UsageEngine` lives in. Also in `src/O-view.App/`
(CONFIRMED by file listing):

| Path | What it is | Problem under this repository's rules |
|---|---|---|
| `Rendering/PanelPalette.cs`, `PanelDensity.cs`, `TokenBarGeometry.cs`, `TrayIconGeometry.cs`, `WorkAreaPlacement.cs` | Colours, spacing, bar geometry, icon geometry, window placement | **Presentation.** [ADR-0001](0001-core-to-skin-data-contract.md) bars display decisions from the shared data path, and [ADR-0003](0003-paneltext-anti-drift-mechanism.md) gives each skin independent ownership of what the user perceives. A shared palette is the pixel equivalent of a shared sentence |
| `Platform/XdgAutostartRegistration.cs`, `Platform/FileLockSingleInstanceGuard.cs` | Concrete Linux-shaped mechanism implementations | Sits in the project **both** skins reference, so the Windows build links a Linux mechanism. [ADR-0002](0002-cross-platform-capability-matrix.md) rejects treating these capabilities as one shared thing — that pattern is what shipped the source's ADR-0009 self-update bug |
| `Updates/ReleaseFeed.cs`, `Pricing/RateCardFeed.cs` | HTTP | Legitimate here (ADR-0002/0001's OVI-135 decision), but it contradicts `UsageEngine`'s "does not touch the network" if the two are read as one layer |

None of this is sloppiness in the source — `XdgAutostartRegistration`'s own
comment makes a real argument (it needs no Linux API, so it "builds and is
tested on both CI platforms"). But a good argument for *testability* is not
an argument for *linkage*, and this repository has to choose which rule
wins.

## Decision

### D1 — Adopt a third layer, `O-view.App`, with one admission rule

Three layers, with one sentence each:

| Layer | Owns | Never contains |
|---|---|---|
| `O-view.Core` | Usage data: providers, storage, computation, status flags | Display text, formats, locale, OS branches, platform limits, HTTP, timers |
| `O-view.App` (the shell) | The running process: composition, the poll loop, lifetime, orchestration, network fetches, diagnostics | Anything the user *perceives* — wording, colour, geometry, layout, placement — and any per-OS mechanism implementation |
| `O-view.Tray` / `O-view.Linux` (skins) | Everything the user sees, and every OS mechanism | Vendor-file parsing, storage, usage computation |

**The admission rule for the shell, stated so review can apply it:** a type
belongs in the shell only if (a) both skins would otherwise write the same
thing, and (b) it produces no value the user perceives, and (c) it needs no
OS-specific API. Failing (b) sends it to the skins; failing (c) sends it to
the skins; failing (a) sends it to whichever single place needs it.

**Rejected: two layers only, with shared concerns duplicated per skin.**
Already rejected twice on this project's own evidence — the update check
(source issue #176, retrying straight back into GitHub's rate limit) and
wording drift (issues #55/#56). Duplication between two skins is the
failure mode this project keeps paying for.

**Rejected: folding the shell into Core.** Core is BCL-only and holds no
timers, HTTP, or process lifetime; ADR-0001's OVI-135 entry makes that a
constraint, not a preference. A poll loop in Core also makes Core untestable
without waiting.

### D2 — The shell owns the process; it never owns a pixel

The shell owns, and is the only place that owns:

1. **Startup and composition** — build the providers
   ([ADR-0005](0005-data-provider-contract.md)), open the store
   ([ADR-0006](0006-local-storage-contract.md) D2: the shell resolves the
   directory and injects it), construct the skin, wire them together.
2. **Single-instance enforcement** — decide *that* there must be one
   instance and act on the answer; the mechanism is the skin's (D5).
3. **Command-line parsing** and the diagnostic entry points.
4. **The poll loop and its cadence** — one clock, one timer, injected
   (`IClock`, `IAppTimer` in the source, CONFIRMED).
5. **The store's and the anchor's lifetime** — open on start, flush and
   dispose on shutdown.
6. **Event decisions, not event presentation** — threshold crossing and
   off-plan state are *detected* in Core, *decided* here (should we raise
   it at all? has it already been raised for this window?), and *presented*
   by the skin.
7. **The update check**, including its fetch and its cooldown — see D3.
8. **The diagnostics bundle**, with redaction.
9. **Graceful degradation** — a failed poll keeps the previous state and
   never crashes the process (CONFIRMED as the source's stated design; a
   monitoring tool that dies on a bad poll is worse than one showing a
   stale number).

It never draws, words, measures, colours, or places anything.

### D3 — The update-check fetch lives in the shell, with one cooldown holder per process

This closes [ADR-0001](0001-core-to-skin-data-contract.md)'s explicitly
deferred question. Its two constraints are both met: the pure rules stay in
BCL-only Core (`RateLimitResponse`, `UpdateCheck`), and the fetch is not
duplicated per skin.

Added, because ADR-0001's 2026-09-26 (OVI-140) entry requires it: **the
cooldown is held once per process, not once per call.** The shell
constructs exactly one feed (or one cooldown holder) at startup and every
skin's check goes through it. The source's Windows head is the named
counter-example — it does `new ReleaseFeed(...)` per check and so discards
the cooldown it just recorded (CONFIRMED) — and must not be copied.

**Note on scope:** the shell fetches the *release feed* only. The rate card
is `Bundled` or `UserFile` per
[ADR-0001](0001-core-to-skin-data-contract.md)'s `RateCardSource` enum —
there is no network rate-card source on the contract, so the source's
`Pricing/RateCardFeed.cs` has no counterpart here unless and until that
enum gains one. Adding a network fetch for pricing would be a contract
change and a new decision, not a porting detail.

### D4 — Three owners of persisted state, and no shadow copies

| Kind | Example | Owner | Where it lives |
|---|---|---|---|
| Unrecomputable vendor-derived facts | usage ledger, weekly-reset anchor | Core | [ADR-0006](0006-local-storage-contract.md) D1 |
| Behaviour settings | alert threshold percent, poll cadence, auto-update opt-in | **Shell** | One settings file in the shell's own directory |
| OS-owned truth | run-at-startup | **The OS** | The registry value / autostart file *is* the state. No copy anywhere else ([ADR-0002](0002-cross-platform-capability-matrix.md)) |
| Perceptual preferences | widget position, per-skin display choices | **Skin** | Each skin's own preference file, never Core's store and never the shell's settings |

**Rejected: one settings file for all of it.** Widget position in a shared
settings file makes one skin's preference a shared schema both skins must
agree on — a slow path back to the duplication D1 exists to prevent, and
[ADR-0002](0002-cross-platform-capability-matrix.md) already names last
position "a skin preference, not Core data."

**Rejected: mirroring run-at-startup into settings** so the menu can render
a checkbox without an OS call. That is the shadow copy ADR-0002 forbids; the
two disagree the first time a user edits the OS side.

### D5 — Per-OS mechanisms: the shell declares the capability, the skin implements it

The shell owns the *interface* for each capability it must drive
(single-instance guard, startup registration, theme source, UI dispatcher,
notification presenter). **Each implementation lives in the skin for that
OS**, selected at compile time by which skin is built — never by a runtime
OS check inside shared code.

This is a deliberate divergence from the source, which keeps
`XdgAutostartRegistration` and `FileLockSingleInstanceGuard` in the shared
project (CONFIRMED). The reasons to diverge:

- [ADR-0002](0002-cross-platform-capability-matrix.md) decides these
  capabilities are "one capability, many implementations — never a single
  shared API pretending the platforms behave the same," and records the
  already-paid cost of getting it wrong (source ADR-0009's amendment).
- A Windows build that links a Linux mechanism invites a runtime selector,
  and a runtime selector is the exact shape of that bug.

**The source's counter-argument is real and is kept, narrowly.** Its
comment's point — this code needs no Linux API, so it is testable on either
runner — is worth preserving. So: **a mechanism's pure rules may be
extracted into shared code as functions over injected inputs** (the
`.desktop` file's contents given a directory; the lock protocol given a
path), on two conditions: the function is named for the mechanism it
serves, not for a neutral abstraction over both platforms; and the
capability is still *registered and invoked* from the skin. That is the same
pattern [ADR-0005](0005-data-provider-contract.md) D5 carries forward from
`ClaudeDataRoots` — pure functions of an injected root, one member that
asks about the OS — and it gets the testability without the linkage.

### D6 — The shell-to-skin seam, in both directions

Two narrow interfaces, replacing "the shell knows about a window":

**Shell → skin** (the skin implements; the shell calls):

| Member | Carries | Note |
|---|---|---|
| `ShowSnapshot(UsageSnapshot)` | The full contract snapshot ([ADR-0001](0001-core-to-skin-data-contract.md)) | Called on every successful poll. The skin decides what, if anything, changes on screen |
| `RaiseEvent(UsageEvent)` | An enum-plus-data event: threshold crossed, off-plan entered, update available, input degraded | Never a sentence, never a title, never a severity colour |
| `SetVisible(bool)` | The widget's requested visibility | Lifecycle only — the shell says "the user asked for the widget," not where or how big |
| `Shutdown()` | — | Skin tears down its OS integration; shell then disposes Core |

**Skin → shell** (the shell implements; the skin calls): `RefreshNow()`,
`SetThresholdPercent(int)`, `SetAutoUpdate(bool)`, `WriteDiagnosticsBundle()`,
`Quit()`.

**Rejected: handing the skin a reference to Core, or to the store.** PDR
§3.2 is explicit that a skin talks to Core only through the contract and
never into storage. One snapshot per poll is the whole data path.

**Rejected: the skin polling the shell.** Two schedules, one cadence to keep
in agreement; the source's single engine-driven loop is the better shape and
is already proven in a shipping app (CONFIRMED by OVI-4's live run, 60-second
cadence observed).

## Gate flag — G4 stays closed, and this ADR is drawn so it can

**Confirmed 2026-09-28:** the board approved this addendum and did not open
G4. This section is now a standing constraint on the shell slices, not a
question.

**What this ADR does:** the shell owns window *lifecycle events* — "show
the widget," "hide it," "shut down." Each skin keeps its own window, its own
toolkit (WPF / Avalonia), its own layout and its own draggable-widget
implementation (PDR §5.3).

**What this ADR deliberately does not do:** put any shared window base
class, shared view model, shared layout, shared geometry, or shared palette
in the shell. D1's admission rule (b) and the `Rendering/` row in the
Context table exist precisely to keep that out, because **a shared widget
implementation is UI unification, and UI unification is gate G4, which is
open.**

**Board flag, not a request:** if the board *wants* the shell to own a
shared widget — and there is a real argument for it, since PDR §5.3 makes
the widget's behaviour identical on both platforms by design — then G4 must
be opened and decided first. This ADR does not ask for that and recommends
against it for now, consistent with PDR §5.2. It flags it because a reviewer
could otherwise read D6's `SetVisible` as a step toward it. It is not one.

## Alternatives considered

**Name the layer something other than a "shell."** Considered: "host,"
"engine," "app." Kept `O-view.App` as the project name for continuity with
the source (a reader moving between repositories should not have to
translate), and use "shell" in prose for the role. The source's own
`UsageEngine` remains a good name for the type inside it.

**Let the shell also own presentation-neutral geometry** (bar widths, icon
geometry), on the argument that geometry is maths, not opinion. Rejected:
`WorkAreaPlacement` and `TrayIconGeometry` decide where a user's window
lands and how an icon reads at 100% and 200% scaling, which is exactly the
kind of thing this project has already decided each skin owns
([ADR-0002](0002-cross-platform-capability-matrix.md)'s positioning row,
[ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s wording principle
generalized). If the two skins later converge on identical geometry,
[ADR-0003](0003-paneltext-anti-drift-mechanism.md)'s golden-master pattern
is the way to hold them together — not a shared implementation.

**Have the shell format nothing but pass through a "presentation hint"** —
a severity, a suggested icon state. Rejected: a severity *is* a
presentation decision wearing an enum's clothes. `UsageLevel` already
exists on the contract and is derived in Core from thresholds; a second,
shell-level hint would be a competing opinion about the same figure.

## Consequences

**Positive:**
- The one question ADR-0001 left open (where the HTTP fetch goes) now has a
  written answer, so the update-check porting slice can be scoped.
- D1's admission rule is short enough to apply in review, which matters
  more than its elegance: the source's shared layer drifted precisely
  because no such rule was written down.
- Both skins get one poll cadence, one store lifetime, and one cooldown, by
  construction rather than by vigilance.

**Negative:**
- **This is the largest new surface in Phase 2 and it is entirely
  unimplemented.** No `O-view.App` exists here; every claim about how the
  shell behaves is **INFERRED** until slices land.
- D5 costs duplication the source avoided: each skin implements its own
  startup registration and single-instance guard, and the pure-rule
  extraction is an extra seam. Accepted, because the alternative is the
  linkage pattern ADR-0002 rejects on evidence.
- D6's snapshot-per-poll push means a skin that wants to re-render
  mid-interval must ask (`RefreshNow`) rather than read. That is the
  intended constraint, but it will feel restrictive the first time a skin
  needs it.
- Nothing in this ADR is verified on Linux, and the Linux skin has **never
  been observed rendering on real hardware in any form**
  ([ADR-0002](0002-cross-platform-capability-matrix.md)). The shell's seam
  will meet its first real test there.

## Slicing guidance for decomposition

| # | Slice | Depends on | Risk |
|---|---|---|---|
| 1 | `O-view.App` project + `IClock`/`IAppTimer` + the layering structural test (shell references Core, never a skin; Core references neither) — **landed** (PR #50, 2026-09-30, OVI-261) | — | **Lowest** — a project, two interfaces, one test. Start here |
| 2 | The poll loop: composition + cadence + failed-poll-keeps-previous-state, against a fake provider — **landed** (PR #51, 2026-10-01, OVI-268) | 1, ADR-0005 slice 1 | Low — no real I/O |
| 3 | D6's two seams, with one skin wired to a fake shell and vice versa — **landed** (PR #52, 2026-10-01, OVI-273) | 1 | Low |
| 4 | Store lifetime + injected directory (ADR-0006 D2) — **landed** (PR #53, 2026-10-01, OVI-277) | 1, ADR-0006 slice 1 | Low |
| 5 | Shell settings file (D4, behaviour settings only) | 1 | Low |
| 6 | Single-instance + startup registration per D5, one skin at a time | 1, 3 | Medium — first real OS mechanism; Windows first, since Linux is unverifiable here |
| 7 | Update-check fetch + one-cooldown-per-process (D3) | 1, 2 | Medium — first HTTP in this repository |
| 8 | Diagnostics bundle + redaction | 1, 4 | Low |

Slices 1–5 need no board answer beyond this ADR. Slice 6 is where
[ADR-0002](0002-cross-platform-capability-matrix.md)'s Linux
"never observed" rows start to bite, and its Linux half should be expected
to ship unverified and labelled as such.

- **2026-09-30 update — slice 1 landed (Kit the Builder, OVI-261, PR #50).**
  `src/O-view.App/O-view.App.csproj` adds the third layer as a BCL-only
  `net10.0` project (matching Core's target framework) that references only
  `O-view.Core`. `IClock` (`src/O-view.App/IClock.cs`) and `IAppTimer`
  (`src/O-view.App/IAppTimer.cs`) carry the shape named in D2 point 4 —
  used only as a naming/shape reference against the source repository's
  `O-view.App/IClock.cs`/`IAppTimer.cs` at `897777b`, not copied — with no
  concrete implementation (`SystemClock`, `ITimerFactory`, a dispatcher- or
  Avalonia-backed timer) in this slice; that is explicitly deferred to
  slice 2's poll loop work. `tests/O-view.App.Tests/LayeringStructuralTests.cs`
  encodes D1's admission rule as a durable check: it reads the
  `ProjectReference` items out of `O-view.Core.csproj` and
  `O-view.App.csproj` directly (rather than reflecting on the built
  assemblies) and asserts Core references neither App nor a skin, App
  references Core but never a skin. Reflection on `Assembly
  .GetReferencedAssemblies()` was tried first and rejected: because App
  currently has no code that actually uses a Core type, the compiler elides
  the unused assembly reference from the built DLL even though the
  `ProjectReference` — and the boundary it represents — still stands, which
  would have made the positive "App references Core" assertion flicker
  false as soon as it passed. Verified live: temporarily adding
  `<ProjectReference Include="..\O-view.Tray\O-view.Tray.csproj" />` to
  `O-view.App.csproj` fails restore with `NU1201` (`net10.0` cannot
  reference `net10.0-windows7.0`) before the structural test even runs,
  confirming the target-framework choice is itself a second, independent
  guard on top of the test. `O-view.App` and `O-view.App.Tests` are added
  to `O-view.slnx` and to both CI jobs' explicit project lists in
  `.github/workflows/ci.yml` — the Linux job lists `net10.0` projects by
  path rather than using the solution file, so both needed a line each.
  `dotnet build`/`dotnet test` run clean on the full solution (354 tests
  passed, 0 failed, across all five test projects including the new
  `O-view.App.Tests`) and on the six-project Linux-job subset individually.
  Not verified: an actual Linux run of this CI job (no Linux runner
  available here; CONFIRMED only that the same six projects build
  individually on this Windows machine with `net10.0`, not
  `net10.0-windows`, targets).

- **2026-10-01 update — slice 2 landed (Kit the Builder, OVI-268, PR #51).**
  `IAppTimer` gained an `Elapsed` event, which slice 1 did not ship — a
  repeating timer with no way to be notified of a tick cannot drive a poll
  loop. This is a deliberate, documented extension of slice 1's shape, not a
  design change: `IClock`/`IAppTimer`'s own doc comments now point at this
  slice's implementations. `src/O-view.App/SystemClock.cs` and
  `src/O-view.App/AppTimer.cs` are the first real implementations —
  `SystemClock` wraps `DateTimeOffset.UtcNow`; `AppTimer` wraps
  `System.Threading.Timer`, not a UI-framework dispatcher timer, keeping the
  seam's whole point (no named UI framework) intact. `UsagePollLoop`
  (`src/O-view.App/UsagePollLoop.cs`) is the composition root D2 point 4
  describes: it takes an `IUsageProvider` (ADR-0005), an `IClock`, an
  `IAppTimer`, and a cadence (`TimeSpan`, a constructor parameter — slice 5's
  settings file does not exist yet, so no default is invented here), wires
  the timer's `Elapsed` event to a poll, and exposes `CurrentSnapshot`. A
  poll that returns normally — including `UsageSnapshot.Unavailable`, which
  is a legitimate "no data" answer — always replaces `CurrentSnapshot`; a
  poll that throws is caught and leaves it untouched (D2 point 9). This is
  defense in depth, not reliance on `IUsageProvider`'s own never-throw
  contract (ADR-0005 D1) being violated: the shell's obligation not to crash
  the process does not get to assume every provider upholds it.
  `tests/O-view.App.Tests/UsagePollLoopTests.cs` adds 6 tests against fakes
  only (`FakeUsageProvider`, `FakeClock`, a hand-rolled `FakeAppTimer` that
  raises `Elapsed` on command) — no real timer or network — covering a
  normal tick, the Unavailable-before-first-tick starting state, a throwing
  provider leaving the previous snapshot in place and the loop continuing to
  poll afterward, and `Dispose` stopping/unsubscribing/disposing the timer.
  `dotnet test O-view.slnx` passes in full: 360 tests (up from 354 at slice
  1), including 12 in `O-view.App.Tests` (up from 6). Deferred to later
  slices, per the slicing table: store/anchor lifetime (4), a settings file
  or settings-driven cadence (5), single-instance/startup registration (6),
  the update-check fetch (7), the diagnostics bundle (8), and threshold/
  off-plan event decisions (D2 point 6, out of scope for this ADR's shell
  layer as currently sliced). Not verified: an actual Linux run (no Linux
  runner available here; same CONFIRMED/INFERRED boundary as slice 1).

- **2026-10-01 update — slice 3 landed (Kit the Builder, OVI-273, PR #52).**
  D6's two interfaces, with the exact members the table above names:
  `IShellToSkin` (`src/O-view.App/IShellToSkin.cs`: `ShowSnapshot`,
  `RaiseEvent`, `SetVisible`, `Shutdown`) and `ISkinToShell`
  (`src/O-view.App/ISkinToShell.cs`: `RefreshNow`, `SetThresholdPercent`,
  `SetAutoUpdate`, `WriteDiagnosticsBundle`, `Quit`). `UsageEvent`
  (`src/O-view.App/UsageEvent.cs`) is the enum-plus-data type `RaiseEvent`
  carries: a `UsageEventKind` (`ThresholdCrossed`, `OffPlanEntered`,
  `UpdateAvailable`, `InputDegraded`) plus nullable payload properties drawn
  only from types ADR-0001/ADR-0005 already define — `UsageLevel` for a
  threshold crossing, `DivergenceReading` for off-plan entry, `ProviderHealth`
  for input degradation. `UpdateAvailable` carries no payload in this slice:
  ADR-0001 names an `UpdateCheckOutcome` enum in its contract table, but no
  Core type implements it yet (D3's update-check fetch is slice 7, not built),
  and this slice's boundaries forbid Core changes — adding that enum here
  would be inventing a Core contract field mid-slice, which is exactly the
  case this ADR's authoring agent is told to escalate rather than resolve
  unilaterally. The kind constant exists so the seam is complete; the
  producer that fills in update-check data is deferred to slice 7. No
  threshold/off-plan/update/health *detection* logic ships here either (D2
  point 6 stays deferred, as slice 2's note already recorded) — this slice is
  the seam's shape and wiring, not the decision logic that calls it.
  `tests/O-view.App.Tests/ShellToSkinSeamTests.cs` and
  `tests/O-view.App.Tests/SkinToShellSeamTests.cs` are structural/contract
  tests against fakes, not a real UI integration: each has a driving harness
  typed to the interface (never the concrete fake) that calls every member in
  turn, and a fake implementation (`FakeSkin`, `FakeShell`) that records what
  it received, asserting the exact snapshot/event/value crossed the seam.
  `dotnet test O-view.slnx` passes in full: 369 tests (up from 360 at slice
  2), including 21 in `O-view.App.Tests` (up from 12, +9: 4 shell-to-skin, 5
  skin-to-shell). Deferred to later slices, per the slicing table: store/
  anchor lifetime (4), a settings file or settings-driven cadence (5),
  single-instance/startup registration (6), the update-check fetch and its
  `UsageEvent.UpdateAvailable` payload (7), the diagnostics bundle (8), and
  all threshold/off-plan/update/health *event-decision* logic (D2 point 6).
  No real skin (WPF/Avalonia) wiring and no shared widget/window base class
  (G4 stays closed) — both explicitly out of scope per this slice's
  boundaries. Not verified: an actual Linux run (no Linux runner available
  here; same CONFIRMED/INFERRED boundary as slices 1 and 2).

- **2026-10-01 update — slice 4 landed (Kit the Builder, OVI-277, PR #53).**
  `src/O-view.App/StoreDirectoryResolver.cs` resolves ADR-0006 D2's documented
  default directory once at startup: `%LOCALAPPDATA%\O-view\` on Windows
  (CONFIRMED), `$XDG_DATA_HOME/O-view/` falling back to
  `~/.local/share/O-view/` on Linux (**INFERRED**, unverified on real
  hardware, same boundary as ADR-0006 D2 itself). Each branch is a separate
  internal function over an injected `Func<string, string?>` environment-
  variable lookup — the pure-rule extraction D5 describes for OS mechanisms —
  so `StoreDirectoryResolverTests` exercises both branches on whichever OS
  the test runner happens to be, rather than only the one matching the host.
  No `Environment.SpecialFolder` call and no OS branch of any kind exists in
  `O-view.Core`; this is the one place in `O-view.App` that reads an
  environment variable or asks `OperatingSystem.IsWindows()`/`IsLinux()` for
  this purpose. `src/O-view.App/StoreLifetime.cs` is the shell-side half of
  ADR-0007 D4's one-owner-of-persisted-state rule: its constructor takes a
  directory, creates it if missing, and constructs exactly one
  `WeeklyResetAnchorStore` and one `UsageLedgerStore` against it, exposed as
  properties that return the same instance on every access for the rest of
  the process; `StoreLifetime.CreateDefault()` is the composition-root call
  the shell makes once at startup, chaining `StoreDirectoryResolver
  .ResolveDefault()` into the constructor. Neither
  `WeeklyResetAnchorStore`'s nor `UsageLedgerStore`'s constructor signature
  changed — both still take only a directory, exactly as ADR-0006 slices 1–2
  (PR #40/#44) shipped them. `tests/O-view.App.Tests/StoreDirectoryResolverTests.cs`
  (8 tests: both branches' happy path, both branches' missing-environment-
  variable failure, and a same-OS cross-check against `ResolveDefault()`) and
  `tests/O-view.App.Tests/StoreLifetimeTests.cs` (4 tests, each against its
  own temp directory, never a real user profile: directory creation,
  tolerating a directory that already exists, same-instance-on-every-access
  for both stores, and both stores being immediately queryable) add 12 tests
  with no real user-profile I/O. `dotnet test O-view.slnx` passes in full:
  381 tests (up from 369 at slice 3), including 33 in `O-view.App.Tests` (up
  from 21). `LayeringStructuralTests` passes unmodified — `O-view.App.csproj`
  still references only `O-view.Core`, no new package or project reference
  was added. Deferred to later slices, per the slicing table: a settings
  file or settings-driven cadence (5), single-instance/startup registration
  (6), the update-check fetch (7), the diagnostics bundle (8). Not verified:
  an actual Linux run (no Linux runner available here; same CONFIRMED/
  INFERRED boundary as prior slices) and no real Windows `%LOCALAPPDATA%`
  profile was exercised end-to-end either — `StoreDirectoryResolverTests`
  covers the resolution logic only, and `StoreLifetimeTests` covers store
  construction only, against temp directories in both cases, not the two
  composed together against a real OS-provided path.
