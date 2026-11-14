---
title: Finding goods
type: architecture
status: active
updated: 2026-11-14
---

# Finding goods

This page describes inventory queries and finding usable goods.

## Finding goods

`Kernel/InventoryIndex.cs` caches derived inventory facts by checkpoint identity
in a `ConditionalWeakTable`. It records lots by owner, building, delivery
destination and vessel, in stable ID order, plus active reserved quantities and
reserved vessel families. Every inventory transition commits a new checkpoint,
so its next query gets a new index; earlier checkpoints keep their own facts.
These facts are never saved. Do not mutate a committed checkpoint's collections.
Quantity rules live in `InventoryRules`: `UnreservedQuantity` is physical,
`AvailableQuantity` excludes spoiled or damaged lots without clamping, and
`UsableQuantity` clamps usable availability to zero. Runtime and system adapters
delegate to these rules; Market, equipment and tool selection no longer keep
separate quantity helpers.

`Kernel/InventoryRules.cs` owns the predicates used by inventory authority and
goods selection: physical and usable quantities, usable parent vessels,
reservation availability, free families and container ownership/location.
Physical movement may preserve damaged or spoiled property. Consumption still
requires usable inputs. `InventoryAuthority` remains the authority for an actual
reservation, transfer, container operation or consumption.

`Kernel/InventoryHolds.cs` adds typed stock holds for Market stalls, deliveries,
supply trips and borrowed goods. A hold covers a lot's whole physical quantity;
vessel operations also check every member, and contents inherit their vessel's
holds. Inventory callers must supply the exact purpose and holder for every
hold they use. Splits retain holds on both portions; detached goods retain
their inherited vessel holds. Wear and repair also check contained stock.
A reservation saves its
authorized holds and rechecks them when consumed or settled. Hold changes use
`HoldStock` and `ReleaseStock`, without adding inventory events or changing stock
history. Custody transitions create and release delivery and borrower holds with
location changes. `MarketStockHolds` updates loans when stock or stall occupancy
changes; animal supply trips hold their exact picked-up root until completion,
cancellation or lost custody. Queries read these holds instead of searching
Town occupancy or delivery pointers for promises. Read-only consumption plans
report each match's required holds; an executor still states only the purposes
and holders it is authorized to use. Saved hold collections compare by value,
so reload retains ordinary lot and reservation equality.

Supply-trip stock is excluded from unrelated goods uses. Animal care can use its
matching trip hold and counts those servings once when protecting family meals.
Collection and delivery permit the named borrower's complete vessel; other
promises stay protected. A forced dismount cancels a trip if it puts down any of
that trip's load, preserving unrelated reservations and property ownership.

`World/Goods/` supplies usable-goods and physical-recovery query uses. `Holdings` excludes unusable
lots or parent vessels, reserved quantities, delivery promises and goods held
for sale at a borrowed Market stall. `ConsumeAt` matches the inventory
reservation boundary, including usable contained ingredients and partially
unreserved quantities; reusable vessels themselves are not consumable inputs.
Requests name exact owners and item kinds, and can restrict a building. The
answer's `Total` and `First` read the same stable matches. It includes each lot's
root vessel, physical place, available quantity and whole-vessel movement size.
Production, farm-food, animal-food, writing-paper, ornament-input and iron-reserve
demand use `Holdings`, keeping their existing owner groups and thresholds. These
counts did not require a route before this migration and still do not. Food kinds
remain the same; house-tool demand and `SharedFoodQuantity` keep their own rules.

Within the Simulation source, `GoodsQueryBoundaryTests` keeps a per-file budget
of direct references to `InventoryCheckpoint.Lots` and `InventoryIndex.Lots`
outside `Goods/` and validation code. The recorded baseline in
[`goods-query-reads.json`](../../../tests/ClankerWorld.Tests/Architecture/goods-query-reads.json)
uses per-file budgets. Named delivery, hold and cargo operations
replace direct inventory writes; their remaining references perform inventory
transitions, while selection uses the shared query.
The guard binds C# property symbols, including aliases and conditional access;
comments, strings and unrelated `Lots` properties do not count. It exempts files
ending in `Validation.cs` and methods beginning with `Validate` or `Ensure`.
Existing physical accounting, inventory operations and state projections retain
their recorded references. New selectors use the shared query. Lower a file's
budget when references disappear; moving references requires reviewing the
changed files' budgets, and cannot spend another file's spare allowance.

`GoodsDemandCoverage.KnowledgeWriting` adds the named same-job coverage rule for
writing demand. `CoveredTotal` includes available sheets plus active paper
reservations with the native `knowledge_writing:<project>:paper` purpose owned
by the queried household or its eligible writers. Other jobs' reservations do
not cover writing demand. Covered sheets must still have a usable parent vessel
and cannot be promised for delivery or held at a borrowed stall. `Total`, `First`,
matches and rechecks remain unreserved; `Collect` refuses a coverage request and
still cannot pick up a reserved vessel family. Coverage and reservation indices
are derived from the current checkpoint and are never saved.

`Spend` and `Sell` share usable, unreserved quantities and delivery exclusions.
Spend excludes stock committed to a borrowed Market stall; Sell lets the Market
check its seller, live occupancy and stock receipt. Warehouse contributions,
Town donations, barter, business payments, ornament gifts, repair inputs and
land payments use these answers with their existing purpose and place filters.
Market selection computes food reserves across split lots once per query;
binding a source lot does not narrow the stock that covers its owner's reserve.
Actions query the current checkpoint again before reserving or transferring.
Already-reserved barter offers retain their exact-claim checks rather than
looking for their committed goods in an unreserved answer.

`StorePersonal` selects carried, unworn personal property for storage, including
damaged or spoiled goods, and refuses delivery promises and reserved vessel
families. Storage still checks destination room and the transfer authority.
Whole-lot custody moves retain the source identity. Only a partial move creates
a split identity and checks whether it already exists; an earlier split cannot
block moving the whole remainder. Ownership and reservation checks still apply.
Physical Market stock counts also remain physical, including damaged contents,
so usable sale selection cannot undercount occupied space. These queries add no
route, saved state or persistent world cache.

`Deliver` extends usable holdings with custody, free vessel families, carrying
room, the native pickup and loaded return routes, and receiving space from
`DestinationRoom`. Requests name a destination building. Matches and action
rechecks share these checks; inbound promises cannot leave a stale load eligible.
A query reuses each root's loaded-route capacity across its matching contents.

Delivery purposes preserve the existing physical operations. Ordinary stock
moves whole vessel families. Farm stock permits the established grain/flour
partial-vessel haul, while its planner retains source deficits, load limits and
whole-family preference. Expansion materials go to a building's outdoor site,
and Town-project materials go to the approved work site named by `Near`;
neither occupies indoor storage. These outdoor uses retain Town material access
and active-project custody. Source ordering and workstation surplus remain caller
filters. House, Farmhouse, Silo, Blacksmith, workstation, expansion and Town-project
selectors share these queries. Restaurant household supply uses the same eligible
matches before applying each source's reserve once. Gathering falls back only
when no stock can supply the destination.

`VesselFor` selects usable, unreserved vessel families and checks room for the
requested additional contents. Jug refill, stored-jug pickup, empty-vessel return
and food-pot selection retain their own ownership, place, route and destination
rules. `ReturnVessel` preserves physical returns of damaged carried jugs;
damage does not repair or discard property. A reserved or damaged jug still
counts as physically present even when it cannot be refilled.
Refill planning tries shores by distance, preserving their row/column ties,
and stops at the first point that passes terrain, loaded reach, occupancy and
return-carry-capacity checks.

`Eat` uses the inventory consumption boundary and requires the actor to carry
the food or its usable vessel, with no delivery assignment. Personal meals keep
their storage-pot and free-family requirement; milk drinking retains partial
unreserved portions across carried lots. Animal care and water supply use
`ConsumeAt`, with their existing yard access, feed reserve and whole-jug checks.
`Cargo` selects physical contents of the named owned handcart, including spoiled
goods and broken carts, but excludes a family reserved for another job. Actions
recheck the same query before unloading; load selection uses `ConsumeAt` and
retains its native custody and food-policy rules. `Kernel/FoodRules.cs` is the
single food catalog: ingredients can be stored without becoming edible servings.

`CarriedDelivery` reads a carried shipment, including unusable property, with
its exact destination and borrower hold authorization. Delivery and recovery
apply their existing usability, equipment and whole-family reservation rules
after this query. They recheck the chosen shipment rather than picking another.

`Salvage` reads unreserved physical communal stock at an abandoned Town from
the actor's current interaction point. It retains damaged or spoiled property
and whole-vessel load limits, excludes held families and grants no private
building access. Its action plan checks the selected quantity and vessel
contents again before moving them.

The remaining uses belong to later steps of
[#1366](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1366). Unsupported
origin/destination combinations are refused rather than silently ignored. Extra
units and a contents kind apply only to `VesselFor`.

`DestinationRoom(buildingId, movingRoot)` is the shared read-only calculation
for space at a building. It subtracts all stored physical units, positive net
growth promised to running or paused production, open business storage
reservations and collected inbound loads. Inbound vessels count with all their
contents, including damaged or spoiled property. Yard stock-supply trips also
promise their carried family to the yard; care and saddling trips do not promise
storage. An arriving load excludes only its own current family from inbound
promises, so delivery does not reserve that family twice. Business exchanges
account for the goods leaving and release only their own storage reservation.
The inventory authority still rechecks every actual transfer.

Storage, returns, custody, departure, yard supply, Warehouse stock, projects and
workstation deliveries use this same room calculation. Native Town recovery
also keeps its allowance for carried returns that have no bound Warehouse.
Capacity comes from `BuildingStorageRules`: private Silos hold 64 units per
footprint tile, Farmhouses retain 96 units, and buildings without a storage
limit remain unbounded. Room queries save nothing and change no state.

Actor-bound `Collect` extends Holdings with native custody/place permission,
free vessel families, actual carrying room and an unoccupied native travel route.
Whole-vessel matches include all contents in their movement size, even when
matching a contained kind. Already-carried supplies need no additional carry
room. The route runs last and is reused for lots at the same position and
interaction range within one answer; native route rules still account for
occupants, shared destinations and an attached cart.

`ReachableHoldings` adds the same native travel route to Holdings without requiring
empty carrying space. Preparation uses it to find usable supplies before
putting aside spare cargo; actual pickup still uses Collect. This keeps a full
load from hiding shared repair inputs, gathering tools or medical supplies
from preparation and demand checks. Planting keeps its current free-cargo
guard before selecting seeds.

`SharedItem` uses Collect for household equipment, repair inputs and planting
seeds, with the existing Warehouse fallback rechecked through the same use.
`CollectEquipment` rechecks the selected lot immediately before its transfer.
Garment and carry-aid choices use ReachableHoldings with their existing
post-equipment capacity check, so replacing a carry aid can make room for
the current cargo. They recheck before transferring or splitting the unit.
Ornament wearing uses Collect and rechecks before taking or splitting one
unit; household custody, storage and free-cargo guards remain additional filters.
Animal-supply sources use Collect and recheck before pickup; care permission,
feed reserves, product capacity, destination room and workstation surplus
remain feature-specific filters. These are the first household pickup callers
of [#1370](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1370).

Personal recovery uses `RecoveryHoldings` for property awaiting retrieval and
`Recover` for a load the agent can fetch now. Both include damaged and spoiled
belongings, while preserving personal ownership, former-household and heir
access, Market commitments, delivery promises and family reservations.
Recover also checks carrying room and the native route, including collection
at an entrance. Routine recovery, collection orders and personal planting-seed
selection share this query; collection rechecks the offered lot before moving
it. Departure guidance retains the holdings count even when cargo is full or
the route is blocked. A parked handcart stays outside personal carrying recovery.

Birth food uses `ConsumeAt` through the same inventory-only query core for live
selection and captured-checkpoint guidance. Household ownership, actor custody
and borrowed Market-stall exclusions remain birth filters. Birth rechecks its
selected food before forming the contributions. It reserves and consumes food
where stored, including usable pots with other reserved contents; it adds no
route or carrying-room requirement. The owner's October 11 clarification is
recorded in [#1676](https://github.com/ClankerWorldOrg/ClankerWorld/pull/1676).

Warehouse stock uses Holdings before route checks and ReachableHoldings
for reachable stock, keeping the resident-Town/abandoned-Town and Warehouse
ordering. Handcart household material counts use the same reachable query.
Preparation still permits making cargo room first; actual cart and construction
material pickups recheck the selected lot with Collect. Town access, top-level
stock, the intended return route and delivery quantities remain native filters.
Expansion materials also use ReachableHoldings and recheck the offered lot
before pickup. Expansion keeps its own household/Town authority, exclusions for
stock already at the site or in custody, household-before-Warehouse ordering
and loaded return capacity. Town-owned ground stock remains eligible for Town
expansion; ordinary Collect retains its existing place permissions. The older
`CanReachSharedItem` helper is removed. Milk-jug selectors were retired when
milk became ordinary food.

With `Explain`, exclusions retain their first failure in this fixed order:
owner, kind, building, damage, spoilage, vessel, empty stock, reservation,
delivery and Market sale promise. Collect then checks custody/place, family
reservations, equipped or hitched goods, carry room and finally reach.
ReachableHoldings checks reach after the Holdings rules. Recovery skips damage,
spoilage and usable-vessel checks, then checks personal custody/place, Market
and delivery commitments, family reservations and access. Recover adds carry
room and reach; RecoveryHoldings leaves those checks to its eventual pickup.
Diagnostics are derived, never saved or
logged every tick. Normal queries use the owner/building index; explanations
scan all lots so they can also explain an owner or place mismatch.

To add a caller, choose the appropriate use and keep its request for both
eligibility and execution. Add feature-specific filters when necessary; never
remove the use's rules. Call `RecheckGoods` against the current checkpoint
immediately before the authoritative action. Production's availability,
missing-input explanation, consumption and reservation share these requests;
its existing handcart, Market and carried-only restrictions remain additional
filters. A broken first vessel cannot hide healthy ingredients later in ID order.
Construction uses Holdings for both affordability and immediate payment, so it
does not divert goods promised to another building or held for sale.
