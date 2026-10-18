---
title: Build and test
type: development-reference
status: active
updated: 2026-10-18
---

# Build and test

This page is for contributors building the code. Players use the Windows
bundle; they do not need the Godot editor or a development SDK.
See [Playing](../playing.md).

The server supports only the game world. Protocol tests default to a populated
native world with automatic advancement disabled; tests can also use
fresh founder setup. `fixture` world mode is rejected at actual server startup.
The unused-code cleanup retires checks of the scripted fixture, its
authoring/persistence branches, the test-only PNG normalizer and prototype
world-level faction/law, currency and culture records. Native pairing, signed
controls, instruction retries, save integrity, content lifecycle, observation
knowledge and replay checks remain. Mod import and approved-asset catalog
validation keep their existing paths.

Protocol checks obtain world and resident identities from the authenticated
baseline. Native before/after checks encode `WorldRuntime.ExportState()`
with `WorldRuntimeCodec`; instruction IDs and issuer IDs come from the
server's receipt and saved native instruction. These checks exercise the
world authorization and persistence boundary used by the game.

Fixture-retirement replay checks cover both native starting paces. The
pre-retirement baseline writes world-systems schema 2 for legacy-map starts and
schema 3 for generated founder starts; both write schema 4 after retirement.
Compare every remaining state field, event and provider observation, and require
exact checkpoint bytes after reload.

## Toolchain

The server and simulation use C# 14 on .NET 10. The exact SDK baseline is
`10.0.401`, selected by [global.json](../../global.json); patch updates within
that feature band are permitted.

The Godot client uses the pinned Godot 4.7.2 .NET SDK and targets `net8.0`,
the desktop script target supported by that engine. This target belongs only
to the client; the server projects remain on .NET 10. The simulation must stay
runnable without Godot, a window manager, a model service or a network
connection once dependencies are restored. See [How it works](how-it-works.md).

The first export target is Windows 11 x64. The current export is an unsigned
portable bundle, and [the portable package](#portable-windows-package) adds the
game's own server to it. The [Windows installer](#windows-installer) combines the
launcher and game packages. These checks do not establish code signing or public
release. A Windows smoke test and paired reconnect were recorded on 2026-09-21;
that historical check does not verify every later build.

xUnit is the test framework. External packages have committed NuGet lock files.
Use `--locked-mode` so an unexpected dependency change fails visibly.

Shared compiler, analyzer and version settings live in
[Directory.Build.props](../../Directory.Build.props). Use its version fields
rather than adding a second version constant.

Ornament trade fixtures create their stock through actual paid native production
and cache only strict checkpoint bytes. Each case decodes fresh state and uses
its own runtime and providers. Setup stops when the real output appears;
protected-stock cases require a valid trade control and an ordinary action
observation before checking refusal, ownership, reservations and capacity.

Named meal fixtures share strict setup checkpoints from one generated Small
world, including separately paid native 1×2 and 2×2 Restaurant placements.
Every recipe case decodes fresh state, adds its own exact ingredients and
runs the actual production duration with independent runtime and providers.
Order bindings, reservations, private output, vessel preservation, strict
reload and paired checkpoint replay remain checked in each existing case.

Restaurant pipeline fixtures also share strict bytes from their original
generated world and genuinely paid Restaurant placement. Each case decodes
fresh state before adding its own ingredients, payment, cargo and visitor
positions, then uses independent runtime and providers. Real milling, cooking,
travel, trade, order survival, refusal and exact checkpoint replay stay checked.

Native land-hearing fixtures use twenty-tick test days for actual elections,
notice windows and expiry rulings. Their age, continuity, animal and weather
clocks are rescaled together. Later-generation family growth keeps its forty-tick test
day; only the completed family checkpoint is reclocked for hearing setup.
Personal consent, saved election ties, strict reload and paired checkpoint
replay remain checked through the real runtime transitions.

The client bundles one third-party font, Fusion Pixel 12px, in
`src/ClankerWorld.GodotClient/UI/Theme/Fonts/`, under the SIL Open Font
License. Keep `fusion-pixel-OFL.txt` beside it; the export preset's include
filter copies that licence into the game data. Do not edit the font file: the
licence reserves the name Fusion Pixel for unmodified copies. The heading
lettering, Timber, is drawn in code in `UI/Theme/TimberFont.cs`.

## Testing model work

Use `ModelWorkHarness.For(world)` for tests of the nonblocking tick. `TickAsync`
commits one tick, then runs scheduled model work; it never admits a reply in
that same tick. Pass `runPendingWork: false` to keep work registered without
starting it, and call `RunPendingWork` when the test permits provider entry.
`ScriptedProvider.Hold()` returns a `HeldCall`: check `IsEntered`, then use
`Reply(choice)` or `Fail()` to complete the actual runtime wrapper. Inline
provider gates keep reply completion independent of the thread pool. Use the
harness's `WaitAsync` for expected provider signals: its thirty-second bound
diagnoses missing signals without advancing native ticks or controlling entry.
A synchronously blocking provider needs a controlled worker to run pending work;
use static `WaitForSignalAsync(signal, deadline)` for its entry and worker guards
so waiting does not start queued work on the waiting thread. When a host tick
owns an uncommitted deterministic request, include that tick in
`WaitForExistingAsync(runtime, predicate, tick)` so the actual host operation
shares the failure bound even while the live model queues are empty.

`SettleAsync` starts pending work and finishes returned wrappers while held
calls remain held. `PendingModelWork` snapshots all four live queues, with the
request and actual completion task. Observe registered, started and finished
calls through `IModelWorkObserver`; admitted, discarded and cancelled outcomes
come from the live runtime. Admission includes its committed world tick. A
refused tick publishes no admission. Use `TickUntilAdmittedAsync(actor, maxTicks)`
for bounded acceptance and `AdvanceTogetherAsync(other)` for paired checkpoints.
Pass a test clock to `ModelWorkHarness.For(runtime, clock)` to control identity,
will and conversation deadlines; production uses `TimeProvider.System`.

Client pairing, signed actions, catalogs and local-server lifecycle tests pass
their clock to the client constructor. Advance it after the actual request or
body read enters. Keep entry and completion waits bounded with
`ModelWorkHarness.WaitForSignalAsync`; its diagnostic deadline does not advance
the test clock or replace the client's timeout or caller cancellation.
The manual clock synchronizes timer state and invokes callbacks outside that
state lock, so a returned HTTP call can dispose its deadline on another thread.

Fixtures that interleave gates run blocking checkpoint and usage completion on
dedicated workers, so a test waiting for entry cannot starve the worker it needs.
Keep the real runtime/host gates and diagnostic deadlines in these tests.

Host fixtures use `AdvanceWithModelWorkAsync(world)` to keep the real host save
and commit path while controlling the same work queues. Use `WaitForAllAsync`
when every pending input must finish before paired checkpoint continuations,
including ordinary decisions, life-moment identities, wills and conversations.
Waiting spends no native ticks; select a kind or actor when another reply must
stay held. Compare complete checkpoint bytes after matching external inputs,
and retain native admission, refusal, rollback and strict reload assertions.

Keep a physical-work checkpoint frozen while observing provider entry or a late
cancelled reply. Collection, storage and field work can progress during extra
ticks; a tick limit alone does not bound a stalled host operation or missing
signal. Expected entry, reply and actual wrapper waits retain shared diagnostic
deadlines. `WaitAsync` starts queued work before waiting; `WaitForSignalAsync`
does not. For synchronous blocking entry, register with
`TickAsync(runPendingWork: false)`, run `RunPendingWork` on a controlled worker,
and use the signal-only wait before releasing that entry.

Retained-guidance field fixtures control immediate, delayed and final-work-tick
dispatch through the model harness. Resume checks exact remaining work or a
completed native harvest when the final tick clears it. Paused checkpoint bytes,
fresh epoch and guidance, cancelled old replies and the once-only harvest event
remain checked in every scenario.

Tests of production scheduling or concurrency retain the production scheduler.
For example, the conversation usage-limit regression must reserve and pause
while the real tick gate is held; a manual scheduler would move that work after
gate release and erase the deadlock proof. Its watchdogs diagnose failures and
do not provide model-entry synchronization.

`AsyncWaitBoundaryTests` rejects new clock waits, timeout cancellations, runtime
reflection and asynchronous provider gates outside `TestSupport/ModelWork/`.
Its expression-specific legacy ledger includes existing provider-timeout and
production-concurrency diagnostics; entries must match the source and can only
shrink against the checkout's first parent. Required PR CI uses a synthetic merge
whose first parent is the base; a local head-only run compares only its preceding
commit. Fetch two commits when running the guard. The nightly workflow also
runs harness tests with one processor.

## Which checks to run

| Change | Check |
| --- | --- |
| Docs or wording only | Check links, headings and accuracy. Run existing documentation checks when files, metadata or links change. No new tests or full runtime suite are required. |
| C# behavior | Build and run the tests for the code you touched. Format the changed code and run the Godot client check when relevant. CI runs the full Release suite. Fix CI failures on the pull request: the author while it is a draft, or the reviewer once it is ready. |
| Godot scripts, scenes or UI | Run the Godot client check and relevant automated tests; inspect the affected controls in the game. |
| Windows export or device storage | Run the Windows export check and relevant native Windows checks. Test the actual bundle on Windows when claiming player usability. |
| Saves, events or compatibility | Include the replay and rollback checks described in [Saves and replay](saves-and-replay.md). During alpha, older saves need not keep loading, so no old-save or migration checks are needed; a save that cannot load must still be refused visibly and preserved. |
| Workflow automation (scripts, workflows, labels and templates in `.github/`) | Run `node --test .github/scripts/*.test.js`, and add or update tests when a script's behavior changes. For `.github/labels.json`, read the pull request's Labels check output to see what it would create, rename or delete; after merging, check that the Labels run on main passed. |
| Release | Follow the applicable [release gate](releasing.md#release-gate), including real player-path checks. |

The CI configuration in [.github/workflows/ci.yml](../../.github/workflows/ci.yml)
is the source for current automated gates. A PR needs green CI before merge;
local checks should fit the change. List checks you could not run and why.

### How CI runs

The Protect main ruleset merges pull requests only through GitHub's merge
queue, by squash, and requires three checks: `verify`,
`windows-documentation` and `windows-provider-storage`. They must pass on a
pull request's head before it joins the queue, and again on each batch the
queue builds on top of main. `verify` passes only
when every part of the Verify workflow passes:

- **scope** decides which of the other jobs the change needs.
- **checks** runs the workflow-script tests and the label list, the Godot
  client check, `dotnet format` and the Windows export.
- **tests (1)** to **tests (8)** split the Release test suite between them, so
  it runs on eight machines at once.

**test-times** then compares the run's test times with main's, taking each
test's median over main's last three green runs, and lists the tests that grew
most in the run summary. It warns when the tests the run shares with main take
40% and two minutes longer in total; tests that slow down
without changing usually mean the simulation got slower, for players too. It
never warns about a single test: one test can take two to four times as long or
as short between runs of the same code, depending on which tests share the
runner with it, while the total varies by about a tenth. This job is not part
of `verify` and never blocks a merge on its own.

Many small slowdowns can add up without any one of them warning, so every
Monday the **Test time budget** workflow adds up main's test times, each test at
its median over main's last five green runs, and compares the total with the
budget in
[.github/scripts/test-time-budget.js](../../.github/scripts/test-time-budget.js).
Over the budget, it opens one P2 tooling issue listing the slowest tests and
classes, and refreshes it each week while main stays over. Once main is a
twentieth under the budget, it closes the issue, unless someone holds it. If the
extra time is really needed, raise the budget in a pull request that says why.

A pull request that changes only documentation (Markdown files and anything
under `docs/`) runs the workflow-script checks and the documentation tests on
Linux and Windows, and skips the rest, including `windows-provider-storage`.

A pull request that changes only documentation and Godot client files also
runs the Godot client check, `dotnet format` and the Windows export, and skips
the test jobs and `windows-provider-storage`. The test project compiles only
the client files its `<Compile Include>` lines name, so no other client file
can change a test result. A change to one of those files, to anything under
`tests/`, or to anything outside the client folder runs everything.

Each batch the merge queue tests on top of main runs everything. The queue then
moves main to the very commit it tested. The push to main reuses the latest
queue run's completed, successful required checks on that exact commit, even
while the workflow finishes its nonblocking work, and runs nothing more.
Main's green runs on this page are successful queue runs whose exact commits
are ancestors of main (or main itself). The timing downloader pins main once
and pages past unmerged successes, including retries after a queue removal,
until it has the requested number of runs or exhausts the history. Missing
artifacts are left out, so fewer samples or the fixed split may be used. Without those required results,
or if they cannot be read, the push runs everything. When a pull request in the queue
fails, the queue rebuilds the ones behind it, and each rebuilt run cancels its
pull request's older queue runs. A newer push to a pull request cancels its
older run.

The test jobs split the tests by how long each took on main, so new slow tests
spread out on their own and nobody needs to rebalance them by hand. Each test
job uploads its durations as a `test-timings-<job>` artifact; **scope**
downloads those of main's last three green runs with
[.github/scripts/main-timings.sh](../../.github/scripts/main-timings.sh), and
[.github/scripts/ci-plan.js](../../.github/scripts/ci-plan.js) plans the split
from each test's median, because one run's times swing too much to balance by:

- xUnit runs four test classes at once on a runner's four cores, and a class's
  tests one after another. A class too long for one job is split by method.
- A test runs in the first job whose filter names it, and the last job runs
  every test no filter names, so a test that is new since main's runs, or
  renamed, still runs exactly once.
- If main's timings can't be read, the split falls back to the fixed one in
  `PinnedShards`.

No run can finish before its slowest single test. Each test job lists its ten
slowest tests in the run summary and job log, and warns about any test over five minutes.
The `test-times` job also prints its complete comparison with main in both places,
so reviewers can read it through the Actions logs API.
Aim for under a minute per test: start a test close to the moment it checks,
in the smallest world that shows the behavior.

Committed-tick timing checks measure a bounded synchronous delay inside the
real commit and child-selection callbacks. Each callback finishes its own delay,
so measurement does not depend on another test continuation releasing it.
The paired native world and checkpoint checks still prove that the measured
work changes no saved state; runtime-gate exclusion is checked separately.

To time tests locally, or list what a job would run under the fixed split:

```bash
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj --configuration Release --logger "trx;LogFileName=timings.trx"
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj --configuration Release --no-build --list-tests --filter "$(node .github/scripts/ci-plan.js filter 1)"
```

The first command records each test's duration in
`tests/ClankerWorld.Tests/TestResults/timings.trx`; the second lists
the tests job 1 would run without timings.

The native Windows storage job also runs the repeated checkpoint-compaction
test and checks that a refused overwrite preserves the previous checkpoint.
It uploads the test report as `windows-storage-evidence`. If the repeated-save
test encounters an I/O failure, that artifact also contains copies of its
disposable world files and first-failure diagnostics, including any temporary
checkpoint captured before cleanup. Successful test worlds are deleted. These
are synthetic test worlds; do not add a player's save directory to the upload
paths.
The deliberate lock and read-only controls mark their evidence with
`ExpectedFailureControl`, so their expected refusals remain distinguishable
from an unexpected stress-test failure.

To retain the same evidence for a local persistence test, set
`CLANKERWORLD_CHECKPOINT_DIAGNOSTICS` to an empty disposable directory before
running `FullyQualifiedName~WorldStateFileTests`. On I/O failure, the
tests copy evidence from their uniquely named temporary worlds there. A
passing rerun does not explain an earlier intermittent access refusal; retain
the failed report and its diagnostics when investigating one.

### Trace unexpected Windows checkpoint failures

For the unexplained overwrite investigation in [#778](https://github.com/ClankerWorldOrg/ClankerWorld/issues/778),
[Trace Windows checkpoints](../../.github/workflows/windows-checkpoint-trace.yml)
is a manual Windows job. Choose `target` for the unchanged 2,500-save test,
with one to twenty runs, or `full-suite` for one unfiltered Release run with
SingleHit coverage, matching the original failure's instrumentation. It first
runs the existing three intentional overwrite-refusal controls to check the
native trace, including an access-denied operation completion for each
synthetic fixture in the test process. It builds once, uses four test processors
and records all six
Simulation/Server/test DLL and PDB hashes before and after the unchanged tests.

On an administrator Windows machine with PowerShell 7 and the pinned .NET SDK:

```powershell
./scripts/trace-windows-checkpoints.ps1 -OutputDirectory C:/temp/checkpoint-trace-new
./scripts/trace-windows-checkpoints.ps1 -Mode full-suite -OutputDirectory C:/temp/checkpoint-trace-full-new
```

Use a new empty output directory each time. Windows' built-in `logman` records
kernel file activity; `tracerpt` decodes it. The trace is circular and limited
to 128 MB. If an unexpected escaping checkpoint exception appears in the
existing observer's evidence, the runner freezes the trace while the unchanged
tests finish. A failed run stops the iteration loop and keeps its failure
status; it does not replace that result with a successful rerun.

The workflow uploads only synthetic checkpoint events, test reports, binary
hashes and existing exception evidence as `windows-checkpoint-trace`. Raw ETL
and decoded whole-machine XML stay in the local `raw` directory, outside the
upload paths. The filtered event file keeps original native XML and resolves
file-object, file-key and IRP identities to synthetic checkpoint paths. The
runner retires file identities on close/delete and IRPs at operation completion
to limit reuse of old associations. Resolved paths are inferred correlations;
always inspect the original fields before attributing an operation.
Filtered paths can also refer to the observer's retained synthetic copies and
polling activity. Check the full path and process before treating event counts
as writes to an original checkpoint.
In a passing full-suite run, earlier checkpoint activity may have fallen out
of the circular window; zero selected events then means no retained checkpoint
activity, not proof that the original failure is fixed. The
trace summary records its capture statistics; check it for lost events or a
truncated window before treating missing activity as evidence.

Compare the unexpected exception's exact path, process, time and operation
with the matching native events. Keep intentional controls separate using
`ExpectedFailureControl`. A matching path or passing target alone does not
identify the cause, and tracing can change I/O timing. Keep the original failed
capture even when a separate control passes. The script changes no persistence
code, durability checks or test assertions.

Hands-on checks above describe useful verification, not a blanket pre-merge
playtest gate. Routine owner playtesting may follow merge under
[Change and verification rules](../../CONTRIBUTING.md#change-and-verification-rules). Keep pending
playtests explicit in the [playtest list](#windows-playtests); do not equate a
passing automated check with actual play.
The separate release gates still apply when preparing a release.

## Full build and test commands

From the repository root with the selected SDK:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
bash scripts/verify-godot-client.sh
bash scripts/verify-godot-windows-export.sh
bash scripts/package-windows.sh   # optional: the portable zip
```

For C# changes, run `dotnet format --no-restore` first if formatting needs
applying, then verify that no changes remain.

`verify-godot-client.sh` downloads the pinned engine archive, checks its
SHA-256, builds the scripts and starts the scene headlessly.
It also prepares a disposable generated world with the hard-world and
accepted-birth fixtures, advances it for three normal game days with local
model replies, and opens every agent's Profile in fresh Godot processes at
1280×720 and 1920×1080. The check times selection, the first Profile button
press and the following layout frames, checks Back and missing optional data,
and fails on engine errors. External watchdogs also catch a stalled main thread.
This adds a native-world run to the client check; it does not contact a model
service or inspect an owner's saves, and does not replace a Windows playtest.
`verify-godot-windows-export.sh` verifies the pinned editor and templates,
creates an unsigned Windows x64 PE bundle and writes a SHA-256 manifest.
The export stages tracked files from the checkout's current commit, so commit
any changes you want in the bundle first. Uncommitted and untracked files stay
out of the export. It reads the game version from the staged project's MSBuild
metadata, then passes that commit as `SourceRevisionId` into the staging build.
It checks that the exported client assembly contains both values. The manifest
records the build line and full commit. Godot requires four numeric parts in
Windows file/product version fields, so those use the assembly file version;
the executable's product name carries the readable version and short commit.
Local builds get their revision from the SDK's Git integration. A source archive
without Git metadata must provide `SourceRevisionId` to MSBuild to identify its
origin; otherwise the game honestly reports `unknown`.
CI uploads that bundle as an artifact. These checks verify the build and
export; they do not replace playing the bundle on Windows.

### Portable Windows package

`bash scripts/package-windows.sh`, run after the export above, builds the
portable alpha package ([#468](https://github.com/ClankerWorldOrg/ClankerWorld/issues/468)):
`export/package/ClankerWorld-<version>-windows-x64.zip` and its `.sha256`. The
zip holds one folder named for the version with the Godot export, a
self-contained `win-x64` world server in `server/` (`dotnet publish`, no .NET install
needed on the player's PC), a short `README.txt`, a `package.json` naming its entry points
(`{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}`)
and a `manifest.sha256` of every file. The launcher and the game read the
entry points relative to the version's package root. When the game is in a
subfolder, it finds the enclosing manifest that names its executable; local
developer runs use the same root for the server and options marker. A newer
package can move its programs without making an installed version look damaged; a
version without `package.json` is older, with its server in `host/`. The script refuses an export that was not built from the current
commit, stages the server and version from committed inputs (excluding local
edits), and checks that the server assembly carries the same version and commit,
since the game reuses only a server of its own exact build
([Device pairing](device-pairing.md#local-server-started-by-the-game)). The server and
Simulation projects list `win-x64` in `RuntimeIdentifiers` so this publish
restores in locked mode. CI uploads the zip as the
`clankerworld-windows-11-x64-portable` artifact. It is not signed by the build job; the owner adds an
[offline release signature](releasing.md#offline-release-signatures) before
distribution. A built zip is not a Windows playtest.

### Windows launcher

Downloaded releases require the owner's [offline signature](releasing.md#offline-release-signatures).
The launcher checks that approval before extraction, retains its approved
manifest hash, and checks the signed manifest and installed files before Play.
The signing key stays outside the build; tests use temporary keys. Explicitly
selected local builds keep the existing Developer mode path.

Release metadata must name files under this repository's GitHub release-download
path. Package and signature responses also check the final HTTPS address after
redirects against GitHub's download hosts. The package stays open exclusively
from download and hashing through extraction; extraction never reopens its path.
The portable README directs players to unpack inside their own Windows user folder.

The launcher ([#1566](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1566))
is the same Godot project exported with the `launcher` feature, which makes
`run/main_scene.launcher` open `res://Launcher/Launcher.tscn` instead of the
game. `bash scripts/verify-godot-windows-export.sh --launcher` exports it to
`export/launcher-windows-x64/ClankerWorldLauncher.exe`, and
`bash scripts/package-windows.sh --launcher` zips it, without a server, as
`ClankerWorld-Launcher-<launcher version>-windows-x64.zip`. The launcher's own
version is `Launcher.Version` in `Launcher/Launcher.cs`, separate from the game
version. CI uploads it as the `clankerworld-launcher-windows-11-x64` artifact.
`bash scripts/verify-godot-client.sh` also opens the launcher scene headlessly
with `--launcher-smoke-test`. Its install, hash-check, repair and remove logic
lives in plain C# under `Launcher/` and is tested in `LauncherTests`.

Developer mode offers separate test data, per-run detailed logs and a local
portable build picker. Build a local portable game with the game export and
package commands above, unpack its zip, then choose its `ClankerWorld.exe`.
The game and package manifests carry `# Launcher options: developer-data-v1`;
the launcher refuses the new run options on a build without that marker so an
older game cannot silently ignore test isolation. Local builds need the world
server their `package.json` names too. Normal installs retain the existing
download and manifest checks; local builds are files the developer chooses.
See [Launcher developer runs](how-it-works.md#launcher-developer-runs) for the
data paths and logging switches. These controls still need the listed Windows
playtest.

The program icon, `src/ClankerWorld.GodotClient/icon.ico`, is generated from
the logo art in `UI/Menu/MenuLogo.cs` and embedded in the exported `.exe`. After
changing that art, rebuild the icon with
`godot --headless --path src/ClankerWorld.GodotClient -- --write-app-icon`.
The UI smoke test fails if the committed icon no longer matches the art.

### Windows installer

The `windows-installer` CI job builds the unsigned per-user setup from the
verified game and launcher zips, then runs it under a fresh standard Windows
user. In Windows Git Bash, run:

```bash
bash scripts/package-windows-installer.sh <game.zip> <launcher.zip> export/setup
```

The script verifies both zip checksums, their internal manifests and their
matching source commit. It compiles the committed installer script and icon
with Inno Setup 6.5.4, downloaded through the shared tool cache and checked
against its pinned SHA-256. The official compiler runs in portable mode.
CI uploads `ClankerWorld-Setup-<game version>-windows-x64.exe`, its `.sha256`
and `build.json` as `clankerworld-windows-11-x64-setup`.

Setup installs the launcher in `%LOCALAPPDATA%\Programs\ClankerWorld` with
Start-menu and optional desktop shortcuts and a per-user uninstaller. It
stages a bundled game on the destination volume and publishes its complete
folder under `%LOCALAPPDATA%\ClankerWorld\versions` only if that version is
absent. Existing version folders remain intact, even if the same version name
came from a different development build. Uninstall removes launcher program
files and shortcuts and keeps game versions, saves, keys, settings and usage.

`scripts/verify-windows-installer.ps1` checks actual setup, upgrade, reinstall,
uninstall and install after removal, including native launcher startup,
shortcuts, HKCU registration and preserved file hashes. Its older setup is a
synthetic metadata/README fixture; it is not a published launcher release.
The required `verify` job waits for this check whenever code checks run.
Native logs and results are in `windows-installer-evidence`; the owner's
Windows 11 install/play/uninstall/reinstall check is kept in the
[playtest list](../../playtest/README.md), and folded into the release checklist
before publishing.

## Compare tick equivalence

For a behavior-preserving runtime change, run the same portable native probe
against the base and candidate checkouts:

```bash
bash scripts/compare-tick-equivalence.sh /path/to/base /path/to/candidate /tmp/tick-proof
```

The script needs the pinned .NET SDK, Python 3 and an output directory without
previous `base` or `candidate` results. Like `measure-town-ticks.sh`, it compiles
one unchanged C# probe against each checkout. It uses the built-in deterministic
provider, with a wrapper that records observations and returns its response
unchanged. It makes no model-service calls.

Defaults are two seeds, sixteen ticks per seed, and both generated Small worlds
and the populated legacy test map. Generated worlds use native first-Town layout,
founder placement and Start World. Override seeds, tick count and modes with
the fourth through sixth arguments, for example:

```bash
bash scripts/compare-tick-equivalence.sh /path/to/base /path/to/candidate /tmp/tick-proof-64 town-project-real-donation 64 generated
```

The comparison includes the initial frame and every committed tick. It compares
decompressed checkpoint bytes exactly, ordered world events and every observed
cognition digest. At the first differing frame it prints the seed, mode and tick,
the first checkpoint byte offset, and the first differing event or digest value.
Exit status is zero for equality, one for a difference, and two for a failed
build, probe or comparison. A failed setup cannot count as equality.

Each output contains the actual checkpoint bytes compressed with gzip, JSON
events and digests, the unchanged probe source, build/probe logs, and the
checkout commit and working-tree status. Keep this evidence with the PR. These bounded scenarios
prove equivalence for their exercised paths; run additional seeds and longer
tick counts when the moved area needs them. They are correctness checks, not
tick-performance measurements. The [runtime system rules](how-it-works.md#runtime-systems)
describe the state and ordering contracts that every refactor step keeps.

## Focused documentation checks

```bash
dotnet restore tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj --locked-mode
dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~DocumentationTests
```

These checks cover front matter on pages under `docs/`, local links and linked
headings in every Markdown file, that each entry in `changes/` starts with
a `- ` bullet, and that game-design chapters don't describe what is built,
such as "not built yet" or "remains unfinished". They do not check that any particular page exists. The
documentation test reads each page with both LF and CRLF line endings, and CI
also runs it on a Windows checkout. When the change needs code checks, that
job also runs `IdentityTextBoundaryTests` against the native checkout paths and
the base commit's identity/text allowances. That job is separate from a Windows game
playtest and from the native provider-storage checks.

To find sentences a change may have made false, search every page for the
names of what changed:

```bash
node scripts/find-stale-docs.js "orchard" "plant_orchard"
```

It lists each paragraph, list item or table row that names one of them and
says something is missing, unfinished or undecided, or that sits under a
**Still to decide** heading. Read each one: the search finds candidates, and
only reading decides whether a sentence is still true.

The script scans `docs/`, `tools/` and the root `README.md` for status wording;
it does not report every obsolete identifier or scan `skills/`. For a rename,
also search the affected pages and skills directly, for example with
`rg -n 'OldType|old/path' docs/development skills`, and compare each technical
name and command with the current code.

## Disk space

Sessions on one machine share the tools they download, and each worktree keeps
its own build output. A shared tool cache, a scratch folder for each run and a
cleanup script keep the disk from filling up, without weakening any check.

### Shared tool cache

The Godot scripts download their pinned tools through
[scripts/tool-cache.sh](../../scripts/tool-cache.sh), into
`~/.cache/clankerworld/tools` (under `$XDG_CACHE_HOME` when it is set). Set
`CLANKERWORLD_TOOL_CACHE` to move it.

- Each entry is keyed by its pinned SHA-256, so every version and platform has
  its own. A download is checked when it arrives and each time it is used, and
  a damaged copy is downloaded again.
- Each archive is unpacked once, made read-only and shared by every worktree
  and session.
- A lock lets several sessions use the cache at the same moment. A changed pin
  downloads the new version; the cleanup later removes the old one.
- Without `flock`, the cache uses an atomic directory lock. A stale or
  incomplete lock stops the run and names the directory to inspect; it never
  guesses that another session's lock can be removed.

### A scratch folder for each run

Each Godot check gets its own temporary folder for Godot's settings, caches and
the game's `user://` files, and removes it when it ends, so runs from different
worktrees or sessions share no state. Build output (`bin/`, `obj/`, `.godot/`)
stays in each worktree, so one worktree's build never affects another's, and
the Windows export still builds only from committed files.

### What to keep

- Put test reports, coverage, logs and other evidence for your work in
  `.evidence/` in your worktree, which git ignores.
- Put what is worth keeping after the work ends in `.evidence/keep/`: evidence
  of a failure, and records a pull request or issue cites that can't be
  recreated.
- Everything else can be rebuilt and goes when the work is finished: build
  output, exports, per-test coverage reports and the output of runs that
  passed.

### Cleanup

`scripts/clean-workspace.sh` lists what can go and why; `--apply` removes it.
Run it when you finish a job on your own machine.

- It removes a worktree only when its work is finished (its branch on origin
  was deleted, as GitHub does after a merge, or its commit is on main), it has
  no uncommitted, untracked or local files such as `.env` or `saves/`, it isn't
  locked or the one you run it from, and nothing in it changed for 12 hours.
  It checks branch deletion on origin directly, and keeps missing or unmounted
  worktree records, unfinished Git operations, hidden local edits and commits
  held only in a worktree's reflog.
  The branch stays, so `git worktree add <path> <branch>` brings the files
  back. `.evidence/keep/` first moves to
  `~/.local/state/clankerworld/kept-evidence`. Archives older than 30 days are
  listed but kept. Inspect them before adding `--prune-kept-evidence` to
  `--apply`; that flag explicitly selects old archives for removal.
- In other idle worktrees it removes only build and test output.
- It removes tool versions no script pins that went unused for 30 days,
  holding the same lock as cache users and checking the age again. Unknown
  names in overridden cache or evidence folders are kept, as are unknown
  files in the older Godot download cache. Interrupted partial downloads are
  cleaned after a day.
- It removes idle managed Godot scratch folders only when their saved owner
  process has ended. Measurements, failure worlds, unmarked older temporary
  folders and live runs are kept for inspection.
- `git worktree lock <path>` keeps the cleanup away from a worktree, for
  example during a long pause.

It covers only the worktrees of the clone holding this script, and refuses
to run from an unrelated repository. Run that clone's copy in each separate
clone. Retention arguments must be positive whole numbers.

## Windows playtests

For native Town performance comparisons, use [Measuring Town tick time](town-tick-measurements.md).
Its headless readouts complement the Windows checks below.

The [playtest list](../../playtest/README.md) holds the merged changes that
still need trying by hand in the Windows game. Automated checks, including the
Windows CI jobs and a passing export, do not count as a playtest.

When you record a playtest result, note what you know of the client and server
commits, the Windows build, the date, the screen resolution and the interface
size it picked; ask the owner for missing details only when a check failed. Say
whether each check passed, failed or could not be run, with what you did and
saw. Leave out private keys, pairing codes, agent thoughts and raw model
replies. The agent receiving the owner's chat report updates the playtest
checklist and records these build details in any linked Bug or Implementation
issues, labelled `from:playtest`; do not create a separate report issue. Follow
the [playtest results procedure](../../playtest/README.md#keeping-the-list-current).

Do not deploy, change credentials or modify the active playtest save just to
run a check; that needs the owner's separate go-ahead. Use a disposable world
for setup, placement and damaged-save cases.

## Hard test worlds

`NormalPathWorld.CreateGenerated` defaults to 128-character saved founder IDs
and names containing accents, emoji and combining marks. The fixture prepares
these identities through the Society founder operations after validating ordinary
GUID placement; the game's founder placement grammar stays unchanged. Tests
refer to `NormalPathWorld.Founder1` through `Founder4` instead of short literals.
`SecondGenerationFixture.Add` adds a child through an accepted Society birth,
including food consumption, parentage, care and household membership, then adds
the physical agent and Town residency. It retains the production-generated child ID.

### Every offered choice

`OfferedChoiceInvariantTests` builds a small seeded hard world with a born child,
split stock, a broken vessel, a borrowed Market stall, a full carrier, occupied
tiles, a swimmer, a mounted rider and a boat passenger. Birth, split stock and paid
building inputs use shared native operations. Stall occupancy, mounted-animal and
underway-boat records are controlled strict-valid fixture state. Each native queued choice
runs in its own trusted server trial copy, through normal admission, action
application and post-action reconciliation. The test requires a strict save
round-trip, acceptance of the exact offered choice without fallback, and progress
or a native refusal reason. It does not recover or skip a throwing action.
Progress includes medical consent, treatment and supply trips; model admission metadata alone does not count.

The normal PR suite checks one seed and one snapshot. Reproduce a reported seed
with:

```sh
CLANKERWORLD_CHOICE_SEED=1815 dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --filter FullyQualifiedName~OfferedChoiceInvariantTests
```

Failures report the seed, scenario, tick, snapshot, agent and choice ID. Set
`CLANKERWORLD_CHOICE_TICKS` to at least the reported snapshot plus one to reach
that case. `CLANKERWORLD_CHOICE_SEEDS` adds consecutive seeds. The independent
`choice-invariants` nightly job checks four seeds and 32 snapshots each, even
when no nightly release is needed. Real failures belong in separate Bug issues
and fixes; this test waits on those fixes rather than suppressing cases.
