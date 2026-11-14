---
title: Settings and controls
type: player-guide
status: active
updated: 2026-11-14
---

# Settings and controls

Pause the world, change settings and use controls and developer tools.

## Pause, settings and controls

The pause button pauses or resumes time. **Menu** opens the Pause Menu and keeps
the world paused. Closing it restores the world to running if it was running
before the menu opened; a world you had already paused stays paused. This also
applies when you reopen the menu while it is still resuming.
Opening ordinary inspection panels leaves time running.
Settings and the Mod Library open inside the Pause Menu window; its back arrow
or **Escape** returns to the menu's buttons.

Rain falls as straight streaks with tiny landing bursts. Storm streaks arrive
in gusts, and snowflake crosses drift sideways. The weather stays light enough
to read the map and freezes with the world when paused.

Game Settings controls the window, theme, weather effects, date/time format
and the model-call limit. Rain, storms and snow fade in or out over about a
second when regional weather changes. Pausing freezes a fade where it is;
loading a world shows its existing weather at once.

At close and mid zoom, autumn adds a few fallen leaves under mature broadleaf
and fruiting or picked orchard trees. They stay off Roads and water and
disappear when autumn ends. You cannot gather them.

Dates name the season and its day, such as **Autumn 2, Year 1 · 14:20**; a new
world's year has four ten-day seasons: Spring, Summer, Autumn and Winter. The
top bar then shows the weather beside the date without naming the season again.
**Date display** can show numbers instead (DD-MM-YYYY, MM-DD-YYYY or
YYYY-MM-DD), and the top bar then names the season beside the weather.
**Time display** offers a 24-hour or 12-hour clock. Both apply at once.
Menus, panels and text grow with your screen in whole steps, so pixel letters
stay crisp: 100% on small screens, 200% at 1080p and 1440p and 300% at 4K.
There is no setting for this. World Settings contains that world's autosaves,
the routine helper and agent model settings. **World generation** shows the world's
size, climate, east/west wrapping and seed as chosen at creation; these are
read-only. Main Menu Settings exposes Game Settings only.

Aging override and routine helper changes apply to the world shown when you submit them.
If another device opens a different world before your request arrives, the
world server refuses the change. Reopen World Settings before trying again. These
controls need matching game and world server updates.

Autosave Apply stays unavailable until the current world's settings have loaded.
If the world changes before Apply reaches the world server, its settings stay unchanged.
Reopen World Settings before trying again.

Press **F1** or **?** for the in-game controls list, with keys drawn as keycaps
and grouped by what they do. With no menu or text field
active, **Space** or **P** pauses, **N** selects the next agent, **Shift+N** the
previous one, **C** centers the selected agent and **H** returns to the opening
Town view. **Escape** closes the newest panel or mode, then opens the Pause Menu.

Use **Tab** and **Shift+Tab** to move between controls in the open panel, with a
visible focus outline. **Enter** activates the focused control; arrow keys
browse lists and choices. Focus stays in menus and returns to the control that
opened a panel when you close it. Numeric fields and scrollable readers are
also reachable with Tab. In a text field, Escape leaves the field first;
press it again to close the panel. The Agents list keeps focus while you browse with
arrows; Enter opens the selected Profile.

Press **K** to focus a tile on the world map. Arrows move one tile, or ten with
**Shift**; the camera follows when the tile leaves the view. **Enter** selects
an agent, building or ground tile there. During founder, first Town or new-agent
placement, Enter uses the same placement action as clicking that tile. Move
the pointer to return to mouse browsing. In **World Map**, Tab to the atlas and
use arrows to move the camera. Key remapping is not available.

With Developer mode on, press **F12** in a world to open **Developer tools** at
the right edge of the screen, and again to close them; **Escape** closes them once nothing newer is
open. Time keeps running. The panel shows the coordinates and facts of the last
tile under the pointer, the frame time, the tick time (how long the server took
to work out the latest step of world time), how many agents are living and
how many actions have been refused because of a game error.
Choose an agent in its list to select them and move the camera to them, and
turn on **Show planned path** to draw the route they are walking, as the server
planned it. The panel also holds the aging override, recovery for a request
whose reply was lost, paused world editing and paired-device management. The
aging override and world editing need the world paused first.
**Edit world** lists the tools and kinds the server offers, using their names.
Choose a tool and kind, then click tiles or drag across them; **Pick tiles on
the map** shows when the tool is active. Gold marks show the tiles in your preview; **Apply** sends the
whole selection for the server to check and save. **Clear preview** starts
again. Resuming time, changing worlds or closing Developer tools clears it.
A refused edit shows the server's reason and leaves the world unchanged.
The typed operations are under **Advanced: typed operations**; their coordinate
bounds follow the current map.

Terrain choices paint ground, water, mountains or peaks. Resource choices add
berry bushes, timber, fallen wood, stone, fiber or clay; object choices add a
camp shelter, storage or cooking fire after the settlement starts. Choose empty
tiles: agents, resources, objects, roads, fields and building sites are protected.
The server saves the whole edit and marks it **Developer edit** in the Event Log.
If the world changed since your preview, refresh and choose the tiles again.

To set up a test, pause and use **Edit selected agent**. Choose a need and its
percentage, goods and a quantity, a skill, or another agent for a partnership,
then press **Apply developer edit**. The server saves accepted changes and
marks each one **Developer edit** in the Event Log. Goods go into the selected
agent's carried load; removal protects equipped items, filled vessels, knowledge
records and reserved or borrowed goods. Starting a partnership requires two
living, unpartnered adults who are not close relatives. A refused edit leaves the world
unchanged; refresh the world view before trying again if the world changed.

**Settings → Game → Model calls** shows how many model calls this installation
has made and sets an optional limit. One count and limit cover all your worlds.
Every call attempt counts, including failed, retried or abandoned ones; tokens
reported by providers are shown for information only. The count never resets
by itself, and loading an older save does not lower it. It is not a currency
budget or provider invoice. When the count reaches 80% of the limit, the Event
Log warns you once. Reaching the limit pauses time: raise or remove the limit,
or choose **Allow 100 more calls**, then resume separately.
**Start World** also waits until you raise or remove the limit or allow more calls;
then select **Start World** again.
After you allow more calls and resume, time keeps running under your new limit.

Type a limit and choose **Set limit** to apply it; leave the field blank to
remove the limit. Refreshing the count preserves an unfinished edit even
after you leave the field. Changing to another server clears the previous
server's displayed information and unfinished edit.
