# Architecture Decision Records

Records of significant architectural decisions for the Oview rebuild of
O-view: the context, the choice, the alternatives, and the consequences.

These are **decided, not drafts.** To change one, add a new ADR that
supersedes it, or amend it in place with a dated note — do not silently
edit history. See [`CLAUDE.md`](../../CLAUDE.md) for the full discipline.

| # | Title | Status | Summary |
|---|---|---|---|
| [0001](0001-core-to-skin-data-contract.md) | The Core-to-skin data contract, as a living document | Accepted | Every value Core can hand a skin: type, unit, real/estimated/unavailable status flag. Cross-references the confirmed presentation-string leaks in the source repository's `O-view.Core.Models`. |
| [0002](0002-cross-platform-capability-matrix.md) | The cross-platform capability matrix, as a living document | Accepted | Per OS capability, what every skin must provide and what each platform currently, actually guarantees — confirmed / confirmed-narrow / inferred / never-observed, unchanged from OVI-4's verification. |
| [0003](0003-paneltext-anti-drift-mechanism.md) | Cross-skin wording golden-master tests replace `PanelText.cs`'s centralization | Accepted | Resolves PDR board question 0. Pins the content facts each skin's wording must state for a given figure, not the exact string — preserving per-skin ownership of wording while catching issue #55/#56-style drift. Flags a new shared test project as a cost for Chief Gary II to scope. |

## Source material

This project's evidence trail, cited throughout the ADRs above:

- The approved PDR (rev. 2, document key `oview-pdr-reissued`) — target
  architecture, board questions, and their recommendations.
- Rae II the Analyst's OVI-4 verification findings (2026-09-08) — the
  file-by-file, line-numbered confirmation of every presentation-string
  claim these ADRs cite.
- [`github.com/mlengmark/O-view`](https://github.com/mlengmark/O-view) —
  the read-only source repository, at commit `897777b` as verified by
  OVI-4. Its own [`docs/adr/`](https://github.com/mlengmark/O-view/tree/main/docs/adr)
  (16 records), `README.md`, and `CLAUDE.md` are cited by number
  throughout (e.g. "source repo ADR-0009") and are the documentation bar
  this repository is expected to meet or exceed.

## Format

Each record carries: Status · Date · Deciders · Context · Decision ·
Alternatives considered · Consequences (positive **and** negative). Every
factual claim within a record carries an evidence label — **CONFIRMED**
(read, run, or grepped against the actual source) or **INFERRED**
(reasonable, not verified) — carried forward from its source rather than
asserted fresh.
