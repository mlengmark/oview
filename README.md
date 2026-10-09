# Oview — O-view rebuild

A notification-area (system tray) app that shows how much of your Claude AI
usage allowance you've used, and when it resets. This repository is the
**rebuild** of [`mlengmark/O-view`](https://github.com/mlengmark/O-view),
taken into a new codebase with a stronger data/presentation boundary and a
documentation trail from the first commit.

> **Status (2026-10-09).** **Phases 1, 2 and 3 are complete**, and Phase 4 is
> most of the way there. The display strings are out of the shared data layer
> and into each skin's own formatter (Phase 1); Core reads Claude's data off
> this machine, persists what it cannot recompute, and is driven by an
> application shell (Phase 2); and the five presentation surfaces — status
> icon, tooltip, detail window, alerts and the right-click menu — are built on
> both Windows and Linux (Phase 3, all eleven
> [ADR-0008](docs/adr/0008-presentation-skin-contract.md) slices built
> 2026-10-04). **Phase 4** adds the controls and the shipping: its control
> surface ([ADR-0009](docs/adr/0009-control-surface-contract.md), all ten
> slices built) is finished, and its update execution and packaging
> ([ADR-0010](docs/adr/0010-update-execution-and-packaging-contract.md), six of
> ten slices built) is in progress. **No skin has yet been run on real
> hardware**, so nothing here is verified against a live desktop.
>
> Board gates: **G0** (this repository), **G1** (target-architecture
> sign-off) and **G4** (UI unification — **option A**: keep two native
> windows, WPF on Windows and Avalonia on Linux, no shared UI layer) are all
> decided. **G3** (macOS) and **G5** (a second AI source) remain open, and
> neither Phase 3 nor Phase 4 needs either. See
> [`docs/adr/`](docs/adr/) for the
> Core-to-skin data contract, the cross-platform capability matrix, and the
> mechanism that replaced `PanelText.cs`'s centralization when its display
> strings moved out of the shared layer, the Phase 2 provider/storage/shell
> contracts, the Phase 3 presentation contract and the Phase 4 control,
> update and packaging contracts — and the approved PDR
> (linked from the ADRs) for the full target architecture.

## What O-view does

O-view sits in the system tray (or the Linux equivalent status area) and
answers three questions at a glance:

1. **How much of my Claude usage limit have I used?** (a 5-hour rolling
   window, and a 7-day window)
2. **When does it reset?**
3. **Is my work drawing from the plan, or billing as extra usage?**

It shows a colour-coded tray icon, a one-line tooltip on hover, a detail
window with account info, usage bars, a per-model breakdown and a 31-day
usage graph, a settings menu, and threshold-based desktop notifications.

Two things are standing product policy, not incidental behaviour, and this
rebuild does not renegotiate them:

1. **It never makes up a number.** Missing or estimated data is always
   labelled as such — see the data contract's status flag in
   [ADR-0001](docs/adr/0001-core-to-skin-data-contract.md).
2. **It only ever shows data from this machine.** Nothing from other
   devices or the cloud.

Today, Claude is the only AI system O-view reads from. Read-only, always,
against every vendor data source it touches, and it never handles a
subscription credential.

## Why a rebuild, not a patch

The existing `O-view` repository is not undocumented or unproven — it
carries 16 decision records, an actively-maintained `CLAUDE.md`, and it
runs today on Windows and Linux. The rebuild exists to correct one specific
structural problem, confirmed by direct reading of the source: several
files that construct user-facing display text — headings, separators,
date/time formats, and in one case a Windows-specific pixel-layout
constraint — live inside the platform-neutral data layer
(`O-view.Core.Models`), rather than in the per-OS presentation code that
should own them. [ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)
documents exactly what belongs on each side of that line, and
[ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md) documents how
the rebuild avoids reintroducing the bug that placement was put there to
prevent in the first place.

## Target architecture, in one sentence

There is exactly one place that knows about usage data (`Core`), and it
knows nothing about how any operating system displays it. Every
OS-specific presentation — wording, formatting, tray icon, window
positioning, startup/theme/update integration — lives in that OS's own
"skin," talking to Core only through a documented, versioned contract.

```
        Canonical Data Layer ("Core")
   (one source reader per AI system; today: Claude only)
     computes %, breakdowns, alert levels; labels every
        value real / estimated / unavailable; never
                   emits display text
                         │
              stable data contract (ADR-0001)
                         │
        ┌────────────────┼────────────────┐
   Windows Skin      Linux Skin        (future skin,
   owns: text,       owns: text,        same contract,
   tray, widget,     tray, widget,      new OS code only)
   startup/theme/    startup/theme/
   updates via       updates via
   Windows APIs      Linux APIs
```

## Documentation standard

Documentation is a deliverable of this rebuild, not a byproduct — see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s introduction for
the full standard this repository follows from commit one. In short:

- **Every non-trivial decision gets a dated ADR** (`docs/adr/NNNN-*.md`)
  stating what was decided, what was rejected, and why. Records are
  amended in place when they turn out to be wrong — never silently
  rewritten.
- **The Core-to-skin data contract is a living, versioned document**
  ([ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)), not a
  one-time table. No skin may consume a contract field not yet documented
  there.
- **The cross-platform capability matrix**
  ([ADR-0002](docs/adr/0002-cross-platform-capability-matrix.md)) is kept
  current as capabilities are actually implemented and verified per skin —
  including updating each cell's evidence label as real hardware testing
  happens.
- **Every factual claim carries an evidence label**: **confirmed** (read,
  run, or grepped against the actual source) or **inferred** (reasonable,
  not verified). Inferred claims are only promoted to confirmed when
  someone has actually verified them.

## Reading order

| Document | Covers |
|---|---|
| [`docs/adr/0001-core-to-skin-data-contract.md`](docs/adr/0001-core-to-skin-data-contract.md) | Every value Core can hand a skin: type, unit, status flag. What Core must never emit. |
| [`docs/adr/0002-cross-platform-capability-matrix.md`](docs/adr/0002-cross-platform-capability-matrix.md) | Per OS capability (tray icon, notifications, startup, self-update, ...): what every skin must provide, and what each platform actually, currently guarantees. |
| [`docs/adr/0003-paneltext-anti-drift-mechanism.md`](docs/adr/0003-paneltext-anti-drift-mechanism.md) | How the rebuild lets each skin own its own wording without reintroducing the bug (issues [#55](https://github.com/mlengmark/O-view/issues/55)/[#56](https://github.com/mlengmark/O-view/issues/56) on the source repo) that `PanelText.cs`'s centralization existed to prevent. |
| [`docs/adr/0004-what-non-windows-ci-could-and-could-not-prove.md`](docs/adr/0004-what-non-windows-ci-could-and-could-not-prove.md) | What a Linux CI job can and cannot prove for this solution, and the two-job workflow that follows from it. |
| [`docs/adr/0005-data-provider-contract.md`](docs/adr/0005-data-provider-contract.md) | Phase 2. How Core reads Claude's data off this machine: one input seam, three named providers, composition by information value, provider health as contract data. Opened gate **G6**. |
| [`docs/adr/0006-local-storage-contract.md`](docs/adr/0006-local-storage-contract.md) | Phase 2. What Core persists on this machine, where, and who owns each file — and why SQLite is the first third-party runtime dependency. |
| [`docs/adr/0007-app-shell-contract.md`](docs/adr/0007-app-shell-contract.md) | Phase 2. `O-view.App` — a third layer between Core and the skins, its admission rule, and what it may never contain. |
| [`docs/adr/0008-presentation-skin-contract.md`](docs/adr/0008-presentation-skin-contract.md) | Phase 3. Four surfaces (status icon, tooltip, detail window, alerts), two native skins, no shared UI layer — gate **G4 = A**. Read **D9** before touching the detail window. Its D2 is now **five** surfaces — see that section's 2026-10-05 amendment and 0009. |
| [`docs/adr/0009-control-surface-contract.md`](docs/adr/0009-control-surface-contract.md) | Phase 4A, **Accepted** (board merged PR #76, 2026-10-05); all ten slices built. The right-click menu as the fifth surface, the three shell settings it exposes, run-at-startup read live from the OS, and `IThemeSource`. No settings window, and the conditions that would re-open that. |
| [`docs/adr/0010-update-execution-and-packaging-contract.md`](docs/adr/0010-update-execution-and-packaging-contract.md) | Phase 4B, **Accepted** (board merged PR #77, 2026-10-05); six of ten slices built. How the build arrived (`InstallKind`) is the only thing permitting an update download or launch; checksum verification fails closed and ships with the download. Declines update channels, a proxy setting and an apt repository. |
| [`CLAUDE.md`](CLAUDE.md) | Contributor guidance — what may be assumed, what must be re-verified, and the evidence-labelling discipline this repository runs on. |

## Relationship to `mlengmark/O-view`

[`mlengmark/O-view`](https://github.com/mlengmark/O-view) is this
project's read-only source of evidence — its 16 ADRs, `README.md`, and
`CLAUDE.md` are cited throughout this repository's own documentation and
are the documentation bar this repository is expected to meet or exceed.
Nothing is written back to it. It continues to exist and run independently
of this rebuild.

## Status of the build

Four projects and five test projects exist and build: `O-view.Core`
(`net10.0`, the canonical data layer), `O-view.App` (`net10.0`, the
application shell), `O-view.Tray` (`net10.0-windows`) and `O-view.Linux`
(`net10.0`) as the two skins, plus `O-view.Core.Tests`,
`O-view.App.Tests`, `O-view.Tray.Tests`, `O-view.Linux.Tests` and the
cross-skin anti-drift harness `O-view.CrossSkin.Tests`. Build and test with
`dotnet build O-view.slnx` / `dotnet test O-view.slnx`.

- **Phase 1 — text extraction: complete.** Every confirmed presentation
  leak in the source repository's `O-view.Core.Models` —
  `TooltipFormatter.cs`, `UsageFormatter.cs`, `PanelStatistics.cs`, and all
  of `PanelText.cs` including the usage-tile caveat (OVI-165) and the
  off-plan banner (OVI-168) — now lives in each skin's own formatter, with
  the wording pinned by content rather than by exact string per
  [ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md).
- **Phase 2 — reading, storing and running: complete.** Every slice in
  [ADR-0005](docs/adr/0005-data-provider-contract.md)'s,
  [ADR-0006](docs/adr/0006-local-storage-contract.md)'s and
  [ADR-0007](docs/adr/0007-app-shell-contract.md)'s slicing tables is
  merged: three Claude providers behind one input seam, composition into a
  single reading with a health signal, three local stores, and the
  `O-view.App` shell with its poll loop, settings file, single-instance
  guard, update check and redacted diagnostics bundle.
- **Phase 3 — the presentation surfaces: complete.**
  [ADR-0008](docs/adr/0008-presentation-skin-contract.md) is **Accepted**
  (2026-10-02, board-merged PR #58; amended the same day by **D9**, which
  adds the detail window's `ShowDetail(UsageDetail)` data path), and all
  eleven slices of its table are built (last: PR #75, 2026-10-04). The
  status icon, tooltip, detail window and alerts exist in both skins — WPF
  on Windows, Avalonia on Linux — each with its own wording.
- **Phase 4 — the controls and the shipping: one half complete, one half in
  progress.** Phase 4A,
  [ADR-0009](docs/adr/0009-control-surface-contract.md) (**Accepted**,
  board-merged PR #76, 2026-10-05), is **built in full**: the right-click
  menu as a fifth surface on both skins, a shell that actually loads and
  honours its settings file, the alert-event decision, ordered quit, and
  light/dark/unknown theme following from the OS. Phase 4B,
  [ADR-0010](docs/adr/0010-update-execution-and-packaging-contract.md)
  (**Accepted**, board-merged PR #77, 2026-10-05), is **six of ten slices
  built**: the install-kind policy table and its two OS detectors, manifest
  and download-URL verification, Windows download-verify-launch, the
  background check cadence with notify-once-per-version, and the Linux
  notify-only path. Still to build: the Windows installer script (PR #95,
  open), the Linux `.deb` and tarball build, the release workflow, and —
  only if the board asks for it — provenance attestation.

**No skin has been run on real hardware.** Everything above is proved by
tests on this machine and on CI; no tray icon, menu, window or notification
in this repository has been seen working on a live Windows or Linux desktop,
and [ADR-0002](docs/adr/0002-cross-platform-capability-matrix.md)'s
never-observed cells stay never-observed.

### Changelog

Dated entries below are a historical log: each states what was true when it
was written, not the current state. For that, read the summary above.

**2026-09-10 — `O-view.Linux` scaffolded (OVI-30).** `src/O-view.Linux`
(`net10.0`) exists with its own minimal `Presentation/TooltipFormatter.cs`,
consuming the same `UsageSnapshot` contract as `O-view.Tray`'s formatter but
with independent wording and no length cap (per ADR-0001/ADR-0003 — the
127-character cap is a Windows API fact and does not travel to this skin).
This is scaffolding to unblock OVI-25's cross-skin golden-master harness,
not a hardware-verification event: no Avalonia UI, no D-Bus/StatusNotifierItem
integration, no tray icon rendering, and no other
[ADR-0002](docs/adr/0002-cross-platform-capability-matrix.md)
capability-matrix row. No Linux evidence label in that matrix changed.

**2026-09-10 — cross-skin golden-master harness scaffolded (OVI-25).**
`tests/O-view.CrossSkin.Tests` (`net10.0-windows`, so it can reference both
`O-view.Tray` and `O-view.Linux` from one project) now runs
[ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)'s golden-master
mechanism: one smoke fixture (the OVI-4 reference reading) is checked
against both skins' own `TooltipFormatter`, pinning content facts (the
percentages, the reset times) rather than exact strings. Harness and
fixture format only — no product code changed, and `PanelText.cs`,
`UsageFormatter.cs`, and `PanelStatistics.cs` are not yet extracted. See
[`tests/O-view.CrossSkin.Tests/README.md`](tests/O-view.CrossSkin.Tests/README.md)
for how to add the next fixture.

**2026-09-10 — `UsageFormatter.cs`/`PanelStatistics.cs` extracted (OVI-27).**
`O-view.Core` gains `TokenCount`, `EstimatedUsd`, `HistoryCoverage`, and
`UsageStatistics` — structured, status-flagged values replacing the K/M
token abbreviation, the `"$"` prefix, the `"unknown"` fallback, and
`PanelStatistics.CoverageNote`'s leaked sentence. `O-view.Tray` and
`O-view.Linux` each gain their own `Presentation/UsageFormatter.cs` and
`Presentation/PanelStatisticsFormatter.cs`, independently worded per
ADR-0003; the Windows skin's figures are tested byte-for-byte against the
source app's own `UsageFormatterTests.cs`/`PanelStatisticsTests.cs`. Three
new golden-master fixtures (ordinary usage, partial history coverage,
fully unavailable) were added to `O-view.CrossSkin.Tests` via a second,
parallel fixture family scoped to the new `UsageStatistics` shape — see
ADR-0003's OVI-27 amendment for why it is parallel rather than a change to
the existing harness types. `PanelText.cs` remains the one not-yet-extracted
confirmed leak, and is a separate, later, higher-risk slice.

**2026-09-11 — `PanelText.cs`'s Freshness/Countdown/SessionReset/WeeklyReset/
WeeklyResetConflict family extracted (Phase 1 slice 3.1, OVI-29).**
`O-view.Tray.Presentation.PanelTextFormatter` and
`O-view.Linux.Presentation.PanelTextFormatter` each now own this wording
independently, per ADR-0003. `DataSourceKind` gained `Stale` (5 values), and
`UsageSnapshot` gained a required `LastIngestAt` field so `Freshness` can
word a reading's age — see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-11 entries
for the full detail, including the one implementation decision made at this
slice's own discretion (where `DataSourceKind.JsonlFallback` falls in
`Freshness`'s wording). The cross-skin harness gained two more fixture
families, `FreshnessFixture` and `PanelTextResetFixture`, parallel to the
existing ones — see
[ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)'s matching
entry. The remaining `PanelText.cs` members (boost chip, usage-tile
caveat, off-plan banner, GitHub rate-limit notice) were separate,
differently-shaped sub-slices, not yet extracted as of this entry; the
boost chip and the GitHub rate-limit notice have since been extracted in
slices 3.3 and 3.4, below.

**2026-09-21 — `PanelText.cs`'s boost chip extracted: `BoostChip`/`BoostCard`
(Phase 1 slice 3.3, OVI-92, PR #15).** `O-view.Core` gains `BoostNotice`
(`Text`, `Percent`, `EndsOn` — a bare calendar date, not a timestamp). Each
skin's own `Presentation/PanelTextFormatter.cs` gains `BoostChip`/`BoostCard`,
independently worded per ADR-0003, and a fifth parallel fixture family in
`O-view.CrossSkin.Tests` (`BoostNoticeFixture`) checks both skins against
five fixtures. `UsageSnapshot` was **not** changed by this slice —
`SessionBoostNotice`/`WeeklyBoostNotice` wait on the future slice that ports
the provider populating them (see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-21 entry).
The source app's 281px Windows panel-width budget is skin-side only, and is
*not* implemented as an actual measure-and-truncate step here: no
`O-view.App` panel window exists yet in this repository to measure a
rendered row against.

**2026-09-22 — `PanelText.cs`'s GitHub rate-limit notice extracted:
`RateLimitedNotice` (Phase 1 slice 3.4, OVI-98, PR #16).** Redoes OVI-80's
closed PR #12 fresh against `main` after slice 3.3; the reviewed design
(OVI-81) is unchanged, only the branch is new — see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-22 entry
for why PR #12 was closed rather than reconciled again. This member needed
**no new Core surface**: its signature (`DateTimeOffset?`, `TimeZoneInfo` ->
`string`) never took a `UsageSnapshot`, only two raw scalars, so it lives
directly in each skin's existing `Presentation/PanelTextFormatter.cs`,
alongside `BoostChip`/`BoostCard`. A sixth parallel fixture family
(`RateLimitedNoticeFixture`, two fixtures: retry-after known and unknown)
was added to `O-view.CrossSkin.Tests` — see
[ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)'s matching
entry. The usage-tile caveat and the off-plan banner are the last
not-yet-extracted `PanelText.cs` members. *(Both have since landed — see the
2026-09-27 entry.)*

**2026-09-26 — CI, and the contract corrections found by building it.**
A two-job GitHub Actions workflow (Windows: the whole solution; Linux: the
`net10.0` projects) is live (OVI-124), designed by
[ADR-0004](docs/adr/0004-what-non-windows-ci-could-and-could-not-prove.md),
which states plainly what a Linux job cannot prove: not the Tray skin, not
the anti-drift harness. Several small contract corrections landed in the
same window: an `Unavailable` reading may not carry a value (OVI-146/149),
tooltips omit a reset flagged `Unavailable` even when one is present
(OVI-144), and the session-reset, weekly-reset and update-check decisions
D2/D3/D4 were written into ADR-0001/0002 (OVI-135/139).

**2026-09-27 — Phase 1 is complete (OVI-165, OVI-168).** The usage-tile
caveat and the off-plan banner — the last two `PanelText.cs` members — are
worded by the skins, and `O-view.Core` holds data only. No display string,
format, locale, OS branch or platform limit remains in the shared layer.

**2026-09-28 — Phase 2 designed (OVI-178, PR #33).** Three accepted
records: [ADR-0005](docs/adr/0005-data-provider-contract.md) (the data
providers, which also opened gate **G6** — the app may invoke the vendor's
own documented, read-shaped command, under three stated limits),
[ADR-0006](docs/adr/0006-local-storage-contract.md) (local storage, which
authorised SQLite as this repository's first third-party runtime
dependency, for the usage ledger only) and
[ADR-0007](docs/adr/0007-app-shell-contract.md) (the `O-view.App` shell and
its admission rule).

**2026-09-29 — 2026-10-01 — Phase 2 built, slice by slice.** Core gained
the `IUsageProvider` seam and three Claude providers (transcript JSONL,
Claude Desktop's plan history, Claude Code's usage cache), composition of
the three into one reading with a provider-health signal, and three local
stores (usage ledger, weekly-reset anchor, poll history) that move a
corrupt file aside rather than overwrite it. `O-view.App` gained its poll
loop with graceful degradation, the shell-to-skin seam in both directions,
store-directory ownership, a behaviour-settings file, a single-instance
guard with startup registration, the update-check fetch with one cooldown
per process, and a redacted diagnostics bundle. Nothing presentational
entered the shell.

**2026-10-02 — gate G4 answered and Phase 3 designed (OVI-318, PR #58;
OVI-326, PR #60).** The board decided **G4 = option A**: keep two native
windows — WPF on Windows, Avalonia on Linux — and build no shared UI layer.
[ADR-0008](docs/adr/0008-presentation-skin-contract.md) is the Phase 3
design that follows: four surfaces (status icon, tooltip, detail window,
alerts), each stated in terms of what it must say rather than how it looks,
with the draggable widget as the design on **both** platforms instead of an
icon-anchored panel on one and a compromise on the other. Its **D9**
amendment adds `ShowDetail(UsageDetail)` and `RequestWidget(bool)`, because
a snapshot alone could not carry the usage statistics or the per-model
split. Gates **G3** (macOS) and **G5** (a second AI source) stay open;
Phase 3 needs neither.

**2026-10-02 — Phase 3's first slice: detail-window and alert fixtures
(OVI-324, PR #59).** `O-view.CrossSkin.Tests` gains `DetailWindowFixture`
and `AlertFixture` — pinned facts for what the detail window must state and
for each of the four alert kinds — as pure data, with no `Render` and no
skin under test, because neither surface has a presenter to wire yet. The
detail-window fixture covers only what `UsageSnapshot` carries; the
statistics and per-model split arrive through D9's `UsageDetail` in a later
slice.
