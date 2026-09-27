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
| 0004 | *(what non-Windows CI could and could not prove)* | **Missing from `main` — see the note below** | Reviewed and merged as PR #19, but into a stacked base branch rather than `main`, so its content never landed. Recoverable; tracked separately. Number **not** reused. |
| [0005](0005-data-provider-contract.md) | The data-provider contract — how Core reads Claude's data off this machine | Proposed | Phase 2. One input seam (`IUsageProvider`), three named Claude providers, composition by information value, provider health as contract data. Carries a board question on whether "read-only against vendor data" permits invoking the vendor's own CLI. |
| [0006](0006-local-storage-contract.md) | What Core persists on this machine, where, and who owns each file | Proposed | Phase 2. Three stores, each justified by being unrecomputable; injected paths; corruption degrades to "not known yet". Carries the SQLite dependency question. |
| [0007](0007-app-shell-contract.md) | The app shell — a third layer between Core and the skins, and what it may not contain | Proposed | Phase 2. Names `O-view.App`, gives it an admission rule, and answers ADR-0001's deferred "where does the HTTP fetch go". Flags gate G4 rather than drifting into UI unification. |

## A gap in this trail: ADR-0004

ADR-0004 is not in this directory, and the gap is recorded rather than
tidied away. Its content was written, reviewed and merged (PR #19,
2026-09-26) — but that PR's base was the branch
`docs/ovi-109-ci-wording-correction`, not `main`, and it merged 81 seconds
*after* that base had itself merged to `main` and been deleted. The content
therefore reached a branch no longer on its way anywhere (CONFIRMED by
`gh pr view 19` and by `git ls-tree origin/main docs/adr/`).

The text is not lost — it survives at merge commit `fa561354` and can be
restored to `main` unchanged. Restoring it is tracked as its own task
rather than folded into an unrelated PR. The number 0004 is reserved for
it, not reused, so that the record it belongs to keeps the number its
review, its PR and any citation of it already use.

The general lesson is already this project's written rule ("branch from
current `main`, never from another open PR — no stacked PRs"); this entry
is what that rule cost the one time it was not followed.

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
