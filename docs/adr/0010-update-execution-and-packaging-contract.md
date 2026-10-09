# ADR-0010: Acting on an update, and shipping the thing that acts — install kind decides, verification fails closed

- **Status:** **Accepted** — board sign-off at gate G2 via the PR #77 merge card
  (interaction `f2a1b66b-1257-4f53-af81-8a0a32b3e3d4`, accepted by local-board).
  Living document: amend in place, as [ADR-0001](0001-core-to-skin-data-contract.md)
  and [ADR-0008](0008-presentation-skin-contract.md) do.
- **Date:** 2026-10-05
- **Deciders:** Adrian II the Architect, pending board sign-off
- **Scope:** Phase 4B — **acting** on the update check that Phase 2 already
  built, and the installer and packages that make acting possible. Windows and
  Linux only.
- **Mandate:** the board accepted **option C** for Phase 4 (decision card
  `80339fe7`, OVI-423): settings/menu and auto-update/packaging proceed as two
  separate amendments, each unblocking its own build slices once approved. The
  first — settings, the right-click menu, the threshold picker, run-at-startup,
  theme following — is [ADR-0009](0009-control-surface-contract.md), OVI-432.
  **This is the second, OVI-433**, and it is the one
  [ADR-0008](0008-presentation-skin-contract.md) D8 deferred explicitly.

## Context

### What already exists, and the one thing that does not

Phase 2 built the update *check*. [ADR-0007](0007-app-shell-contract.md) D3 put
the fetch in the shell with one cooldown holder per process, and the pure
comparison in Core. All of the following is **CONFIRMED** by reading this
repository at `main` `653748c`:

| Piece | Where | State |
|---|---|---|
| `UpdateCheck.Evaluate(currentVersion, releaseJson, asset)` | `src/O-view.Core/Updates/UpdateCheck.cs` | Built, tested. The asset selector is **already a caller-supplied parameter**, so Core has no platform knowledge |
| `ReleaseAssets` — frozen asset names, plus selectors for the Windows installer, a `.deb` per architecture, a tarball per RID, and `None` | `src/O-view.Core/Updates/ReleaseAssets.cs` | Built, tested |
| `ReleaseVersion`, `RateLimitResponse` | `src/O-view.Core/Updates/` | Built, tested |
| `AvailableUpdate` — and it **already carries `ChecksumsUrl`**, documented as "null is not *skip the check*" | `src/O-view.Core/Updates/UpdateCheck.cs` | Built |
| `ReleaseFeed` + `IReleaseFeedTransport` + `HttpReleaseFeedTransport` | `src/O-view.App/Updates/` | Built, tested |
| `ShellSettings.AutoUpdateEnabled`, versioned JSON store | `src/O-view.App/ShellSettings.cs`, `ShellSettingsStore.cs` | Built, tested |
| Both skins' wording for an `UpdateAvailable` alert | `O-view.Tray/Presentation/AlertToastFormatter.cs`, `O-view.Linux/Presentation/AlertNotificationFormatter.cs` | Built |

**And nothing calls any of it.** `ReleaseFeed.CheckAsync` has **no production
caller** — the only call sites in the repository are in
`tests/O-view.App.Tests/Updates/ReleaseFeedTests.cs` (CONFIRMED by grep across
`src/` and `tests/`). Nothing in `src/` raises `UsageEventKind.UpdateAvailable`,
so the wording both skins already have is unreachable.

The reason is precise and it is the hinge of this whole record: **`CheckAsync`
requires a `ReleaseAssetSelector`, and nothing in this repository knows which
one to pass**, because nothing knows how this build arrived on the machine. The
missing piece is not the check. It is the answer to *what kind of install am I*.

### Why this is the higher-risk half of Phase 4

Phase 4A wires a menu over types that already exist. This half downloads an
executable from the network and runs it, and the thing it runs replaces the
running application. The source repository's own history is the evidence for
treating it carefully — it shipped **two** failures here, both found late:

1. **A Linux user had no update path whatsoever** for several releases. The
   design had handed an apt build `ReleaseAssets.None` reasoning "it must never
   install anything", but `Evaluate` only reports `UpdateAvailable` when the
   selector matches, so the build returned `Unknown` forever — and the stated
   fallback, `apt upgrade`, could not work either because the `.deb` installs no
   apt source and no O-view repository exists. Found at the v0.6.0 release gate
   (CONFIRMED: source repo ADR-0009, amendment 2026-08-03, correcting its own
   2026-07-30 amendment). [ADR-0002](0002-cross-platform-capability-matrix.md)
   already records this as "a bug the source already shipped once".
2. **Downloaded installers were executed unverified** for roughly three and a
   half weeks — `DownloadInstallerAsync` fetched the exe and `LaunchInstaller`
   handed it to `Process.Start`, with the installer deliberately unsigned, so
   the only integrity guarantee was TLS to `api.github.com` (CONFIRMED: source
   repo ADR-0009, amendments 2026-07-24 and 2026-08-18).

Neither failure was visible from the outside. Both are cheap to not repeat, and
the decisions below are shaped mostly around not repeating them.

## Decision

### D1 — How the build arrived is detected once, in the shell, and it is the only thing that decides what may be downloaded or executed

Port the source's `InstallKind` and `UpdatePolicy` into `O-view.App`.

| `InstallKind` | What it means | Action on a newer release |
|---|---|---|
| `WindowsInstaller` | Inno per-user install under `%LOCALAPPDATA%\Programs\O-view` | **Install in place** (D3, D6) |
| `WindowsPortable` | A loose exe wherever the user put it | **Open the release page** — a running single-file exe cannot overwrite itself, and the installer would create a parallel install beside it |
| `LinuxPackage` | Installed by apt/dpkg | **Notify once per version and stop** (D4). dpkg owns these files |
| `LinuxTarball` | Extracted by the user | **Notify once per version and stop** (D4) |

`UpdatePolicy.MayDownloadAndRun(kind)` is `true` for exactly one of these four,
and it is the **only** predicate in the codebase permitted to gate a download or
a process launch. The table is pure — no OS calls, no file system — so it is
unit-tested exhaustively over the enum.

**Detecting which kind you are is per-OS, so it follows
[ADR-0007](0007-app-shell-contract.md) D5:** the shell declares
`IInstallKindSource`, each skin supplies the implementation, selected at compile
time by which skin is built — never by a runtime OS check in shared code. The
Windows implementation compares the executable's directory against the installed
path; the Linux one looks for the dpkg-owned install location. Both are thin, and
both are the only part of this decision that cannot be unit-tested on CI.

**This does not go in Core.** `InstallKind` is a platform limit and
`UpdateAction` is a platform branch, and [ADR-0001](0001-core-to-skin-data-contract.md)
forbids both in Core. It is also not in the skins, because the policy must not be
able to differ between them.

**Rejected: inferring the install kind from the OS alone.** Windows has two
kinds with opposite permissions and so does Linux. An OS check would give the
portable exe the installer's permission to self-replace, which is the one thing
it demonstrably cannot do.

### D2 — Detection is separated from permission, and the separation is a tested invariant from the first slice

`UpdatePolicy.DetectionAsset(kind, architecture)` returns the asset that would
*actually* install this build — the `.deb` for its Debian architecture, the
tarball for its RID, the installer for either Windows kind — so every build can
**recognise** a newer release. `MayDownloadAndRun` remains the only thing
deciding whether anything is fetched or run.

This is the source's shipped bug, carried forward as a requirement rather than
as a warning. **Two tests are mandatory in the slice that introduces
`UpdatePolicy`, before any slice may download anything:**

- For every `InstallKind`, on a published architecture, `DetectionAsset` is not
  `ReleaseAssets.None` — i.e. every install kind can see an update.
- Over **all four** enum values,
  `MayDownloadAndRun(kind) == (kind == InstallKind.WindowsInstaller)` — the whole
  truth table of D1, pinned in one assertion. A weaker test that only checks the
  two concepts disagree for `LinuxPackage` would re-prove the source's one
  historical bug while leaving `WindowsPortable` and `LinuxTarball` free to be
  granted download permission by a later edit.

The second test is why the separation cannot be collapsed back together: every
install kind keeps its own detection asset, and exactly one of them may act on
it. New enum values are a deliberate amendment to this record, not a test fix —
adding a case makes the test fail until the permission is decided here in
writing.

An architecture this project does not publish for yields `ReleaseAssets.None`
and therefore `Unknown`, rather than pointing a user at a package that would not
run on their machine. That is the honest answer and it is
[ADR-0001](0001-core-to-skin-data-contract.md)'s never-fabricate rule applied to
our own release feed.

### D3 — Verification fails closed, and it ships in the same slice as the download — not after it

The release job publishes `SHA256SUMS` beside every asset
(`ReleaseAssets.ChecksumsName`, already frozen in Core). Before the Windows head
launches anything it verifies the downloaded bytes against that manifest.

**It refuses the update** on a missing manifest, an entry that does not parse, an
asset the manifest does not name, or a digest that does not match. Falling back
to "install anyway" when the manifest is absent would mean an attacker who can
replace the asset simply omits the manifest. The accepted cost is real and
named: **a release that forgets to publish `SHA256SUMS` strands every user on
their current version with no way to be told** — the same failure mode as
renaming a frozen asset name, and guarded the same way (D5).

Two smaller rules travel with it, because they are the same question — *does the
app trust what the feed told it?*

- **The download URL is checked against an allowlist of GitHub hosts.**
  `browser_download_url` arrives inside the JSON and the app executes what it
  fetches, so the URL is a trust decision. Port `ReleaseDownloadUrl`: https plus
  an exact host match.
- **The temp filename is built from the parsed version, never the raw tag.**
  `ReleaseVersion.TryParse` truncates at the first `-` or `+`, so in the source a
  tag of `v9.9.9-../../../../Startup/evil` parsed cleanly as `9.9.9` while the
  tag string kept the traversal segments — and the tag was what reached
  `Path.Combine` and decided where a downloaded executable landed (CONFIRMED:
  source repo ADR-0009, amendment 2026-08-18).

**The ordering is part of the decision, not a nicety.** The slicing table (below)
puts verification and download in **one** slice. In the source these were three
and a half weeks apart, and the gap was a shipped build that executed unverified
network bytes. There is no partial-credit version of this.

**What this does not establish, stated plainly because the record should not
overclaim:** the manifest ships from the same release as the asset it describes.
It proves the bytes are the ones that release published; **it does not prove the
release is honest.** Whoever can replace the installer can replace `SHA256SUMS`
beside it. It defends against tampering in transit, a partially-swapped asset
set, and a truncated download. The control that covers a compromised publishing
account is provenance attestation — see D8 and the board question.

### D4 — A Linux build downloads, extracts and executes nothing. The guard is asserted, not assumed

Both Linux install kinds notify and stop. The guard lives as an assertion inside
the Linux update path — a later edit that makes this head "helpfully" install
something trips a test rather than shipping, which is how the source eventually
held the line after its own amendment.

**"Once per version" is persisted, not held in memory.** The check runs on a
cadence in an app designed to run for days; an in-memory flag re-nags after every
restart. It is stored as the last-announced tag in **the shell's settings file**
([ADR-0007](0007-app-shell-contract.md) D4, behaviour settings, shell-owned) —
not per skin. The shell owns the check (D3 of ADR-0007), so it owns the record of
what it has already announced, and one copy means the two skins cannot disagree.
This rule applies to Windows too: the background path announces once per version
and stops there.

**The notice must not tell a `.deb` user to run `apt upgrade`.** There is no
O-view apt repository for `apt` to learn a version from, so the command reports
nothing to do, and a user who runs it and sees nothing concludes the
notification was wrong. It names the real step — download and install the next
package — instead. **The wording itself is each skin's**
([ADR-0001](0001-core-to-skin-data-contract.md),
[ADR-0003](0003-paneltext-anti-drift-mechanism.md)); what this record fixes is
the constraint and the reason, not a string.

### D5 — Packaging publishes exactly the names Core already froze, and the release job proves it did

`ReleaseAssets` is already in Core and is already the single home for these
names, on the stated reasoning that the workflow writes them and the checker
matches them, so restating them separately makes them drift and the symptom is an
app that quietly stops updating. Packaging therefore has no naming freedom.

| Artefact | Built by | Notes |
|---|---|---|
| `O-view-Setup.exe` | Inno Setup, `installer/O-view.iss` | Per-user into `%LOCALAPPDATA%\Programs\O-view`, `PrivilegesRequired=lowest`, **no elevation**. Start Menu shortcut. Uninstall entry. SmartScreen is the only friction |
| `O-view.Tray.exe` | portable single-file publish | A first-class manual download, not a fallback |
| `o-view_<version>_<arch>.deb` | `packaging/linux/build.sh` | amd64 and arm64 |
| `o-view-<version>-<rid>.tar.gz` | same | linux-x64 and linux-arm64 |
| `SHA256SUMS` | **the release job itself**, never staged by hand | Asserts an entry per asset and that verification passes against the staged bytes before publishing |

**Unified releases: every tag carries both platforms' assets.** `releases/latest`
is then always the right thing to read. If platform-partial releases are ever
adopted, two changes become mandatory *before* the first one ships — walking
`/releases` instead of reading `releases/latest` (otherwise a Linux-only release
hides an older Windows one and Windows installs stall silently), and a distinct
outcome for "no build for your platform" so it stops being
indistinguishable from a broken feed. Neither is built here, because building
half of them is worse than building none.

**The installer's optional "start automatically when I sign in" task must write
the same `HKCU\...\Run` value, same value name and same quoted-path format, as
the in-app toggle** that [ADR-0009](0009-control-surface-contract.md) D3 drives.
One authoritative setting, not two competing ones. ADR-0009 D3 already requires
the menu to read the OS live rather than mirror it, which is what makes this safe
— but the installer and the app must still agree on *which* value they are both
looking at.

### D6 — The post-update relaunch is handed to the shell, not launched by the installer

For an installed Windows build: run the installer silently as an independent
process, in update mode, and exit so the exe is not held locked. The installer's
Restart Manager integration finishes closing the old instance, upgrades in
place, and relaunches — **via `explorer.exe`**, which re-parents the new process
to the shell so O-view starts in the ordinary interactive user context rather
than inheriting whatever it was given mid-install. Explorer returns immediately,
so there is no window flash.

The normal post-install "launch now" step must stay skipped under the silent
update path, or a normal user install double-launches. This is ported from source
repo ADR-0010 with no change; it is a learned lesson and there is no reason to
re-derive it.

### D7 — Nothing installs itself in the background. `AutoUpdateEnabled` means *check* automatically, never *install* automatically

This is a contract clarification and it is the decision most likely to be read
wrong, so it is stated as its own rule.

[ADR-0009](0009-control-surface-contract.md) D2 ships the menu item as
"Check for updates automatically", backed by
`ISkinToShell.SetAutoUpdate(bool)` and `ShellSettings.AutoUpdateEnabled`. A
builder could reasonably read "auto-update" as "install without asking". **It
does not, and must not.**

| Path | What happens |
|---|---|
| Background check, `AutoUpdateEnabled` true | Check on a cadence; on a newer release, raise `UpdateAvailable` once per version and stop. **Never downloads. Never installs** |
| Background check, `AutoUpdateEnabled` false | No periodic check at all |
| **Manual "Check for updates now"** | Runs the same check interactively and **always reports an outcome** — up to date, a newer version, rate-limited, or "could not tell" |
| Download and install | Only from an **explicit, confirmed user action**, and only where `MayDownloadAndRun` is true |

Silently downloading and running an executable from the network is exactly the
behaviour a security-conscious user distrusts, and the confirmation costs one
click. **A build that may not install still checks**, so it can always tell the
user something shipped.

**The manual "Check for updates now" menu item is this record's to add.**
ADR-0009 D2 deliberately left it out, on the reasoning that a menu item which can
only ever report "a newer version exists and this app cannot do anything about
it" is worse than not having it yet. Once D1–D3 land, the result is actionable
and the item becomes correct, so it is added here (slice 5) rather than in 4A.
This is the one place where Phase 4B adds a menu item, and it adds **no member**
to either seam — `ISkinToShell` and `IShellToSkin` do not move in this record.

### D8 — Out of scope, and staying out

- **A real apt repository.** It would make "`apt upgrade` does the work" true,
  and it is the better end state. It needs signing keys, hosting and its own
  record. A notification the user can act on is a complete answer, just not the
  most convenient one. Revisit if `.deb` downloads prove awkward in practice.
- **Provenance attestation and code signing.** Deferred, not rejected — and
  raised to the board as a named question below rather than buried here.
- **An update channel (beta/stable) and proxy settings.** Both are settings that
  [ADR-0009](0009-control-surface-contract.md) D1 names as reopening the
  settings-window question, and D1 assigns that cost to this record if it lands
  here. It does not land here: neither is needed to ship an update, and adding
  either would pull a settings window into the highest-risk slice set in the
  project. If the board wants channels, that is a later amendment and it carries
  the window with it.
- **macOS** — gate **G3**, open. No `.pkg`, no Sparkle, no `InstallKind` member
  named or shaped to anticipate one.
- **A second AI source** — gate **G5**, open.
- **A shared UI layer** — gate G4, closed as A.

**Escalation check:** no part of Phase 4B as scoped here requires G3 or G5 to be
opened. It does raise one question that is the board's and not mine — the next
section.

## The board question this record cannot answer itself

**Should the release job add Sigstore-backed provenance attestation
(`actions/attest-build-provenance`, verifiable with `gh attestation verify`), or
is checksum verification against the release's own manifest sufficient for now?**

The tradeoff, stated fairly:

- D3's manifest check proves the bytes match what the release published. It does
  **not** prove the release is honest. Whoever can replace `O-view-Setup.exe` can
  replace `SHA256SUMS` beside it (CONFIRMED by construction; the source repo
  states the same limitation of its own implementation).
- Attestation is the control that covers a compromised publishing account, and it
  is **free for public repositories**. Its cost is `id-token: write` and
  `attestations: write` on the release job — a permission widening on the job
  that produces the executable users run, which is exactly the kind of change
  that deserves a decision rather than being smuggled into a slice.
- Authenticode code signing is a third option and a different one: it removes
  SmartScreen friction, costs money annually, and does nothing attestation does
  not already do for integrity.

**Recommendation:** ship Phase 4B with D3's checksum verification, and add
attestation as a separate, single-purpose slice immediately after — so the
permission widening is reviewed on its own and is not entangled with the update
path. Do **not** pursue code signing now; SmartScreen friction on a per-user
installer with no UAC prompt is a real but survivable cost, and the money buys
less safety than the free control does.

This is recorded as a question, not a decision, because it trades security
posture against release-pipeline permissions and that is the board's call.

## Amendments to earlier records

- **[ADR-0008](0008-presentation-skin-contract.md) D8** defers "packaging,
  installer and self-update behaviour" and its 2026-10-05 (OVI-432) amendment
  names OVI-433 as where they went. **This record is that destination**; D8's
  deferral is now answered and needs no further edit.
- **[ADR-0009](0009-control-surface-contract.md) D2** leaves out a manual
  "check for updates now" item and assigns it to OVI-433. **D7 above adds it.**
- **[ADR-0009](0009-control-surface-contract.md) D1** flags an update channel or
  a proxy setting as plausibly reopening the settings-window question in this
  record's territory. **D8 above declines both**, so the menu-only decision in
  ADR-0009 D1 stands unchanged.
- **[ADR-0002](0002-cross-platform-capability-matrix.md)**'s Self-update row and
  its 2026-09-25 amendment are consistent with D1 and D4 and need no edit; this
  record is the design that row's Linux column was pointing at.
- **[ADR-0007](0007-app-shell-contract.md) D3/D4** are unchanged. D3's shared
  feed is what D7's checks go through, and D4's shell-owned behaviour settings
  are where D4's last-announced tag lives.

## Alternatives considered

**One "Phase 4" record covering settings, menu, auto-update and packaging.**
Rejected by the board at OVI-423 (option C) and the split is right: 4A wires a
menu over existing, tested types, while this half changes `.csproj` files, adds
packaging artefacts and `.github/` workflow changes, and touches a code path that
executes downloaded binaries. Different risk class, different review attention.

**Putting `UpdatePolicy` in Core so both skins and the shell can see it.**
Rejected — `InstallKind` is a platform limit and `UpdateAction` is a platform
branch, and ADR-0001 forbids both in Core. The shell is the correct owner and is
reachable by everything that needs it.

**Letting each skin own its own update policy.** Rejected for the reason the
source gives for encoding the table once: two heads that can quietly disagree
about whether they may overwrite files is the failure mode, not the fix.

**Self-replacing on Linux by writing to the install directory.** Rejected, and
not as a near-miss. Files installed by dpkg are owned by dpkg; overwriting them
is silently reverted by the next `apt upgrade` or leaves the package database
describing a version no longer on disk. Anthropic's own Claude Desktop for Linux
ships through an apt repository and does not self-update either — following the
platform's convention rather than fighting it. This already shipped as a real bug
once and [ADR-0002](0002-cross-platform-capability-matrix.md) records it as such.

**Verifying after launch, or warning instead of refusing.** Rejected — see D3.
A check that can be bypassed by omitting the manifest is not a check.

**Downloading in the background once a newer version is found, so the install is
instant when the user confirms.** Rejected. It moves the network fetch of an
executable to before the user has agreed to anything, which is precisely the
property D7 exists to preserve, and it buys a few seconds.

## Consequences

**Positive**

- Every install kind can see that a newer version shipped — including the two
  Linux kinds, which in the source could not, silently, for several releases.
- The one predicate that permits execution is pure, is named, and is tested
  exhaustively over a four-member enum.
- Nothing executes bytes it has not verified, from the first slice that downloads
  anything.
- Asset names stay a single decision in Core, so the workflow and the checker
  cannot drift apart.
- The update check built in Phase 2 stops being dead code.

**Negative, and accepted**

- **A release that forgets `SHA256SUMS` strands every user**, with no way to tell
  them. Guarded by the release job generating and checking the manifest itself
  (D5), not eliminated.
- **The installer stays unsigned**, so Windows users see SmartScreen on first
  install. Unchanged from the source; revisited only if the board chooses code
  signing.
- **`.deb` and tarball users update by hand.** Honest and slightly inconvenient.
  The apt repository that would fix it is deferred (D8).
- **Install-kind detection cannot be proven on CI.** It is the one part of D1
  that requires a real installed build on each platform, and
  [ADR-0004](0004-what-non-windows-ci-could-and-could-not-prove.md)'s limits
  apply. Slices 1 and 6 name this explicitly.
- **The verification path is the hardest thing in Phase 4 to test honestly.** A
  passing test suite does not prove an update succeeded on a real machine; it
  proves the refusals refuse.

## Slicing guidance for decomposition

Order matters here more than in any earlier phase: **nothing downloads before
verification exists.** Slices 1–3 are the gate.

| # | Slice | Depends on | Risk | Merge |
|---|---|---|---|---|
| 1 | **`InstallKind`, `UpdatePolicy` and `IInstallKindSource` in `O-view.App`** (D1, D2): the pure policy table, `DetectionAsset`, `MayDownloadAndRun`, the shell-declared interface, and **D2's two mandatory invariant tests**. No OS implementation, no IO — **landed** (PR #89, OVI-507) | — | Low — pure logic over an enum | agent |
| 2 | **Per-skin `IInstallKindSource` implementations** (D1): Windows installed-path comparison, Linux dpkg-location check, selected at compile time — **landed** (Kit the Builder, OVI-511, PR #90): `WindowsInstallKindSource` (`src/O-view.Tray/Platform/WindowsInstallKindSource.cs`) ports the source repository's `UpdateService.CurrentInstallKind` case-insensitive directory comparison unchanged; `LinuxInstallKindSource` (`src/O-view.Linux/Platform/LinuxInstallKindSource.cs`) ports `Program.DetectInstallKind`'s dpkg-path check but replaces its raw `StartsWith` with a directory-boundary comparison, because the source version would misclassify a sibling directory such as `/usr/lib/o-view-extra` as the dpkg install. Both take the comparison path and the executable-path lookup as injectable constructor parameters, the same shape as `RegistryStartupRegistration`/`XdgAutostartRegistration`, so the tests prove the comparison and never the live environment (D1's risk note). No consumer constructs either yet — that is slices 4 and 6 | 1 | Medium — the one part not provable on CI; test the path comparison, not the environment | agent |
| 3 | **`ChecksumFile` and `ReleaseDownloadUrl` in Core** (D3): manifest parsing, the https + exact-host allowlist, and the parsed-version temp path. **Pure, no IO, fails closed.** Tests for every refusal case — missing manifest, unparseable entry, unnamed asset, digest mismatch, non-GitHub host, traversal in the tag — **landed** (Kit the Builder, OVI-514): `ChecksumFile` (`src/O-view.Core/Updates/ChecksumFile.cs`) ports the source repository's manifest parser unchanged. `ReleaseDownloadUrl` (`src/O-view.Core/Updates/ReleaseDownloadUrl.cs`) ports `IsTrusted`'s host allowlist unchanged and adds `TempFileName(string? tag)`, which the source built inline at the `UpdateService` call site by interpolating the already-parsed `ReleaseVersion` rather than the tag; moving that one line into Core keeps the traversal fix next to the host check it was written beside in the ADR, pure and tested. No consumer wires either in yet — that is slice 4 | — | Low–medium — pure, but the refusal cases *are* the deliverable | **board** (security-relevant contract) |
| 4 | **Windows update execution** (D3, D6): wire `ReleaseFeed` into the shell's composition root with the selector from slice 1; download, **verify against slice 3**, launch silently in update mode, exit. **Download and verification in one slice** — **landed** (Kit the Builder, OVI-529): `WindowsUpdateExecutor` (`src/O-view.Tray/Updates/WindowsUpdateExecutor.cs`) composes `IInstallKindSource` (slice 1/2), `ReleaseFeed` (ADR-0007 D3) and `ChecksumFile`/`ReleaseDownloadUrl` (slice 3) into one fail-closed path: `MayDownloadAndRun` gates every install kind but `WindowsInstaller`; the installer URL and the checksums URL are each checked against `ReleaseDownloadUrl.IsTrusted` before anything is fetched; the manifest is downloaded and `ChecksumFile.DigestFor` must find the installer's entry before the installer itself is downloaded; the computed SHA-256 must match before `IInstallerLauncher.Launch` runs. `IInstallerDownloader`/`IInstallerLauncher` isolate HTTP and the process launch behind interfaces, the same seam shape as `IReleaseFeedTransport`, so `WindowsUpdateExecutorTests` drives every D3 refusal case (untrusted installer host, untrusted checksums host, missing/empty/unparseable/unnamed-asset manifest, failed downloads, digest mismatch) plus the happy path against fakes, with no live GitHub call. **The app launches the installer directly** (`ShellInstallerLauncher`, ported from the source repository's `UpdateService.LaunchInstaller` unchanged: `/SILENT /update=1`, `UseShellExecute = true`) rather than via `explorer.exe` — re-reading D6 closely, the `explorer.exe` re-parenting is the *installer's own* `[Run]`-entry relaunch of the app once it finishes (slice 7, `installer/O-view.iss`, not built here), not a mechanism for this app to launch the installer; `explorer.exe` does not reliably forward arbitrary command-line switches to what it opens, which the installer's silent-update flags need. **No production caller invokes this against the real network yet** — deciding *when* to run it is slice 5's `AutoUpdateEnabled`/notify-once cadence, which needs a `ShellSettings` field this slice does not add (D7); `WindowsUpdateExecutor` is composed with real `HttpInstallerDownloader`/`ShellInstallerLauncher` dependencies and ready for that slice to call, the same staged pattern slice 2's doc comment used | 1, 2, 3 | **High** — the app replaces itself; the only slice that executes a downloaded binary | **board** |
| 5 | **The background cadence, notify-once-per-version, and the manual check item** (D4, D7): last-announced tag in `ShellSettings`, `AutoUpdateEnabled` gating the periodic check only, `UpdateAvailable` raised so both skins' existing wording becomes reachable, and the "Check for updates now" item — **landed** (Kit the Builder, OVI-557): `ShellSettings` (`src/O-view.App/ShellSettings.cs`) gains `LastAnnouncedUpdateTag` (nullable string, default null), persisted by `ShellSettingsStore` the same additive-field way `AutoUpdateEnabled` was, and written back through `AppShell.RecordAnnouncedUpdateTag` — a plain public method, not a new `ISkinToShell` member, per this row's "adds no member to either seam". `UpdateCadence` (`src/O-view.App/Updates/UpdateCadence.cs`) is the shared cadence/dedupe type both skins compose one instance of: its background path (`AppTimer`-driven, D7's "on a cadence") does nothing unless `AutoUpdateEnabled` is true, then calls the existing `ReleaseFeed`/`UpdatePolicy.DetectionAsset`/`IInstallKindSource` pipeline slices 1–4 already built, raises `UsageEventKind.UpdateAvailable` and records the tag only when it differs from `LastAnnouncedUpdateTag` — persisted dedupe, not `UsageEventDecider.DecideUpdateAvailable`'s in-memory one, which this type supersedes for production wiring. `CheckNowAsync` is the manual path: runs unconditionally, always returns a real `UpdateCheckResult` (never collapsing `Unknown`/`RateLimited` into `UpToDate`), and records a newly-seen tag silently so a manual discovery suppresses the next background announcement of the same version. Both skins wire one `UpdateCadence` into their composition root (`Program.cs`, `WindowsInstallKindSource`/`LinuxInstallKindSource` respectively, `HttpReleaseFeedTransport`, a 24-hour cadence — INFERRED, no ADR names an exact interval) and thread it into their existing menu controller/adapter pair, which gained a "Check for updates now" item calling `UpdateCadence.CheckNowAsync()` directly (not through `ISkinToShell`) and reporting the outcome through each skin's own new `AlertToastFormatter.FormatManualCheck`/`AlertNotificationFormatter.FormatManualCheck`, reusing the existing toast/notification surface rather than adding one. The existing `UsageEventKind.UpdateAvailable` wording in both formatters is unchanged and is now reachable in production for the first time via the background path. Tests (`UpdateCadenceTests`) drive the cadence/dedupe/manual-check logic against fakes only, with no live GitHub call, matching this row's own boundary | 1, 4 | Medium — settings-schema change plus a menu item in both skins | agent (seam unchanged); **board** if `ShellSettings` gains a field — **it does here, so this PR is a board merge** |
| 6 | **Linux notify-only path** (D4): both Linux kinds notify and stop, with the never-download assertion as a test | 1, 2, 5 | Medium — provable by fake-backed tests only | agent |
| 7 | **`installer/O-view.iss`** (D5, D6): per-user Inno installer, `PrivilegesRequired=lowest`, Start Menu shortcut, uninstall entry, optional startup task writing **the same** `Run` value as ADR-0009 D3's toggle, silent update mode, `explorer.exe` relaunch | 4 | **High** — new build artefact, writes to the registry, and no CI can prove the install experience | **board** (packaging) |
| 8 | **`packaging/linux/build.sh`** (D5): `.deb` for amd64/arm64 and tarballs for linux-x64/linux-arm64, named exactly as `ReleaseAssets` matches | — | Medium | **board** (packaging) |
| 9 | **The release workflow** (D5): build every asset, **generate `SHA256SUMS` in the job**, assert an entry per asset and that verification passes against the staged bytes, then publish | 7, 8 | **High** — `.github/` change, and the manifest guard is the thing D3 depends on | **board** (CI) |
| 10 | **Provenance attestation**, *only if the board answers yes* to the question above: `id-token: write` + `attestations: write` on the release job, nothing else | 9 | Medium — a permission widening, reviewed alone | **board** |

Slices 3, 4, 7, 8, 9 and 10 are board merges by this project's rules —
security-relevant contract, packaging, `.github/`, and a path that executes
downloaded code. That is six of ten, and it is the right ratio for this phase
rather than a sign the slicing is too coarse.

**Slice 1 is the whole unblocking step.** It is low-risk, pure, and it is what
turns `ReleaseFeed` from dead code into something callable. Kit can start there
the moment this record merges.
