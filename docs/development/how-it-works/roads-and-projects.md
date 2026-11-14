---
title: Roads and Town projects
type: architecture
status: active
updated: 2026-11-14
---

# Roads and Town projects

This page describes roads, bridges and Council-approved street lanterns.

## Roads and bridges

Roads and bridges are world-owned and permanent. Walking never adds a Road
tile. The agreed rules are in
[Towns](../../game-design/towns.md#how-roads-and-bridges-appear); this section
describes how the current code applies them.

**One saved crossing for everything.** A bridge is a saved record with a stable
ID built from its position (for example `bridge-90-3-ew-2`), a design
(`plank_span_1` or `plank_span_2`), what built it (`road` or `traffic`), both
entrance tiles, its river tiles and the tick it was built. A Road bridge also
names its route, `road:<Town>:<building>`. Movement does not read terrain
alone: the runtime turns the saved bridges into passable decks on the map
(`WorldMap.BridgeDecks`). A deck is walked end to end along the bridge, never
sideways into the river or diagonally, at dry-ground cost. A Road bridge's deck
counts as Road for the Road speed factor; a traffic bridge's does not. The
observation sends the same records, so Godot draws the deck and the tile card
and hover say "Bridge" from exactly what movement uses.

**What can be bridged.** A crossing starts on buildable ground, runs straight
across one or two river tiles and lands on buildable ground. Lake, ocean,
coast, a third water tile, a mountain bank or an existing deck ends the search,
so those are never bridged. Crossings can run across the east/west seam of a
wrapping map.

**Streets that meet a river.** Town growth (in [Town building sites](town-building-sites.md#town-building-sites)) can cross a river up to two
tiles wide on a new bridge, in two places:

- A new side street is found by `RoadRoutePlanner`, which applies the side-street
  rules in [Town building sites](town-building-sites.md#town-building-sites) and searches at most 32,768 tiles. Besides ground steps it may
  cross an existing bridge, or a new crossing, in a straight cardinal line. A
  new crossing costs 200 per river tile, like wading a one-tile river, so an
  existing bridge is cheaper. Ties break by cost, then row, then column, then
  discovery order.
- A street running on past a door (`TownStreets.Wander`) that heads straight at
  a river may cross it. The far bank must be clear, and it counts as one step
  of the run-on. The first Town's starting layout does not use this rule and
  keeps its streets on dry land.

Each proposal is checked whole before anything is saved: every new Road tile is
clear buildable ground, each step is a legal street step or a bridge, and every
new crossing is still legal, shares no water with another and is not redundant.
Road tiles, the building's entrance and new bridges are then committed together
in the same tick, and the border grows around the new Road tiles on both banks.
If there is no legal side street, nothing changes and a `town_road_unconnected`
event records the reason (`no_entrance`, `route_unavailable` or
`redundant_crossing`). A bridge with Road at both ends counts as a link between
them when finding street ends. If that bridge is an end's only link, its
canonical entrance order supplies the outward heading for the run-on. This
keeps the street heading away from the river across the east/west world seam,
including on narrow wrapped maps. The same clearance and three-tile frontage
checks apply.

**Same connected banks.** Two crossings join the same banks only when they
cross the same river, joined through its water, and each end of one reaches an
opposite end of the other by walking along that water's shore without crossing
it. Each shore step obeys the ordinary foot-movement rules: impassable Peaks
break the connection, while walkable Mountain terrain does not. A tributary
mouth or a separate stream breaks the shore, so a bridge over a
different nearby stream never blocks another. There is no distance limit; the
comparison examines at most 4,096 river tiles and, if that runs out, does not
treat the banks as the same. It first checks the shore beside the connecting
water path. If that is incomplete because the river widens, it follows the
water between the crossing spans to include the wider shore, staying within
eight tiles of the connecting water. The spans and that reach stop the
expansion from going around distant headwaters and joining separate
tributaries; a locally proven connection needs no whole-river search.
Along one unbranched stretch of river this allows
one bridge, however long the stretch. A new crossing over the same banks as an
existing bridge is not built; a Road uses the existing bridge instead, and one
route never builds two bridges over the same banks.

**Traffic bridges.** Only a committed foot step counts. Stepping from a bank
onto an unbridged crossing of one or two river tiles starts a wade. On a
two-tile crossing, stepping onto its other water tile keeps the same wade
open; a step out of the water onto the opposite bank completes a crossing.
Route previews, blocked moves, waiting and turning back add nothing, and
walking on Roads or bridges is not wading. Both widths use the same rule and
the same saved evidence, keyed by the crossing's bridge ID (such as
`bridge-124-62-ew-2`): open wades, and completed crossings from the last two
world days, at most five per agent per crossing, which is enough to decide the
rule exactly. A crossing with a bank that is not buildable, such as a
mountain, can be waded but gives no evidence, since no bridge could land there.
At the end of each tick, a crossing with six completed crossings by at least two
agents gets a `traffic` bridge across its whole width (`plank_span_1` or
`plank_span_2`). It is not built, and its evidence is cleared, if a bank holds
a building, resource or map object, or an existing bridge already joins the
same banks. A traffic bridge adds no Road tiles. Wading only reads terrain, so
a bridge laid across one tile of a two-tile crossing from the other direction
leaves the other tile wadeable back to its own bank, but the deck cannot be
entered from the side.

Events are `bridge_built` (`road:<bridge>:<route>` or `traffic:<bridge>`),
`traffic_bridge_not_built`, `town_road_unconnected` and, if a run-on fails its
final check, `town_road_extension_refused`. The server logs them as
`bridge` and `road_route` lines with IDs and reason codes only.

## Council-approved street lanterns

Paid street lanterns reuse the shared Town-project ledger and the ordinary
building projection. A one-tile roadside `Position` and its approved adjacent
Road `Entrance` bind the exact edge; two immutable definitions carry the style
and trial cost. Live sites use uncontested Town title and existing Road tiles.
The same actual delivery reservations and work receipts pay for completion;
direct placement cannot bypass them. Lanterns are excluded from doorway-based
Road generation and extension. Derived project tags let the owner client show
Road-side facts without guessing from names or dimensions. The saved receipt
rules and schema are in [Saves and replay](../saves-and-replay.md#council-approved-town-projects).

The normal map draws those projected fittings with Claude's approved
`NightLightShapes.StreetLantern` renderer. It uses the bound Road tile and edge,
shows fittings by day and adds light only during the derived night interval.
No fuel, lit flag or visibility authority is introduced. The road remains open
and a visible fitting can be selected for its paid project details.


Town growth is described in [Town building sites](town-building-sites.md).
