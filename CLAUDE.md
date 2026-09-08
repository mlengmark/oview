# CLAUDE.md — Oview (O-view rebuild)

Guidance for AI coding assistants and human contributors working in this
repository. Read this before writing code or documentation here.

## What this is

The rebuild of [`mlengmark/O-view`](https://github.com/mlengmark/O-view), a
desktop notification-area app that reports Claude AI token usage. This
repository is currently **documentation only** — no `O-view.Core`,
`O-view.App`, `O-view.Tray`, or `O-view.Linux` equivalent exists here yet.
Gate G0 (this repository) and gate G1 (target-architecture sign-off) have
both passed board review; see [`README.md`](README.md) and
[`docs/adr/`](docs/adr/) for what that approved.

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

Until the first implementation commit lands, contributions here are
documentation: ADRs, the data contract, the capability matrix, and
plain-language feature documentation. When implementation begins, this
file will be extended with the layering rules, build/test commands, and
platform constraints that govern code — following the model (but not
necessarily every specific rule) of the source repository's own
`CLAUDE.md`.
