---
title: Time of day and building completion
type: architecture
status: active
updated: 2026-11-14
---

# Time of day and building completion

This page describes day and night, visual effects and building completion.

### Time of day and night

Time of day is worked out from the elapsed tick and the world's saved calendar
(ticks per day, season lengths and calendar offset). Darkness itself is not
saved. `DaylightRules` follows the 24-hour clock the game shows, where a tick's
clock minute is its tick of day × 1,440 ÷ ticks per day. Night follows the
seasons ([#891](https://github.com/ClankerWorldOrg/ClankerWorld/issues/891),
replacing the same-all-year night of
[#641](https://github.com/ClankerWorldOrg/ClankerWorld/issues/641)):
`DaylightRules.NightShare` gives 30% of the day on the first day of summer,
50% on the first day of winter and 40% on the first days of spring and autumn,
with an even daily step between them across each season's own length. Night is
centred on midnight: 19:12 to 04:48 at 40%, 20:24 to 03:36 at 30% and 18:00 to
06:00 at 50%. Dusk and dawn each fade over the clock hour centred on the start
and end of night (18:42–19:42 and 04:18–05:18 at 40%), so the darker half of
each fade counts as night and night covers exactly its share of the day. At
360 ticks a day a 40% night is 144 ticks, a summer-start night 108 and a
winter-start night 180, with 15-tick fades. Each tick uses its own calendar
day's share, so a night's evening follows that day and its morning the next;
both are fully dark around midnight, so nothing jumps. The rule uses whole
numbers only, so every platform agrees on every tick. Darkness is reported in
basis points, 0 in daylight and 10,000 at full night. New playable worlds and
their founder setup begin at 06:00, after the dawn fade. A saved zero-offset
world keeps midnight at elapsed tick zero. The first day of a new world
therefore has 18 hours left; later days retain their full duration.

Night adds a provisional chill of 15 exposure points per tick at full night,
faded in and out with the darkness (`NightChillAtFullDarkness`). It is added to
the weather, climate and season exposure (`OutdoorExposure`), and clothing (35
for a basic garment when dry), shelter (45) and a lit fire (+90) offset it just
as they offset weather. The same outdoor exposure decides the choice to put on
better clothing, the warmth budget for a scouting trip and the preference for
making garments. Wear on a worn garment still follows the weather alone, so a
mild night adds no repair work. In mild clear weather a basic garment or any
shelter cancels the chill; with no protection an agent loses about a fifth of
their warmth over a night. There are no night-only limits on choices, travel,
work or conversation, and no sleep or energy. Longer winter nights mean more
hours of chill. Night does not change weather or crops yet.

The warmth action keeps an agent in place when their current protection stops
cooling. Forest and tree cover give a provisional 22 storm-protection points,
compared with a building's 45. A cooling agent prefers a reachable permitted
building over natural cover, with a usable lit hearth first. Once in a
building, they keep its cover unless a usable lit hearth is reachable. Natural
cover remains a fallback when no building destination is reachable;
existing access permissions, routes and fuel use still apply.
The lit destination must be a heating building the agent can use; a
storm-shelter guest invitation alone does not grant use of a household hearth.

During a storm, an agent with no House can shelter in their own Town's completed
Town Hall. The full Hall footprint gives the same 45 protection points as an
invited House. Warmth travel and generic shelter orders choose a reachable
footprint tile, so an occupied anchor does not hide the rest of the Hall.
Permission rechecks Town membership, absence of a House and weather at the Hall.
It grants no hearth or stock access and does not satisfy the family housing gate
or change birth placement. Unfinished orders recheck permission; completed
arrivals remain historical after the storm or Hall disappears.

The owner snapshot carries `darknessBasisPoints`, decided by the server from the
same rule. The Godot client's `NightLayer` draws a deep blue wash, at most 40%
opaque, over the visible map just above the ground, roads, buildings and trees,
and below map labels, agent markers, weather and panels. It eases between the
once-a-tick readings, shows a newly opened world's darkness at once, and looks
the same in both themes. The World Map panel is not darkened.

`GoldenHourLayer` draws one click-through multiply immediately above that
wash and below night lights, map labels, moving figures and weather. It uses
the approved rose E9A3A0 at dawn and gold F2B160 at dusk, up to 22%. Strength
is `4 × darkness × (1 − darkness)`, so the server's existing seasonal twilight
determines its timing, with zero effect at full day or night. Morning versus
evening comes from the accepted world tick, ticks per day and saved calendar
offset; the client never computes another night-length rule. The local
strength eases between reports in up to half a second. Pause freezes it;
world changes, rewind, clock-half changes and gaps over a game hour settle
directly to the current reading. It is unsaved display state and adds no
server fields, events, game effects or new night tint.

Night lights ([#890](https://github.com/ClankerWorldOrg/ClankerWorld/issues/890))
are drawn by `NightLightsLayer`, above the night and golden-hour washes and under labels and
agents. It takes each placed building's family, footprint, door side and the
roof, yard and door spots from `BuildingSprites.Plan`, and decides from the
snapshot whether it is occupied (a living agent stands within its footprint)
or working (a production job runs there); nothing new comes from the server and
nothing is saved. `NightLightShapes` turns that into rows of light in the
building's 32-unit tile space: spills from windows on the front and both
sides, never the back, and from the door; a forge pool in a Blacksmith's yard;
and a Warehouse's wall lantern, whose unlit fitting also shows by day. Pools
have ragged edges from three slow sine waves seeded per tile; fires move
faster. The layer steps that drift every eighth of a second, scales light by
the eased darkness and draws rows weakest first through a shader that
brightens the ground and pulls its hue toward the light, so the ground keeps
its texture. At overview zoom a lit building is a warm speck. The same shapes
draw the art preview's night proposal (`tools/ArtPreview/Proposed/NightLights.md`),
including designs not in the game yet and the street lanterns of
[#892](https://github.com/ClankerWorldOrg/ClankerWorld/issues/892).

`SmokeLayer` uses the same living, non-draft occupancy and running-job facts,
restricted to occupied Houses and working Blacksmith forges. `SmokeArt` anchors
the approved B column to the building atlas’s actual flue or ember pixels at
16 and 32 px. It groups translucent pixels into horizontal rows, draws only
camera-visible sources (including wrapped copies), and steps the existing
weather clock at 12 frames per second. Pausing freezes that clock. Smoke adds
no server fields, fuel use, heating rules or saved state.

`StoredStockLayer` draws approved stock A below the night wash and below map
labels, agents and weather. `StoredStockPiles` reads accepted per-building
`StoredItems`, `StoredQuantity` and `StorageCapacity`; it never infers stored
contents from household totals or ground stock. Wood uses logs, listed bulk
food/crop/seed/fiber/herb kinds use sacks, and other kinds use crates. Positive
contents use the some drawing, or the full drawing at 80% of a known capacity.
Unknown capacity stays at some. These are visual fullness bands, not physical
container counts or new capacity rules.

Each group takes a distinct known Town tile within two cardinal steps of the
recorded entrance, on the front or nearest side wall. Roads, every building's
entrance and footprint, bridges, resources, fields, ground stock and construction
sites are excluded, along with water, mountains and unknown ground. No suitable
tile means no pile for that group. Native world-pixel primitives preserve the
approved casts, colours and paint order at both atlas sizes. Drawing is bounded
to visible tiles and wrapped copies; unchanged piles reuse their commands.
No server fields, inventory transitions, saved state or collision rules change.

### Building completion moments

`BuildingCompletionLayer` follows accepted owner snapshots and the actual map
camera. A new placed building must replace a previously observed construction
site with the same definition and footprint, with its placement tick between
the last and current observations. Only a visible close/mid-zoom completion
starts the local three-second effect. Cancelled sites, existing buildings,
buildings first observed while paused and off-screen completions never queue a moment.
The first observation is a quiet baseline. World, map or observer timeline
changes, rewind and gaps over a game hour clear display history; removal
clears that building's active effect.

The layer draws above roofs and night lights, below objects, figures and
weather. `BuildingCompletionArt` shares the approved C pixel spans with the
art reference: fourteen C9AC7C dust puffs spread and settle over 1.4 seconds,
and five FFF6D8 twinkles stop after 2.4 seconds. It uses native horizontal
spans without particle nodes or textures generated per frame. Active moments
are bounded to 128; the oldest leaves if a larger visible batch completes.
The local clock freezes while paused, resumes from the held frame, follows
zoom and wrapped copies, and hides at overview. No state, event, protocol,
construction or replay rule changes.
