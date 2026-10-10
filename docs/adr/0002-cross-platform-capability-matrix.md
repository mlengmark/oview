# ADR-0002: The cross-platform capability matrix, as a living document

- **Status:** Accepted — living document; update evidence labels as real
  hardware verification happens. Do not compress a label upward without a
  new, dated verification event.
- **Date:** 2026-09-08 · **Amended** 2026-09-10 (OVI-30), 2026-09-25 (OVI-135),
  2026-09-26 (OVI-140), 2026-10-02 (OVI-342 — seam cross-references only),
  2026-10-09 (OVI-570 — seam cross-references and one new row; no label moved),
  2026-10-09 (OVI-607 — gate G7 parity note; no cell and no label changed),
  2026-10-10 (OVI-621 — gate G7 parity slice P2; one new row, W1's Linux gap
  moved from INFERRED to CONFIRMED)
- **Deciders:** Adrian II the Architect, per board sign-off at gate G1
  (2026-09-08T19:53:12Z)
- **Formalizes:** the approved PDR (rev. 2, `oview-pdr-reissued`), §4
- **Evidence standard:** every cell below carries **CONFIRMED**, **CONFIRMED,
  narrow** (confirmed, but on a smaller surface than the label alone would
  suggest — see the note attached), **INFERRED**, or **never observed on
  real hardware**, exactly as labelled in Rae II's OVI-4 verification pass
  (2026-09-08) and the source repository's own `README.md` / `CLAUDE.md`.
  **No label in this ADR has been upgraded from those sources.** Where this
  ADR adds detail beyond the PDR table, that detail is cited to its
  specific source (OVI-4, or the source repo's own docs) rather than
  asserted fresh.

## Context

The target architecture requires each per-OS "skin" to implement the same
set of capabilities — tray icon, notifications, startup registration,
theme-following, and so on — against the shared Core-to-skin contract
([ADR-0001](0001-core-to-skin-data-contract.md)). But those capabilities
are **not** a single shared API with two implementations behind it
(PDR §5.4; source repo `CLAUDE.md` rule 1's table). They are genuinely
different mechanisms per OS, and treating them as if they were the same
already shipped a real bug once: the source repository's ADR-0009 was
amended after an assumption that `apt upgrade` handled Linux self-update
turned out to be wrong.

This ADR is the register that keeps that distinction visible: for each
capability, what every skin is required to provide, and — separately —
what each platform can *currently, actually* guarantee, evidence-labelled
rather than asserted.

## Decision

**Adopt the following table.** Read every "Linux guarantees" cell together
with the honesty notes beneath it — this matrix does not compress "fixed,
not re-tested" or "never observed" into "supported."

| Capability | Every skin must provide | Windows guarantees | Linux guarantees |
|---|---|---|---|
| Tray/status icon rendering | Render `UsageLevel` as a small icon; launch the widget on activation | **CONFIRMED** — `NotifyIcon`, per-monitor DPI v2 re-render (source repo ADR-0003) | **CONFIRMED, narrow** — StatusNotifierItem via D-Bus, seen working on KDE Plasma/Wayland tarball installs (2 hardware reports). GNOME without an AppIndicator extension and X11 are **INFERRED**, not observed |
| Tooltip display | Own text, format, and any length limit; render session/weekly percent + resets | **CONFIRMED** — `NotifyIcon.Text`, 127-char OS limit (Windows-only; must not travel to other skins per [ADR-0001](0001-core-to-skin-data-contract.md)) | **Never observed on real hardware** — desktop-environment-dependent SNI tooltip support, untested |
| Detail window launch & positioning | Draggable widget, launched from the tray/menu-bar icon; skin remembers last position (a skin preference, not Core data) | Can additionally use `Shell_NotifyIconGetRect` for icon-anchored placement — this target design deliberately does not rely on this (see "Alternatives considered") | **Cannot report icon position at all** — protocol limitation, StatusNotifierItem has no equivalent. Current fallback is a fixed work-area corner (source repo ADR-0013). **Never observed rendering on real hardware in any form** — blocked historically by a deadlock (#124), then a segfault (#143) |
| Notifications | Fire on a user-set threshold crossing, and nothing else. Expressed by the shell↔skin seam ([ADR-0007](0007-app-shell-contract.md) D6, shipped OVI-273): the shell pushes `IShellToSkin.RaiseEvent(UsageEvent)` with `UsageEventKind.ThresholdCrossed`; the skin sets the threshold back via `ISkinToShell.SetThresholdPercent`. The skin decides nothing about *when* — see [ADR-0008](0008-presentation-skin-contract.md) D2, "present `RaiseEvent` and nothing else" | **CONFIRMED** — balloon/toast, shipped | Freedesktop notification implemented; **never observed firing on real hardware** |
| Startup registration | Register/unregister at login via the OS's own canonical mechanism; the OS is the sole source of truth (no shadow copy in Core settings — [ADR-0007](0007-app-shell-contract.md) D4). Expressed by `IStartupRegistration` ([ADR-0007](0007-app-shell-contract.md) D5, shipped OVI-283): the shell declares the capability, the skin implements it — `RegistryStartupRegistration` on Windows, `XdgAutostartRegistration` on Linux | **CONFIRMED** — `HKCU\...\Run` key (source repo ADR-0009) | XDG autostart file implemented; **hardware-unverified** that login actually triggers launch (INFERRED from code) |
| Theme-following | Match OS light/dark preference automatically. Expressed by `IThemeSource` ([ADR-0009](0009-control-surface-contract.md) D6, **shipped** — `RegistryThemeSource` PR #83, `ThemeRepaintController` PR #86, `LinuxThemeSource` PR #87, `LinuxThemeRepaintController` PR #88): the shell declares the capability and carries one of three values (light / dark / **unknown**), each skin implements the read and owns every colour — the two repaint controllers map the same three readings to deliberately different palettes. An absent registry value or absent portal is reported as `Unknown`, never guessed, and each skin resolves `Unknown` to a named fallback rather than a crash | **CONFIRMED** — reads `AppsUseLightTheme` | Desktop theme portal implemented; **never observed on real hardware** |
| Single-instance enforcement | Guarantee exactly one running copy per user session. Expressed by `ISingleInstanceGuard.TryAcquire()` ([ADR-0007](0007-app-shell-contract.md) D5, shipped OVI-283) — `MutexSingleInstanceGuard` on Windows, `FileLockSingleInstanceGuard` on Linux. One capability, two unrelated primitives; the seam is the only thing they share | **CONFIRMED** — named mutex | No equivalent primitive exists; needs its own mechanism (file lock or socket) — genuinely a different implementation, not a shared API |
| Install & distribution | Get itself onto the machine via the OS's native packaging convention; register uninstall. **How it was installed is itself a capability**, because the self-update row below turns on it: expressed by `IInstallKindSource.Current` ([ADR-0010](0010-update-execution-and-packaging-contract.md), seam shipped PR #89, both implementations — `WindowsInstallKindSource`, `LinuxInstallKindSource` — PR #90), which each skin implements by inspecting its own platform's evidence. Never a runtime OS check in shared code ([ADR-0007](0007-app-shell-contract.md) D5) | **CONFIRMED** — Inno Setup installer, per-user, Start Menu shortcut, uninstall registry entry | Headless CI (Ubuntu/Debian/Fedora containers) **confirms install-and-start only** — proves nothing about UI, since containers have no compositor. Hardware reports used a tarball, not the `.deb`; `.deb`-on-real-hardware is untested |
| Self-update | Act on the shared update check's result: self-replace only if the OS's package model allows it, else notify-only. The check itself (fetch, rate-limit detection, `retryAfterUtc`) is **not** per-skin — see the 2026-09-25 (OVI-135) note below. **The "if" is one shared predicate, not two skin opinions:** `UpdatePolicy.MayDownloadAndRun(InstallKind)` ([ADR-0010](0010-update-execution-and-packaging-contract.md), shipped PR #89) is the only thing that decides, over the `InstallKind` the Install row's seam reports; the per-OS variance is which execution path that answer selects — `WindowsUpdateExecutor` (PR #92) or Linux notify-only (PR #94) | **CONFIRMED** — installer self-replaces via Restart Manager, checksum-verified first | **Must never self-replace** under a package-manager install (the package manager owns those files) — getting this wrong already shipped a real bug once (source repo ADR-0009 amendment) |
| Menu / control surface | Offer the same control items from the tray/menu-bar icon ([ADR-0009](0009-control-surface-contract.md) D1/D2), each skin deciding every item's rendered state by re-reading the live source on open — never a cached copy — and wording its own labels. Adds no shell↔skin seam member; the menu is a skin surface over seams that already exist. **Who paints the menu is a per-OS fact, not a shared one** — see the two cells beside this one; a skin must not assume it owns its menu's colours | Skin-painted: a `ContextMenuStrip` on the existing `NotifyIcon`, so the skin owns its colours and must repaint it itself on an `IThemeSource` change. **INFERRED from code** — `TrayMenuController`/`TrayMenu` (PR #84), repaint via `ThemeRepaintController` (PR #86); the adapter layer is untested, and the source app's own tray context menu is the CONFIRMED precedent for the mechanism, not for this implementation | Host-drawn: a `NativeMenu` assigned to the existing `TrayIcon.Menu`, rendered by the host desktop's own menu widget — it already follows the desktop theme and **has no themeable part a skin can set**, so the Windows cell's repaint obligation has no Linux counterpart. **Never observed on real hardware** — shipped labelled unverified, no interactive Linux display reachable (`LinuxMenuController`/`LinuxTrayMenu`, PR #85; PR #88's note) |
| Menu-dismiss-on-outside-click | Dismiss the widget/menu on an outside click | **CONFIRMED** — Win32 `AttachThreadInput`-based fix | No direct equivalent; compositor-dependent. One hardware-found bug here (#129, panel self-dismissing on an unfocused compositor) fixed but not re-tested |
| Hover card timing | Own styled hover card (ADR-0008 D11a §I), with 400 ms initial delay and 3000 ms between-show delay applied per element, never inherited from a container — see the 2026-10-10 note below | **CONFIRMED** — `ToolTipService.InitialShowDelay`/`BetweenShowDelay`/`ShowDuration`, all three settable per element; 20 s show duration applied exactly as designed | **CONFIRMED, narrow** — `Avalonia.Controls.ToolTip` exposes `ShowDelay` and `BetweenShowDelay` (both applied); **no show-duration equivalent exists** — Avalonia tooltips close on pointer-exit only, with no exposed hook to cap how long one stays open. Named platform limit, not faked (waiver candidate W1, `docs/parity/g7-detail-window-parity.md`) |

### 2026-09-10 update — `O-view.Linux` project scaffolded (OVI-30)

`src/O-view.Linux` (`net10.0`) now exists in this repository, with a
minimal `Presentation/TooltipFormatter.cs` that consumes
[ADR-0001](0001-core-to-skin-data-contract.md)'s `UsageSnapshot` contract
independently of `O-view.Tray`'s formatter. This was scaffolded solely to
give OVI-25's cross-skin golden-master harness real code to invoke on both
skins; it is **project scaffolding, not a hardware-verification event**.
No capability row above changed, no UI, tray icon, D-Bus/StatusNotifierItem
integration, startup registration, or self-update mechanism was added, and
**no evidence label in the table above was upgraded** as part of this
slice — the Linux column's guarantees remain exactly as verified (or not)
by Rae II's OVI-4 pass, unaffected by this scaffold existing.

### 2026-09-25 amendment — Self-update row: the check is shared, only acting on it is per-OS (OVI-135)

The Self-update row used to say every skin must "check for a new version". That wording
leaves open who owns the update check and the `retryAfterUtc` value that
`RateLimitedNotice` renders. The OVI-133 drift check labelled the owner **INFERRED: the
skin**. A direct read of the source repository at `897777b` refutes that. **CONFIRMED**:
- The pure rules are in Core, with no HTTP: `RateLimitResponse` and `UpdateCheck`, under
  `src/O-view.Core/Updates/`.
- The fetch and the rate-limit cooldown are in the shared `O-view.App` project
  (`ReleaseFeed`), "held here rather than in either head" (source issue #176).
- Each head only decides what to do with the result. Windows downloads and self-replaces
  (`O-view.Tray/Updates/UpdateService.cs`); Linux only notifies
  (`O-view.Linux/Updates/LinuxUpdateNotice.cs`).

**Decision:** detecting an update is one shared capability, and its result is contract data
([ADR-0001](0001-core-to-skin-data-contract.md) rows `UpdateCheckOutcome` and
`UpdateRetryAfterUtc`, same date). *Acting* on it stays "one capability, many
implementations", exactly as the Windows and Linux cells above already say. Those cells
and their evidence labels are unchanged; only the "every skin must provide" wording
moved. **Rejected:** a per-skin check. It duplicates the one piece that is genuinely the
same on every OS, and a per-head check is the pattern that let the source retry straight
back into GitHub's limit before issue #176 moved the cooldown into the shared layer.

**2026-09-26 (OVI-140):** the shared cooldown must also last for the whole process. The
source's Windows head builds a new `ReleaseFeed` per check and so drops it (CONFIRMED at
`897777b`). See ADR-0001's D4 amendment, same date, for the evidence and the rule.

### 2026-10-02 amendment — three rows now name the seam that expresses them (OVI-342)

When this matrix was written, "every skin must provide" could only be prose: no seam
existed. Three of those rows now have one in code, and the table did not say so — found by
OVI-335's drift check. **Correction only.** The three rows' "every skin must provide"
column now points at its interface:

- **Notifications** → `IShellToSkin.RaiseEvent(UsageEvent)` / `ISkinToShell.SetThresholdPercent`
  ([ADR-0007](0007-app-shell-contract.md) D6; `src/O-view.App/`, PR #52, OVI-273).
- **Startup registration** → `IStartupRegistration` ([ADR-0007](0007-app-shell-contract.md)
  D5; PR #55, OVI-283).
- **Single-instance enforcement** → `ISingleInstanceGuard` (same ADR, same PR).

**No Windows or Linux guarantee cell changed, and no evidence label moved.** Nothing was
re-verified on hardware for this entry, and a seam existing is not evidence that a platform
does the thing — the Linux cells still read exactly as Rae II's OVI-4 pass left them. The
seam makes the *obligation* checkable in code; it does not make the *behaviour* observed.

> **2026-10-05 addition (OVI-432):** the **Theme-following** row now points at
> `IThemeSource` too ([ADR-0009](0009-control-surface-contract.md) D6) — the
> fourth and last row in this table whose "must provide" column was still prose
> only. It is marked **designed, not yet built**, which the three rows above it
> were not: ADR-0009 awaits board sign-off and no implementation exists. Every
> word of the paragraph above applies unchanged — **no guarantee cell changed
> and no evidence label moved.** The Linux theme cell still reads "never
> observed on real hardware" and is expected to still read that after Phase 4A
> ships, since no interactive Linux display is reachable from this environment
> or from CI ([ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md)).

**Where the rest of the capability story now lives**, since this ADR predates all four:

| Read this for | ADR |
|---|---|
| The shell↔skin seam, who owns persisted state, the shared update check | [ADR-0007](0007-app-shell-contract.md) D3–D6 |
| What each skin must state on each surface, and the draggable-widget design (G4 = A) | [ADR-0008](0008-presentation-skin-contract.md) D1–D5, D9 |
| Where the numbers come from, and the degraded/unavailable cases a skin must survive | [ADR-0005](0005-data-provider-contract.md) D1, D4 |
| What is persisted locally, and the local-machine-only boundary | [ADR-0006](0006-local-storage-contract.md) |

This matrix stays the register of **per-OS variance and evidence**; those four own the
contracts. Where they disagree with a row here, the row here is the one to fix.

### 2026-10-09 amendment — three rows now name their seam, and the menu surface gets a row (OVI-570, D4/D5)

Found by OVI-566's drift check. **Correction and one addition only.**

- **Theme-following** (D4) no longer says `IThemeSource` is "designed, not yet
  built" — it shipped across four slices: `RegistryThemeSource` (PR #83),
  `ThemeRepaintController` (PR #86), `LinuxThemeSource` (PR #87),
  `LinuxThemeRepaintController` (PR #88). The 2026-10-05 addition above
  anticipated exactly this and is left standing as written, including its
  prediction that the Linux cell would still read "never observed on real
  hardware" afterwards. **It does, and that is the correct reading** — four
  shipped slices are four pieces of code, and code existing has never been
  hardware evidence in this table.
- **Self-update and Install & distribution** (D5) now name the pair that
  expresses them: `IInstallKindSource.Current` reports how this copy was
  installed (seam PR #89, both implementations PR #90), and
  `UpdatePolicy.MayDownloadAndRun(InstallKind)` (PR #89) is the single shared
  predicate deciding whether self-replacement is allowed at all. Naming it
  here is the point: the "notify-only on a package-managed install" rule that
  the source repository got wrong once is **one function**, not a judgement
  each skin makes for itself.
- **Menu / control surface** (D5) is a **new row**. Its per-OS variance —
  Windows paints its own `ContextMenuStrip` and must therefore repaint it on a
  theme change; Linux's `NativeMenu` is drawn by the host desktop and has no
  themeable part at all — existed only in an ADR-0009 slicing-table cell
  (row 10, PR #88). That is per-OS variance, which is this table's job to
  hold, and a slicing table is a build plan that stops being read once its
  slices land.

**No existing Windows or Linux guarantee cell changed, and no evidence label
moved.** Nothing was verified on hardware for this entry. The new row's two
cells are new, not relocated, and carry the labels their evidence supports and
no more: the Windows cell **INFERRED from code** (the adapter is untested; the
source app's menu is precedent for the mechanism, not for this implementation),
the Linux cell **never observed on real hardware**.

### Reading the Linux column honestly

Per OVI-4 (2026-09-08), Linux's evidence base is **two hardware reports,
not one**, both on the same configuration (Arch-based, KDE Plasma,
Wayland, tarball install), against v0.6.1 and v0.6.5 of the source
repository:

- **Seen working, from those reports:** data resolution on every path
  (5,645/5,645 plan-history samples in the first report), and the tray
  icon appearing.
- **Fixed, never re-tested since — four issues:** #124 (first-click
  deadlock), #125 (slow menu), #129 (panel self-dismissing on an
  unfocused compositor), #143 (first-click segfault from a D-Bus-thread
  violation). The source repository's own `CLAUDE.md` states plainly that
  "fixed" here means "understood and patched," not "confirmed working" —
  no automated test can reach a live session bus and a UI dispatcher
  together, which is exactly how three of these four shipped undetected.
- **Never observed on real hardware, by anyone, in any form:** the detail
  panel itself, the tooltip, notifications, theme-following, the menu
  actually rendering, X11, GNOME without an AppIndicator extension, and
  the `.deb` package (the hardware reports used a tarball).
- **What headless CI does and does not prove:** container-based CI
  (Ubuntu 22.04/24.04, Debian 12, Fedora) confirms the package installs
  and starts. It cannot and does not prove a tray icon is visible, a
  tooltip is legible, or a theme is followed, because containers have no
  compositor. OVI-4 additionally ran the Linux build's own offscreen
  diagnostic hooks (`--diagnose`, `--probe`) on this (Windows) machine —
  confirming the shared data layer degrades gracefully off-target, which
  is **not** evidence about real Linux desktop UI behaviour.
- **2026-10-09 note (OVI-585/591, gate G7 detail-window parity — no cell
  changes).** The G7 parity amendment to
  [ADR-0008](0008-presentation-skin-contract.md) (D4, D10, D11) and
  [ADR-0005](0005-data-provider-contract.md) (D2, D6c, D7) changes **no cell in
  this matrix**. Every cell that work touches is still **never observed on real
  hardware**, and G7 does not close until each platform has had a real-desktop
  observation of the panel (dragged, restarted, reopened where it was left) and
  of the menu. Agents cannot perform that step. This matrix is updated from those
  observations, never from merged slices. Recorded here so a reader of the
  amendment does not infer that building the slices moved a label.

### 2026-10-10 amendment — Hover card timing gets a row; W1's Linux gap is now measured, not guessed (OVI-621, gate G7 parity slice P2)

**New row.** Both skins now build the one styled hover card ADR-0008 D11a §I
describes (`HoverCard` in each skin's own `Presentation/`), replacing every
default system tooltip in the detail window with a bordered card on the
window's own theme colours, in two shapes (`Figure`, `Text`).

**W1's "INFERRED unverified" is resolved to CONFIRMED, in the direction the
waiver candidate expected.** `docs/parity/g7-detail-window-parity.md` W1 asked
for Avalonia's actual tooltip surface to be read before the board decides
anything. Done: reflecting `Avalonia.Controls.dll` 12.1.3 (and confirmed by a
failing-on-purpose unit test against the real default) shows `ToolTip.ShowDelay`
and `ToolTip.BetweenShowDelay` exist and behave as expected — `ShowDelay`'s own
unset default is already 400 ms, `BetweenShowDelay`'s is 100 ms — but **no
`ShowDuration` property, attached or otherwise, exists anywhere in the
assembly.** Avalonia's tooltip popup closes when the pointer leaves the owning
element and exposes no hook to force an earlier or later close. This slice does
not build a custom popup-and-timer reimplementation to fake the 20 s cap — W1 is
still open, and a skin quietly diverging from what it says it does is the
mistake W1 exists to prevent. `HoverCard.ApplyTiming` on Linux applies the two
properties that exist and documents the third's absence at the call site.

**No existing cell's label moved** except the one above (the Linux hover-card
cell is new, not relocated). Nothing else in this table changed.

## Alternatives considered

**Collapse the matrix to a single "supported / not supported" column per
platform.** Rejected — this is precisely the compression the source
repository's own `README.md` and `CLAUDE.md` deliberately avoid ("Released
is not the same as verified, and neither is fixed"), and the PDR's
evidence standard requires carrying the same three-way distinction
(confirmed / fixed-not-retested / never-observed) forward rather than
flattening it for readability.

**Treat OS capabilities as one shared abstraction with platform-specific
backends selected at runtime.** Rejected, per PDR §5.4 — startup
registration, theme-following, single-instance enforcement, install, and
self-update are different enough in mechanism (a Windows registry key has
no Linux equivalent; a Linux package manager's ownership of installed
files has no Windows equivalent) that a shared abstraction would either
leak platform assumptions through it or force one platform's model onto
the other. The source repository's ADR-0009 amendment is the concrete,
already-realized cost of getting this wrong once.

## Consequences

**Positive:**
- A contributor or reviewer can tell, capability by capability, exactly
  how much real-world verification stands behind a claim of "Linux
  support" — rather than inheriting an unqualified checkmark.
- Each platform's skin can be built and reviewed against its own row
  without pretending symmetry that doesn't exist yet (e.g., Linux's
  single-instance enforcement is scoped here as "needs its own mechanism,"
  not "port the Windows mutex").

**Negative:**
- This matrix requires active upkeep: a cell's evidence label must be
  updated (with a dated note, per this repository's decision-record
  discipline) the moment new hardware verification happens, or it will
  silently go stale in the direction of overclaiming. That upkeep cost is
  accepted as part of the documentation standard's requirement (PDR §7)
  that "documentation claims [not] outrun what has actually been tested."
- Several Linux rows remain **never observed on real hardware** as of this
  ADR. This ADR does not resolve that; it records it accurately so Phase 1
  planning is not built on an unverified assumption.
