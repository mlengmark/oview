# Gate G7 — detail-window parity: build order, prerequisite, waiver candidates

- **Status:** the design this works through was **accepted by the board on
  2026-10-09** (OVI-591, card `4e515cef`), as one card covering the amendment
  text and this build order together. The amendment itself lives in the records
  it amends — [ADR-0008](../adr/0008-presentation-skin-contract.md) D4, D9e–D9h,
  D10, D11 and [ADR-0005](../adr/0005-data-provider-contract.md) D2, D6c, D7.
  This file is the build order and the open items beside it.
- **Date:** 2026-10-09 · drafted as OVI-585, transcribed into the repository by
  OVI-607
- **Directive:** OVI-584 (gate G7) — the detail window must reach design parity
  with `mlengmark/O-view` before the rebuild is "done". OVI-584's checklist A–K
  is the acceptance list; see ADR-0008 D10a.
- **Acceptance is not authorisation for a slice.** Each row below is its own
  issue, its own PR and its own review, exactly as every other slicing table in
  this repository.
- **Evidence standard:** as [ADR-0002](../adr/0002-cross-platform-capability-matrix.md).
  Nothing here is observed on real hardware, and merging a row proves tests
  passed, never that a desktop works.

## 1. The one blocking prerequisite — a rate card

**This repository has no rate card, so no "Est. value" figure can be rendered at
all.** CONFIRMED by reading
`src/O-view.Core/Statistics/LedgerUsageStatisticsSource.cs`: its own header says
`RateCardSource` "is reserved, nothing emits a `RateCardStamp` yet", and every
`EstimatedUsd` it produces is `UsageValueStatus.Unavailable`.
[ADR-0005](../adr/0005-data-provider-contract.md) D6b rejected building one, by
name: "its own decision with its own record, it needs vendor figures nobody here
has confirmed".

Checklist items that therefore **cannot** be met, however well they are
rendered: §D's two value tiles (`Est. value today`, `Est. value over 31 days`),
§D's `excl. N unpriced` relief, §D's off-plan relabel to `Est. spend today`,
§E's per-segment estimated values, and §G's standing `Off-plan usage · last 31
days` figure. They render an honest gap — which is correct behaviour and is not
parity.

This is **not a waiver candidate**: nobody is proposing to drop the feature. It
is a missing prerequisite that only the board can authorise, because it trades
scope and rests on vendor pricing figures whose provenance someone must own. It
needs its own record and its own card. The build order below sequences the
affected slices so they can merge either way: each renders the unavailable state
first, and the figures light up when the rate card lands.

## 2. Waiver candidates — one card each; until a card is accepted the item stays required

**W1 — §I's hover timing may not be expressible on Linux.** 400 ms initial
delay, 3 000 ms between-show, 20 s show duration are three WPF `ToolTipService`
properties. *What the source does:* sets all three, per element, on Windows.
*Proposed instead:* 400 ms on both platforms; between-show and show-duration on
Windows; on Linux, whatever the toolkit actually exposes, with the shortfall
written into ADR-0002 as a named platform limit rather than faked. *What the
user loses:* on Linux, sliding along bar segments may re-wait per segment, and a
card may dismiss on the toolkit's schedule.

> **2026-10-10 verification done (OVI-621, slice P2).** The INFERRED label
> above is resolved to **CONFIRMED**, in the direction this card already
> expected: `Avalonia.Controls.dll` 12.1.3 exposes `ToolTip.ShowDelay` and
> `ToolTip.BetweenShowDelay` — but **no `ShowDuration` property exists
> anywhere in the assembly**, under any name, attached or otherwise.
> Avalonia's tooltip closes on pointer-exit only and exposes no hook to cap
> or extend that. ADR-0002 now carries this as a named row (2026-10-10
> amendment) rather than an open question. **This slice does not build a
> custom popup/timer reimplementation to fake the 20 s cap** — that is
> exactly the "shortfall... faked" this card was written to avoid. **The
> card itself is still open** — this only supplies the verification it was
> waiting on.
>
> **2026-10-10 — the card is accepted (OVI-593, Option A) and applied
> (OVI-665).** `HoverCard.ApplyTiming` on Linux applies only the 400 ms
> initial delay. `BetweenShowDelay` is deliberately left unset rather than
> forced to the Windows 3000 ms, so it resolves to the toolkit's own unset
> default — measured **100 ms**, CONFIRMED against `Avalonia.Controls`
> 12.1.3 — exactly what "Linux uses what the toolkit actually offers" above
> asked for. `HoverCardTests` pins both that the initial delay resolves per
> element, not by inheritance, and that the between-show delay is never
> forced to a local value. See ADR-0002's "Hover card timing" row.

**W2 — §D/§I's hover-only per-model figures are an accessibility defect the
source documents as a known limitation.** Porting it ports the defect. This is
*not* a request to drop anything — the hover cards stay exactly as specified.
The card asks to **add** keyboard-reachable focus on the legend entries carrying
the same figure in the accessible name. Under "at least parity" an addition needs
no waiver; if the board agrees it is simply a line in slice P11.

**W3 — §F's majority-shading of a boundary-crossing day.** The source shades a
day that contains a weekly boundary with whichever band holds the majority of the
day, and records that the split is unrecoverable from daily rollups. That is an
approximation affecting colour only, and it is defensible — but it is the one
place the panel's colour asserts something the data does not support. *Proposed
alternative, if the board wants it:* shade such a day neutrally, so no week
claims it. *What the user loses:* a one-column visual discontinuity in the ramp,
twice a month. **Recommendation: no waiver — port the source's majority rule.**
Listed because a reviewer will ask, and the answer should already be written
down.

## 3. Build order — one slice, one mergeable PR, lowest-risk first

Sequencing rule: the proof mechanism first, then no-contract-change skin work,
then the contract, then the surfaces that need it, then the adjacent surfaces.
**Every slice carries [ADR-0008](../adr/0008-presentation-skin-contract.md)
D11a's render-proof obligation for the states it touches** — stated once here
rather than repeated in twenty-three rows. Merge labels follow OVI-584's rule:
anything touching `UsageDetail`, `ISkinToShell`/`IShellToSkin` or an ADR is a
**board** merge.

| # | Slice | §A–K | Merge | Tests it owes beyond its own unit tests | Depends on |
|---|---|---|---|---|---|
| **P0** | Offscreen render hook in both skins, writing the current panel in both themes | — (D11a) | agent | that every required state name resolves to a file | — |
| **P1** | Off-screen fallback for a stale remembered position, both skins + cross-skin agreement test | A | board (ADR-0008 D4) | partial-intersection = fallback; no-geometry = centre (Linux); request-vs-granted logged | — |
| **P2** | The one styled hover card: two shapes (`Figure`, `Text`), both skins, no bare system tooltips | I | agent | `HoverTimingFixture` — 400/3000/20 000 ms resolving **on each element** | P0 |
| **P3** | Panel chrome: ~400 px width, light/dark palettes, theme re-read on open, 4.5:1 accent | I | agent | contrast assertion on the accent against both surfaces | P0 |
| **P4** | Icon-click toggle incl. the 400 ms close-then-click rule; Windows foreground + `AttachThreadInput`; Esc | A | agent | toggle timing at 399/401 ms; never-stuck assertion | — |
| **P5** | **Contract:** D9e's four `UsageDetail` members + two `UsageSnapshot` boost members, with `Unavailable` sentinels | — | **board** | sentinel-default test per member; no skin reads it yet | — |
| **P6** | **Ledger:** `GetDailySeries` + `GetTokenKindTotals`; reset-boundary derivation and its three kinds; shell assembles | — | **board** (ADR-0005) | DST window (23/25 h days); no-anchor → Monday-fallback kind; absent day ≠ zero day | P5 |
| **P7** | `IAccountIdentitySource` reading `oauthAccount` only | B | **board** (ADR-0005 D7) | reads no other key in the file; missing/malformed file → `Unavailable` | P5 |
| **P8** | Header: title, freshness line, account block + tier badge | B | agent | `AccountIdentityFixture` | P3, P7 |
| **P9** | Usage bars: proportional fill, 50/70 bands, both reset formats, weekly-unknown hover + `/usage` copy | C | agent | band boundaries at 49/50/69/70; the three weekly states | P3 |
| **P10** | Statistics tiles 2×2, coverage caption, no-I/O flip, **size stability**, affordance glyph, disabled tile | D | agent | tile size identical in both views; breakdown built in populate, not on click; `CoverageCaptionFixture` | P3, P5 |
| **P11** | Model colour: Core-ranked order, three chromatic slots, the "Other" fold, legend names-only, unpriced relief | D | agent | `ModelColourOrderFixture` — same slot on all four tiles at 1/2/3/4/5 models | P10 |
| **P12** | **Built 2026-10-10 (OVI-667).** Token-kind bars for today and 31 days, per-segment hover cards, breakdown table behind the view switch | E | agent | `TokenKindFixture`; share text against carried `Total` | P6, P2 |
| **P13** | The 31-day graph: one column per local day, per-week intensity ramp, vertical centred date labels, blank columns | F | agent | `DailySeriesFixture`; label centring from measured transform bounds, not a constant | P6 |
| **P14** | Reset gridlines: amber dotted, drawn **last**, true fractional position in the day, kind on hover | F | agent | `ResetBoundaryFixture`; a Monday-fallback line lands on a column edge, a plan line inside one | P13 |
| **P15** | Off-plan live banner: three wordings, provenance clause, link to Claude's usage settings | G | agent | `OffPlanBannerFixture` | P3 |
| **P16** | Notify once per onset, via ADR-0009's existing event decider; no Est. figure where nothing can be billed | G | agent | one notification per onset, not per poll | P15 |
| **P17** | Standing `Off-plan usage · last 31 days` section, two-clause caption, coverage caveat | G | agent | renders the unavailable state until the rate card exists (§1) | P15 |
| **P18** | "No usage data" banner — what was checked, what was observed, never a claim about the machine | H | agent | wording fixture; no machine-state assertion | P3 |
| **P19** | "Why so large?" disclosure fold: 191 ms / 125 ms, `KeySpline(0.02,0.16 0.20,0.96)`, 180° chevron, no fade | H | agent | fold geometry: no jump, no overshoot; grows upward when the work area would clip it | P3 |
| **P20** | **Cowork audit-log ingest source** on the JSONL path, writing to the ledger | J | **board** (ADR-0005 D2) | absent Cowork → unchanged figures; ingest is idempotent | P6 |
| **P21** | Windows branded menu flyout: ~272 px card, 34 px rows, header with mark and version; **docked to the work-area corner**, four taskbar positions, auto-hide, cursor picks the monitor only | K | board (ADR-0009) | never placed at the pointer; all four taskbar edges; toggle via P4's 400 ms rule | P4 |
| **P22** | Windows menu rise: clip reveal + last 20 px of slide, 230/150 ms, same keyspline, downward with a top taskbar, reopen cancels close | K | agent | close completes before hide; reopen mid-close cancels | P21 |
| **P23** | Menu row set on both skins — Run at startup, Update automatically (installed builds only), notify-at 70/80/90 %, Copy diagnostics, **Weekly reset…**, Check for updates…, Exit; toggles keep it open, actions close first, ticks show the state **after** the write | K | agent | tick reflects the post-write state, not the request; Linux keeps the host's native menu | P21 |
| **P24** | Branded dialogs replacing `MessageBox`, including the weekly-reset entry dialog | K | agent | Esc/Enter; primary names the action; foreground handling | P3 |
| **P25** | Branded installer: `brand/` wizard images, Welcome page enabled, own copy | K | **board** (packaging) | installer builds; images present | — |
| *(not a parity slice)* | Correct the README's "31-day usage graph" claim and the board digest's "done" wording | OVI-584 item 5 | agent | — | — |

**On P21 and D4.** The menu docking is **not** a reversal of
[ADR-0008](../adr/0008-presentation-skin-contract.md) D4 and needs no icon
rectangle: OVI-584 §K places it at *the work-area corner next to the taskbar*,
which is the same geometry D4 already uses for the widget's first placement, and
explicitly **not** at the pointer and **not** under the icon. So
`Shell_NotifyIconGetRect` stays unused, as D4 requires, and the board's "the menu
is not draggable" decision sits alongside D4 rather than against it. Stated
because a reviewer will read "docked" as the thing D4 rejected.

## 4. Corrections this design made to OVI-584's own inferred claims

OVI-584 labelled its "Contract consequences" section inferred and asked for each
line to be checked against the code. The checks, kept here so a reader of the
amendment can see what moved and why:

| OVI-584 said | Verdict |
|---|---|
| D4 stays as written; add the off-screen fallback | **Confirmed.** `DetailWindowPositionController.ResolveShowPosition()` returns a saved position with no screen check in either skin (CONFIRMED — read both files). |
| `UsageDetail` must carry account identity, a 31-day series, reset boundaries, token-kind totals | **Confirmed.** `UsageDetail` is `(Snapshot, Statistics, Models)` and nothing else (CONFIRMED — `src/O-view.Core/Models/UsageDetail.cs`). |
| Boost notices belong on `UsageDetail` / D9 | **Corrected.** They belong on **`UsageSnapshot`** — a boost is a plan-meter fact read live from the vendor cache, on the same path as the percentages, and ADR-0001's 2026-09-21 entry already reserved `SessionBoostNotice`/`WeeklyBoostNotice` there. On `UsageDetail` the boost would be invisible to the tooltip and one fact would have two homes. |
| The shell keeps assembling; the per-week colour scale and gridline placement are presentation | **Confirmed and sharpened** — see ADR-0008 D10b. The split needed one rule the existing records did not state: a **proportion of two carried figures** is presentation; a **sum or average over vendor rows** is Core's. |
| A Cowork provider behind `IUsageProvider` is in scope for ADR-0005 | **Partly corrected.** Cowork audit logs are token counts from transcript-shaped records, not a plan meter, so they reach the tiles and the graph through the **ledger** (ADR-0006), not through `UsageSnapshot` — an ingest reader on `JsonlUsageProvider`'s path. See ADR-0005 D2's 2026-10-09 amendment. |
| New ADR-0003 fixtures for every new content fact | **Confirmed.** Eight families, in ADR-0008 D11b. |
| — not stated — | **The 31-day graph's bar is output tokens, not all-kinds.** The source uses `day.OutputTokens` (CONFIRMED — `PopupWindow.xaml.cs:1105–1107`), and its own `PanelStatistics` comment records that the field was renamed *away from* `TotalTokens` because "a bar sized by output under a name [saying total]" lied. The contract field is therefore `OutputTokens`. A builder who writes `TotalTokens` here reintroduces a defect the source already fixed. |
| — not stated — | **"About 400 px wide" does not contradict the 281 px figure** in ADR-0001's 2026-09-21 entry. The source's `docs/ui-spec.md` §2 says "roughly 400 px wide" (CONFIRMED) and 281 px is a *text-row truncation budget inside* that window, not the window. Named so nobody reconciles two numbers that were never in conflict. |
