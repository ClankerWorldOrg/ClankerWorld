---
title: Releasing
type: release-policy
status: active
updated: 2026-10-18
---

# Releasing

The latest release is **v0.1.0-alpha.1**. A successful build or commit is not
a public release. Keep the game version separate from the compatibility versions used
by saves, replay, content packages and network contracts.

## Game version

Public releases use [Semantic Versioning](https://semver.org/) and annotated Git
tags beginning with `v`. The first experimental line is `v0.1.0-alpha.n`;
`v1.0.0` is a deliberate stable-product promise, not simply the first build
that starts. Compatible corrections to a released line use patch versions.
Individual commits do not need version bumps because their source revisions
already identify exact builds.

The current .NET version metadata comes from
[`Directory.Build.props`](../../Directory.Build.props), not a second hand-maintained
constant. See the [build reference](build-and-test.md) for the selected toolchain.
The default version is `0.1.0-alpha.1`, matching the latest release. Development
builds can set `VersionSuffix=dev`; a version label alone does not make a
build a published release.
The SDK includes the checked-out source revision in the assembly's informational
version. Settings and Developer tools show that version with seven commit
characters, and the tooltip gives the full commit. The server's `/api/v1/status`
reply reports its own `build`, `version` and full `sourceRevision`, without world
or device state. A tagged release must report both its game version and source
revision.

## Matching client and server builds

For an owner-authorized remote world server update, follow the
[deployment checklist](remote-server-deployment.md). It covers staging,
paused backups, accounting, credentials and rollback; a private update does
not require a public release or tag.

When an action's signed format changes, ship or deploy the matching server before
distributing the client. A bundled local server and client must come from the
same verified source revision. Record both revisions for a private deployment;
a successful reconnect alone does not prove that New World or another changed
action is compatible.

Before distribution, use an approved test device and a disposable world to
preview and create with the accepted Advanced settings at the real signed HTTP
boundary. Verify an older server produces the update explanation, preserves the
pairing and cannot receive a downgraded action. New World preview and creation
require the server's authenticated `clankerworld.owner-world-creation.v3`
advertisement. A server advertising only v2, or no matching format, needs an
update; the client keeps the pairing and never sends a downgraded action.
See [Device pairing](device-pairing.md#client-and-server-updates).

## Save and content compatibility

The public version does **not** decide whether a saved world loads. The
existing save/replay envelopes carry their own contract, simulation and schema
versions; generator, clock, content-lock and asset versions are separate where
relevant. Change each field only when its semantics change, and cover replay
in tests. Once a release promises that older saves load, cover old-save
handling and migration too; during alpha they are not required (see
[Saves and replay](saves-and-replay.md)). A cosmetic game-version bump is not a
save migration.

Content packages have their own versions and compatibility/dependency rules.
Do not infer package compatibility from an alpha game label. Build revision and
engine-release information may be recorded for diagnostics, but must not alter
canonical world state or replay digests. The [current feature summary](../what-works.md)
describes today's supported save behavior; historical schema-specific release
notes remain in Git history rather than a growing checklist here.

[Saves and replay](saves-and-replay.md) owns backup requirements, matching
application/save rollback and the explicitly approved pre-release identifier
reset. That exception never permits deleting saves. During alpha an older save
may stop loading, but it is refused with a reason and kept.

## Release scope and merge day

The owner decides when a release is being prepared and what it needs. When
more is open than one release can take, release labels in
[`.github/labels.json`](../../.github/labels.json) split the open issues
([Release scope](../../CONTRIBUTING.md#release-scope)): `release:alpha-2`
marks those for `v0.1.0-alpha.2`, the current release. Alpha 1 is published
and `release:alpha-1` is retired. No next release after alpha 2 has been named;
until the owner names one, other new issues keep no release label.

1. Fixers take current-release issues first, then issues with no release
   label, and never start a next-release issue before the current release is
   published. Only new P0 or P1 bugs and work the owner adds join the current
   release; every other new issue gets the next release's label.
2. A merge day is a day the owner spends getting the repository as clean as
   possible before a playtest, and they say so in chat. Fixers finish the
   open issues and reviewers merge what is ready, until nothing is left open.
   Work that needs no other pull request open, such as renaming folders across
   the code, goes in last.
3. The session the owner asks to prepare the release follows the
   [release gate](#release-gate) below.
4. After publishing, move the release's label to `retired` in `labels.json`.
   The next release's label becomes the current one: update the label names in
   CONTRIBUTING's [Release scope](../../CONTRIBUTING.md#release-scope), the
   fix-issue and find-bugs skills and this page. Add a new next label only when
   the owner names that release.

## Release gate

Prepare a release when the owner requests it; do not tag every merged change.
Before publishing:

1. Choose the version in `Directory.Build.props`: for alpha 1, keep
   `VersionPrefix` at `0.1.0` and set the default `VersionSuffix` to `alpha.1`.
   Run
   `bash scripts/collect-changes.sh` to move the entries waiting in `changes/`
   into `CHANGELOG.md`; the script requires full Git history. Rename
   `## Unreleased` to the dated release heading and add a new empty
   `## Unreleased` above it. Keep each release as one flat list without
   categories.
2. Run the applicable build, test and Godot-export gates. The owner also runs
   the [Windows release smoke check](#windows-release-smoke-check) below.
   Fold every per-change file in `playtest/` into this release's checklist,
   `playtest/alpha-1.md` for alpha 1
   ([how](../../playtest/README.md#keeping-the-list-current)), and list its
   remaining checks in the release notes as "not yet checked by hand". Update
   links to files deleted by the fold, then run the documentation checks.
3. If compatibility changed, verify replay and rollback from a matching backup.
   Also verify migration and old-save handling when the release promises that
   older saves load.
4. After merging main into the release branch for the last time, run
   `bash scripts/collect-changes.sh` again. Move every newly collected bullet
   from `Unreleased` to the start of the dated release section, leaving
   `Unreleased` empty. Merge the release changes through the
   [contribution review process](../../skills/review-merge/SKILL.md).
5. The session that prepared the release fetches main and verifies the reviewed
   release PR's squash commit on GitHub's `origin/main`. Confirm its required
   checks passed and that `git ls-tree --name-only <commit> changes/` lists
   only `changes/README.md`. If it still has entries, collect them through a
   reviewed follow-up before tagging; do not tag an uncollected commit. Build
   the export and portable package from that verified commit: both scripts
   stage committed inputs, so local version edits are excluded. Check that the
   client, bundled server, package name and manifest report the chosen version
   and the same source commit.
6. That preparing session creates and pushes the annotated tag for the verified
   release commit, then verifies that GitHub resolves it to that commit.
   Never move or delete a pushed tag. Publish a GitHub release when there is a
   distributable artifact or useful release note. For an alpha, attach the
   [portable Windows zip](build-and-test.md#portable-windows-package) and its
   `.sha256` built from the tagged commit, keeping their file names: the
   launcher finds a game release by its `v<version>` tag and those assets,
   and verifies the package's SHA-256 against the owner's signed approval. Keep
   the `.sha256` for manual checks and older launchers. Add its
   [offline signature](#offline-release-signatures) before publishing. For a
   later alpha that includes the installer, also attach the
   [per-user setup](build-and-test.md#windows-installer) and its `.sha256` from
   the same tagged commit. The setup includes that game's
   current launcher and game; keep the portable zip for launcher downloads.
7. The launcher has its own releases, tagged `launcher-v<launcher version>`
   with the [launcher zip](build-and-test.md#windows-launcher). Publish one only
   when the launcher changed. Players download it themselves: the launcher
   never updates itself, it only says that a newer launcher exists.

### Rehearse without publishing

Use a detached scratch worktree of fetched `origin/main`, with full history.
Fold the playtest files, collect the changelog, set the version and run the
applicable gates there. Run the collector again and leave `Unreleased` empty.
Keep those scratch edits uncommitted.

The export and package scripts exclude those edits. To rehearse alpha 1's
artifact metadata from the unchanged main commit, pass the same override to
both commands:

```bash
VersionSuffix=alpha.1 bash scripts/verify-godot-windows-export.sh
VersionSuffix=alpha.1 bash scripts/package-windows.sh
```

Keep the zip, checksum, logs and pending manual checks as rehearsal evidence.
They identify the tested main commit, not a reviewed release commit. Record
Windows-only gates and approved-device checks that could not run. Post-merge
verification waits for a real release PR; pushing release changes, tagging and
publishing are outside the rehearsal.

### Windows release smoke check

Before each release, the owner checks the actual Windows bundle on an approved
test device and disposable world:

- Start the game. From the portable zip, it starts its own local server and pairs
  without a code; otherwise pair with the matching server.
- Create a New World.
- Load a saved World.
- Save, then quit.

Record the build and each result. A failed check gets a Bug issue and blocks
the release unless the owner explicitly accepts it. This short release check
does not claim that the release checklist's remaining checks were performed.

## Offline release signatures

The launcher requires an owner's detached approval before installing a downloaded
game or starting an installed release. The `.zip.sig` JSON sidecar binds the
package name, its SHA-256 and its internal `manifest.sha256` hash. ECDSA P-256
with SHA-256 signs a versioned canonical payload. The launcher pins only the
public PEM in `Launcher/ReleasePackageSignature.cs`; an empty pin refuses every
release. It retains the verified sidecar as `.release-signature.json` and checks
that approval, the manifest and all listed files before Play. A missing or wrong
signature refuses the operation and keeps existing installations and saves.
Developer mode's explicitly selected local builds retain their separate path.

The owner creates and keeps the private signing key offline, outside the
repository, GitHub, CI secrets and build machines. On the owner's offline signing
computer, with OpenSSL installed, use a new empty folder on offline storage.
The encryption password is entered locally, never in a command or chat. In a
POSIX shell, run `umask 077` first; the OpenSSL commands also work in PowerShell:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -aes-256-cbc -out clankerworld-release-private.pem
openssl pkey -in clankerworld-release-private.pem -pubout -out clankerworld-release-public.pem
```

Keep the password and an offline backup of the encrypted private file safe.
OpenSSL asks for the password again when signing. Give contributors only the public
PEM to pin through a reviewed launcher change. Do not paste, upload or log the
private key. No build job signs with it.

After verifying the exact tagged build and its existing checksums, move its
artifacts to the signing computer. Keep each package's original name. With
Python 3.11 or later and OpenSSL:

```bash
python3 scripts/sign-release-package.py sign ClankerWorld-<version>-windows-x64.zip clankerworld-release-private.pem
python3 scripts/sign-release-package.py verify ClankerWorld-<version>-windows-x64.zip clankerworld-release-public.pem
```

The tool creates `<package>.sig` and refuses to replace an existing approval.
For the installer, sign the game zip first and keep its sidecar beside it when
running `package-windows-installer.sh`; the installer then carries the game's
retained approval. Sign and verify the finished installer with the same two
commands, replacing the zip name with its `.exe` name. Sign a separately
published launcher zip as well. These detached approvals do not replace Windows
Authenticode signing or remove Windows' unknown-publisher warning.

Attach the original artifacts, their `.sha256` files and their `.sig` sidecars
to a **draft** GitHub release. Verify all sidecars against the pinned public key
before publishing. Release immutability was enabled for this repository on
October 18, 2026, with the owner's approval in chat
([#2275](https://github.com/ClankerWorldOrg/ClankerWorld/issues/2275)). Check
that it remains enabled before publishing, and confirm the new release is
immutable afterwards; the setting does not protect earlier releases. All assets
must be attached before publication. A repository administrator controls the
setting; the signing tool does not change it.

Nightlies need the same offline approval to be installable by this launcher.
The owner chose draft nightlies for offline signing in chat on October 18
([#2275](https://github.com/ClankerWorldOrg/ClankerWorld/issues/2275)).
Download the game and launcher artifacts from the nightly run, sign the game
zip, and rebuild the installer from that exact commit with the game's sidecar
beside the zip. Sign the final installer, replace the draft's preliminary setup
and checksum, attach both `.sig` files, verify them, then publish the draft.
The preliminary installer is not the final signed distribution: rebuilding it
includes the game's retained approval without putting a private key in CI.

## Nightly builds

The [Nightly workflow](../../.github/workflows/nightly.yml) stages a build of
main every evening as a draft GitHub pre-release. After the owner signs and
publishes it, Developer mode can try main
without a release (the owner's choice, October 10). It starts at 21:17 UTC,
which is 23:17 in Amsterdam in summer and 22:17 in winter, so the build is
ready by the owner's usual evening playtest (the owner's choice, October 15).
GitHub can start a scheduled run late, and **Run workflow** on main starts one
at any time. A nightly is not an alpha release and needs no alpha release gate;
it still needs the offline approval above before publication:

- It is tagged `v<version>-nightly.<date>.<run>`, such as
  `v0.1.0-nightly.20261011.12`, and the game reports that version. It carries
  the portable game zip and its `.sha256`, so the launcher installs and checks
  it the same way as an alpha. Every nightly also includes
  `ClankerWorld-Setup-<version>-windows-x64.exe` and its `.sha256` beside the
  portable zip. The [per-user setup](build-and-test.md#windows-installer)
  bundles the launcher and game from the same commit, using the same package
  scripts as CI. Linux builds both packages; Windows builds the setup before
  the staging job verifies the downloaded game and setup against their SHA-256
  files before attaching all four files to a draft. Only that job can write
  releases; it runs only on main and does not check out or execute repository
  code. It never publishes the unsigned draft.
  New nightly installs are offered in Developer mode. Installed
  versions and a version explicitly requested by a saved world remain
  available when Developer mode is off.
- Both build jobs use the commit on main that triggered the workflow, even
  when main advances before either job starts. It runs only when the newest
  nightly has a different commit and main's CI passed on the selected commit.
  Run it by hand from Actions with the main branch selected to stage a draft
  sooner.
- It keeps the seven newest nightly builds and deletes older ones with their
  tags, the one exception to never deleting a pushed tag. New drafts from this
  workflow do not create tags, so cleanup removes only those drafts. Never use
  a nightly tag for anything else.

Add a changelog entry in `changes/` in the same commit as player-visible
gameplay/UI, world-runtime, save-compatibility, deployment, packaging or
security changes; see [the contribution rules](../../CONTRIBUTING.md#keep-documentation-and-the-changelog-useful).
Documentation-only and test-only edits need no changelog entry unless they
alter an explicit supported promise.
