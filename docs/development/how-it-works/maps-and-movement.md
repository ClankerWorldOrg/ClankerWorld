---
title: Maps and movement
type: architecture
status: active
updated: 2026-11-14
---

# Maps and movement

This page describes maps, movement, weather and terrain presentation.

## Maps, movement and terrain

The *starting site* is where a world's founders begin: the first storage,
shelters and paths. Code names it `StartingSite`; walking reachability from
it (`IsReachableFromStartingSiteOnFoot`) bounds the first Town's ground.

The client's `WorldTerrainLayer` keeps unsaved regional ground-snow cover
from accepted owner observations. Only regions seen snowing get an entry;
the interval until the next observation uses that last reported weather.
One game hour of snowfall fills the cover and two hours without snow melt it.
Calendar pace converts ticks to hours; local frame time never ages it. A new
view begins empty, including when a checkpoint is loaded. World, map and
timeline changes and tick rewinds clear the history. It adds no server fields,
saved state, events or movement effects.

Short observed outdoor agent and animal moves leave pressed footprints on
that cover, using the shortest east-west path on wrapped maps. Spawns, moves
over three tiles, repeated observations and moves while paused make no
trail. Prints fade in one game hour; at most 4,096 are held, and small overview
zooms keep their positions without drawing tiny marks. The visible ground
pass draws the approved snow colours after Roads and before bridges,
buildings, trees and moving markers. It skips water, permanent snow and
building/construction tiles. Up to 256 generated snow-overlay textures are
cached, independent of map area; overview uses up to 256 small 32-tile chunk
textures instead of a draw command per snowy tile. Changes to buildings,
construction, bridges and Roads invalidate that overview cache. Cover itself
is indexed by weather region.

Roofs read that same observed region cover, including tiles blocked from the
ground overlay. The building pass draws a cached transparent snow texture over
actual roof planes at close/mid zoom. It shares the approved A2 slope and tint
rules; comparison with an undecorated roof excludes flues, signs and other
fittings. Roof outlines, open yards and grass shadows remain clear. Each roof
is clipped by weather region before drawing, including wrapped copies, so a
snowy side does not whiten a dry neighbour. At most 256 textures are cached;
cover changes reuse them. No extra node, server field, event or saved state is added.

Small and Medium generated maps are connected to the normal world path. The
older tiny map remains a populated native test setup; generation is no longer
merely a separate primitive. Large/Huge/Mega generator outputs do not imply
playable storage, observation or performance support.

`MapLayerManifestCodec` writes the binary layer preimage; `MapManifestCodec`
writes canonical BOM-free UTF-8 text. Both share one SHA-256 stream lifetime,
finalizing only after the emitter's writer is disposed. Their framing and
canonical bytes remain separate and unchanged.

Generation saves climate zone, elevation, hydrology, surface and vegetation
cover separately beside the stable terrain summary. New World preview and
creation select saved hydrology revision 1: inland lakes retain a connected
low basin of at most 0.5% of map area, with displaced open-water area added
along ocean shores. This is provisional visual tuning, not a new player slider.
Rivers terminate at lakes or oceans; this revision does not invent lake outlets.
The [Small/Medium comparison](../assets/inland-water-comparison.png) shows seed
`inland-water-0` at 45% water with wrapping (historical left, revised right).
It is a generator-layer rendering, not a Godot screenshot or native playtest.
The playable map keeps the geography's water, elevation and climate at the
north and south edges. Surface, vegetation and resource placement use the
same rules there as elsewhere, with no final overwrite or edge-resource
filter. Latitude cooling still produces polar climates when enabled. Movement
bounds stop agents at the north and south edges; only columns can wrap.
Changed map digests can also change candidate selection and the first Town's
layout, which is seeded from the digest, for the same seed.
Revision 0 (including absent revision fields) retains the historical generator;
unsupported revisions are rejected rather than substituted. Individual trees are
objects/resources. Old generated worlds recover missing layers from saved
deterministic options and retain their original resource layout.

Traveler routes apply `BuildingMovementRules` to the same terrain steps in
forward searches, reverse reachability, cart routing and local scouting. Reverse
reachability discards already reached adjacent positions before checking the
canonical foot/swimming edge and building walls, avoiding repeated terrain
checks during broad return-route proofs. A building
footprint connects to outdoor ground only through its recorded entrance; without one,
it uses the drawing's default south doorway. Shared walls and outdoor diagonal
corners are closed. Expanded footprints use their effective dimensions, and a
changed building list invalidates the derived geometry and route searches.
Movement within the departure or destination building remains possible; an
unrelated building cannot supply a shortcut through its single doorway.
Interaction-range routes end outdoors or at the exact target tile; they do not
stop short on a building's interior approach tiles.
Construction-site reachability uses the same walls. Placement and expansion
leave existing doorways outdoors and require usable ground for a new doorway;
Developer map edits cannot cover an existing doorway.
Strict restore checks doorways too; [Saves and replay](../saves-and-replay/load-and-identities.md#building-doorways)
owns the validation and alpha compatibility rules.

Movement reads water and elevation; build eligibility also reads surface.
Public foot-cost queries validate the tile or complete step. Searches that just
obtained a legal neighbor reuse the same base-cost formula without repeating
that validation.
River and bridge steps establish endpoint passability through their complete
crossing checks; dry-land steps check both endpoints and diagonal shoulders.
Diagonal foot steps cost 141% of cardinal entry and require both shoulder tiles
to be passable, including clear occupancy during a move. A river is waded in a
straight cardinal line from one dry bank to the opposite one, across one or two
river tiles: never along the river and never diagonally into, through or out of
the water. A river tile costs what its narrowest crossing costs: 200, half
dry-ground speed, where one tile of water separates dry banks, and
`WorldMap.TwoTileWadingFootCost` (300, a third of dry-ground speed) where it
takes two. The two-tile speed is provisional. A third water tile in the line,
or any lake or ocean tile, prevents wading. A two-tile line through a tile that also lies on
a two-tile line across the other axis does not count either: that tile is a
corner of a river one tile thick that runs diagonally, and wading it would turn
inside the water and walk along the channel. Where a one-tile spur or a river's
head meets a two-tile line, an agent can still turn once inside the water; no
wading route crosses more than two water tiles, and such a turn records no bridge
evidence. A built bridge makes its river tiles walkable at
dry-ground speed, end to end along the bridge only (see
[Roads and bridges](roads-and-projects.md#roads-and-bridges)). Mountains are slower to cross and
cannot be built on; peaks are impassable.

Agent routing also allows cardinal swimming steps through wider rivers and
lakes. `SwimmingRules` keeps those steps separate from ordinary foot movement,
so carts, animals, Roads and building placement retain their terrain rules.
Provisional swimming cost is 800 per step, with at least 75 warmth lost on each
committed tick while swimming, including waiting between steps. Boat passengers
receive ordinary weather exposure instead. A swimmer seeking shelter or heat
must reach dry ground before that destination counts as arrived. Starting
requires at least 6,000 warmth, illness below 2,500 and no more than four
physically carried units, including vessel contents. Equipped clothing and
carry aids follow the usual cargo exemptions. Pickups and whole harvests with
a known delivery destination check the load after collection against the
return route. A foot or cart return keeps its usual capacity; a freshwater-only
return limits loose pickups to the remaining swimming capacity and keeps an
oversized vessel family intact. Ordinary milk uses the shared Store stocking
checks for the source, destination and loaded return. Yard supplies, personal and
household planting seeds, autonomous replanting, repair cloth, cart-building
materials and child-care food pass their actual destination through selection
and pickup checks. Medicine uses the consenting patient's last observed position,
without discovering a moved patient remotely. Tool-repair preparation uses the
same Blacksmith destination as collection, so unavailable return routes produce
a blocker rather than leaving the order doing. Water collection also keeps room to return
with the jug and its contents. A rider, an animal
leader or a cart puller cannot start swimming. Eligibility checks the actor
before gathering moving dependants, who must meet the same starting conditions.
Infants and adults carrying guardian-placement
dependants cannot start. A swimmer whose condition changes can still leave
the water. Occupancy, bridge entrances and ordinary wading restrictions remain
authoritative; the sea cannot be swum. Task selection uses weakly cached
freshwater connectivity keyed by immutable map identity, checking the agent's
current swimming eligibility separately. Actual weighted route searches cache
swimming eligibility alongside terrain, Roads and occupancy. Swimming is
projected from the actual water position, and the map alternates existing approved poses and displays
**Swimming** beside the agent. No sprite atlas or saved movement-mode field
changes.

Loaded pickup and harvest checks need return reachability, rather than a
particular route. They share one reverse search per destination, interaction
range, immutable map, occupied tiles and current swimming eligibility. This
keeps a blocked passage from starting a whole-map search from every resource
site. The weighted pickup search still selects the actual interaction tile;
tools, complete harvest loads and swimming capacity retain their checks. Carts
keep their own route planner. Tests can recompute a shared reachability answer
through `DerivedVerification.RecomputeOnHit`; no search is saved.

Book writing, selected-cart repair and tool repair plan their remaining
material pickups as a sequence. A transient search tries the existing input
order first, then the other orders, checking each actual interaction point
with the cumulative physical load. Separate paper lots count as separate
pickups. Cart and tool repairs also check the final
trip to the cart or Blacksmith; writing can begin at the last pickup. The next
action resolves its source and permission again. Tool repair retains its
ordinary spare-cargo storage and gathering-tool preparation when those are
needed before a complete collection plan can be formed. No plan is saved.

Checkpoint map acceptance keeps a weak, derived starting-site reachability cache for
each map's current starting point. Before reuse it compares actual terrain,
water, elevation, surface, wrapping and bridge-axis data with an owned topology
snapshot. A changed origin or borrowed collection invalidates that result; a
fresh traversal uses owned data so an old terrain index cannot hide tile edits.
Placement, layer and manifest checks still run on every acceptance. No route
rules change. See [Saves and replay](../saves-and-replay.md) for the load boundary
and [Starting-site reachability measurements](../starting-site-reachability-measurements.md) for native
save, allocation and exact continuation evidence.

Resources are placed in bounded 16×16 cells with climate and cover biases, then
recorded in their actual 64×64 chunks. Sparse/Normal/Abundant provisionally
attempt alternating cells, one site per cell or two sites per cell. Food choices
use the actor's foot-accessible terrain component and skip sites whose current
occupied routes cannot reach harvesting range. Adjacent food needs no route
search. Candidate generation searches only when gathering or a food instruction
needs a source, and stops at the first reachable site. Caregivers use the same
selection. When fetching food for an infant, caregivers also skip household
food stores whose collection point has no currently unoccupied route. They try
other household food before gathering wild food; inaccessible stock is left
untouched. This does not change household ownership or food-policy rules.
Immutable map connectivity is cached once per map; movement rechecks
occupancy before each step. An agent gathering for its own project, and the check
that a project's inputs exist, still need a route from the starting site. Heating,
helping another agent's project and Blacksmith ore use the actor's current
reachable area (see [Material gathering](material-gathering.md#material-gathering)). Boat transport uses physical Town assets and typed Council permission, as
described in [Ports and communal boats](boats-and-handcarts.md#ports-and-communal-boats). Trees and planting are described in
[Trees and planting](trees-and-generation.md#trees-and-planting).

The client camera uses the approved B cubic ease-out for Find/selection jumps
(0.7 seconds) and zoom steps (0.35 seconds). `CameraEasing` is shared with the
completed camera review. Main advances only local camera state on process
frames, keeps zoom anchored to the cursor and uses the shortest wrapped travel.
New requests start at the shown position/scale; drag and keyboard input cancel
motion and apply immediately. Quick cards hold their screen position through
travel. Zoom refreshes map sprites only when integer tile size changes; opening
another world discards old motion. No observation, server request or save is added.

Autumn ground leaves use the shared `AutumnLeaves` drawing promoted from the
approved October 9 art review. The observed season enables the leaf pass,
using authoring data first and the world summary when it is absent, as the
landscape colors do. No clock, saved cover or resource is added. At sprite zoom it visits
only visible trees and one neighboring tile, draws at most fourteen sparse
leaves per eligible tree, and rejects Roads, Market plazas, bridges, building
footprints and water. The pass runs beneath buildings and nature sprites.
Overview zoom skips the leaf commands. Wrapped copies use the same map seeds.

Godot draws camera-visible tiles from a compact terrain index and samples it
for the overview. It does not create a Control per tile. Generated terrain uses
row-major packed bytes, with separate layer digests. Signed cache claims omit
unchanged map data only when world and digests match. Initial/changed maps and
some control receipts still send the whole map. Viewport/chunk transfer remains
unfinished.

Each world observation store retains one map-derived fertility index
and layer digest. A replacement committed map or a different world seed replaces
that entry. Resources, fields, events and other dynamic observations are still
projected from the captured state on every refresh; the cache stores no viewer
snapshot or saved authority.

Main-map dragging requires a held middle mouse button. Opening the pause menu
or losing application focus clears the drag. The client observes releases
before GUI controls consume them and stops on motion without the middle-button
mask, so a missed release cannot make ordinary hover pan the camera.

The regional-weather prototype saves an episode for each 32×32 region. Ordinary
episodes last one-quarter to one saved day; storms last at most three-quarters
of a day and allow no new storm for at least half a day afterward. Small fixture
calendars round durations to whole ticks. Conditions use the region-center
climate, previous condition, seed and a shared pre-transition neighbor snapshot.
No model call or camera state participates; drifting visuals are not fronts.

The October 1 default preset reduces rain, storm and snow weights by exactly
one quarter after climate and latitude adjustments. The removed share goes to
clear and cloudy weather. Integer weights use four units per old weight point,
so small weights keep the same exact reduction. Explicit custom weather
profiles retain their declared weights. Episode neighbor and persistence
bonuses use the same weight scale. Custom profile totals may reach
`int.MaxValue`; climate conversion uses wider intermediate arithmetic, and
episode weights remain wide through persistence bonuses and storm cooldown.
These adjustments cannot wrap into negative weights. Further tuning remains provisional in
[#204](https://github.com/ClankerWorldOrg/ClankerWorld/issues/204).
Wet neighbors add at most four rain-weight points;
a reduction to base precipitation weights offsets that bonus. The fixed-seed
comparison is recorded in [the prototype report](../weather-episode-prototype.md).
Local survival and crop exposure use the active episode. Soil moisture retains
the older three-day daily-weather estimate in this first experiment; it is not
a saved moisture grid or an episode-integrated rainfall model. Existing saves
keep their active weather when loaded; the first resumed tick imports it into
an episode. See [save handling](../saves-and-replay/validation-weather-and-events.md#regional-weather-episodes).

The client renders the approved pixel-streak overlay through `WeatherStreaks`:
straight rain and three-pixel landing bursts, two-down-one-across storm streaks
in eastward gusts, and wind-blown snowflake crosses. The review tool uses the
same geometry, timing and ink. `WeatherLayer` caches seven tiny nearest-filtered
textures and submits one command per particle, bounded by the camera. Its
precipitation timing and particle count describe synchronous command creation;
they exclude later layout, drawing and GPU work. Each weather kind keeps a
soft spatial mask, while a local 1.2-second smooth transition changes its
opacity. Outgoing precipitation survives until its fade finishes; retargeting
starts from the last displayed amount. The linear masks retain full coverage
at regional junctions without changing an unchanged neighbour when a fade ends.
Pausing holds both clocks, and a new world shows its recorded weather at once.
Cloud haze, storm flashes and their settings remain available. Nothing new is
saved or sent by the server.

The generator vendors [FastNoiseLite](../../../src/ClankerWorld.Simulation/ThirdParty/FastNoiseLite/README.md).
Its drainage approach draws on [Red Blob's noise guide](https://www.redblobgames.com/maps/terrain-from-noise/),
[Mapgen2](https://github.com/redblobgames/mapgen2) and
[Mapgen4](https://github.com/redblobgames/mapgen4/blob/master/map.ts). These sources
explain the approach; they do not settle open terrain design or performance.
