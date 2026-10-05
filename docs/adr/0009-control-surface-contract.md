# ADR-0009: Phase 4A — the control surface: one menu, three settings, and a theme the OS owns

- **Status:** **Accepted** — the board signed this record off by approving and
  merging PR #76 (CONFIRMED: approval card
  `6c233a56-bb67-464c-8b38-3ea847e36703`, tracked on OVI-436, now closed; merged
  by the board account `mlengmark`, 2026-10-05T10:35:06Z, merge commit
  `3172e63`). The slicing table at the foot of this record is therefore open for
  decomposition, the same bar [ADR-0008](0008-presentation-skin-contract.md)
  held to. Living document: amend in place, as
  [ADR-0001](0001-core-to-skin-data-contract.md) does.
- **Date:** 2026-10-05 · **Amended** 2026-10-05 (sign-off recorded, OVI-443)
- **Deciders:** Adrian II the Architect, signed off by the board
- **Scope:** Phase 4A — the right-click menu, the notification-threshold picker,
  the run-at-startup toggle, user-facing settings, and following the OS
  light/dark theme. On Windows and Linux only.
- **Rests on:** the board's Phase 4 scope decision, **option C** (card
  `80339fe7`, OVI-423, accepted 2026-10-05): settings/menu and
  auto-update/packaging proceed as **two separate amendments**, each unblocking
  its own build slices once approved. This record is the first of the two. The
  second — auto-update and packaging — is OVI-433 and is **not** scoped here.
- **Formalizes:** [ADR-0007](0007-app-shell-contract.md) D2 point 6, D4 and D5,
  which named these capabilities and their owners but left every one of them
  unbuilt; and the approved PDR (rev. 2, `oview-pdr-reissued`) §5.4.
- **Evidence standard:** as [ADR-0002](0002-cross-platform-capability-matrix.md).
  Every factual claim about existing behaviour is **CONFIRMED** or **INFERRED**,
  carried forward from its source. **This record upgrades no evidence label in
  ADR-0002** — in particular it does not promote any Linux cell, and theme
  following stays "never observed on real hardware" after it, exactly as
  ADR-0002 line 52 leaves it.

## Context

Phase 3 is fully landed: all eleven slices of
[ADR-0008](0008-presentation-skin-contract.md)'s table are built, last one
PR #75 (CONFIRMED by `git log` on `main` at `653748c`). The four surfaces draw
on both platforms.

Rae II's gap check (OVI-421) named seven things the source app does that
Phases 1–3 do not. Two of them — self-update execution and packaging — are
OVI-433's. The remaining five are this record's: run-at-startup, the threshold
picker, the right-click menu, user-facing settings, and theme following.

**The gap is smaller than the feature list suggests, and in a different place.**
Rae II's check reads the slicing tables; this record also reads the code. What
is actually missing is mostly *wiring of a seam that already exists*, not new
contract. CONFIRMED by reading this repository at `653748c`:

| What the menu needs | What exists today | What is actually missing |
|---|---|---|
| A command for each menu item | `ISkinToShell` already declares `RefreshNow`, `RequestWidget`, `SetThresholdPercent`, `SetAutoUpdate`, `WriteDiagnosticsBundle` and `Quit` — the full set, shipped in PR #52 (OVI-273) | A shell-side implementation. Both composition roots use a local `PendingSkinToShell` in which **every member except `RequestWidget` throws** |
| Persisted settings | `ShellSettings` (threshold percent, poll cadence, auto-update opt-in) and `ShellSettingsStore`, with tests | Both `Program.cs` files pass `ShellSettings.Default.PollCadence` and **never open the store**. Nothing reads or writes a settings file in a running process |
| Run-at-startup | `IStartupRegistration` plus `RegistryStartupRegistration` (Windows) and `XdgAutostartRegistration` (Linux), all with tests, shipped PR #55 (OVI-283) | **Neither implementation is constructed anywhere.** Both are dead code reachable only from their own tests |
| A threshold that does something | `UsageEventKind.ThresholdCrossed`; both skins present a raised event (slices 7 and 11) | The decision that an alert is *due* — [ADR-0007](0007-app-shell-contract.md) D2 point 6 — is unbuilt. Both skins' alert wiring has **no production caller** (CONFIRMED, stated in `Program.cs`'s own summary comment) |
| Diagnostics | `DiagnosticsBundle` / `DiagnosticsBundleWriter`, shipped PR #57 (OVI-292) | No caller |
| A way to quit | — | Nothing. Both processes run `ShutdownMode.OnExplicitShutdown` with no quit path and **exit only by being killed from outside** (CONFIRMED) |
| A menu | — | Nothing on either platform |
| Theme following | — | Nothing. ADR-0007 D5 names "theme source" as a shell-declared capability, but **no such interface exists in code** (CONFIRMED by grep: the only match for "theme" in `src/` is an unrelated comment in `StatusIconGlyphRenderer`) |

So Phase 4A is four jobs, in this order: **give the shell a real
`ISkinToShell`**, **build the one piece of shell logic D2 point 6 still owes**,
**add the menu as a surface on each skin**, and **add the one capability that
has no seam yet, theme**. Only the last is new contract.

## Decision

### D1 — The menu is the fifth surface. It is also the *only* settings surface in Phase 4A; there is no settings window

[ADR-0008](0008-presentation-skin-contract.md) D2 enumerates four surfaces. The
menu is a fifth, with its own row in that table (see "Amendments to earlier
records" below). Every obligation in D2's "must not" column applies to it
unchanged.

Phase 4A ships **no separate settings window.** All three settings and all
three actions live in the menu.

**Why.** Three settings — a percent from a small fixed set, and two booleans —
is a menu's natural size, and it is CONFIRMED to be exactly how the source app
presents them (Rae II, OVI-421: a right-click menu of seven items, no settings
window anywhere in the source). A settings window would be a **sixth** surface,
and a surface is not cheap in this architecture: by D1 of ADR-0008 it must be
implemented twice, independently, and by D7 it pulls the ADR-0003 anti-drift
harness along with it. That is two window implementations, two preference-wiring
paths and a new fixture family, to show three controls that already fit in a
menu the user opens from the same icon.

**What was asked for, and why this record declines it.** OVI-432's brief lists
"settings window" in scope. This record addresses it rather than dropping it:
the judgment is that a window is the wrong shape for Phase 4A's content, and
the cost falls on the part of the architecture (two independent skins) the board
deliberately chose at G4 = A. **This is the one decision in this record that the
board may reasonably want to overturn**, so it is flagged as such in the PR
rather than buried here.

**What would re-open it.** A settings window becomes the right answer when any
of these is true, and a later amendment should add it then rather than now:

- A setting appears that cannot be expressed as a checkbox or a pick-one list —
  free text (a custom data root), a number with a range, a file path.
- The count of settings passes roughly eight, where a menu stops being
  scannable.
- A setting needs explanatory text to be safe to change — a menu item has
  nowhere to put a sentence.

The first of those is plausible in OVI-433's territory (an update channel, a
proxy). If it lands there, the window is OVI-433's cost to carry, not this
record's.

### D2 — Nothing is added to the shell-to-skin seam for the menu. The API it needs already exists

The menu adds **no member** to `ISkinToShell` and **no member** to
`IShellToSkin`. Each of its items maps onto a member shipped in PR #52:

| Menu item | Calls | Reflects state from |
|---|---|---|
| Refresh now | `ISkinToShell.RefreshNow()` | — (an action) |
| Notification threshold → 70 / 80 / 90 % | `ISkinToShell.SetThresholdPercent(int)` | The shell's settings file (D4) |
| Run at startup | the skin's own `IStartupRegistration` (D3) | **The OS, read live** (D3) |
| Check for updates automatically | `ISkinToShell.SetAutoUpdate(bool)` | The shell's settings file (D4) |
| Copy diagnostics | `ISkinToShell.WriteDiagnosticsBundle()` | — (an action) |
| Show usage details | `ISkinToShell.RequestWidget(true)` | — (an action; already wired) |
| Quit | `ISkinToShell.Quit()` | — (an action, D7) |

**This matters for decomposition, not just tidiness.** A seam change is a board
merge by this project's rule; wiring is not. Because the seam does not move,
most of Phase 4A's slices are agent merges, and the board's attention is spent
on the two slices that genuinely introduce something — the theme interface (D6)
and process termination (D7).

**"Check for updates" is deliberately absent from this table as an action.**
The menu's auto-update item is the persisted opt-in only
([ADR-0007](0007-app-shell-contract.md) D4). A *manual* "check now" item needs
the fetch to be reachable and its result actionable, which is OVI-433's. Adding
a menu item here that can only ever report "a newer version exists, and this
app cannot do anything about it" is worse than not having it yet.

### D3 — Run-at-startup is read from the OS every time the menu opens, and the checkbox shows what the OS did, not what the user clicked

Three rules, all of them consequences of principles already decided:

1. **Read, never cache.** The menu's checked state comes from
   `IStartupRegistration.IsEnabled()` called as the menu opens — never from a
   value held in memory since the last open, and never from the shell's settings
   file. [ADR-0007](0007-app-shell-contract.md) D4 rejects mirroring this into
   settings outright, and ADR-0002's startup-registration row names the OS the
   sole source of truth. A user who removed O-view from Task Manager's startup
   page, or deleted the `.desktop` file, must see that reflected the next time
   the menu opens.
2. **Render the result, not the request.** On toggle, the skin calls
   `IStartupRegistration.Apply(enable)` and sets the checkbox from its **return
   value**, which is the state as it actually stands afterwards. `Apply`'s own
   documentation already states why: a registry write or a file write can fail,
   and "reporting the requested state regardless would be a fabricated fact
   about the user's machine." This is the never-fabricate-a-number principle
   applied to a boolean.
3. **A failed toggle must be visible, in that skin's own words.** If `Apply`
   returns a state other than the one requested, the checkbox snaps back and the
   skin says so. The wording is the skin's (ADR-0008 D1) and the fact is the
   same on both: the change did not take effect. Neither skin may silently
   leave the checkbox where the user put it.

**The skin calls `IStartupRegistration` directly; this does not go through
`ISkinToShell`.** ADR-0007 D5's condition is that a per-OS capability "is still
*registered and invoked* from the skin", and the implementation for each OS
already lives in that OS's skin project. Routing it through the shell would
need a new seam member *and* a new return path (`Apply` answers, and every
`IShellToSkin` member is one-way by design), to move a call from the project
that owns the mechanism to the project that does not.

**Rejected: a new `ISkinToShell.SetRunAtStartup(bool)` plus an
`IShellToSkin.ShowStartupState(bool)` push to report the outcome.** It is
symmetrical with the other settings, which is its only attraction. It costs two
seam members (a board merge), widens the one-way push direction with a
query-answer shape it does not otherwise have, and makes the shell a
pass-through to code living in the skin. The asymmetry in D2's table — two
settings through the shell, one straight to the OS — is not an inconsistency; it
is D4's three owners of state showing through, correctly.

### D4 — The shell loads its settings at composition and persists every change. The menu holds no state

Today both composition roots use `ShellSettings.Default` and never open
`ShellSettingsStore` (CONFIRMED). Phase 4A fixes that, with the flow fixed in
one direction:

1. On start, the shell loads `ShellSettings` from its store and composes the
   poll loop with the **loaded** cadence, not the default.
2. A menu change calls `SetThresholdPercent` / `SetAutoUpdate`. The **shell**
   validates, persists, and holds the new value.
3. The menu's rendered state on next open is read back from the shell's
   settings, not from the widget's own memory of the click.

A corrupt or unreadable settings file degrades to `ShellSettings.Default` and
is moved aside rather than overwritten — the convention already established for
every other store in this repository (OVI-236/238).

**Rejected: let the menu keep its own copy and write it on change.** Two
writers to one file, and a menu that disagrees with the running poll cadence
the first time a write fails. The settings file has one owner
([ADR-0007](0007-app-shell-contract.md) D4) and the menu is not it.

**Validation belongs to the shell, and is not cosmetic.**
`SetThresholdPercent` takes an `int`; `ShellSettings` documents 0–100. The shell
clamps or rejects out-of-range values. A skin offering three fixed choices
cannot send a bad one today, but the seam is public and the next skin is not
this record's to predict.

### D5 — The threshold picker is built *after* the event decision it controls, in the same phase, not before it

[ADR-0007](0007-app-shell-contract.md) D2 point 6 — decide *that* a threshold
crossing should be raised, and whether it has already been raised for this
window — is unbuilt, and both skins' alert presentation consequently has no
production caller (CONFIRMED). A threshold picker shipped on its own would be a
control that visibly changes a number which nothing reads: the worst kind of
feature, because it looks like it works.

So the event-decision slice is **in this phase and ordered before the picker**
(slice 3 before slice 6 in the table below). Its contract is already written in
ADR-0007 D2 point 6 and this record does not re-decide it; it only refuses to
ship the picker without it.

Scope note, to keep that slice from growing: it decides the **four existing
`UsageEventKind` values** and raises each at most once per occurrence. It does
not add a fifth kind, and `UpdateAvailable`'s *fetch* remains ADR-0007 D3's and
OVI-433's.

**2026-10-05 update — slice 3 landed (Kit the Builder, OVI-457).** `UsageEventDecider`
(`src/O-view.App/UsageEventDecider.cs`) decides all four kinds, each edge-triggered
and re-armed exactly as the source app's `ThresholdWatcher`/`CheckOffPlan` were
(the only prior art for the dedup shape): raise once on the way up, re-arm on the
way back down, or once per provider/version. `ThresholdCrossed` compares
`UsageSnapshot.SessionUtilizationPercent` against the **loaded**
`ShellSettings.AlertThresholdPercent`, never a constant, and is wired into both
`Program.cs`s over the existing poll loop — its first production caller.
`OffPlanEntered` reuses the same `IUsageStatisticsSource` seam
`DetailPushCoordinator` already reads (ADR-0008 D9a), not a new ledger read, and
is wired alongside it. `InputDegraded` and `UpdateAvailable` are decided and
under test but still have **no production caller**: neither `Program.cs` composes
a `CompositeUsageProvider` yet (both still build a plain `JsonlUsageProvider`), so
there is no real `ProviderHealth` list to pass, and the update-check fetch stays
OVI-433's. The sentence above this update — "is unbuilt… no production caller" —
now holds only for those two kinds.

### D6 — Theme: the OS owns the preference, the shell carries the fact, the skin owns every colour — and "unknown" is a real answer

This is the one genuinely new capability in Phase 4A.
[ADR-0007](0007-app-shell-contract.md) D5 lists "theme source" among the
capabilities the shell declares and each skin implements, but no interface
exists (CONFIRMED). Phase 4A adds it, under four rules:

1. **The shell declares `IThemeSource`; each skin implements it** — per D5,
   selected at compile time by which skin is built, never by a runtime OS check.
   Windows reads `AppsUseLightTheme` (CONFIRMED as the source app's mechanism,
   ADR-0002 line 52); Linux asks the desktop portal for
   `org.freedesktop.appearance` `color-scheme`, over the `Tmds.DBus.Protocol`
   dependency slice 8 of ADR-0008 already brought in — **no new package**.
2. **What crosses the seam is a preference, not a palette.** The interface
   carries one of three values — light, dark, **unknown** — and nothing else. No
   colour, no brush, no hex string, no "is dark" boolean. Core holds no colour
   today and must not start; the shell holds none either (ADR-0007 D2: "it never
   draws, words, measures, colours, or places anything"). Each skin maps the
   preference onto its own resources, independently, and the two skins' palettes
   are **not** required to match — they are native windows on different
   platforms, which is what G4 = A chose.
3. **`Unknown` is reported, never guessed.** The registry value can be absent;
   the portal can be absent entirely, which on Linux is ordinary rather than
   exceptional. In that case the skin applies its platform's own default
   appearance and the seam says `Unknown` — it does not report `Light` because
   light is the commoner default. This is the never-fabricate principle applied
   to an enum, and it is the same discipline ADR-0001 applies to every
   `Unavailable` figure. A skin may *render* a default; it may not *claim* a
   preference nobody stated.
4. **Re-read on every open, and subscribe for live changes.** Re-reading when a
   window or menu opens is CONFIRMED as the source app's behaviour and is the
   cheap, reliable half. A live change notification — `WM_SETTINGCHANGE` on
   Windows, the portal's `SettingChanged` signal on Linux — repaints an
   already-open window. The portal read and its signal must stay **off the UI
   thread**, the same rule ADR-0008 D6 imposed on the session-bus probe and for
   the same reason.

**Rejected: a shared theme-to-colour mapping in the shell or a shared project,
so both skins pick the same greys.** It is the shared UI layer G4 = A declined,
arriving one type at a time, and it would put a colour in the one layer ADR-0007
D2 forbids colours in. Two skins choosing their own greys is the intended
outcome, not drift.

**Rejected: following the theme only at window-open time, with no subscription.**
It is simpler and matches the source. But the detail window is a widget a user
may leave open, and a window that stays light while the desktop goes dark looks
broken in a way a tray icon does not. The subscription is one slice, after the
read works.

### D7 — Quit becomes reachable, and shutdown keeps the shell's ordering

Both processes today exit only by being killed from outside (CONFIRMED). The
menu's Quit item is the first reachable call into `ISkinToShell.Quit()`, and the
shell decides the order, as ADR-0007 D2 point 1 and point 5 require:
`IShellToSkin.Shutdown()` so the skin tears down its OS integration first, then
stop and dispose the poll loop, then flush and dispose the stores, then let the
platform loop exit.

**The mechanism of "let the platform loop exit" is the skin's** — WPF's
`Application.Shutdown` under `ShutdownMode.OnExplicitShutdown`, Avalonia's
classic-desktop lifetime equivalent. The *order* is the shell's and is
identical on both; the call that ends the loop is not.

This is the slice with the highest ratio of risk to line count in Phase 4A: a
quit path that disposes the SQLite store while a poll is in flight is a
corrupted-ledger bug, and it is reachable from one menu click. It is called out
separately in the table for that reason.

### D8 — Out of scope, and staying out

- **Auto-update execution and packaging** — OVI-433, the sibling amendment
  under the board's option C. Not here, including any manual "check for updates
  now" menu item (D2).
- **macOS** — gate **G3**, open. No menu-bar skin, no menu-bar idiom, and no
  type named or shaped to anticipate one.
- **A second AI source** — gate **G5**, open. No setting for choosing or
  configuring a source.
- **A shared UI layer** — gate G4, closed as A. The menu is implemented twice
  (D1 of ADR-0008), and so is the theme mapping (D6).
- **A settings window** — declined for Phase 4A with its re-opening conditions
  stated (D1).
- **Any setting not already in `ShellSettings`.** Phase 4A exposes the three
  values ADR-0007 D4 already assigned to the shell. It adds no fourth.

**Escalation check, per this task's brief: no part of Phase 4A as scoped here
requires G3, G4 or G5 to be opened first.** The three settings, the menu and
theme following are all per-OS capabilities of the two skins that already exist,
in the "one capability, many implementations" shape ADR-0002 requires. Theme
following is the only one that touches a platform mechanism this repository has
never driven, and it is a read, on an existing dependency.

## Amendments to earlier records

Carried in the same PR as this record, so no merged record is left stating
something this one contradicts:

- **[ADR-0008](0008-presentation-skin-contract.md) D2** — "Four surfaces"
  becomes five: a **Menu** row stating that every skin must present the items
  in D2's table above, read run-at-startup live from the OS, and render the
  state the OS reports rather than the state requested; and must not hold its
  own copy of a shell setting or decide what any threshold means.
- **[ADR-0008](0008-presentation-skin-contract.md) D8** — the out-of-scope list
  gains a pointer: settings, the menu and theme following are Phase 4A, this
  record; packaging and self-update remain deferred to OVI-433.
- **[ADR-0002](0002-cross-platform-capability-matrix.md)** — the
  **Theme-following** row's "every skin must provide" column gains its
  interface pointer, `IThemeSource`, in the same correction-only shape OVI-335's
  drift check used for the other three rows. **No guarantee cell changes and no
  evidence label moves**: the Windows cell stays CONFIRMED-for-the-source, the
  Linux cell stays "never observed on real hardware", and a seam existing is not
  evidence that a platform does the thing.

## Alternatives considered

**One "Phase 4" record covering settings, menu, auto-update and packaging.** The
board already rejected this at OVI-423 by choosing option C, and the choice
holds up on inspection: this record's content is wiring an existing seam and can
start immediately, while OVI-433's changes `.csproj` files, adds packaging
artefacts and touches the update code path — a different risk class and a
different reviewer's attention. Bundling them would hold the cheap, safe half
hostage to the expensive half's review.

**Build the menu first, then make its items work.** Tempting because the menu is
the visible part and would demo well. Rejected: it produces, deliberately, a
menu of controls that do nothing — a threshold that nothing reads (D5), a quit
that cannot quit, a startup toggle calling dead code. The table below therefore
wires the shell first and adds the menu once there is something behind every
item.

**Put the threshold picker in the detail window instead of a menu.** The window
is already built, which would make this nearly free, and it has room for
explanatory text. Rejected: the window is a *data* surface by ADR-0008 D2 and
D3 — it renders the last `UsageDetail` and nothing else, and holds no number the
shell did not give it. Controls that write settings would give it a second,
contradictory job, and D3's "renders what it was pushed" rule is the main thing
keeping that surface honest.

**Expose poll cadence in the menu alongside the other two settings.** It is in
`ShellSettings`, so it would cost one more item. Rejected: 60 seconds is
CONFIRMED against the source's shipping cadence (ADR-0007 D6), and a user who
sets it to one second turns a monitoring tool into a file-system load generator
against the vendor's data directory. It stays file-editable and absent from the
menu until there is a reason, which is a decision this record makes explicitly
rather than by omission.

## Consequences

**Positive.**

- Six pieces of already-merged, already-tested, currently-unreachable code —
  both `IStartupRegistration` implementations, `ShellSettingsStore`,
  `DiagnosticsBundleWriter`, and both skins' alert presentation — acquire a
  production caller. Phase 4A deletes more dead code than it adds contract.
- The app can be quit by its user for the first time (D7).
- The seam does not move (D2), so the board's merge attention in this phase is
  spent on two slices rather than ten.
- The last capability in ADR-0002's matrix with no seam in code gets one (D6),
  closing the gap OVI-335's drift check found in its own terms.

**Negative, and accepted.**

- **Five surfaces, implemented twice, is the G4 = A bill arriving again.** The
  menu is written once per skin and the theme mapping is written once per skin.
  This record does not relitigate that; it notes that each new surface makes the
  cost of G4 = A visible, and that this is the fifth.
- **The anti-drift harness grows again** (ADR-0008 D7): a menu fixture family,
  in the `…Fixture` / `…Fixtures` / `…SkinUnderTest` /
  `…GoldenMasterCrossSkinTests` shape the existing families already use. Slice 1
  pays this before either menu is written.
- **Theme following will ship unverified on Linux, and this record says so in
  advance.** No interactive display and no session bus is reachable from this
  environment or, per [ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md),
  from the Linux CI runner. ADR-0002's Linux theme cell will read exactly as it
  does now after slices 8 and 10 merge, and those PRs must say so in "Risk / not
  verified" rather than implying the code working means the platform works.
- **The run-at-startup asymmetry is a thing reviewers will query** (D3): two
  settings go through the shell and one does not. The reasoning is D4's three
  owners of state, and it is written down here so the next person to notice it
  finds an answer instead of a bug.
- **Declining the settings window is a judgment that may be overturned** (D1).
  If the board wants one, it is an eleventh slice on this table, not a
  re-design: the settings and their owners do not change, only the surface
  rendering them.

## Slicing guidance for decomposition

One slice = one PR. Ordered lowest-risk first; shell before skins, Windows
before Linux, per ADR-0008's own ordering rationale. **This table is open for
decomposition** — the board signed this record off on 2026-10-05 (see
**Status**).

| # | Slice | Depends on | Risk | Merge |
|---|---|---|---|---|
| 1 | Cross-skin **menu content fixtures** in `tests/O-view.CrossSkin.Tests/`, in the existing `…Fixture` / `…Fixtures` / `…SkinUnderTest` / `…GoldenMasterCrossSkinTests` shape. Pins the content facts each menu must state — which items exist, that the startup item reports the state the OS returned rather than the one requested (D3), that a failed toggle is stated and not swallowed, that a threshold item shows the shell's persisted value — **not** identical labels, per [ADR-0003](0003-paneltext-anti-drift-mechanism.md). Pure, no toolkit, no UI, runs on both runners | — | **Lowest** — tests only; defines what both menus must say before either exists | agent |
| 2 | **A real `ISkinToShell` in `O-view.App`**, replacing both `PendingSkinToShell` copies: load `ShellSettings` from `ShellSettingsStore` at composition, compose the poll loop with the **loaded** cadence, implement `RefreshNow` / `SetThresholdPercent` / `SetAutoUpdate` / `WriteDiagnosticsBundle` (D4), delegate `RequestWidget` to the existing `DetailPushCoordinator`, clamp out-of-range percents, degrade a corrupt settings file to `Default` and move it aside. `Quit` still throws — that is slice 4. No UI | — | Low–medium — shell wiring over types that already exist and are tested; the settings round trip and the corrupt-file path are what to test hardest | agent |
| 3 | **The event decision ([ADR-0007](0007-app-shell-contract.md) D2 point 6)**: decide and raise each of the four existing `UsageEventKind` values at most once per occurrence, with the "already raised for this window" rule, reading the threshold from slice 2's loaded settings. Gives both skins' alert presentation its first production caller — **landed** (OVI-457): `ThresholdCrossed` and `OffPlanEntered` are wired into both `Program.cs` composition roots over the existing poll loop and ledger-statistics seam; `InputDegraded` and `UpdateAvailable` are decided and tested but still have no production caller — no `CompositeUsageProvider`/`ProviderHealth` is composed in either process yet, and the update-check fetch is OVI-433's | 2 | Medium — the dedup-per-window rule is the whole slice and is where an off-by-one costs a user a missed alert or a duplicate one | agent |
| 4 | **Quit (D7)**: `ISkinToShell.Quit()` implemented with the shell's ordering — skin `Shutdown()`, then poll loop, then stores — plus the Windows skin's loop-exit call. Must not dispose the store under an in-flight poll | 2 | **Medium–high** — smallest diff, largest blast radius in the phase: a wrong order is a corrupted ledger, reachable from one click | **board** (process lifetime and store disposal) |
| 5 | **`IThemeSource` in `O-view.App` + the Windows implementation (D6)**: the three-value preference (light / dark / **unknown**), `AppsUseLightTheme`, `WM_SETTINGCHANGE` for live changes, absent value reported as `Unknown`. No consumer yet — nothing is repainted by this slice | — | Low–medium — a registry read and a window message, both verifiable here | **board** (new shell interface — a seam addition) |
| 6 | **Windows menu (D1, D2)**: a `ContextMenuStrip` on the existing `NotifyIcon` with the seven items, threshold as a pick-one group reading slice 2's persisted value, run-at-startup read live from `RegistryStartupRegistration` on open and rendered from `Apply`'s return (D3), the failed-toggle statement, and slice 4's quit. Split per item group if it passes ~500 lines | 1, 2, 3, 4 | Medium — first menu; `IStartupRegistration`'s first production construction | agent |
| 7 | **Windows theme applied**: the detail window and the menu consume slice 5, repainting on live change. The theme-to-brush mapping is this skin's own and is shared with nothing (D6 point 2) | 5, 6 | Low–medium — verifiable here | agent |
| 8 | **Linux `IThemeSource` implementation (D6)**: the desktop portal's `org.freedesktop.appearance` `color-scheme` read plus its `SettingChanged` signal, over the existing `Tmds.DBus.Protocol` dependency, **off the UI thread** (ADR-0008 D6's rule), portal-absent reported as `Unknown`. **No new package reference** | 5 | **High** — no session bus reachable here or on CI; proven by fake-backed orchestration tests only, exactly as ADR-0008 slice 8's `DBusSessionBusNameWatcher` was | agent |
| 9 | **Linux menu (D1, D2) + the Linux loop-exit half of quit**: Avalonia `NativeMenu` on the existing `TrayIcon`, independently implemented from the Windows menu (ADR-0008 D1 — own wording, own structure code), run-at-startup live from `XdgAutostartRegistration`, same D3 rules | 1, 2, 3, 4, 6 (as reference) | **High** — no interactive Linux display reachable; ships labelled unverified | agent |
| 10 | **Linux theme applied**: the detail window consumes slice 8, repainting on the portal signal. Own mapping, matching nothing in the Windows skin | 8, 9 | **High** — **never observed** on real hardware; ADR-0002's Linux theme row ships **unchanged** | agent |

Slices 1–3 and 6–10 need no board answer beyond this record. Slice 4 changes
process termination and store disposal, and slice 5 adds a shell interface; both
are board merges. Slices 8–10 should each be expected to ship with their
ADR-0002 rows **unchanged**, and their PR bodies should say so in "Risk / not
verified" rather than implying the code working means the platform works.

If the board overturns D1 and wants a settings window, it is an **eleventh**
slice depending on 2, 5 and 6 — not a re-plan of this table.
