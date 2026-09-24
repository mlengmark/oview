# O-view.CrossSkin.Tests

ADR-0003's anti-drift harness. It builds a versioned set of golden-master
fixtures — a canonical `UsageSnapshot` paired with the content facts every
skin's rendering of it must state — and runs each fixture against **every**
skin's own string-construction code from one `dotnet test` invocation. See
[`docs/adr/0003-paneltext-anti-drift-mechanism.md`](../../docs/adr/0003-paneltext-anti-drift-mechanism.md)
for why this mechanism (pinning facts, not exact strings) replaces
`PanelText.cs`'s centralization guarantee.

**This slice ships the harness and one smoke fixture only** — proving the
mechanism actually invokes both skins' code and actually fails on a real
mismatch. It does not retrofit every existing `TooltipFormatter` test, and
it does not extract `PanelText.cs`, `UsageFormatter.cs`, or
`PanelStatistics.cs` (those are separate, later slices this harness
unblocks).

**2026-09-10 update (OVI-27) — a second, parallel fixture family.**
`UsageFormatter.cs`/`PanelStatistics.cs`'s extraction needed fixtures over
a differently-shaped Core snapshot (`UsageStatistics`, not `UsageSnapshot`).
`Fixtures/UsageStatisticsFixture.cs`, `Fixtures/UsageStatisticsSkinUnderTest.cs`,
and `UsageStatisticsGoldenMasterCrossSkinTests.cs` mirror
`GoldenMasterFixture`/`SkinUnderTest`/`GoldenMasterCrossSkinTests` exactly,
parallel to them rather than a change to them — see ADR-0003's 2026-09-10
(OVI-27) amendment for why, and its flagged concern about a third such
family. `ContentFact` is shared as-is between both families; it was
already snapshot-agnostic.

**2026-09-11 update (OVI-29) — two more parallel fixture families.**
`PanelText.cs`'s `Freshness`/`Countdown`/`SessionReset`/`WeeklyReset`/
`WeeklyResetConflict` family needed fixtures shaped around inputs
`GoldenMasterFixture` doesn't carry:
- `Fixtures/FreshnessFixture.cs`, `Fixtures/FreshnessSkinUnderTest.cs`, and
  `FreshnessGoldenMasterCrossSkinTests.cs` mirror `GoldenMasterFixture`/
  `SkinUnderTest`/`GoldenMasterCrossSkinTests`, plus the one extra `UtcNow`
  scalar `Freshness` needs that the tooltip's `Format` delegate never did.
- `Fixtures/PanelTextResetFixture.cs`, `Fixtures/PanelTextResetSkinUnderTest.cs`,
  and `PanelTextResetGoldenMasterCrossSkinTests.cs` cover `Countdown`,
  `SessionReset`, `WeeklyReset`, and `WeeklyResetConflict` — four raw-scalar
  members that don't take a `UsageSnapshot` at all, and don't share one
  input shape with each other either. Rather than four separate
  fixture/skin/test trios, `PanelTextResetFixture.Render` closes over
  whichever one `PanelTextResetSkinUnderTest` member and inputs a given
  fixture exercises.

See ADR-0003's 2026-09-11 (OVI-29) amendment for the reasoning. Both new
families are parallel and additive; `GoldenMasterFixture`/`SkinUnderTest`
were not touched or widened.

**2026-09-21 update (OVI-92) — a fifth family, for `BoostChip`/`BoostCard`.**
`Fixtures/BoostNoticeFixture.cs`, `Fixtures/BoostNoticeSkinUnderTest.cs`,
`Fixtures/BoostNoticeFixtures.cs`, and `BoostNoticeGoldenMasterCrossSkinTests.cs`
follow `PanelTextResetFixture`'s member-selecting-`Render` shape: `BoostChip`
and `BoostCard` share one input shape (a `BoostNotice`, one `DateTimeOffset`,
one `TimeZoneInfo`) but check different content facts, so one fixture type
closes over which member a given fixture exercises rather than needing two
near-identical trios. See ADR-0003's 2026-09-21 (OVI-82/OVI-92) amendments
for the sign-off trail and the fixture set.

**2026-09-22 update (OVI-98) — a sixth family, for `RateLimitedNotice`.**
`Fixtures/RateLimitedNoticeFixture.cs`, `Fixtures/RateLimitedNoticeSkinUnderTest.cs`,
`Fixtures/RateLimitedNoticeFixtures.cs`, and `RateLimitedNoticeGoldenMasterCrossSkinTests.cs`
mirror `FreshnessFixture`'s single-member fixed-field shape, not
`PanelTextResetFixture`'s or `BoostNoticeFixture`'s member-selecting-`Render` shape:
`RateLimitedNotice` is a single fixed two-scalar shape (`DateTimeOffset?`,
`TimeZoneInfo`) and the only member of its own family, so there was no
multi-member closure to build. This reuses an already-reviewed shape rather
than introducing a new one, so it did not need a fresh Chief Gary II/Quinn
sign-off under ADR-0003's "third differently-shaped family" rule. Two
fixtures pin: the formatted retry time when GitHub sent one, that no clock
time is rendered when it did not, and — in both cases — that the notice
reassures the reader their own connection/install is not at fault. See
ADR-0003's 2026-09-22 (OVI-98) amendment.

## Why this project targets `net10.0-windows`

`O-view.Tray` targets `net10.0-windows`. A `net10.0-windows` project
can reference a `net10.0` project (`O-view.Linux`, `O-view.Core`) without
issue, so targeting `net10.0-windows` here is what lets one project — and
one `dotnet test` run — invoke both skins' code. The consequence, flagged
by ADR-0003 itself: this harness is declared Windows-only, same as
`O-view.Tray` and `O-view.Tray.Tests`.

"Declared" is the precise word today. This section used to say the
harness "only builds and runs on Windows" and that a Linux-only runner
"will not build [it] at all". **That was wrong** (corrected 2026-09-24,
OVI-126). `dotnet test O-view.slnx` on `ubuntu-latest` (.NET SDK
10.0.401) built this project and passed all 6 of its tests, alongside
`O-view.Tray` and `O-view.Tray.Tests` (CONFIRMED:
[run 35984892283](https://github.com/mlengmark/oview/actions/runs/35984892283),
step "Probe (temporary)"). The likely reason is that no `-windows`
project sets `UseWPF` or `UseWindowsForms` yet (INFERRED; not isolated).
Once the Tray skin gains real Windows UI code, a Linux build of this
project is expected to fail with NETSDK1100 (INFERRED). So this project
must still run on a Windows runner in CI. Do not make a Linux runner its
only home.

## How to add a fixture

Six fixture families exist so far: `GoldenMasterFixture` (`UsageSnapshot`,
the tooltip's slice), `UsageStatisticsFixture` (`UsageStatistics`, the
usage-figures/history-coverage slice, OVI-27), `FreshnessFixture`
(`UsageSnapshot` plus `UtcNow`, OVI-29), `PanelTextResetFixture` (four
raw-scalar members, OVI-29), `BoostNoticeFixture` (`BoostNotice` plus
one `DateTimeOffset` and one `TimeZoneInfo`, shared by `BoostChip` and
`BoostCard`, OVI-92), and `RateLimitedNoticeFixture` (one
`DateTimeOffset?`/`TimeZoneInfo` raw-scalar member, OVI-98). Add to
whichever family already matches the shape your fixture needs; only add a
new parallel family for a genuinely new shape, and read ADR-0003's
amendments first — a new family is the point that ADR flags as worth
Chief Gary II's and Quinn's sign-off rather than a unilateral call.

1. **Add the fixture** as a new `GoldenMasterFixture` entry in
   `Fixtures/GoldenMasterFixtures.cs`'s `All` list. A fixture is:
   - `Snapshot` — a fully populated `UsageSnapshot` (the ADR-0001 contract).
   - `DisplayZone` — pass a fixed `TimeZoneInfo` (usually `TimeZoneInfo.Utc`)
     so the fixture's expected wall-clock times don't depend on the machine
     running the test.
   - `ContentFacts` — the facts every skin's rendering must satisfy, built
     with `ContentFact.Contains("...")` for substring checks (a percentage,
     a rounding result, a reset time) or `new ContentFact(description,
     predicate)` for anything a substring check can't express (e.g. "the
     text contains some estimate/fallback disclosure, in whatever wording
     this skin uses").
2. **Do not pin exact strings.** Per ADR-0003, two skins are free to word
   the same fact differently (e.g. `"~57%"` vs. `"57%(est.)"`); the fixture
   only fails a skin that states a different number, drops a required
   disclosure, or applies a different rounding rule.
3. **Run `dotnet test tests/O-view.CrossSkin.Tests`** (or `dotnet test
   O-view.slnx` on Windows) and confirm the new fixture passes against both
   skins' current code.
4. **A new skin** (a third OS head, for example) is added once, in
   `GoldenMasterCrossSkinTests.Skins`, as a `SkinUnderTest` wrapping that
   skin's own formatter call — every existing fixture then runs against it
   automatically.
5. **Changing what a fixture pins is a reviewed, dated change** — treat it
   with the same discipline as an ADR edit (see `CLAUDE.md`). A silent edit
   that happens to change a pinned fact is exactly the drift this harness
   exists to catch; make the reasoning for the change visible in the PR.

## Confirming the harness actually catches drift

Before this slice shipped, the smoke fixture's `"57%"` content fact was
changed to `"58%"` locally and `dotnet test` was re-run: both skins failed
with a clear message naming the fixture, the skin, its actual rendered
text, and the unsatisfied fact. The change was then reverted and the suite
passes again. See the OVI-25 PR description for the full before/after
output.
