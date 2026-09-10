# ADR-0002: The cross-platform capability matrix, as a living document

- **Status:** Accepted — living document; update evidence labels as real
  hardware verification happens. Do not compress a label upward without a
  new, dated verification event.
- **Date:** 2026-09-08
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
| Notifications | Fire on a user-set threshold crossing, from `NotificationThresholdCrossed` only | **CONFIRMED** — balloon/toast, shipped | Freedesktop notification implemented; **never observed firing on real hardware** |
| Startup registration | Register/unregister at login via the OS's own canonical mechanism; sole source of truth (no shadow copy in Core settings) | **CONFIRMED** — `HKCU\...\Run` key (source repo ADR-0009) | XDG autostart file implemented; **hardware-unverified** that login actually triggers launch (INFERRED from code) |
| Theme-following | Match OS light/dark preference automatically | **CONFIRMED** — reads `AppsUseLightTheme` | Desktop theme portal implemented; **never observed on real hardware** |
| Single-instance enforcement | Guarantee exactly one running copy per user session | **CONFIRMED** — named mutex | No equivalent primitive exists; needs its own mechanism (file lock or socket) — genuinely a different implementation, not a shared API |
| Install & distribution | Get itself onto the machine via the OS's native packaging convention; register uninstall | **CONFIRMED** — Inno Setup installer, per-user, Start Menu shortcut, uninstall registry entry | Headless CI (Ubuntu/Debian/Fedora containers) **confirms install-and-start only** — proves nothing about UI, since containers have no compositor. Hardware reports used a tarball, not the `.deb`; `.deb`-on-real-hardware is untested |
| Self-update | Check for a new version; self-replace only if the OS's package model allows it, else notify-only | **CONFIRMED** — installer self-replaces via Restart Manager, checksum-verified first | **Must never self-replace** under a package-manager install (the package manager owns those files) — getting this wrong already shipped a real bug once (source repo ADR-0009 amendment) |
| Menu-dismiss-on-outside-click | Dismiss the widget/menu on an outside click | **CONFIRMED** — Win32 `AttachThreadInput`-based fix | No direct equivalent; compositor-dependent. One hardware-found bug here (#129, panel self-dismissing on an unfocused compositor) fixed but not re-tested |

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
