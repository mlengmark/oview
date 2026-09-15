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
  `net10.0` and must build and test on a non-Windows runner — that is
  Core's platform-neutrality enforcement mechanism, not a formality.
  `O-view.Tray` and its test project target `net10.0-windows` and only
  build on Windows. `O-view.Linux` and its test project also target
  `net10.0` and build on any runner, same as Core. `O-view.CrossSkin.Tests`
  (the [ADR-0003](docs/adr/0003-paneltext-anti-drift-mechanism.md)
  golden-master harness) targets `net10.0-windows` because it references
  `O-view.Tray` directly, so it only builds and runs on Windows too.
- Follow the layering rule from the section above absolutely:
  `O-view.Core` gains no display strings, formatting, locale-sensitive
  formats, platform-imposed limits, OS-conditional branches, or platform
  APIs. If you're not sure whether something belongs in Core or a skin,
  it almost always belongs in the skin.
- A new Core-to-skin contract field is documented in
  [ADR-0001](docs/adr/0001-core-to-skin-data-contract.md) before a skin may
  consume it (per that ADR's own consequences section).
