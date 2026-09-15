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

## Why this project targets `net10.0-windows`

`O-view.Tray` only builds as `net10.0-windows`. A `net10.0-windows` project
can reference a `net10.0` project (`O-view.Linux`, `O-view.Core`) without
issue, so targeting `net10.0-windows` here is what lets one project — and
one `dotnet test` run — invoke both skins' code. The consequence, flagged
by ADR-0003 itself: this harness only builds and runs on Windows, same as
`O-view.Tray` and `O-view.Tray.Tests`. When this repository gets a CI
workflow, this project must run on whatever runner already builds
`O-view.Tray` — not a Linux-only runner, or it will not build at all.

## How to add a fixture

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
