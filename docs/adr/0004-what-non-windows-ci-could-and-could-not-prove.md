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

Every factual claim below carries an evidence label. **CONFIRMED** means
read in the source at this branch, grepped, or run. **INFERRED** means
reasoned, not verified.

## In one paragraph, for non-technical readers

O-view has two "skins": a Windows one and a Linux one. Each writes its own
on-screen wording. One test suite, the anti-drift harness, checks that
both skins say the same thing about the same number. That suite needs the
Windows skin's code, so it can only run on Windows. Adding an automated
Linux build would therefore **not** check that the two skins agree. Today
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
(CONFIRMED on Windows). **No run has been made on Linux.** Every "would
execute on Linux" cell below is therefore INFERRED from the target
framework and references. None of them has been observed on a Linux
machine.

| Project | Target framework | References | Builds and runs on a Linux runner? |
|---|---|---|---|
| `src/O-view.Core` | `net10.0` (CONFIRMED) | none (CONFIRMED) | **Yes** (INFERRED; plain `net10.0`, no Windows reference) |
| `tests/O-view.Core.Tests` | `net10.0` (CONFIRMED) | Core (CONFIRMED) | **Yes** (INFERRED) |
| `src/O-view.Linux` | `net10.0` (CONFIRMED) | Core (CONFIRMED) | **Yes** (INFERRED) |
| `tests/O-view.Linux.Tests` | `net10.0` (CONFIRMED) | Linux (CONFIRMED) | **Yes** (INFERRED) |
| `src/O-view.Tray` | `net10.0-windows` (CONFIRMED) | Core (CONFIRMED) | **No** (INFERRED; see the note below) |
| `tests/O-view.Tray.Tests` | `net10.0-windows` (CONFIRMED) | Tray (CONFIRMED) | **No** (INFERRED; follows Tray) |
| `tests/O-view.CrossSkin.Tests` | `net10.0-windows` (CONFIRMED) | Core, Tray, Linux (CONFIRMED) | **No** (INFERRED; follows Tray) |

A Linux job would therefore build two source projects and run two test
projects: `O-view.Core.Tests` (13 tests) and `O-view.Linux.Tests` (52
tests).

**Note on the "No" rows.** By default the .NET SDK refuses to build a
`-windows` target framework on a non-Windows machine unless the project
sets `EnableWindowsTargeting=true` (INFERRED from SDK behaviour; not run
here, because no Linux machine was available). This ADR does not
recommend setting that flag. See "Alternatives considered", R3.

## What it would not verify

1. **The ADR-0003 anti-drift harness.** `O-view.CrossSkin.Tests` is the
   only mechanism that proves the Linux skin's wording states the same
   content facts as the Windows skin's. A Linux job cannot build it
   (INFERRED, per the table). A Linux-only CI setup would leave the
   cross-skin guarantee exactly where it is today: held only by someone
   running `dotnet test` on Windows by hand.
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
content facts in one test run. It already said that run needs a Windows
runner (CONFIRMED: ADR-0003, "Consequences"). The harness runs the Linux
skin's code on Windows, not on Linux. For the code that exists today, that
loses nothing, because that code is pure, uses invariant culture, and
every fixture runs in UTC (CONFIRMED, per point 4). Whether the output
would be byte-identical on a Linux runtime is INFERRED, not observed. So
there is nothing to escalate as a finding about shipped work. The one
real gap is that no machine runs the harness automatically. That is a
CI question, which is this ADR's question.

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
  - *Benefit:* the harness could run on Linux. For the reasons in point 4
    above, that run proves nothing the Windows run does not (INFERRED).
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
  `EnableWindowsTargeting=true`** so the harness at least builds on
  Linux.
  - *Rejected:* it hides the boundary rather than removing it. When the
    Tray skin gains real Windows UI code (WPF, `NotifyIcon`,
    `Shell_NotifyIconGetRect`, all expected by ADR-0002), a Linux build
    that references it becomes fragile or meaningless (INFERRED). It
    also runs against OVI-109's recorded position that the harness's
    Windows-only status is "a documented structural ceiling, not a
    defect to fix by changing a target framework."

The honest answer: the harness's Windows-only status costs nothing while
a Windows CI job runs it. It only matters if CI runs on Linux alone. The
fix for that is a Windows job, not a restructured harness.

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
  job builds and tests the four `net10.0` projects.
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
- R1 remains open as a future option. If the Tray skin's formatters
  ever need to run somewhere Windows is unavailable, this record is the
  starting point, and it should be amended rather than re-derived.
