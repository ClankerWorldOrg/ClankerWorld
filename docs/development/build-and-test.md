---
title: Build and test
type: development-reference
status: active
updated: 2026-10-13
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

The local server relaunch fixture separates request timeout from caller cancellation.
Its timeout rows keep the caller token live and bound observation separately;
the cancellation row cancels only after the held relaunch request starts.
A controlled row delays delivery of the actual two-second request timeout beyond
the former five-second caller timer, retaining the real server and secret checks.

The native land-transfer fixture sends each eligible adult one suggestive
instruction when its private provider changes from waiting to reading or
consenting. Changing a test provider's flags does not wake an admitted idle
intention. A controlled row reserves the actual garment stock to keep an
unrelated clothing choice from waking the readers, then verifies their exact
requested guidance, offered reading choice and admitted idle reply. Each phase
keeps its forty-tick limit and waits for ordinary and life-moment identity
replies before admission. Parenthood and midlife requests use a separate task
queue and defer that adult's ordinary choices until their replies are admitted.
The fixture answers the actual optional-identity choice by keeping the person's
identity. A controlled row holds a real midlife reply and checks that the
fixture waits, then retains all notice, consent, checkpoint and replay checks.
Reading and silence still give no consent; each adult must personally accept,
including the separately held filer. Server disk writes, unchanged permissions
before agreement, rollback, strict reload and complete-byte replay remain checked.

The Town membership deadline fixture captures the exact registered model
request at the last valid acceptance tick without waiting for the provider's
held-request startup signal. It checks that request's acceptance choice and
remaining world time, then observes entry of that same provider callback before
advancing to expiry. The entry wait has a separate thirty-second diagnostic
bound and preserves complete checkpoint bytes. Private release gates complete
inline, so releasing the late reply does not queue extra provider continuations.
A private deferred-entry row gates that startup signal until expiry and checks that the late reply
uses the same request; another row delays real provider entry for six seconds.
Reply and actual wrapper-completion waits retain their five-second bounds.
Completion reuses the shared request-task wait, which also finishes when no
call is pending; saved attempt status is not a live task boundary. A deferred-return
row holds the provider after reply construction and checks that the shared wait
remains incomplete with checkpoint bytes unchanged. The completion loop retains
eight world ticks. The remaining deadline, stale-generation, thought exclusion,
physical-property, later ordinary
sponsorship, pause, replay, rollback and strict-reload checks remain.

The held housing-reply fixture captures the registered request before waiting
for provider startup. Its deferred-start rows keep that same request pending
through lesson progress, then observe entry with the world frozen. It retains
the unapproved housing request, no admission or completed reply, refused-tick
preservation and strict reload. The thirty-second entry bound is diagnostic;
waiting for it spends no native ticks.

Shared compiler, analyzer and version settings live in
[Directory.Build.props](../../Directory.Build.props). Use its version fields
rather than adding a second version constant.

The owner-action body-deadline fixture completes its stalled read directly from
the actual read token's cancellation callback. It retains the initial partial
JSON, both body locations and both cancellation sources, the 500 ms client
setting and five-second diagnostic waits. The stream adds no asynchronous
continuation between cancellation and read completion. The following real pause
must still acquire the released action gate.

The held collection-cancellation check keeps the first collected load fixed
while waiting for actual provider invocation, the validated operative request
and the constructed late reply. Its deferred-entry case opens a preparation
gate only after checking that first frame. Each signal has a thirty-second
diagnostic deadline; advancing ticks to poll it could collect another load
before cancellation. The check retains real request and order identity,
exact saved bytes while waiting, one collection event, the untouched second
load and strict checkpoint reload after cancellation.

The retained-guidance farming and held repair-cancellation fixtures wait for
the provider's start signal by polling the native nonblocking tick path. A
committed tick schedules background work but does not guarantee that its worker
has started. The checks cover immediate entry and entry after two more committed
ticks. Their polls retain a forty-tick limit and a separate thirty-second cancellation guard
for each tick, including after pause and reload in the farming fixture. The
repair check also waits for the released reply before checking that cancellation
prevents repairs. Use this pattern only where advancing ticks preserves the
intended cancellation boundary; orders that can already complete physical work
must keep the world at that boundary while waiting for provider entry.

The held lesson-continuation fixture captures the actual registered request
and model task, permits at most six additional native ticks, then observes
provider entry at a frozen checkpoint with a thirty-second diagnostic bound.
Its deferred-entry cases hold preparation until two more ticks commit; one also
delays worker entry by four seconds. The same registered call must stay pending,
and waiting must leave checkpoint bytes unchanged. The progress baseline is
captured only after entry, with room for three further lesson steps. The offered
partnership answer stays held throughout; denied-tick bytes, continued training,
unanswered partnership and strict reload remain checked. Pause must finish the
actual cancelled model task without changing the paused checkpoint.

The initial identity-pause fixture snapshots its registered worker through the
existing shared model-request wait after one committed native tick. It observes
the real request and cancellation token at provider entry with the complete
checkpoint frozen, then checks pause cancellation and the finished late reply
without changing the paused checkpoint. The wait is captured before pause
removes the live pending call; no new private-field reflection is added.
The saved identity opportunity remains unconsumed on strict restore, with no
late personality, aspiration or identity event applied. Immediate, deferred and
four-second entry cases use thirty-second phase diagnostic bounds; no extra
native tick is spent on scheduling.

Paired native host checkpoint fixtures must finish matching external inputs
before advancing either continuation. Ordinary model decisions, life-moment
identity replies, wills and conversation turns use separate task queues. Native
household formation also finishes ordinary and life-moment replies before
counting its next bounded tick, using the shared land-transfer wait. Formation
succeeds only from committed membership; constructing a provider reply does
not end the fixture's offered choices. Waiting
only for ordinary decisions can compare a requested identity in one checkpoint
with a kept identity in the other. The child household-change fixture awaits all
four queues without advancing another tick, retaining complete checkpoint bytes,
strict file reload, cargo and refused-arrival checks. Its controlled identity
check holds a real provider request to verify that the fixture waits at this boundary.
Town food-inheritance checks likewise await the registered will task at the death
checkpoint before its next admission tick, so polling a background worker cannot
advance the will deadline.

The held storage-cancellation check keeps the world at its first committed
load while waiting for actual provider invocation, the validated operative
request and the late reply. Its deferred-entry row holds provider preparation
until that first frame is checked and the test opens the entry gate. Each
signal has a thirty-second diagnostic deadline. Advancing native ticks during
this wait could store another load before cancellation, because chosen work
continues while a model waits. The check retains one stored load, the untouched
second load and exact checkpoint reload after releasing the cancelled reply.

The held field-cancellation fixture stops at its first working frame, or after
the deliberate two additional ticks in its deferred-entry row. It captures the
existing shared model-task wait before cancelling the order, then waits for
actual provider entry without advancing the world. The deferred row also delays
entry by one second. Waiting for entry and releasing the cancelled reply must
keep checkpoint bytes unchanged; the original eight subsequent ticks still
check the cancelled order, unspent seed, released reservation and absent planting
event. Polling additional ticks for entry could finish planting before cancellation.

The long-recipe tool-request fixture admits its request through the personal
provider and saves it through the real server. For byte-for-byte continuation,
it pauses that world, captures one checkpoint, and restores both compared
worlds with synchronous idle providers for every resident. The personal
provider returns for withdrawal. Independent background reply timing is not a
matching replay input, even when both providers choose idle.

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
provider gates keep reply completion independent of the thread pool.

`SettleAsync` starts pending work and finishes returned wrappers while held
calls remain held. `PendingModelWork` snapshots all four live queues, with the
request and actual completion task. Observe registered, started and finished
calls through `IModelWorkObserver`; admitted, discarded and cancelled outcomes
come from the live runtime. Admission includes its committed world tick. A
refused tick publishes no admission. Use `TickUntilAdmittedAsync(actor, maxTicks)`
for bounded acceptance and `AdvanceTogetherAsync(other)` for paired checkpoints.
Pass a test clock to `ModelWorkHarness.For(runtime, clock)` to control identity,
will and conversation deadlines; production uses `TimeProvider.System`.

The proof fixtures use this harness; remaining clock waits move in #1818. New
model tests use the harness and no reflection into runtime queues.

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
moves main to the very commit it tested, so the push to main finds that green
run and runs nothing more, and main's green runs on this page are the queue's.
A push the queue didn't test runs everything. When a pull request in the queue
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
slowest tests in the run summary and warns about any test over five minutes.
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
entry points from the version they check or start, so a newer package can
move its programs without making an installed version look damaged; a
version without `package.json` is older, with its server in `host/`. The script refuses an export that was not built from the current
commit, stages the server and version from committed inputs (excluding local
edits), and checks that the server assembly carries the same version and commit,
since the game reuses only a server of its own exact build
([Device pairing](device-pairing.md#local-server-started-by-the-game)). The server and
Simulation projects list `win-x64` in `RuntimeIdentifiers` so this publish
restores in locked mode. CI uploads the zip as the
`clankerworld-windows-11-x64-portable` artifact. It is unsigned, and a built
zip is not a Windows playtest.

### Windows launcher

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
