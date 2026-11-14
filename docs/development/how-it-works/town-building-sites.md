---
title: Town building sites
type: architecture
status: active
updated: 2026-11-14
---

# Town building sites

This page describes Town layouts, construction sites and household planning.

`FirstTownLayoutPlanner` lays the first Town street first, using
`TownStreets`. A main road runs both ways from the chosen site along its most
open line, bending in 45° steps, never more than 45° from the heading it set out
on. Side streets leave it every three to five tiles, mostly at right angles. A
street takes a diagonal step only where both corner tiles are clear ground, and
keeps a one-tile gap from other streets. The five buildings then take the
nearest lots whose door opens onto a street, with a one-tile gap between
buildings. Finally every dead end is cut back to three tiles past the last door
on it, and branches with no door are removed. The same site on the same map
always gives the same layout.

The Town border is all land within about three tiles of the Town's buildings
and Roads (three straight out, rounded at the corners). It leaves out water and
is clipped at map edges. It grows around each building that joins the Town and
around that building's new Road. The saved border is authoritative: loading
checks that it lies on the map and covers the Town's origin and every assigned
building. Older worlds keep their saved rectangular border. There is no
wrapped-seam claim geometry, competing-claim graph or automatic second-Town
founding. Filters display saved Town and household-building facts, not invented
general land ownership.

A building that joins a Town later is joined to the Road network from one of the
tiles directly beside its footprint. That starting tile is saved as the
building's entrance, and the map draws the door on that side. The first Town's
planner saves the entrance of each lot it chose. A new Town building also keeps
a clear fallback entrance if its Road cannot connect. A roadless building with
no recorded entrance uses the middle of its south side, which placement must
leave clear.

Towns grow along their streets. `TownLayoutService` gives a site whose door can
face an existing Road a `road_frontage` bonus. A building beside a Road needs
no new Road. Otherwise a new side street runs to the nearest Road. It stays
inside the (already grown) border where it can, may step diagonally where both
corner tiles are clear, and pays extra for each tile beside an existing Road,
so it meets streets rather than running alongside them. Then every dead end
fewer than three tiles past its nearest door carries on in its own direction
where the land allows (`town_road_extended`). The border grows around all the
new local Road tiles. Local streets join Roads within their own Town's border;
they do not choose another Town's network as their destination.

When a Town gains its first completed building, `GenerateRoadBetweenTowns`
selects the nearest other Town by wrapped foot distance between recorded
origins, breaking equal distances by Town ID. A completed household building
incorporated during founding triggers the same link. `RoadRoutePlanner` searches
from the building's legal entrances to Roads in the selected Town, or to
building entrances inside its border or its unoccupied origin when it has no
Roads. The route uses
the existing Road movement discount, without the local side-street adjacency
penalty. It may reuse existing Roads and bridges, including Roads subsequently
overlapped by household land. Existing diagonal Road edges retain their land
access, while physical corner obstructions still refuse the step. New tiles
and bridge banks still avoid held land, pending requests, resources and
building footprints. The complete route
and any new one- or two-tile river crossings validate before commitment.
Inter-Town search uses admissible distance bounds to direct it toward the
selected Town within the existing 32,768-tile search budget; local side streets
keep Dijkstra ordering.
Road and attached-cart queues use row-major tile indices after their existing
score/cost fields, preserving row/column ties without enqueue counters.
`town_road_linked` records the selected pair; `town_road_link_unconnected`
records a refused route. A failed link does not choose a farther Town. These
inter-Town Roads use the ordinary saved Road tiles and bridge records, keep the
same rendering and persistence, and never expand either Town's title or border.
The Town's saved `FirstBuildingCompletedTick` prevents a replacement building
from repeating the link after removal or reload, including a failed first link.
Reassigning a completed Warehouse to an empty Town uses the same first-building
trigger. When that Warehouse stands outside the receiving Town, the Road starts
at the receiving Town's origin and leaves the remote building's entrance alone.

Street neighbours, headings, diagonal corners and clearance use the map's
east-west wrap. A Road across the seam counts toward the distance to the last
door, and a short dead end can carry on through that seam in its original
direction. Rows do not wrap, building footprints still stay within map bounds,
and bridge headings follow their saved axis and span.

Layout queries freeze their walking-cost dictionary and occupied tiles once.
Contexts reuse both immutable inputs; mutable external inputs are still copied
and all costs validated. Cost and occupied-tile lookups retain ordinary coordinate
equality even when external inputs use custom comparers.
`TownLayoutService` captures one immutable layout context per decision and
normally offers at most five legal sites with reasons for footprint, route,
resources, purpose and compact growth. A model selects a site-specific candidate
or chooses another action; refusal starts no project. Accepted projects retain
their tile. If it becomes illegal, the project blocks and retries after sixty
ticks. An unchanged idle choice is reconsidered after 300 ticks, sooner if
urgent needs or legal choices change. Weights and retry values are provisional.
Material scoring searches at most five map tiles from a site, including the
east-west seam; farther resources cannot change its rank. Recipe and expansion
input checks share reachable-tool results and a lazily collected accessible
Warehouse list only within one agent's read-only candidate query. Stock
quantities and reservations are still read for each input.
Recipe planning also gathers placed workstation definitions once per query and
shares its read-only warmth result with household-building eligibility.
Ownership, input stock, capacity and routes still pass their later live checks.
Project-input source scans check the required resource kind before looking up its availability by ID;
wood still accepts construction sources. Matching sources keep their current
ecology quantity, foot or swimming reachability and tool checks. This only
reorders read-only filters, with no cache or saved-state change. The
[construction input measurements](../construction-input-query-measurements.md)
record the matched native checks and timing limits. Warehouse discovery
matches a set of current definition IDs instead of scanning definitions for
every building. Later queries and actions check current stock, membership and
routes again; these optimizations add no saved state or persistent cache.
Dense material searches walk fixed distance-ring offsets in their original
order, reading current positions with the same wrapping and map-edge checks.
Purpose scoring filters related buildings once per context and keeps its
distance, building-ID and tag tie rules. Border-growth scoring counts the same
rounded footprint margin that actual placement adds, without sorting those
tiles for every candidate.
Site evaluation first checks an origin that belongs to the footprint.
An already blocked origin refuses the site; custom
shapes that omit it still check only their covered tiles. Clearance and required
Town titles share one footprint walk. Market Road permissions, protected tiles
and later route and ranking checks retain their existing rules. See [footprint query measurements](../footprint-query-measurements.md)
for the allocation comparison and mixed native timings.
Continuing an idle intention skips a second full candidate query after the
ordinary enqueue phase has reconsidered current choices. Orders, conversations,
care and ongoing work keep their earlier continuation guards, and the ordinary
idle action still runs its cleanup.
See [construction query measurements](../construction-query-measurements.md) for
matched native timings, candidate/state equivalence and remaining limits.
Town Hall, Market, stall and lantern offers use their shared action-plan
validator for the full site check, including pending proposals. Port proposals
also share physical placement obstacles within their synchronous query.
Market slots stay separate from other occupied
objects, and actual placement gathers current facts again. Layout searches rent
tile costs and mark settlement in the same array, preserving cost/Y/X order
and stopping rules. Settled neighbours skip repeated terrain checks; remaining
edges check current terrain, building walls and occupied corners.
See [placement query measurements](../placement-query-measurements.md)
for the follow-up comparison.
The [Warehouse query measurements](../warehouse-query-measurements.md) record a
later bounded repair, exact native equivalence and its mixed small-case timings.
Household planning computes each prospective building's identity once per design,
before checking placed buildings for it. Each later query reads current reservations
again. See [household plan query measurements](../household-plan-query-measurements.md)
for native candidate/state equivalence, repeated timings and allocation results.
Layout, A* and shared route searches reuse the step legality established just
before computing step costs. Public step costs and movement still
validate the complete step; terrain, Road discounts, occupied corners and queue
order retain their existing rules. See [route step cost measurements](../route-step-cost-measurements.md)
for matched native captures and timing limits.
Layout and shared route queues order equal costs by the tile index used by their
arrays, preserving row/column order without a separate enqueue counter.
Live movement and planned ground/bridge routes share the same Road cost
adjustment, including its minimum cost and Road-bridge distinction.
Layout foot-cost, Road, attached-cart and default shared walking-route searches use a value-type neighbor enumerator instead of
allocating the public iterator for each visited tile. Both paths keep the same
offset order and suppress repeated coordinates on one- or two-column wrapped
maps before checking legality. Each step reads current terrain and bridge axes;
no neighbor result is cached between steps or searches.
The attached-cart search applies its cart step restrictions directly: entering
that branch already established the actor's hitch for this synchronous query.
See [layout neighbor measurements](../layout-neighbor-query-measurements.md)
for profiling, native state-change comparisons, lower allocations and mixed timings.
The later [walking-route neighbor measurements](../route-neighbor-query-measurements.md)
record the default-route follow-up; custom callbacks, including swimming,
keep their existing neighbor path.
Road-frontage scoring reuses the rectangular footprint and cardinal direction
checks with direct loops, avoiding nested iterator and predicate allocations.
The [Road-frontage measurements](../road-frontage-query-measurements.md) record
matched native captures, lower allocations and mixed timing limits.
Site evaluation enumerates placement footprints lazily, rejecting blocked tiles
without allocating the remaining translated points. It keeps full-footprint
legality before limiting offered candidates. See
[footprint evaluation measurements](../footprint-evaluation-measurements.md)
for matched native results and allocation/timing limits.
Building plans follow what a household needs, not a role. An adult whose
household lacks a House, Farmhouse, Blacksmith, Silo, Tailor Shop, Clinic or Restaurant is offered ranked sites
for it once the household has the build costs in hand: stock the household
owns anywhere, plus its members' stock. The shared `Holdings` query excludes
reservations, delivery promises and borrowed Market sale stock from both this
gate and the missing-material count. Each kind is planned at most once
at a time and a household never holds two of a kind; a second member choosing
the same kind in the same tick is refused, and a project stops if its household
comes to hold that kind. Only a household holding a Farmhouse plans a Silo, and
its sites must lie within two tiles of that Farmhouse, counting diagonals, with
touching sites ranked first. This provisional reading of "next to" keeps a Silo
possible when Roads, resources or later buildings take the tiles beside it. While the first building it still needs lacks a material, one
adult at a time is offered to gather it from a reachable source. Buildings the
Town shares, including a new Warehouse, are never offered to a household. The
kinds are listed in `HouseholdBuildingKinds`. The optional Store uses the same
household planning and ownership rules, with 1×1 and 1×2 footprints.

For Stores, Tailor Shops, Restaurants, Clinics and Blacksmiths, household plans
also share a Town limit: one of each kind per eight recorded residents, rounded
up. `ResidentsPerSharedShop` in `WorldRuntime.HouseholdPlanning.cs` is the
provisional tuning value. `TownHasRoomForSharedShop` counts placed buildings
assigned to the Town and other residents' live construction projects, across
all designs of the kind. Offers and plan admission, including construction
orders, use that same check. Admitted construction keeps its slot and may finish
if the population falls. Existing buildings above the limit remain; Houses, Silos, the Warehouse
and Council-approved Town projects keep their separate rules.

`WorkBuildingHasInputs` also filters autonomous plans and material preparation
by work the building can actually do. Farmhouses and Silos need reachable
household fields or planting stock, including an available seed site within
twelve tiles of the Town origin. Animal Yards need reachable wild animals;
another yard waits until existing Town yards have no free animal places.
Other work buildings need accessible inputs for at least one of their own
active recipes. The shared goods query excludes unusable, reserved and promised
stock and checks access and reach; natural supplies use shared gathering and
reach rules. Construction costs cannot double as the station's only input
stock. Restaurants also protect one day's meals for the Town's residents.
Houses and Stores have no production-input prerequisite. Admission rebuilds
the same eligible offers, and project start and continuation recheck the rule
before spending construction materials. Explicit owner construction orders
use the same prerequisite; already placed buildings remain in place. No
eligibility result is cached or saved.

Harvests remain household-owned lots on their actual field tile. An adult
carries a load of at most four raw crops or planting items to the household's Farmhouse or Silo.
The Farmhouse holds a provisional 96 items; the private Silo holds 64 units per
footprint tile. Both count deliveries already on their way;
pickup and delivery both check remaining space. Source selection checks the
adult's route to each pile or vessel and the route from there to farm storage;
an earlier blocked source does not hide later reachable stock. The same checks
run again when the hauling action executes. Grain prefers the Farmhouse,
while other farm stock prefers the Silo. Selection tries the next permitted
store if the preferred one cannot accept the actual load or its route is blocked.
A filled vessel must fit with its contents; only grain and flour retain their
approved partial withdrawals. An explicit destination prevents fallback to
another store. Ready-to-eat greens and fruit go to
the household's House. Neither stock nor ownership moves
remotely.

Flour hauling from the Farmhouse to the House checks each source with the
actual haul planner before choosing it. A broken pot or another unusable
source does not hide later usable flour. Pickup repeats this selection;
reservations, carrying room, destination space, whole-vessel transfers and
the existing partial flour withdrawals still apply.

Ordinary milling can draw needed household grain from its Silo into its
Farmhouse. Available and inbound Farmhouse grain reduce the pickup; recipe
demand, free storage, reservations, carrying room and both walking routes
still apply. Contained grain uses the existing partial pot-withdrawal rules.

Food selection tests the entire harvest load, including orchard seeds, so a
nearer oversized harvest does not hide a reachable one that fits. Making
room uses the permitted household serving or actual harvest size. Accepted
infant caregivers can collect household food for their dependent without
needing to be hungry themselves; current care authority, usable food,
reservations, capacity and real travel are checked again at execution.

House pickup counts the whole vessel family against carrying and destination
space; loose deliveries keep their four-unit limit. Loose-stock selection
also checks movement's current unoccupied pickup and source-to-House routes
before choosing a pile. Candidates, ordinary pickup and House-stock delivery
orders share that selection, so an occupied first pile does not hide later
reachable goods. Ownership, reservations and whole-vessel checks still apply.
An unusable carried
delivery leaves ordinary hauling and can offer `recover_household_delivery`.
Candidate and action both check the current household-owned destination,
physical load, selected or repaired gear, active family reservations and a
route to the shared storage spot. Recovery moves the same stock through the inventory authority,
clears its delivery pointers and records `household_delivery_recovered`.
Spoiled contents may be physically withdrawn from an owned usable pot, with
the existing family, quantity, destination and reservation guards. They
remain unusable for eating, recipes and new reservations. No agent disposal
action or player discard control is added.
