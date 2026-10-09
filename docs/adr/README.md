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
| [0004](0004-what-non-windows-ci-could-and-could-not-prove.md) | What non-Windows CI could and could not prove for O-view | Accepted | A Linux CI job runs Core, Core.Tests, Linux and Linux.Tests, not the ADR-0003 anti-drift harness or the Tray skin, which are Windows-only. Recommended a two-job workflow (Windows: whole solution; Linux: the `net10.0` projects) and no harness restructuring — approved and live in `.github/workflows/ci.yml` (OVI-124); see the record's 2026-09-27 amendment. |
| [0005](0005-data-provider-contract.md) | The data-provider contract — how Core reads Claude's data off this machine | Accepted 2026-09-28; D6c extended 2026-10-02 (OVI-326) | Phase 2. One input seam (`IUsageProvider`), three named Claude providers, composition by information value, provider health as contract data. Opened **gate G6**: the app may invoke the vendor's own documented, read-shaped command so the vendor refreshes its own cache — under three limits stated in the record. |
| [0006](0006-local-storage-contract.md) | What Core persists on this machine, where, and who owns each file | Accepted 2026-09-28 | Phase 2. Three stores, each justified by being unrecomputable; injected paths; corruption degrades to "not known yet". SQLite authorised as this repository's first third-party runtime dependency, for the usage ledger only. |
| [0007](0007-app-shell-contract.md) | The app shell — a third layer between Core and the skins, and what it may not contain | Accepted 2026-09-28; D6 seam widened 2026-10-02 (OVI-326) | Phase 2. Names `O-view.App`, gives it an admission rule, and answers ADR-0001's deferred "where does the HTTP fetch go". Gate G4 stays closed: no shared widget, no shared window base class. Its G4 flag is now answered — see 0008. |
| [0008](0008-presentation-skin-contract.md) | The presentation contract — four surfaces, two native skins, no shared UI layer | Accepted 2026-10-02 (board merged PR #58); amended 2026-10-02 (D9, OVI-326) | Phase 3. Implements gate **G4 = A** (two native windows, accepted 2026-10-02): WPF on Windows, Avalonia on Linux, nothing presentational in the shell. Defines what the status icon, tooltip, detail window and alerts must each state; makes the draggable widget the design on both platforms and declines `Shell_NotifyIconGetRect`; carries source ADR-0013's session-bus probe forward as mandatory. **D9** adds the detail window's data path — `ShowDetail(UsageDetail)` plus `RequestWidget(bool)` — because snapshot-only could not carry the statistics or the per-model split; 13 slices, Windows first. **D2 is now five surfaces, not four** — see its 2026-10-05 amendment and 0009. |
| [0009](0009-control-surface-contract.md) | Phase 4A — the control surface: one menu, three settings, and a theme the OS owns | Accepted 2026-10-05 (board merged PR #76) | Phase 4A under the board's option C (OVI-423), the first of two Phase 4 amendments; packaging and self-update are the second (OVI-433). Adds the **menu** as ADR-0008's fifth surface and declines a separate settings window, with the conditions that would re-open that. Adds **no seam member** — `ISkinToShell` already carries every command the menu needs; what is missing is a shell that implements it, a settings file that is actually loaded, and the ADR-0007 D2 point 6 event decision the threshold picker would otherwise control nothing with. Run-at-startup is read live from the OS and renders what the OS did, not what was clicked. `IThemeSource` is the one new interface: three values including **unknown**, no colour anywhere outside a skin. 10 slices, shell first, Windows before Linux. |
| [0010](0010-update-execution-and-packaging-contract.md) | Acting on an update, and shipping the thing that acts — install kind decides, verification fails closed | Accepted 2026-10-05 (board merged PR #77) | Phase 4B, the second of the board's two option-C amendments (card `80339fe7`, OVI-423); the first is 0009. Answers 0008 D8's deferral of packaging and self-update. How the build arrived (`InstallKind`) is the only thing permitting a download or a launch, detected per-skin and decided by one pure table in the shell; **detection is separated from permission**, carrying forward the source's shipped bug that left Linux with no update path at all. Checksum verification **fails closed and ships in the same slice as the download**. `AutoUpdateEnabled` means *check* automatically, never *install* automatically. Declines update channels, a proxy setting and an apt repository; raises the signing/attestation tradeoff to the board as an open question. 10 slices, six of them board merges. |

## A note on ADR-0004's dates

0004 was written, reviewed and merged on 2026-09-26 — but into another pull
request's branch rather than into `main`, 81 seconds after that base had itself
merged and been deleted, so its content never landed. The text was never lost
(it survived at merge commit `fa561354`) and reached `main` unchanged on
2026-09-28 as its own task, OVI-179, rather than folded into an unrelated PR.
The number 0004 was reserved for it, not reused, so the record keeps the number
its review, its PR and every citation of it already use. Its header date is the
date it was decided, not the date it landed here; see the note at the head of
the record.

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
