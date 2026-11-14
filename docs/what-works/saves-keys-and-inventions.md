---
title: Saves, keys and inventions
type: product-status
status: active
updated: 2026-11-14
---

# Saves, keys and inventions

Saves, recovery, keys, inventions and deletion.

## Saves, keys and inventions

The game warns when free space on the server's save volumes is below a
provisional 1 GiB. The notice stays visible while autosaves run, appears in
Save World before saving or overwriting, and clears when space recovers.
Unknown space is shown separately. These checks never prevent a save or delay
an emergency save; an actual disk-full write can still fail.

The [Linux hosting guide](../hosting-linux.md) describes the server-only Linux x64
download, fresh systemd installation and private comparison-code approval.
The service includes its runtime and keeps installation state outside its
program folder. A fresh-machine reboot and Windows join playtest is pending.

A written [remote world server deployment checklist](../development/remote-server-deployment.md)
is available for manual server updates. It covers staging a build, preserving
installation state, checking the updated server while paused and rolling back
if needed. The staged deployment command remains unfinished; the portable
Windows package is built in CI but not yet playtested. Each deployment or
rehearsal needs its own recorded results.

Worlds keep their own saves, model assignments and autosave settings. API keys,
pairing and the model-call count and limit belong to the installation. The
Windows server protects stored keys for the current Windows user; Unix servers use
private file permissions. Damaged or wrong-user key data is preserved. Moving
installations may require re-entry.

The rotating autosave schedule recovers within its configured interval after
the server's clock moves backwards, including across a restart.
Rotation keeps the newest copies on each known branch despite earlier future
dates, while named manual saves remain separate.
Autosaves that recovery cannot use stay on disk and do not count toward rotation,
including copies needing a deleted model key. Reducing the count or turning
rotation off keeps a verified usable predecessor.

If the active saved state cannot open on restart, the server stays available for
pairing and an authenticated **Recover world** screen. It offers the last
verified autosave from the affected world's current history. Recovery requires
an explicit choice, retains the refused bytes, leaves other saves unchanged
and opens the recovered world paused. Unusable newer autosaves are skipped;
without a usable autosave, the screen offers checking again or quitting. Older
alpha formats remain refused, with a format explanation and no migration.
Automated restart, file-preservation and native UI checks cover this path; the
[Windows playtest](../../playtest/alpha-1.md) remains pending.

If a live world tick fails because of one agent's action, the server refuses
that action and keeps time moving. The agent waits instead, their current order
is cancelled, and the same choice stays unavailable to them for one world day.
The Event Log explains what happened; Developer tools count refused actions.
Faults that cannot be traced to one action still hold the world paused. A failed
active recovery save can retry while paused; other remaining faults require
operator inspection. Preserve unsaved in-memory progress before restarting.

Named saves can be overwritten after choosing one and confirming. Load World
shows the selected world's save timeline directly and loads a chosen save on any
branch, or its newest save when none is chosen. The Pause Menu's **Load Save**
opens the current world's timeline directly. Playing on from an older save starts a new
branch and keeps the saves of the first version of events
([#679](https://github.com/compoodment/ClankerWorld/issues/679)). Load World, Load Save and
Save World draw a world's branches as a timeline over its seasons, mark where
the running world continues with **You are here**, and keep the save list,
grouped by branch, behind a **Timeline / List** switch
([#680](https://github.com/compoodment/ClankerWorld/issues/680)). A server from
before the timeline can't say where the world continues, so the marker is left
out. Branch rows and list tags use the recorded source save's name, such as
**From Before the flood**, and keep it if the source save is renamed or
replaced. Long labels fit within the controls and show their full text on
hover. Numbered labels remain when the source name is missing. These screens
have automated native checks but have not been checked by hand in the Windows
game yet. Unusable list
metadata is isolated so sound saves remain reachable. During the alpha, a save
from an older build may stop loading after an update; the game refuses it with a
reason and keeps the file. The agreed [mod compatibility](../game-design/inventions-and-mods.md#inventions-mods-and-technology)
rules await implementation. Recovery-history cleanup previews older verified
copies and requires explicit confirmation; its copy count and the available
disk-space warning's threshold stay provisional. Technical rules
are in [saves and replay](../development/saves-and-replay.md).

The Mod Library lists the current world's recorded packages read-only, one card each with
its name, who proposed it, its version and whether it is in use. Previously recorded
data-only building proposals and server approval rules remain, but agents no
longer create the retired shelter, storehouse or hearth proposals, and they do
not build approved designs, because households plan only their own buildings. General invention,
player review controls, personal libraries and mod import/export are unfinished.
The existing data-only package API checks one 512-character limit for content
IDs, including full definition IDs and resource kinds. Longer IDs are refused
before a package is recorded. Accepted IDs work in crafting, wills, land-sale
prices and resource knowledge. A will shows a short goods label while keeping
the full identity for inheritance.
Canonical asset IDs, reservation budgets and server approval catalog checks are
built; the test-only PNG normalizer and its rights tooling have been retired.
A complete creator experience is unfinished. Arbitrary generated scripts are
disabled. The agreed
[invention sandbox](../game-design/inventions-and-mods.md#inventions-mods-and-technology)
is unfinished. Multiplayer and public worlds are outside the current plan.

## Work and testing still to do

[Issues](https://github.com/compoodment/ClankerWorld/issues) holds bugs, work,
experiments and open decisions. [The playtest list](../../playtest/README.md)
records hands-on checks still needed for merged changes. Code, tests and exports are evidence
for the build; an export alone is not a Windows playtest or proof of the running
server. This page does not verify the owner's installed game or running server.

New World includes Low/Normal/High terrain controls under More options and a
resettable 50%-water preset for Small and Medium. All settings feed the matching
preview and saved generation options. See [More New World options](../playing/creating-a-world.md#more-new-world-options);
Windows interaction and real preview latency still need playtesting.

## Permanent deletion

The save list offers confirmed deletion of one save, including from the
title screen before entering the current world. Load World offers
separate deletion of an inactive world and all its saves. Other worlds, provider
credentials and pairing remain unchanged. Interrupted deletion stays hidden
from loading and is retried on server startup. Native Windows interaction still
needs playtesting. See [save controls](../playing/saving-and-reporting.md#save-and-return).

Save World also offers **Preview cleanup** for recovery history. It starts with
a provisional three verified copies per save (adjustable from one to ten), keeps
the latest verified copy and any recovery currently continued from, and names
every proposed removal before confirmation. Manual saves, migration originals,
unclassified older copies and unverifiable recovery copies are protected. Canceling
does nothing; a changed preview is refused. Nothing runs automatically or deletes
by age. The [Windows playtest](../../playtest/alpha-1.md) is pending.
