# CLAUDE.md — Oview (O-view rebuild)

Guidance for AI coding assistants and human contributors working in this
repository. Read this before writing code or documentation here.

## What this is

The rebuild of [`mlengmark/O-view`](https://github.com/mlengmark/O-view), a
desktop notification-area app that reports Claude AI token usage. Gate G0
(this repository) and gate G1 (target-architecture sign-off) have both
passed board review; see [`README.md`](README.md) and
[`docs/adr/`](docs/adr/) for what that approved.

**Implementation has begun, one reviewed slice at a time.** As of Phase 1
slice 1 (OVI-10), `O-view.Core` and `O-view.Tray` exist here with exactly
the surface `TooltipFormatter`'s extraction needed — the Core-to-skin
contract's tooltip-relevant fields ([ADR-0001](docs/adr/0001-core-to-skin-data-contract.md))
and the Windows skin that turns them into tooltip text. `O-view.App` does
not exist yet; do not assume any capability beyond what a merged slice has
actually added. Check [`docs/adr/0001`](docs/adr/0001-core-to-skin-data-contract.md)'s
"current codebase status" section for what has and has not landed.

**2026-09-10 update (OVI-30) — `O-view.Linux` now exists here too, as a
minimal scaffold.** `src/O-view.Linux` (`net10.0`) and its own
`Presentation/TooltipFormatter.cs` exist solely to give OVI-25's
cross-skin golden-master harness real string-construction code to invoke
from both skins. This is project scaffolding, not a hardware-verification
event: no Avalonia UI, no D-Bus/StatusNotifierItem integration, no tray
icon, and no other [ADR-0002](docs/adr/0002-cross-platform-capability-matrix.md)
capability-matrix row lives here yet, and no Linux evidence label in that
matrix changed as part of this slice.

**2026-09-10 update (OVI-25) — the cross-skin golden-master harness itself
now exists.** `tests/O-view.CrossSkin.Tests` (`net10.0-windows`) implements
[ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)'s mechanism: it
references `O-view.Tray`, `O-view.Linux`, and `O-view.Core` from one
project, runs a versioned set of golden-master fixtures (one, so far — the
OVI-4 reference reading) against both skins' own `TooltipFormatter`, and
fails if either skin omits or contradicts a pinned content fact. See
[`tests/O-view.CrossSkin.Tests/README.md`](tests/O-view.CrossSkin.Tests/README.md)
for how to add a fixture. This slice ships no product code changes and
does not extract `PanelText.cs`, `UsageFormatter.cs`, or
`PanelStatistics.cs` — those remain separate, later slices.

**2026-09-10 update (OVI-27) — `UsageFormatter.cs`'s and
`PanelStatistics.cs`'s one presentation leak extracted.** `O-view.Core`
gains `TokenCount`, `EstimatedUsd`, `HistoryCoverage`, and `UsageStatistics`
— the ADR-0001 rows these two files' presentation logic needs, as
structured, status-flagged values. Each skin gains its own
`Presentation/UsageFormatter.cs` (K/M token abbreviation, `"$"` prefix,
unavailable-value fallback) and `Presentation/PanelStatisticsFormatter.cs`
(the coverage caveat that replaces `PanelStatistics.CoverageNote`),
independently worded per ADR-0003, and a second parallel fixture family in
`O-view.CrossSkin.Tests` (`UsageStatisticsFixture`, alongside the existing
`GoldenMasterFixture`) checks both skins against three fixtures (ordinary
usage, partial history coverage, fully unavailable). `PanelText.cs` remains
the one not-yet-extracted confirmed leak — a separate, later, higher-risk
slice.

**2026-09-11 update (OVI-29) — the first slice of `PanelText.cs` is
extracted: `Freshness`/`Countdown`/`SessionReset`/`WeeklyReset`/
`WeeklyResetConflict`.** `O-view.Tray.Presentation.PanelTextFormatter` and
`O-view.Linux.Presentation.PanelTextFormatter` each own this wording
independently. `DataSourceKind` gained `Stale`; `UsageSnapshot` gained a
required `LastIngestAt` field. The remaining `PanelText.cs` members (the
boost promo chip, the usage-tile caveat, the off-plan banner, the GitHub
rate-limit notice) are separate, differently-shaped sub-slices with their
own new Core surface, not yet extracted — see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-11 entry
for the full detail.

**2026-09-21 update (OVI-92) — the third sub-slice of `PanelText.cs`'s
split is extracted: `BoostChip`/`BoostCard`.** `O-view.Core` gains
`BoostNotice` (`Text`, `Percent`, `EndsOn` — a bare calendar date, not a
timestamp). Each skin's own `Presentation/PanelTextFormatter.cs` gains
`BoostChip`/`BoostCard`, independently worded per ADR-0003, and a fifth
parallel fixture family in `O-view.CrossSkin.Tests` (`BoostNoticeFixture`)
checks both skins against five fixtures. `UsageSnapshot` was **not**
changed by this slice — `SessionBoostNotice`/`WeeklyBoostNotice` wait on
the future slice that ports the provider populating them (see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-21
entry). The source app's 281px Windows panel-width budget is confirmed
skin-side only, and confirmed *not* implemented as an actual
measure-and-truncate step here — no `O-view.App` panel window exists yet
in this repository to measure a rendered row against. The usage-tile
caveat, the off-plan banner, and the GitHub rate-limit notice remain the
last not-yet-extracted `PanelText.cs` members.

**2026-09-22 update (OVI-98) — `PanelText.cs`'s GitHub rate-limit notice
extracted: `RateLimitedNotice`.** Redoes OVI-80/closed PR #12 fresh
against main at `80d3913` (post-`BoostChip`/`BoostCard`); the reviewed
design (OVI-81) is unchanged, only the branch is new — see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)'s 2026-09-22 entry
for why PR #12 was closed rather than reconciled a third time. Like OVI-80
established, this member needed **no new Core surface at all**: its
confirmed signature (`DateTimeOffset?`, `TimeZoneInfo` -> `string`) never
took a `UsageSnapshot`, only two raw scalars, so it lives directly in each
skin's existing `Presentation/PanelTextFormatter.cs`, alongside
`BoostChip`/`BoostCard`. The usage-tile caveat and the off-plan banner are
the last not-yet-extracted `PanelText.cs` members.

**Do not assume this repository contains working code.** If you are looking
for the current, running implementation, that is
[`mlengmark/O-view`](https://github.com/mlengmark/O-view) — read-only,
never to be written to from here.

## The one rule that matters most

**There is exactly one place that knows about usage data (`Core`), and it
knows nothing about how any operating system displays it.** Every
OS-specific presentation decision — wording, formatting, tray icon
rendering, window positioning, startup/theme/update integration — belongs
in that OS's own skin. Core talks to a skin only through the documented
data contract ([ADR-0001](docs/adr/0001-core-to-skin-data-contract.md)),
never the reverse, and never through a pre-formatted string.

This is not a restatement of good practice in the abstract. The source
repository already violated it once — `TooltipFormatter.cs`,
`PanelText.cs`, `UsageFormatter.cs`, and one property of
`PanelStatistics.cs` all construct user-facing display strings inside
`O-view.Core.Models`, confirmed by direct reading of the source (see
[ADR-0001](docs/adr/0001-core-to-skin-data-contract.md) for the full,
file-by-file account). Do not reintroduce that pattern here: if code you
are adding needs to decide *how a number is worded*, it does not belong in
a Core-equivalent layer, no matter how convenient it is to put it there.

## Evidence-labelling discipline

Every factual claim about this project — runtime behaviour, hardware
verification, cross-platform parity, what a file does — carries one of two
labels:

- **CONFIRMED** — read, run, or grepped against the actual source by
  someone on this project.
- **INFERRED** — reasonable, but not independently verified.

Never silently promote an inferred claim to confirmed. If you verify
something that was previously only inferred, say so explicitly and update
the label where it's recorded — do not just restate it as fact. This
convention is not specific to audit documents; it applies to ADRs,
`README.md`, code comments, and PR descriptions alike.

## The decision-record trail

Every non-trivial decision gets a dated, numbered ADR in
[`docs/adr/`](docs/adr/) (`NNNN-title.md`) stating what was decided, what
was rejected, and why — follow the format documented in
[`docs/adr/README.md`](docs/adr/README.md).

**ADRs are decided, not drafts.** To change one, add a new ADR that
supersedes it, or amend the existing one in place with a dated note — do
not silently edit history. The source repository's own `ADR-0009` is the
worked model: it was amended five separate times as reality diverged from
what it originally decided, each amendment dated and left visible rather
than folded invisibly into a rewrite.

If you believe an ADR here is wrong, say so and propose superseding it. Do
not silently deviate from a decided record.

## Standing product principles — do not renegotiate these

Carried forward unchanged from the source repository and restated as
explicit contract obligations in [ADR-0001](docs/adr/0001-core-to-skin-data-contract.md):

1. **Never fabricate a number.** Every value in the data contract carries a
   status flag — real, estimated, or unavailable. "Unavailable" renders as
   an explicit gap, never as zero or blank.
2. **Local machine only.** No data from other devices or cloud sessions.
3. **Read-only against every vendor data source.** Files belonging to
   Claude, or any future AI system's client, are read, never written.
4. **No credential or token handling.** Evaluated and rejected twice
   already in the source repository (`ADR-0002`, `ADR-0015`). Not open for
   reconsideration by adding code here.

## Gates this project runs against

Build work is authorized incrementally, gated by explicit board sign-off —
not assumed from a design document alone:

| Gate | Decides |
|---|---|
| G0 | This repository's existence, name, owner, visibility — **passed** |
| G1 | Target-architecture / PDR sign-off — **passed** |
| G2 | Per-slice merge approval |
| G3 | Platform ambition (Windows + Linux now, macOS deferred) |
| G4 | UI unification (keep two native windows, or unify) |
| G5 | Any second AI usage source — requires its own dedicated audit first |

Do not scope or begin work that a still-open gate would authorize. If
you're unsure whether a gate has passed, check the current PDR and ADR
trail rather than assuming.

## Working in this repository right now

Contributions are either documentation (ADRs, the data contract, the
capability matrix, feature docs) or an approved, gated implementation
slice. For code:

- Build and test with the solution file: `dotnet build O-view.slnx` and
  `dotnet test O-view.slnx`. `O-view.Core` and its test project target
  `net10.0` and are *intended* to build and test on a non-Windows runner —
  that is the enforcement mechanism Core's platform-neutrality is meant to
  rest on, not a formality. **No such run has ever happened.** This
  repository has no CI workflow — no `.github/`, no `.circleci/`, no
  `.gitlab-ci.yml`, `azure-pipelines.yml`, `.travis.yml`, `appveyor.yml`
  or `Jenkinsfile` (CONFIRMED by directory listing, 2026-09-23) — and
  nothing has built or tested this rebuild's code on non-Windows hardware.
  Until CI exists, the layering rule is held by review and by the
  structural tests in `O-view.Core.Tests`, not by a green non-Windows
  build. [ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)'s
  2026-09-10 amendment records the same thing ("this repository has no CI
  workflow yet"); whether to add CI, and what it would cover, is an open
  board decision, not something to settle in passing.
  `O-view.Tray` and its test project target `net10.0-windows` and only
  build on Windows. `O-view.Linux` and its test project also target
  `net10.0` and build on any runner, same as Core.
- **The anti-drift harness can only ever run on Windows.**
  `O-view.CrossSkin.Tests` (the
  [ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md) golden-master
  harness) targets `net10.0-windows` because it references `O-view.Tray`
  directly (CONFIRMED —
  `tests/O-view.CrossSkin.Tests/O-view.CrossSkin.Tests.csproj`). It is the
  one mechanism that proves the Linux skin's strings match the Windows
  skin's, and its target framework means **adding CI would not change
  this**: a non-Windows runner cannot build this project at all, so a
  Linux CI job would silently skip the very harness that covers the Linux
  skin. Anyone adding CI should read that as a stated constraint, not
  discover it from a red build — and should not assume a `ubuntu-latest`
  job covers the Linux skin's wording. This is a documented structural
  ceiling, not a defect to fix by changing a target framework; see
  [`tests/O-view.CrossSkin.Tests/README.md`](tests/O-view.CrossSkin.Tests/README.md)'s
  "Why this project targets `net10.0-windows`" for the mechanics.
- Follow the layering rule from the section above absolutely:
  `O-view.Core` gains no display strings, formatting, locale-sensitive
  formats, platform-imposed limits, OS-conditional branches, or platform
  APIs. If you're not sure whether something belongs in Core or a skin,
  it almost always belongs in the skin.
- A new Core-to-skin contract field is documented in
  [ADR-0001](docs/adr/0001-core-to-skin-data-contract.md) before a skin may
  consume it (per that ADR's own consequences section).
