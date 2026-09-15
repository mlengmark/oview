# Oview — O-view rebuild

A notification-area (system tray) app that shows how much of your Claude AI
usage allowance you've used, and when it resets. This repository is the
**rebuild** of [`mlengmark/O-view`](https://github.com/mlengmark/O-view),
taken into a new codebase with a stronger data/presentation boundary and a
documentation trail from the first commit.

> **Status:** early implementation. Gate **G0** (this repository) and gate
> **G1** (target-architecture sign-off) have both passed board review.
> Phase 1 slice 1 has landed `O-view.Core` and `O-view.Tray` with exactly
> the surface `TooltipFormatter`'s extraction needed. `O-view.Linux` exists
> as a minimal scaffold (OVI-30) — its own `TooltipFormatter`, no UI, no
> tray icon, no D-Bus integration — added only to unblock the cross-skin
> golden-master harness. That harness, `O-view.CrossSkin.Tests` (OVI-25),
> now exists too, with one smoke fixture proving it invokes both skins'
> string-construction code and catches a deliberate content mismatch.
> Phase 1 slice 2 (OVI-27) has since extracted `UsageFormatter.cs`'s and
> `PanelStatistics.cs`'s one presentation leak the same way — a new
> `UsageStatistics` contract type in `O-view.Core`, and each skin's own
> `UsageFormatter`/`PanelStatisticsFormatter`. `PanelText.cs` and
> `O-view.App` do not exist yet. See
> [`docs/adr/`](docs/adr/) for the
> Core-to-skin data contract, the cross-platform capability matrix, and the
> mechanism that replaces `PanelText.cs`'s centralization once its display
> strings move out of the shared layer — and the approved PDR (linked from
> the ADRs) for the full target architecture.

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
| [`CLAUDE.md`](CLAUDE.md) | Contributor guidance — what may be assumed, what must be re-verified, and the evidence-labelling discipline this repository runs on. |

## Relationship to `mlengmark/O-view`

[`mlengmark/O-view`](https://github.com/mlengmark/O-view) is this
project's read-only source of evidence — its 16 ADRs, `README.md`, and
`CLAUDE.md` are cited throughout this repository's own documentation and
are the documentation bar this repository is expected to meet or exceed.
Nothing is written back to it. It continues to exist and run independently
of this rebuild.

## Status of the build

Phase 1 slice 1 has landed: `TooltipFormatter.cs`'s display-string
construction is extracted out of the platform-neutral layer. `O-view.Core`
(`net10.0`) defines the tooltip-relevant slice of the Core-to-skin contract
([ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)) as structured,
status-flagged values; `O-view.Tray` (`net10.0-windows`) owns turning those
values into tooltip text, including the 127-character `NotifyIcon.Text`
cap. Build and test with `dotnet build O-view.slnx` / `dotnet test
O-view.slnx`. `PanelText.cs` and `O-view.App` are separate, later slices
and do not exist here yet; `UsageFormatter.cs`'s and `PanelStatistics.cs`'s
presentation leaks were extracted in Phase 1 slice 2 (OVI-27), below.

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
