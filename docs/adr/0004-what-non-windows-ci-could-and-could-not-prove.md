# ADR-0004: What non-Windows CI could and could not prove for O-view

- **Status:** Proposed — a recommendation for the board, not yet decided.
  Nothing in this record adds CI; it answers what CI would buy before
  anyone is asked to fund it. Becomes Accepted or Rejected, amended in
  place with a dated note, once the board answers the question at the
  end.
- **Date:** 2026-09-24
- **Deciders:** Adrian II the Architect (author); Quinn the Reviewer
  (review); the board (decision, carried by Chief Gary II).
- **Origin:** OVI-111, from Rae II's cross-platform claim ledger (OVI-104),
  row 2a.
- **Corrected 2026-09-24 (OVI-126), before merge, while still Proposed.**
  The first draft said a Linux runner *cannot build* the three
  `net10.0-windows` projects and would fail with NETSDK1100. A one-off
  probe refuted that: `dotnet test O-view.slnx` on `ubuntu-latest` (.NET
  SDK 10.0.401) built all seven projects and passed all 124 tests —
  Core.Tests 13, Linux.Tests 52, Tray.Tests 53, CrossSkin.Tests 6
  (CONFIRMED: [run 35984892283](https://github.com/mlengmark/oview/actions/runs/35984892283),
  step "Probe (temporary)", read from the run log). The sections below
  are corrected in place: the table, "How the Linux job should invoke
  them", "What it would not verify" point 1, R1's benefit, and R3. The
  recommendation does not change; "Why the Linux job still excludes the
  harness" says why.

Every factual claim below carries an evidence label. **CONFIRMED** means
read in the source at this branch, grepped, or run. **INFERRED** means
reasoned, not verified.

## In one paragraph, for non-technical readers

O-view has two "skins": a Windows one and a Linux one. Each writes its own
on-screen wording. One test suite, the anti-drift harness, checks that
both skins say the same thing about the same number. That suite needs the
Windows skin's code. Today that code happens to build on Linux too, but
it is declared Windows-only and is expected to stop building on Linux
once the Windows skin gains real Windows screens. So the suite belongs on
Windows, and an automated Linux build should **not** be relied on to check
that the two skins agree. Today
it would add very little at all, because all of the code that exists is
pure text formatting with no operating-system behaviour to test. What this
repository actually lacks is any automated test run, on any operating
system. So the recommendation is a small CI setup with two jobs: a Windows
job that runs everything, including the anti-drift harness, and a Linux
job that runs the parts that can run there. It also recommends leaving the
harness as it is.

## Context

`CLAUDE.md` and `src/O-view.Core/O-view.Core.csproj` describe a
non-Windows build and test of `O-view.Core` as the thing that enforces
Core's platform-neutrality. OVI-109 corrected the tense of that claim. It
now says the run is *intended* and has never happened, because this
repository has no CI workflow (CONFIRMED: `CLAUDE.md` on
`docs/ovi-109-ci-wording-correction`, and ADR-0003's 2026-09-10 OVI-25
amendment). This ADR does not reopen that wording. It builds on it.

What OVI-109 could not settle is ledger row 2a:
`O-view.CrossSkin.Tests` targets `net10.0-windows` because it references
the Tray skin (CONFIRMED:
`tests/O-view.CrossSkin.Tests/O-view.CrossSkin.Tests.csproj`). That
harness is ADR-0003's anti-drift mechanism, so a Linux-only CI job cannot
run it. The board should not be asked to decide on CI until that limit,
and the rest of what CI would and would not cover, is written down.

## What a non-Windows CI run would verify today, project by project

Source: `O-view.slnx` and each `.csproj`, read at this branch (CONFIRMED).
Execution evidence: `dotnet test O-view.slnx` was run on the Windows
agent runner on 2026-09-24 (.NET SDK 10.0.302). All four test projects
passed: Core.Tests 13, Tray.Tests 53, Linux.Tests 52, CrossSkin.Tests 6
(CONFIRMED on Windows). The same four passed on `ubuntu-latest` in the
OVI-124 probe (CONFIRMED: run 35984892283; see the correction note at the
top). That is one run, at one SDK version, of the code as it stands on
2026-09-24.

| Project | Target framework | References | Builds and runs on a Linux runner? |
|---|---|---|---|
| `src/O-view.Core` | `net10.0` (CONFIRMED) | none (CONFIRMED) | **Yes** (CONFIRMED by the probe) |
| `tests/O-view.Core.Tests` | `net10.0` (CONFIRMED) | Core (CONFIRMED) | **Yes** (CONFIRMED by the probe) |
| `src/O-view.Linux` | `net10.0` (CONFIRMED) | Core (CONFIRMED) | **Yes** (CONFIRMED by the probe) |
| `tests/O-view.Linux.Tests` | `net10.0` (CONFIRMED) | Linux (CONFIRMED) | **Yes** (CONFIRMED by the probe) |
| `src/O-view.Tray` | `net10.0-windows` (CONFIRMED) | Core (CONFIRMED) | **Yes today** (CONFIRMED by the probe); **expected No** once Tray gains Windows UI (INFERRED; see the note below) |
| `tests/O-view.Tray.Tests` | `net10.0-windows` (CONFIRMED) | Tray (CONFIRMED) | **Yes today** (CONFIRMED by the probe); follows Tray (INFERRED) |
| `tests/O-view.CrossSkin.Tests` | `net10.0-windows` (CONFIRMED) | Core, Tray, Linux (CONFIRMED) | **Yes today** (CONFIRMED by the probe); follows Tray (INFERRED) |

The Linux job recommended below builds two source projects and runs two
test projects: `O-view.Core.Tests` (13 tests) and `O-view.Linux.Tests`
(52 tests). That is a choice of scope, not a technical limit. See "Why
the Linux job still excludes the harness".

**How the Linux job should invoke them.** It *could* run
`dotnet test O-view.slnx` today; the probe did exactly that. It should
not. It should name the four `net10.0` projects explicitly, or use a
solution filter (`.slnf`) that lists only them. No such filter exists yet
(CONFIRMED: no `.slnf` file anywhere in the repository). A solution-level
Linux run would start failing, with no change to the workflow, the day
Tray gains Windows UI (INFERRED; see the note below). It would also pull
in projects whose Linux result the design does not rely on. PR #20 (the
OVI-124 CI slice) already names the four projects.

**Note on the `net10.0-windows` rows.** They build on Linux today because
no project sets `UseWPF` or `UseWindowsForms`. Without those, nothing
references the Windows desktop framework, and the SDK check behind
NETSDK1100 has nothing to reject (INFERRED; consistent with the probe but
not isolated). Once Tray sets either property, a Linux build of Tray,
Tray.Tests and CrossSkin.Tests is expected to fail with NETSDK1100 unless
`EnableWindowsTargeting=true` is set (INFERRED from SDK behaviour, not
yet observed here). ADR-0002 expects that Windows UI code (WPF,
`NotifyIcon`, `Shell_NotifyIconGetRect`). This ADR does not recommend
setting the flag. See "Alternatives considered", R3.

## What it would not verify

1. **The ADR-0003 anti-drift harness.** `O-view.CrossSkin.Tests` is the
   only mechanism that proves the Linux skin's wording states the same
   content facts as the Windows skin's. A Linux job *can* build and run
   it today (CONFIRMED by the probe), but the recommended Linux job does
   not, and no Linux job could once Tray gains Windows UI (INFERRED, per
   the note above). A Linux-only CI setup would therefore leave the
   cross-skin guarantee on a footing due to expire: it would hold today
   and silently stop holding later. The durable home for the harness is a
   Windows job.
2. **The Windows skin and its own tests.** `O-view.Tray` and
   `O-view.Tray.Tests` (53 tests) carry the Windows-only presentation
   facts, including the 127-character `NotifyIcon.Text` cap (CONFIRMED:
   `src/O-view.Tray/Presentation/TooltipFormatter.cs`). A Linux job
   covers none of this.
3. **Anything about real Linux desktops.** The Linux skin in this
   repository is text formatting only. It has no Avalonia, D-Bus or
   StatusNotifierItem code yet (CONFIRMED: the comment in
   `src/O-view.Linux/O-view.Linux.csproj` and its four `Presentation/`
   files). A green Linux CI job says nothing about tray icons, windows or
   positioning on a real desktop. ADR-0002's "hardware-unverified" status
   for Linux is unchanged by CI of any kind.
4. **Much that a Windows run does not already prove, today.** All the
   code that exists is operating-system-independent:
   - Core is eleven record/value types with no I/O (CONFIRMED:
     `git ls-files src/O-view.Core`, and a grep for `System.IO`, `File.`,
     `Path.` and `Environment.` across `src/` and `tests/` found nothing).
   - Every skin string is built with `CultureInfo.InvariantCulture`
     (CONFIRMED by grep of both skins' `Presentation/` folders).
   - Every harness fixture pins `TimeZoneInfo.Utc` (CONFIRMED by grep of
     `tests/O-view.CrossSkin.Tests/Fixtures/`).

   So a Linux run of Core.Tests and Linux.Tests is expected to give the
   same result as the Windows run (INFERRED). That changes once the code
   gains behaviour that really does differ by operating system: vendor
   data-file readers (paths, case sensitivity), time-zone IDs, and the
   Avalonia head. At that point the Linux job starts to earn its keep.
   It is cheap to have in place before that happens.

**Is the anti-drift guarantee weaker than ADR-0003 claims? No.** ADR-0003
promises that both skins' wording is checked against the same pinned
content facts in one test run. Its "Consequences" section said that run
"likely needs to run on the same Windows CI runner the Tray build already
uses". That was hedged prose, not a verified fact. The fact itself is
CONFIRMED by the project file: `O-view.CrossSkin.Tests` targets
`net10.0-windows` (CONFIRMED:
`tests/O-view.CrossSkin.Tests/O-view.CrossSkin.Tests.csproj`, and
ADR-0003's 2026-09-24 amendment under that sentence). The harness runs
the Linux skin's code on Windows, not on Linux. For the code that exists
today, that loses nothing, because that code is pure, uses invariant
culture, and every fixture runs in UTC (CONFIRMED, per point 4). The
OVI-124 probe also ran the harness on a Linux runtime, and all six
fixture families passed there (CONFIRMED: run 35984892283). So, for
today's code and fixtures, the output matches on both runtimes: observed
once, not guarded by any standing job. So there is nothing to escalate
as a finding about shipped
work. The one real gap is that no machine runs the harness
automatically. That is a CI question, which is this ADR's question.

**When this conclusion must be revisited.** "Not weaker" holds only
while the Linux skin's code behaves the same on every runtime. Because
the harness runs that code on the Windows runtime, the moment the Linux
skin gains OS- or runtime-sensitive behaviour, the cross-skin guarantee
for Linux holds only under Windows runtime conditions. Examples:
- the `displayZone ?? TimeZoneInfo.Local` default at
  `src/O-view.Linux/Presentation/TooltipFormatter.cs:20` being exercised
  rather than overridden by a pinned zone (CONFIRMED the default exists;
  every harness fixture passes `TimeZoneInfo.Utc` today, per point 4);
- Avalonia text measurement or layout feeding into any string;
- anything culture-, ICU- or time-zone-database-dependent.

When any of these lands, revisit this conclusion and route R1 below.
The Linux job in option (a′) does not cover this gap on its own: it runs
`O-view.Linux.Tests` on Linux, but not the cross-skin comparison.

## Could the anti-drift harness be restructured to run platform-neutrally?

**It could, but it should not be restructured now.** The cost is real and
buys nothing that the anti-drift guarantee needs. Three routes were
considered:

- **R1: move the Tray skin's string formatters into their own `net10.0`
  project** (for example `O-view.Tray.Presentation`), and have the
  harness reference that project instead of `O-view.Tray`.
  - *Feasible today:* the Tray skin's four `Presentation/` files use no
    Windows API. A grep for `DllImport`, `LibraryImport`,
    `System.Windows`, `Microsoft.Win32` and `SupportedOSPlatform` in
    `src/O-view.Tray` source matched nothing, and the csproj sets neither
    `UseWPF` nor `UseWindowsForms` (CONFIRMED). The `net10.0-windows`
    target is currently a declared boundary, not a technical need.
  - *Cost:* a new project per skin, or at least for Tray. This changes
    the skin layout that ADR-0001 and ADR-0002 describe. The new project
    would also need a standing rule that it may never gain a Windows
    type, and that rule would need its own enforcement. It does not
    change the Core data contract (CONFIRMED: only skin and test projects
    would move), so it is not scope growth under the escalation rule.
  - *Benefit:* the harness would keep building on Linux after Tray gains
    Windows UI. It already builds there today by accident of the current
    code (CONFIRMED by the probe). R1 would make that a guarantee by
    design. For the reasons in point 4 above, a Linux run proves nothing
    the Windows run does not while the Linux skin stays runtime-neutral
    (INFERRED).
- **R2: replace direct skin references with serialized expected output.**
  Each skin's own test project would check its strings against a shared,
  checked-in fixture file, and the harness would stop referencing skin
  assemblies.
  - *Cost:* rewriting all six fixture families (CONFIRMED count:
    `tests/O-view.CrossSkin.Tests/README.md`). It also loses the type
    checking that the current C# fixtures give. Worst, one run that
    checks both skins becomes two runs that must both happen, so the
    guarantee holds only if CI runs on both operating systems. That is
    weaker than today's single-run design, not stronger.
- **R3: leave the target framework alone and set
  `EnableWindowsTargeting=true`** so the harness keeps building on
  Linux.
  - *Today the flag would do nothing:* the harness already builds on
    Linux without it (CONFIRMED by the probe). The first draft of this
    ADR assumed otherwise.
  - *Rejected for later:* once the Tray skin gains real Windows UI code
    (WPF, `NotifyIcon`, `Shell_NotifyIconGetRect`, all expected by
    ADR-0002), the flag is what would keep a Linux build going. At that
    point it hides the boundary rather than removing it: a Linux build
    that references Windows UI code becomes fragile or meaningless
    (INFERRED). The harness's Windows-only status is a declared boundary
    today and becomes a hard one when Tray gains Windows UI. It is not a
    defect to fix by changing a target framework or setting a flag
    (OVI-109's position, as corrected by OVI-126 in `CLAUDE.md`).

The honest answer: the harness's Windows-only status costs nothing while
a Windows CI job runs it. It only matters if CI runs on Linux alone. The
fix for that is a Windows job, not a restructured harness.

### Why the Linux job still excludes the harness

The probe raised the question of whether the Linux job should also run
the harness (and Tray.Tests) while it still can. **Decided: no** (Adrian
II, OVI-126). The recommendation below is unchanged. Reasons:

- **It is a guarantee due to expire.** It stops working the day Tray
  sets `UseWPF` or `UseWindowsForms` (INFERRED). The job would go red on
  a PR that did nothing wrong, and the fix would be to take the harness
  back out. Nobody should build a check that is expected to be removed
  under pressure.
- **It adds little today.** Point 4 still holds: the code is pure,
  invariant-culture and pinned to UTC. The probe has already recorded
  the one thing a Linux harness run shows, that output matches on the
  Linux runtime for today's fixtures.
- **Its real value arrives when it can no longer be had cheaply.** A
  Linux harness run matters once the Linux skin gains runtime-sensitive
  formatting (see "When this conclusion must be revisited"). That is
  likely to land alongside or after Tray's Windows UI, when the build
  no longer works on Linux (INFERRED; the order is not known). The
  durable way to get that value is R1, decided when that trigger fires,
  not a temporary job step now.

If the board wants cross-runtime evidence for the harness before R1, the
harness can be added to the Linux job as a step **explicitly marked
temporary**, to be removed when Tray gains Windows UI. This record does
not recommend it.

## Options for the board, with costs

- **(a) Add non-Windows CI only, with its limits written down.**
  - *Covers:* Core, Core.Tests, Linux, Linux.Tests (65 tests).
  - *Does not cover:* the anti-drift harness, the Tray skin, or its 53
    tests.
  - *Cost:* one small workflow slice for Kit.
  - *Value today:* close to zero beyond what a Windows run already gives
    (point 4). It also has the most misleading failure mode: a green
    badge that looks like cross-platform proof and is not.
  - *Not recommended on its own.*
- **(a′) Add CI with two jobs.** A Windows job builds and tests the whole
  solution, including the anti-drift harness and the Tray skin. A Linux
  job builds and tests the four `net10.0` projects, named explicitly or
  through a solution filter (see "How the Linux job should invoke
  them" and "Why the Linux job still excludes the harness").
  - *Covers:* everything that exists, automatically, on every PR.
  - *Cost:* one small workflow slice for Kit, plus runner time. Windows
    runners cost more per minute than Linux runners on GitHub-hosted
    private repositories (INFERRED from GitHub's published billing; this
    repository's plan and visibility were not checked).
  - *Value:* for the first time, the anti-drift harness runs without a
    person remembering to run it. The Linux job is in place before the
    code that needs it (data readers, the Avalonia head) lands.
- **(b) Restructure the harness first (R1), then add Linux CI.**
  - *Cost:* R1's new-project cost and the layering rule that comes with
    it, before any CI value arrives.
  - *Value:* the same coverage as (a′), bought at a higher price.
    Rejected; see the section above.
- **(c) No CI.** Keep the layering rule held by review and by
  `O-view.Core.Tests`'s structural tests, as today.
  - *Cost:* none up front.
  - *Risk:* the anti-drift harness and every other test run only when an
    agent or person runs them by hand, on a runner whose .NET toolchain
    has already failed at least once (OVI-110, Finding B; INFERRED from
    the OVI-111 brief; the toolchain worked on 2026-09-24 for this
    ADR). A drift regression could merge unnoticed.

## Decision (proposed)

**Recommend (a′).** Add one CI workflow with a Windows job for the whole
solution and a Linux job for the four `net10.0` projects. Do not
restructure the anti-drift harness. When the workflow lands, `CLAUDE.md`
should state what each job does and does not prove, following this
record.

**The question for the board (yes/no):** *Approve one implementation
slice for Kit: a CI workflow with a Windows job (full solution, including
the ADR-0003 anti-drift harness) and a Linux job (Core, Core.Tests, Linux,
Linux.Tests), with the harness left unrestructured?*

- **Yes:** Chief Gary II scopes the slice.
- **No:** this record is amended to Rejected, and option (c) stands as
  the documented position.

## Alternatives considered

The alternatives are options (a), (b) and (c) and routes R1 to R3 above,
each with its reason for rejection. Also considered and rejected: **a
Windows-only CI job with no Linux job.** It would cover everything that
exists today and cost less. It was rejected because the Linux job is the
enforcement mechanism that `CLAUDE.md` and the Core csproj already say
Core's platform-neutrality is meant to rest on. Leaving it out would make
that documented intent permanently hypothetical, and the job costs little
to add.

To be precise about what that job adds: a Linux *build* adds very little
over a Windows build. Target-framework resolution and the CA1416
platform-compatibility analyzer behave the same on either operating
system (INFERRED). CA1416 fails the build on both, because Core, Linux
and Tray set `TreatWarningsAsErrors` (CONFIRMED: each `.csproj`). A
`DllImport` in Core would compile on Linux just as it does on Windows
(INFERRED from SDK knowledge; not run). The Linux job's real
value is **test execution on the Linux runtime**. So a green Linux badge
is not, by itself, proof that Core is platform-neutral. Core's
neutrality at compile time is held by its `net10.0` target, the
analyzer, review, and `O-view.Core.Tests`'s structural tests, on either
runner.

## Consequences

**Positive:**
- The CI decision reaches the board with a precise statement of what
  each job proves. Nobody has to discover from a red build, or from a
  misleadingly green one, that a Linux job skips the anti-drift harness.
- If (a′) is approved, the anti-drift harness becomes automatically
  enforced for the first time.

**Negative:**
- (a′) spends Windows runner time on every PR. The Linux job's value is
  mostly deferred until OS-sensitive code lands (INFERRED, per point 4).
- R1 remains open as a future option. The trigger that matters most is
  **when the Linux skin gains OS- or runtime-sensitive formatting**
  (see "When this conclusion must be revisited"). At that point the
  harness's Windows-only run stops being a complete check of the Linux
  skin, and this conclusion and R1 must be revisited. A second, weaker
  trigger is the Tray skin's formatters needing to run somewhere Windows
  is unavailable. Either way, this record is the starting point, and it
  should be amended rather than re-derived.

**Records this one supersedes in part:**
- ADR-0003's "Consequences" sentence saying the combined cross-skin test
  "likely needs to run on the same Windows CI runner the Tray build
  already uses" assumed a CI runner that does not exist (CONFIRMED: no CI
  configuration on `main`). ADR-0003 carries a dated 2026-09-24 amendment
  under that sentence pointing here. This record now owns the CI-runner
  question that ADR-0003 left open.
