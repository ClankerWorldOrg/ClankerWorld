---
title: Launcher hosting and developer runs
type: architecture
status: active
updated: 2026-11-14
---

# Launcher hosting and developer runs

This page describes launcher hosting, developer runs and world-list requests.

## Launcher hosting

`Launcher.Hosting.cs` runs the installed version's existing `LocalServer`
startup proof, approval and save/stop paths without opening the game. Hosting
requires authenticated release approval and a verified package with the
`host-app-v1` manifest marker. It uses the
same installation data root, including provider credentials and lifetime usage;
Developer test data remains separate. Reuse checks the protected host status
for the same mode and stop option, so another game or host cannot silently
take over its lifecycle.

`TailscaleHost` checks the installed helper's private-network status and exact
Serve configuration. It sets up only one HTTPS route to the ordinary loopback
listener, refuses public Funnel or another service's setup, and removes only
that checked route when hosting stops or setup confirmation fails. Changed
external routes are preserved; failed cleanup asks the player to check Tailscale.
Helper output is bounded and never
logged or shown as an error. The approval listener is never forwarded.

`LauncherSettings` records the hosted version and data choice. The optional
current-user Windows Run entry starts the launcher with `--resume-hosting`;
it resumes that saved choice minimized. The host window otherwise minimizes
on close; Stop hosting saves before exit. Raw comparison codes remain only in
process memory for host-local display, outside authority exports and world
state. [Device pairing](../device-pairing.md) owns the approval protocol.

The runtime uses an authenticated-presence signal while idle rather than a
polling timer. A launcher host saves a paused checkpoint after its last client
leaves, then waits or stops according to its option. Failed writes leave a
paused running host and retry before any optional exit. Normal active tick
cadence and explicit Resume are unchanged. Hands-on Windows/Tailscale checks
are in the playtest list.

## Launcher developer runs

The launcher also owns remote joining through `Launcher.Join.cs` and
`DeviceJoinSession`. Device registration storage has no Godot dependency and
keeps only public metadata; the current-user signer remains in the key store.
The normal and test-data folders use distinct registrations and key names.
[Device pairing](../device-pairing.md#pairing-flow) owns the protocol, host-kind
metadata and reconnect contract.


`LauncherSettings` remembers test-data and detailed-log choices. They apply
only with Developer mode on. Test data defaults on and lives under
`%LOCALAPPDATA%\\ClankerWorld\\launcher\\test-data`; server saves, provider settings,
usage, authority, launch secret and logs all derive from that root. Client
preferences, registration and pending submissions use its `client/` folder,
and the Windows signing key uses a separate `.TestData` name. Usual client
storage and keys retain their existing locations. Folder buttons and saved-world
version counts follow the selected data root. Quit to Launcher carries that
choice with the world/version hint.

`GameStarter` uses separate process arguments for local and installed games.
It sends `--test-data` and `--detailed-logs` only with `--developer-mode`.
Godot's `--log-file` points at `logs/game.log` in the selected data root.
Because that option disables Godot's own rotation, `GameStarter.PrepareGameLog`
moves the last run to `logs/game.previous.log` before starting the game,
replacing the older backup. A retry with no new log preserves that backup.
On Windows, Quit to Launcher uses shell launch so the returned launcher does
not inherit the game's open log handle and block the next Play's rotation.
Unix handoffs retain direct execution and separate arguments.
Standalone runs keep Godot's normal timestamped rotation. The project enables
`application/run/flush_stdout_on_print` in release builds too, so reports and
logs left by a forced close include the latest printed line.
`--verbose` applies only to a detailed run. The local server receives a
per-process `Logging:LogLevel:ClankerWorld=Debug` override and records a bounded
launch diagnostic. Framework logging, saved settings and the prohibition on
private model payloads in logs stay unchanged. Problem reports read these
selected logs through the existing redactor. A build without the manifest's
launch-option capability marker is refused when test data or detailed logs
are selected. A local build must include its local server. No save schema changes.

## World-list requests

Select World refuses an empty, missing or whitespace-only catalog ID with HTTP
400 before authorization or mutation, using the same required-payload guard as
the manual-save endpoints. Nonempty selections still require the signed owner
request and the existing world, compatibility and restore checks.

Launcher handoffs carry the catalog entry ID with the requested game version.
Both process boundaries use separate arguments; `--choose-world` accepts only
the catalog's 32-character hexadecimal ID, never a path or a display name.
Quit to Launcher obtains the active ID through the existing signed, read-only
catalog request before stopping the server. A failed read keeps the game open;
retry repeats the read. Failed stopping or launcher startup retains the
resolved version and ID together.

The game still starts at Main Menu. On the first successful visible Load World
listing, it selects the matching ID and consumes the hint. Failed listings
retain it for retry; later refreshes do not reapply it over a player's choice.
Successfully entering a world also discards the startup hint.
A missing ID leaves every row unselected with an explanation. Existing
compatibility and action-availability checks apply. Selection sends no pause,
select-world, load, save or delete request, and never enters a world.

The private server can retain an untouched, nongenerated bootstrap checkpoint
before the player creates a world. Its founder observation reports
`requiresWorldCreation`; this is derived from the saved state, independent of
whether the response includes cached terrain. Continue and Load World route
that observation into the ordinary New World screen before entering play or
resuming time. Preview selection, explicit acceptance of missed coverage
targets and signed creation remain the only way through that screen. The
redirect neither replaces the checkpoint nor removes its catalog entry, and
founders or authored progress prevent it.

New World preview refreshes wait for both an existing preview and a pending
owner action to finish. Menu visibility, observation generation and preview
revision still fence the waiting work, so only the current options are
requested and closing or switching screens discards the old refresh.

The server serializes read-only preview generation and terrain packing on a
separate coordinator gate. A running configured world can keep ticking and
saving while a preview computes; preview requests never hold its provider/world
mutation gate. Concurrent previews on that coordinator still run one at a time.
The same bounded candidate selection and complete terrain/layer packing are
used. Create and Select retain the world-mutation gate, confirmed pause,
preview identity checks and durable provider/save transactions.

Continue also belongs to the current Main Menu navigation. Opening Settings,
New World or Load World, returning to Main Menu, or starting another Continue
expires the earlier entry attempt. Its late refresh can update observations,
but cannot enter play or resume time behind another menu. A fresh Continue
after returning still enters normally.

The manual Save World and Load Save dialog owns one list read per opening.
Load World embeds those same history controls when a world is selected, using
the [catalog save-history read](../saves-and-replay/load-and-identities.md#browsing-saves-from-load-world)
without activating the world.
Closing it, creating, overwriting or deleting a save, or starting another opening
cancels the previous read. Late success and failure replies cannot replace current rows,
selection or status, including across a world or pairing change. Clearing or
rebuilding rows recomputes Load, Overwrite and Delete availability. Creating a
new save remains available while listing is slow.

Load World keeps a visible checking state until its signed catalog request
finishes. Back cancels the client request; a late response cannot overwrite a
newer list or New World screen. Results trigger layout after population so the
first opening can display them. The server checks inactive checkpoints once per
unchanged file in each process, then reuses only that structural result. It
hashes the file bytes to notice replacements and still checks required history
and model configuration every time; selecting a world performs a fresh restore.
When a private server starts, a background task makes those checks for every
inactive world, so the first list can reuse them. It holds the world-mutation
lock only to read the catalog and each file, never while decoding or restoring,
and a world it cannot check is left for the list to report as usual.
Open captures the chosen world's ID before
pausing, so a later catalog refresh cannot change its target. Open, Create and
Delete share the owner-action gate; selecting a different row cannot re-enable
Open or Delete until the current action finishes. Cleanup checks the current
selection rather than a row retained across an await.

Client cancellation stops the server between world assessments. It does not
interrupt one restore already holding the mutation lock.

World Settings reads autosaves for its current opening, paired device and
observed world. Closing Settings, choosing Game Settings or changing worlds
invalidates the read and disables Apply. Late replies and failures cannot
replace a newer opening's controls. A reply must name the observed world
before Apply becomes available, and Apply checks that context again before
sending the displayed interval and rotation.

The server checks rotating autosaves after an advancing tick. A UTC sample older
than the schedule's anchor resets and persists that anchor before checking the
interval. It keeps the last saved world tick and creates no copy at the reset;
the next due save still requires an advancing world. Disabled schedules return
before any write, and a clock reset checks the world identity before persisting.
