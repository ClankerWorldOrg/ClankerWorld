---
title: Saving and reporting
type: player-guide
status: active
updated: 2026-11-14
---

# Saving and reporting

Save or recover worlds and report what you notice.

## Save and return

When a save or another world changes model or key assignments, the game shows
those settings before loading. Check the model names and key labels, then
confirm to use them, or cancel to keep your current world and settings. A load
never supplies a new API key from the save.

If the server's save disk is low on space, a warning stays visible while you
play and appears in Save World before saving or overwriting. Make room soon;
saves can fail when the disk fills. The warning is advisory: manual saves,
autosaves and automatic recovery still try to save. If free space cannot be
checked, the game says so. The initial warning level is below 1 GiB and may
change after playtesting.

If the world server restarts with a saved world it cannot open, **Recover world**
appears before you enter the world. It offers the last usable autosave from
that world's current history and says when it was saved. Choose **Recover
autosave** to open it paused. Progress since that autosave will be absent from
the recovered world; your other saves stay unchanged and the refused file is
kept. There is no automatic replacement.

If one agent's action causes a game error during play, the agent waits instead
and their current order is cancelled. The Event Log explains the refusal. Time
keeps moving, and that agent cannot choose the same action for one world day.
Developer tools show the total number of refusals in this world. A fault that
cannot be traced to one action still leaves the world paused.

If no usable autosave is available, recovery stays blocked. **Check again**
checks for a usable autosave without changing your world. **Quit Game** closes
the game and its local world server, keeping the refused save unchanged for
recovery. A lost recovery reply also offers **Check again**
so you can find out whether recovery finished before trying it again.

**Save World** creates a named save for the current world. Its name starts as
the world's date, so **Save** works straight away; type another name first if
you like. The card under the timeline says whether the new save continues a
branch or starts a new one. To overwrite a save, choose it, then **Overwrite**,
and confirm; typing the same name does not overwrite it. Autosaves can't be
overwritten. When a long name makes the recovery copy's name reach the
save-name limit, it is shortened between whole characters, including emoji.
World Settings offers rotating
autosaves as well as automatic recovery. If the world server's clock moves backwards,
the next rotating save waits one configured interval from the corrected time.
Autosaves that recovery cannot use, including copies needing a deleted model key,
are kept separately from the chosen number of usable copies, including when rotation is off.
**Load World** shows each world as a
card with a small map of it, when it was last saved and its seed, and marks the
current world and any it cannot open. Selecting a card shows that world's save
timeline below it. Choose a save and **Load**, or double-click the save. With no
save chosen, **Load** opens the newest save; if there are no saves yet, it opens
the world as it is now. Double-clicking a world card does the same. Long card names are shortened to fit; hover a world or save
card to read its full name and details. Open
and Delete are unavailable while another world action finishes. A world that
another game version last saved, and this one can't open, is marked
**Saved by <version>**. In a game the launcher started, its red **Open in
<version>** button asks first, saves and closes the game, and opens the
launcher with that version chosen, ready to install if needed. After **Play**,
open **Load World** on the Main Menu: the same world is selected, ready for
**Load**. If it is no longer in the saves folder, the game explains that
and lets you choose another world. Started without
the launcher, the game names the version to start instead. If that version is
neither installed nor available to download, the launcher keeps it selected and
explains why it cannot start. Failed saving or opening the launcher keeps the
game open, and **Try Again** keeps the chosen version and world.

To go back to an earlier save, choose its world in **Load World** and pick it
on the timeline, then **Load**, or double-click the save. During play, the Pause
Menu's **Load Save** opens the current world's timeline directly. Your world as
it was is saved first. Playing on from an older save starts a new **branch**,
so the saves from the first version of events stay as they were.

Load World, Load Save and Save World share a **timeline** of the world's saves: a line for
each branch across the world's days, with the seasons along the top. A new
branch bends down from the save it grew from. Each branch's newest save has a
small banner, older saves are open circles and autosaves are small diamonds;
the key above the timeline shows which is which. **You are here** marks the
world as it is now, at the end of its branch, or on a dotted **New branch** row
when your next save will start one. Click a save to see it described
underneath; the arrow keys also move between saves and keep the selected name
in view when the window changes size, until you scroll the timeline yourself.
**List** shows the saves as
cards instead, grouped by branch, with tags such as **From Before the flood**
and **Latest**. A branch keeps the name of the save it started from, even if
that save's name changes later. Long labels are shortened to fit; hover the
timeline's branch name or a branch tag to read the full label. Branches without
a recorded source name keep their numbered label.
Saves from before branches existed are shown as **Earlier saves**.

**Quit to Menu** and Main Menu **Quit Game** ask for confirmation. Closing the
last client stops time and model work after about five seconds. Returning makes
no offline progress or change to a manual pause. An unfinished model call can
be cancelled and retried later; the provider may charge for both attempts.

To remove one save, choose it in **Load World**, **Load Save** or **Save World** and choose
**Delete**. Load a world before deleting one of its saves.
The confirmation also opens when you manage the current world's saves straight
after starting the game. It names the save; deletion is permanent and
leaves its world
and other saves alone. In Load World, **Delete World** removes that
world and all its saves. Open or create another world first if the target is
active. Canceling either confirmation changes nothing. These controls require
a paused world.

To clean older recovery copies, pause the world and open **Save World**. Set
**Recovery copies per save** (1–10; initially a provisional 3), then choose
**Preview cleanup**. The confirmation lists the exact copies to remove and keep.
**Delete these copies** permanently removes only that preview's older verified
recoveries. Canceling changes nothing. Cleanup stays off until you ask for it;
it never runs automatically or deletes by age.

The latest verified recovery for each save is kept, along with any recovery the
running world continues from. Manual saves, migration originals, older copies
without recovery records and saves the world server cannot verify are kept. If
the saves change while the preview is open, preview again before confirming.

## Report what you notice

Use [GitHub Issues](https://github.com/compoodment/ClankerWorld/issues/new/choose)
for a bug report. Rough notes are welcome: what you did, what happened, what
you expected and the build you used. Report results from the playtest list to
an agent in chat; there is no separate playtest report issue.
[Contributing](../../CONTRIBUTING.md#issues-and-design-questions) explains the report options.
Some fixes still need a Windows playtest; the [playtest list](../../playtest/README.md)
says what to try. Include failures as well as successes.
