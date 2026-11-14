---
title: World and controls
type: product-status
status: active
updated: 2026-11-14
---

# World and controls

World creation, controls and the game interface.

## World and controls

| Feature | Status | Current limits |
| --- | --- | --- |
| Join a world on another computer | Available in the launcher | HTTPS on a private network, comparison-code approval, saved device connection and signed access check before Play. Host startup hints depend on the host kind reported during pairing; older hosts get a general network/host check. The server-only Linux download has its [own guide](../hosting-linux.md). Windows playtesting is pending. |
| Host a world for other devices | Available in the launcher | Uses private Tailscale Serve HTTPS and the installed world's existing saves, keys and model-call count. The host screen shows the address, comparison-code requests, Approve, Decline and Stop hosting. Closing minimizes it. The last game leaving saves and pauses; an option stops the server instead. Automatic restart at Windows sign-in can be turned off. Developer test data stays separate. Play on this PC opens the running host without a pairing step or another server; closing that game leaves hosting available. Windows hosting/reboot/local-play playtests are pending. |
| Create, select, save and load worlds | Available in the game | Loads show changed model and key assignments for confirmation before applying them. Oversized save files are refused and preserved. Small/Medium maps; larger playable worlds are unfinished. The earlier manually signed Windows installer, normal Play, Repair check, save and reopening a saved Town were checked on October 18; the automatic signing workflow still needs its first release check ([details](https://github.com/ClankerWorldOrg/ClankerWorld/pull/2422)). Quit to Launcher and Open in another version keep the world's selection in Load World after Play; opening it remains your choice. Default Balanced Small/Medium previews try up to three candidates for the Normal forest and mountain targets. |
| Wooded forests, patchy beaches and reduced wet weather | Available in newly created worlds | Forest grass has many trees, forest-floor tiles always have trees, and cacti stay on desert sand. Default rain, storm and snow weights are one quarter lower across climates. Density and Windows performance still need owner playtesting. |
| Choose the first Town and place four founders before starting | Available in the game | On-map, guidance-only hints for nearby food, fertile ground, wood, stone and open space for Roads; exact factor tuning remains provisional. Starting supplies are fixed; New World offers no extra supplies or survival grace period. |
| Move around and inspect the map | Available in the game | Zoom, overview, wrapped east/west movement, tile facts, building cards with stored items as icons, and separate filters for Town title, household use, disputes, building property and Town borders. The disputes filter shows contested tiles in pending household land requests. Building Details lists the workstation’s registered recipes with input/output quantities, and running work shows its recipe and the materials held for it. Details also lists recent recorded storage additions and removals. |
| Approved art for playable content | Available in the game | Clinics and Restaurants have their approved exteriors. Medicinal herb patches and picked or depleted natural sites use their matching drawings on the map and tile cards. Potatoes, cultivated green seeds, medicinal herbs, diamond ornaments and simple meals use their approved item icons. Refined gold, gold ore, plain gold ornaments, iron fittings, saddles, cooked eggs, milk, berry and fruit porridge, rich meals and leather sacks have their own icons from the October 7 art review; crude wooden axes and pickaxes use the wooden tool icons. [Windows visual checks](../../playtest/alpha-1.md) are still wanted. |
| Pause, inspect agents, view family trees and read events | Available in the game | The Agents list keeps its browsing position through information refreshes; selecting another agent still brings that card into view. Deceased profiles retain recorded thoughts and memories, and show any final will and final words; old deaths without an archive cannot be reconstructed. |
| Lost server replies | Available in the game | After a lost reply to an instruction, cancellation or world edit, reconnecting to the same running world checks the exact request automatically. **Your last request** also offers **Check and send again** or **Discard it** without Developer mode. A loaded, switched or restarted world requires the player to review the result before sending a new request. Discarding does not undo something the server already accepted. |
| Developer tools | Available in the game | With Developer mode on (the launcher's setting, the `--developer-mode` start option, or any run from the Godot editor), **F12** opens them in a world without pausing it: the tile's coordinates and facts, frame time, how long the server takes per step of world time, the agent count, jumping to an agent and drawing their planned path, plus the aging override and paired-device management. While paused, **Edit world** offers named terrain, resource and camp-object choices: click or drag across empty map tiles, review the preview and Apply. The server saves each accepted batch as **Developer edit** and explains refusals without changing anything. While paused, **Edit selected agent** sets fullness, warmth, illness or nutrition, gives/removes carried goods, adds/removes skills, and starts/ends partnerships; each accepted change is saved and marked **Developer edit** in the Event Log. There are no time tools such as stepping one tick. The Windows playtest is pending. |
| Report a problem | Available in the game | **Report a problem**, in **Settings → Game → Build**, saves a zip of the game's logs and a summary for a bug report, with keys and secrets blanked. The world server's logs are included only when the game runs its own server, which needs the unpublished local package ([#468](https://github.com/ClankerWorldOrg/ClankerWorld/issues/468)); a world server on another machine keeps its logs there. The player sends the zip by hand. The Windows playtest is pending. |
| Display and interface settings | Available in the game | Light and Dark text contrast is checked at 4.5:1 or better, including button states, fields, menus and badges. Themes, window size, weather switches, a 24-hour or 12-hour clock, and dates by season (the default, such as Autumn 2, Year 1) or as DD-MM-YYYY, MM-DD-YYYY or YYYY-MM-DD. A server too old to report season lengths shows numeric dates. The interface grows with the screen in whole steps (100%, 200% at 1080p and 1440p, 300% at 4K) with no setting, and the game always draws at the screen's own resolution. Windows visual and keyboard acceptance is still being checked. |
| Keyboard controls | Available in the game | Tab/Shift+Tab traverse the open menu or panel, Enter uses a control, and arrows browse lists. K focuses world tiles for arrow movement and Enter selection or placement; World Map arrows move the camera. Numeric editors and scrollable readers have visible focus. Key remapping is outside the baseline. [Windows keyboard acceptance](../../playtest/alpha-1.md) is pending. |

Building Details shows up to ten recent recorded storage changes, newest first,
with the exact additions or removals and when they happened. Moving a filled
vessel includes its contents. These records survive saving and reconnecting;
older records follow the existing bounded history archive. An empty list says
no recent changes were recorded, and inspection teaches agents nothing.

Building quick cards and Details show a storage-space bar and exact occupied
space against the building's recorded limit. The count includes all physically
stored goods, including other owners' goods and materials held for work; the
household's item grid is a separate view. An older server without a recorded
limit keeps its item grid and has no guessed capacity bar.

Agents walk out through a building's doorway and stay outside until they reach
the next building's doorway, including when buildings share a wall.
People inside a building, standing under its roof, are hidden from the map.
At close and mid zoom the building shows a small badge in its top-right corner
with how many people are inside; hovering it names them, and the building's
card lists them. People in an open yard, on a pier or at a Market stall stay
on the map.

On a fresh server, Continue opens New World instead of entering the
empty world the server starts with. The normal map preview and acceptance steps still apply, and saved
worlds with founders or other progress keep opening normally.

Generated maps let ordinary terrain reach the north and south edges, without
forcing a straight strip of polar sea. Agents stop at those edges; east/west
wrapping and latitude cooling keep their existing settings.
Earlier worlds whose maps differ from regeneration no longer load; their saves are kept.

New worlds open paused with nothing built. Choosing the first Town lays a winding
main road with side streets, then places two Houses, a Warehouse, Farmhouse and
Blacksmith along them, each with its door facing its packed-dirt Road and a short
doorstep path joining it. Streets run on a few tiles past the last building,
and the Town border keeps about three tiles of spare land around buildings
and Roads. The Houses hold
household food; the Warehouse supplies a wooden axe and pickaxe. Four unrelated
founders form two starting households: the first holds the Farmhouse and the
second holds the Blacksmith. The player presses Start World explicitly.

Default Balanced Small/Medium previews measure forests and mountains against
dry land and try at most three deterministic maps for the Normal settings. The
preview describes the selected map in plain words, such as "Plenty of forest
and some mountain ranges". Its tooltip gives the measured shares of every
playable map tried and lists, in the order tried, attempts that have no room
for a first Town. Create World makes the selected map exactly. If no attempted
map has room, it asks the player to try another seed or change the settings.
If the selected map has less or more forest or mountains than a balanced
world, the preview says so, and Create World stays unavailable until the
player ticks **Keep this map anyway** or changes the seed. Uniform Dry and
polar-only settings do not use those trial bands; each band applies only while
its own forest or mountain control is Normal. This is connected to the normal
game and world server path; the current measurements are
automated fixture evidence, not a Windows visual playtest or a final balance
claim.

World time and model calls stop after the last connected client's short grace
period. Returning makes no offline progress; a manually paused world stays
paused. New worlds use six-minute days and a 40-day year as the current playtest
pace, subject to model/server load.

Every world day has a night, longer in winter and shorter in summer: 40% of the
day at the start of spring and autumn (19:12 to 04:48 on the clock, about
2 min 24 s of a six-minute day), 30% at the start of summer (20:24 to 03:36)
and 50% at the start of winter (18:00 to 06:00), changing a little each day in
between. Dusk and dawn each fade over an hour (15 seconds at that pace). A new world starts at
06:00 on Spring 1, Year 1, after dawn, so founder setup happens in full
daylight. Loading an existing world keeps its saved clock. At night the map darkens with a
gentle blue wash at every zoom, under map names, agents and weather, and it is
colder outdoors (see [Life, work and society](life-work-and-society.md#life-work-and-society)).

Dawn adds a soft rose glow and dusk a gold one, following seasonal twilight.
Both fade away in full day and night. Names, moving figures,
night lights and weather stay above the glow, and pause holds its transition.
The [Windows glow check](../../playtest/alpha-1.md) is pending.

At close and mid zoom, an occupied House or working Blacksmith forge emits
a soft grey column of smoke that rises, leans east and fades. Empty Houses
and idle forges emit none; pausing freezes the smoke. The appearance still
needs a Windows hands-on check.

Stores show approved log, crate and sack piles on clear Town ground beside
their doors at close and mid zoom. Their empty, some and full drawings follow
recorded contents and capacity; the full drawing starts at 80% of a known
limit. Unknown limits use the some drawing. Piles never replace recorded goods
or block movement, and stay hidden where safe beside-door space is unavailable.
The appearance still needs a Windows hands-on check.

Buildings in use glow: light falls on the ground from the windows on a
building's front and sides and from its open door, never from its roof. A House
is lit only while someone is inside; a Farmhouse, Store, Tailor Shop, Workshop
or other building while someone is inside or a job runs there; and the
Blacksmith's forge glows in its yard while it works. A Warehouse shows only a
lantern by its loading doors, lit while someone fetches or stores goods, and a
Silo stays dark. Each pool of light has a ragged edge that drifts slightly, and
the forge flickers. Zoomed out, a lit building is a warm speck. Night lights have
not been checked by hand in the Windows game yet. Night
adds no rules of its own: agents need no sleep or energy, and nothing limits
their choices or travel at night; they only react to the cold. The night chill
is a provisional amount for playtesting, and night has not been checked by hand
in the Windows game yet. Night effects on weather are not built.
