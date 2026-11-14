---
title: Installation and connection
type: player-guide
status: active
updated: 2026-11-14
---

# Installation and connection

Install the game, host a world and connect devices.

## Install or remove the launcher

Run `ClankerWorld-Setup-<version>-windows-x64.exe`. It installs the launcher
and includes that game version without an administrator prompt. Start
**ClankerWorld** from the Start menu; setup can also add a desktop shortcut.
Download the setup and its `.sha256` from a nightly pre-release on
[GitHub Releases](https://github.com/ClankerWorldOrg/ClankerWorld/releases),
beside the portable game zip. Each setup includes that nightly's launcher and game.
Windows may warn about an unknown publisher because the alpha is not signed.

To remove the launcher, open **Settings → Apps → Installed apps** in Windows,
choose **ClankerWorld Launcher**, then **Uninstall**. This keeps your installed
game versions, worlds, keys and model-call count. Running setup again, or a
newer setup, keeps those files too. An existing copy of the bundled game
version is reused.

## Host a world for other devices

On the Windows computer that will run the world, install Tailscale, join your
private network and enable its HTTPS certificates. Use Tailscale on the playing
computer too. Hosting is for your private network; do not enable public Funnel.

In the launcher, choose an installed game version and **Host a world for other
devices**, then **Start hosting**. Older builds need an update before they can
host this way. The launcher becomes a host app and shows the address to use on
the playing computer. It keeps the same worlds, saved keys and model-call count
as this installation. Developer mode's test-data choice stays isolated and is
remembered for automatic restart.

When a device asks to join, compare its code with the one on the host screen.
Choose **Approve** only when they match, or **Decline** to refuse it. A request
that was pending when the server restarted needs to be declined and started
again so both computers can show the same code.

When the last game leaves, the world saves and pauses. The host waits without
advancing time or calling models. **Stop hosting when the game quits** stops it
instead; choose this option before starting the host. By default the host
starts again when you sign in to Windows after a restart. Turn off **Start
hosting again when I sign in to Windows** to disable that.

Closing the host window minimizes it and keeps hosting in the background.
**Stop hosting** saves the world, stops the server and turns off the private
forwarding address without removing worlds or approved devices. A failed save
keeps the host alive and paused so you can free space and retry. If stopping
cannot be confirmed, the hosting controls stay available for retry.

To play on the hosting computer, choose **Play on this PC** on the host screen.
The game uses the running host's version and data folder, with no address or
comparison-code step. Older game builds need an update first. The host window
stays available in the background. Quitting that game leaves hosting and other
devices connected; the last game leaving follows the stop option above.

## Connect your device

To run the world on an always-on Linux x64 machine, follow the
[Linux hosting guide](../hosting-linux.md). It covers the server download,
private HTTPS access, service installation and local device approval.

For a world on another computer, open the launcher and choose **Join a world
on another computer**. Enter the host's HTTPS address. Both devices must be on
its private network. Compare the displayed code with the person approving the
device on the hosting computer; approve only when they match. The launcher
checks approval automatically, and **Check approval** checks again immediately.
Do not post the code, keys or private save data in an issue.

Once approved, choose a game version and **Play**. The launcher checks the saved
connection with this device's key and starts the game already connected. Choose
**Continue** from the game's Main Menu to enter the world. Later launches reuse
the saved connection without pairing again. The game has no address-entry or
pairing screen.

If the host cannot be reached, the launcher explains how to start it if it is
stopped: **Start hosting** on a launcher host, or the service command on a
terminal host. A connection failure can also mean the private network is
unavailable. Approval and certificate failures have their own messages.

Choose **Play on this PC** to return to your local worlds. To join another host,
open the joining screen and choose **Forget this connection** first; the
private device key stays in your Windows account's key store.

A game folder that carries its own `server` folder runs the world on your PC
instead. Starting `ClankerWorld.exe` then starts that world server, pairs with
it without a code and opens the Main Menu. Quitting saves the world and stops
the server. Your saves, keys and model-call count are kept in
`%LOCALAPPDATA%\ClankerWorld`, outside the game folder, and the server's log is
`logs\server.log` there, with the launch before it in `logs\server.previous.log`.
If the server cannot start, the game says why and offers **Try Again** and
**Report a problem**. If saving fails while quitting, the game stays open and
offers **Try Again** after you fix the save problem; it keeps the world server running.
Until the first alpha release, this portable package is the
`clankerworld-windows-11-x64-portable` build from CI
([#468](https://github.com/ClankerWorldOrg/ClankerWorld/issues/468)).

The **launcher**, `ClankerWorldLauncher.exe`, installs and starts these game
versions for you. **Play** installs the newest published version if needed and
opens its Main Menu; you still choose worlds in the game's **Load World**.
**Versions** lists each installed version with its size and the worlds it last
saved, with **Repair** (checks every file against the release) and **Remove**
(keeps your worlds), plus the versions you can install. **Settings** has
**Developer mode**, which turns on F12 Developer tools in the game and lists
the nightly builds of main under **Versions**, and buttons for the saves and
logs folders. With Developer mode on, **Use test data folder** is on by default:
the game keeps its test worlds, keys and settings apart from your usual data.
**Open test data folder** shows that folder; the saves and logs buttons follow
the selected folder. Turn **Use test data folder** off to play your usual worlds.
**Detailed logs** adds diagnostic detail for the next run. **Run a local build**
lets you choose a locally built portable `ClankerWorld.exe` with its `server`
folder. Builds too old to support these data and logging options are refused
until you choose a newer build or turn the options off.
Started without the launcher, the game turns
Developer mode on with the `--developer-mode` start option
(`ClankerWorld.exe -- --developer-mode`); runs from the Godot editor always
have it. A game the launcher started has **Quit to Launcher** on its Main Menu,
which saves, closes the game and reopens the launcher with that version chosen.
After **Play**, choose **Load World** on the Main Menu: the previous world is
already selected. It opens only when you choose **Load**. The launcher checks each download against the
release's signed approval before unpacking it and checks installed files before
Play. It never installs anything without your click, and never updates itself:
it tells you when a newer
launcher is published. If it can't reach GitHub, you can still play an
installed version. Until the first alpha release, it is the
`clankerworld-launcher-windows-11-x64` build from CI
([#1566](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1566)).

If the publisher withdraws a release, the launcher hides it from normal install
and Play choices. A world asking for that version gets an explanation; the
launcher keeps its installed files and your saves. Developer mode shows
withdrawn games with a warning. Downloading them still needs a valid release
approval.

### If a request loses its reply

If an instruction, cancellation or world edit loses its reply, the game keeps
it and checks it automatically after reconnecting to the same running world.
Other owner controls wait until the server confirms or refuses that request.

**Your last request** offers **Check and send again** or **Discard it** without
Developer mode. If a save was loaded, a different world opened or the server
restarted, check whether your request already took effect before sending it
again. Check and send again then releases the old saved request; an instruction
for the same agent and world is ready to review in their Profile before you
press Send. Discard it restores the other controls but does not undo anything
the server already accepted. Use **Tab** or **Shift+Tab** to reach the recovery
buttons and **Enter** to choose one; focus then returns to the screen underneath.

### Report a problem

**Report a problem**, in **Settings → Game → Build**, saves a zip to send with a bug report
and shows it in your file manager. It holds the game's log up to that moment
and the previous run's log, including after a forced close, the world
server's logs when the server runs on your PC, and a summary of the game
version, Windows version, graphics card, screen and open world. It never
includes keys, pairing codes, saves, prompts or model replies, and anything in
a log that looks like a key is blanked. Reports go in a `reports` folder (in
`%LOCALAPPDATA%\ClankerWorld` when the game runs its own server), and only the
five newest are kept. Nothing is uploaded; you choose where to send the file.

If the server changes, open the launcher's joining screen and choose **Forget
this connection**, then enter and approve the new host. To return to an
already approved host after local play, choose **Use this world**, then **Play**.
[Device pairing](../development/device-pairing.md) has the operator instructions.

If New World asks for matching updates, update the game and have the server
updated too. Keep your current device pairing; pairing again does not repair
that version mismatch. You can still reconnect to the existing world.
