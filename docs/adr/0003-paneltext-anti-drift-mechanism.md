# ADR-0003: Cross-skin wording golden-master tests replace `PanelText.cs`'s centralization

- **Status:** Accepted — design decision. Implementation (the test project
  and its fixtures) is not built by this ADR; it is a dependency of Kit the
  Builder's display-string extraction slice. **Cost/tooling note:** this
  decision introduces a new shared test project — see "Consequences" and
  the escalation note at the end. That cost is flagged to Chief Gary II for
  scoping; this ADR decides the mechanism, not the build ticket.
- **Date:** 2026-09-08
- **Deciders:** Adrian II the Architect, resolving board question 0 of the
  approved PDR (rev. 2, `oview-pdr-reissued`, §8)
- **Resolves:** PDR board question 0 — *"does the board want to overturn
  `PanelText.cs`'s centralization, and if so, what replaces the guarantee
  it was providing (no two skins wording the same figure differently)?"*
  The board's recommendation, approved at gate G1, was to proceed with
  extraction **conditional on** Phase 1 defining a concrete replacement
  mechanism first. This ADR is that mechanism.

## Context

`PanelText.cs`, in the source repository, is not an oversight. Its own
header doc comment states its centralization inside
`O-view.Core.Models` is deliberate, citing
[issue #55](https://github.com/mlengmark/O-view/issues/55) and
[issue #56](https://github.com/mlengmark/O-view/issues/56) — two panels
(the Windows WPF head and the Linux Avalonia head, per source repo
ADR-0013) wording the same figure differently — as the failure it exists
to prevent. Rae II's OVI-4 verification pass confirmed this is a real,
argued position, not a leak nobody noticed: **CONFIRMED**, quoting the
file's own reasoning: *"Two panels wording those differently is the same
failure as two panels computing them differently, and issues #55 and #56
were both exactly that."*

[ADR-0001](0001-core-to-skin-data-contract.md) requires this centralization
to end — Core must never emit display text, by contract. That is not in
question here. What is in question, and what the board explicitly
conditioned extraction on, is: **once `PanelText.cs`'s single shared
implementation is gone, what stops the exact bug it was built to prevent
from recurring?**

Two candidate mechanisms were on the table per the PDR's board question 0:
a shared, non-Core text-resource module both skins consume, or a
golden-master cross-skin wording test. This ADR picks one and rejects the
other, with reasons.

## Decision

**Adopt a golden-master cross-skin wording test as the anti-drift
mechanism. Do not introduce a shared text-resource module.**

### What this means concretely

1. **A versioned set of golden-master fixtures**, each a canonical Core
   snapshot (a fully populated instance of the [ADR-0001](0001-core-to-skin-data-contract.md)
   contract's fields) paired with the **content facts** every skin's
   rendering of that snapshot must state, not the exact string each skin
   must produce. For example, for a snapshot with
   `SessionUtilizationPercent = 57.0`, `SessionResetAt = 20:59 local`,
   `WeeklyUtilizationPercent = 14.0`, `WeeklyResetAt = Mon 23:00 local`,
   the golden master pins: the session figure must render as `57%`
   (rounding rule: integer, no decimal); the weekly figure must render as
   `14%`; both reset times must render in the viewer's local time; and if
   `DataSourceKind` is `JsonlFallback` or `Estimate`, the rendered text
   must contain an estimate/fallback disclosure — it does not pin the
   exact sentence used to say so. One fixture reuses OVI-4's actual
   observed runtime output (`5h: 57% · resets 20:59 · 7d: 14% · resets Mon
   23:00`) as a known-good reference point.
2. **Each skin's own string-construction code is exercised directly**
   against each fixture's Core snapshot — the same functions that replace
   `TooltipFormatter`/`PanelText`/`UsageFormatter` once extracted into
   `O-view.Tray` and `O-view.Linux` respectively — and the resulting
   strings are checked against that fixture's pinned content facts.
3. **A skin fails the test if it omits a pinned fact, contradicts one
   (e.g. states a different percentage for the same input), or applies a
   different rounding/labelling rule than the fixture specifies** — not if
   it phrases the sentence differently from the other skin. Two skins are
   still free to say "Est. spend today" and "Estimated spend (today)"
   differently; they are not free to have one say "57%" and the other
   "58%" for the same input, or to have one silently drop the
   "estimated" disclosure the other keeps.
4. **Changing a pinned content fact is a reviewed, dated change to the
   fixture file** — the same discipline this repository already applies to
   ADRs. A silent one-skin edit that happens to change what the fixture
   pins would fail CI; changing what's pinned is a visible diff a reviewer
   sees.

### Why this, and not a shared text-resource module

A shared, non-Core module both skins import for their wording would
recentralize exactly what [ADR-0001](0001-core-to-skin-data-contract.md)
just moved out of Core — only one layer further out. That conflicts with
the target architecture's own stated ownership rule (PDR §3.2): *"A skin
owns, independently of every other skin: exact wording/phrasing/formatting
of everything the user reads."* A shared module means neither skin fully
owns its wording; it would also reintroduce cross-skin coupling that the
source repository's own ADR-0013 deliberately avoided when it chose two
independent native UIs specifically to avoid destabilizing the shipped
Windows app while adding the Linux head. Recoupling the two skins' text
through a shared dependency cuts directly against that reasoning.

A golden-master test enforces the *invariant* `PanelText.cs` was
protecting — no two skins disagree on the facts a figure states — without
requiring either skin to give up ownership of how it says those facts.
This is also closer to how the source repository already catches this
class of bug elsewhere: it relies on structural/unit tests plus documented
review discipline (`CLAUDE.md`'s ADR-following rule), not runtime shared
code, to keep the two heads honest against each other.

## Alternatives considered

**Shared non-Core text-resource module, consumed by both skins.**
Rejected — see above. It solves consistency by recentralizing ownership,
which the target architecture's skin-ownership principle and the source
repository's own reasoning for splitting the UI (ADR-0013) both argue
against. It would also reintroduce a single point of failure: a bug or
platform-inappropriate assumption in the shared module (the exact failure
mode `PanelText.cs`'s 281px WPF-specific budget already demonstrates) would
now affect every skin at once, rather than being contained to the skin
that introduced it.

**No mechanism — extract, and rely on manual review to catch wording
drift.** Rejected — this is close to the status quo before `PanelText.cs`
existed, and issues #55/#56 are direct evidence that manual review alone
did not catch this class of bug the first time. The board's conditional
approval explicitly required something concrete here, not a process
reminder.

**A snapshot/byte-diff test requiring both skins to produce identical
strings.** Considered and rejected as too strict: the two skins have
different, legitimate platform constraints (the 127-character Windows
tooltip cap has no Linux equivalent; panel pixel widths differ by toolkit)
that will and should produce different exact text for the same figure.
Pinning exact strings would either force artificial parity that fights
real platform differences, or immediately need per-platform exceptions
that erode the test's value. Pinning *content facts* instead of exact
strings is why this ADR's mechanism checks facts, not bytes.

## Consequences

**Positive:**
- The specific, already-realized failure mode (#55/#56 — two panels
  disagreeing on what a number means) has an automated check, not just a
  design intention.
- Each skin keeps full ownership of its own wording, consistent with
  [ADR-0001](0001-core-to-skin-data-contract.md)'s contract boundary and
  the source repository's stated reasons for two independent UIs.
- The fixture set doubles as living documentation of what every displayed
  figure is supposed to mean — useful independently of the test passing.

**Negative, and the escalation this ADR flags:**
- This requires a **new shared test project** (or equivalent test
  infrastructure) capable of constructing Core-contract snapshots and
  invoking both `O-view.Tray`'s (net10.0-windows) and `O-view.Linux`'s
  (net10.0) string-construction code from one place. That is a build/CI
  tooling decision — which project, which CI runner(s) it needs (a
  combined test referencing the Windows-only `O-view.Tray` head likely
  needs to run on the same Windows CI runner the Tray build already uses,
  not the `ubuntu-latest` runner the source repository uses for
  Core/App) — and is **beyond what this documentation-only task
  authorizes**. Per this role's boundaries, that cost is flagged to Chief
  Gary II for scoping into Kit the Builder's extraction slice, rather than
  decided unilaterally here.
- The fixture set needs initial content (a representative set of Core
  snapshots covering ordinary usage, near-limit usage, estimated/fallback
  data, and unavailable data) before the test can catch anything. Building
  that set is part of the extraction slice this ADR unblocks, not this
  ADR itself.
- A golden-master test only catches drift between the two skins *at the
  fixtures it covers*. It does not replace design-time judgment about
  which facts are safety-critical enough to pin — that judgment starts
  here, with this ADR's fixture examples, and should grow as real drift
  incidents (hopefully none) or near-misses are found.
