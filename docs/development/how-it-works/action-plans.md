---
title: Action plans and capabilities
type: architecture
status: active
updated: 2026-11-14
---

# Action plans and capabilities

This page describes retained action plans and actor capabilities.

Child choices carry a server-only `ChildActionPlan` with their selected person,
resource or lot, household, House, route purpose and quantity. The same
`CheckActionPlan` validates it when offered, after reply admission and again
before application in agent order. Application uses those selected values;
continuing intentions plan their next step from the current position. Plans
are excluded from prompts, observation digests and saved state. A stale plan
records a `choice_refused` event with a plain refusal reason in accepted history;
automatic stale choices stay out of the player Event Log.
The private child forwarders remain for existing callers.
Deferred admission checks the original offered plan before the scheduler
completes its reply. A planned identity that disappeared from the current
choices reaches its typed refusal; current order/context exclusions still
refuse it, while unplanned identities and request/epoch checks retain their
existing scheduler gates. An accepted fallback keeps its own plan and never
receives the refused provider choice's plan metadata.
Before dispatching a queued observation, the host checks every planned choice
against the current world with `CheckActionPlan`. Choices invalidated by earlier
actions are removed from the request and its digest is rebuilt; other choices
can still proceed. A reply already in flight retains its original plan; refusal
clears the intention so the agent can make a fresh choice. A finished order's
undelivered acknowledgement retains its original message.

Town communal-stock choices use `TownStockActionPlan`. Warehouse donations keep
their selected lot, load and Warehouse; salvage keeps the selected communal
stock and, for a vessel, its complete family of lot identities, owners, kinds
and quantities. The ordered routes apply those values after the same offer,
admission and application check. Neither path selects replacement goods during
application. Private forwarders retain their original signatures.

Household stock choices use `HouseholdStockActionPlan` for collected deliveries,
preparation-tool storage, loose-stock pickup, spare-food storage and unusable
shipment recovery. Their original branch order and choice wording remain.
Each plan binds its household, selected lot, House or storage pile, quantities,
and vessel family. The shared check revalidates that exact shipment and named
hold authorization before any movement or transfer. `GoodsPlanFamily` supplies
one immutable family capture for both Town salvage and household shipments.

Food-space recovery uses `FoodCargoActionPlan` to bind every cargo lot and quantity,
the original storage destination and any orchard reservation release. It checks
the same cargo at offer, admission and application. New spare stock never replaces
the selected load; only the captured orchard units are released when set down.
The shared `SpareCargo` and `OrchardFoodRecovery` goods uses keep ordinary physical
stock separate from the urgent adult seed exception. Legacy order and project callers retain their existing forwarders; planned
tool preparation shares captured cargo storage.
Blacksmith preparation shares the captured cargo check and storage commit.

Farm stock choices use `FarmStockActionPlan` for grain and seeds going to private
farm storage and flour going to the House. The plan keeps the source, destination,
resource amount and whole-vessel or partial-load transfer form. Its shared check
revalidates that load, the trip, capacity and any milling deficit; it never selects
replacement stock or a different store. Existing delivery-order forwarders remain.

Workstation supplies use `WorkstationSupplyActionPlan` for carried inputs, stock
pickups and source gathering. One codec retains the existing supply choice ID.
Each plan binds the workstation and stock family or resource, harvesting tool
and yield. Gathering applies the captured harvest through the common material
commit path; it does not select another tool or source. `CarriedInputs` validates
physical custody and the named borrowed-stock authorization.

Store stocking uses `StoreStockActionPlan` to keep its selected lot, Store and
load. The same check retains current food reserves, best tools, shelf target,
physical room and pickup/delivery route rules. Shelf accounting includes usable
physical stock even when promised; selecting a new load uses the shared goods
query and named hold authorization. Shop trade and order forwarders remain.

Blacksmith delivery choices use `SmithDeliveryActionPlan` for personally mined
ore and other on-site inputs. They retain the chosen supplies, quantities and
pickup or direct-delivery step. Direct loads must remain physically carried;
personal storage invalidates that captured delivery step. One quantity check preserves the dedicated ore
rule and the ordinary input rule, including their different incoming-stock and
load limits. Order delivery keeps its existing forwarder.

Blacksmith gathering uses `SmithGatherActionPlan` to retain the source, tool
and complete harvest. A preparation step binds either the shared tool pickup
or the spare cargo, its quantities and storage destination. The same check
revalidates that step without selecting replacements, and ordinary gathering
applies the frozen harvest through the common material commit. Repair choices also keep their selected materials and preparation.

Tool collection uses `ToolCollectionActionPlan` for the exact shared tool and
its pickup route. It retains family eligibility, carried-tool tiers and room
for a pending whole harvest, including the captured cargo that could make
room after an upgrade. Its check and commit share the gathering-tool pickup
boundary; application does not select another tool.

Tool repair uses `ToolRepairActionPlan` to bind the worn tool, carried materials,
missing stock or resource/tool/harvest, and any cargo storage step. Input routing
selects an order once and checks that same order with its complete load. The
repair check also revalidates shared-stock collection permission before cargo preparation or pickup. The
repair commit consumes the captured material IDs; personal and borrowed tools
retain their original split, gift, reservation and receipt rules. Legacy order
forwarders choose a fresh step and share that commit. Planned and legacy
collection use one exact equipment pickup operation, preserving the distinction
between borrowing tools and transferring repair materials into personal custody.

Rare mining uses `RareMiningActionPlan` to keep the selected deposit, pickaxe
and full yield. Its declared capability policy preserves urgent-food, warmth
and ongoing-project gates. The household trial quota continues to count all
physical owned rare material, including promised or unusable units, through
named goods accounting; the check does not pick a new deposit or tool.

Field choices carry a `FieldActionPlan` with the selected tile, crop, planting
lot and replanting claim, hoe or optional sickle, and work duration. Farming
checks that same plan before starting work or collecting its exact seed.
`FieldPlanting` goods queries admit only usable free stock or the field's own
replanting reservation. Field reach explicitly uses terrain eligibility while
temporary occupants pass; movement continues to check the actual route.

Tree planting and replanting carry `TreeActionPlan` with the exact seed, its
orchard reservation, the selected open tile or stump, and the chosen species.
The same check preserves those selections before pickup or planting. Shared
seeds use the common exact equipment pickup operation. `TreePlanting` goods
queries include only the seed's own orchard claim alongside free usable stock;
new claims and regrown or newly occupied targets refuse the original plan.

Household jug, pot and empty-vessel choices carry `VesselActionPlan`. Whole
vessel moves retain the exact family; pot operations retain their selected food
lot and quantity. Filling binds its jug, shore, amount and prospective House
return. One check revalidates capacity, source reserves, promises, food policy
and the same trip before the captured operation applies. Shore searches use
the shared reach service with explicit origins and complete prospective loads.

Clothing and carrying-aid choices carry `PrivateEquipmentActionPlan` with the
selected stock, owner, pickup and previous slot. One upgrade rule serves both
selection and checking. The `Equip` goods use checks custody, promises, place
permission and reach while leaving room calculations to the equipment swap:
an upgrade may make space even when ordinary collection has no free room.
The commit keeps the existing one-unit split or transfer and displaced item.

Personal equipment repair carries `EquipmentRepairActionPlan` with the worn
item, private work site, exact carried material lots and ordered shared pickups.
The shared input-trip planner chooses an order once; its check revalidates that order
with cumulative loads and the return trip at offer, admission and application.
It also checks room for every missing input. `EquipmentRepair` goods selection retains
broken items while excluding existing reservations and promises. Starting work
reserves the captured materials through the same commit used by repair orders;
continuation still uses the saved work record and reservation IDs.

Fire tending carries `FireActionPlan` with the selected hearth and exact
carried wood, shared pickup, or resource/tool/harvest. It rechecks the same fuel,
return load and hearth before applying; a changed last-tree harvest is refused
instead of changing its seed yield. The fuel commit is also used by shelter
orders and preserves the existing payment and fire duration.

`WarmthActionPlan` retains the chosen building, lit-hearth requirement, tree
cover or decision to remain protected. Checking and applying preserve current
cover that already stops cooling. A lost destination or blocked route gets a
plain refusal instead of selecting another destination while applying.

Ornament choices use typed wear/remove/gift identities with their original
bounded hash spelling. `OrnamentActionPlan` retains the selected unit, owner,
pickup, previous worn slot or recipient as each operation needs. One check
serves offer, admission and application; limits follow eligibility filtering.
Fresh personal admission remains necessary to wear, remove or give an ornament.
Continuing an intention can only walk toward that exact stock; public operations
and plans share the native one-unit commits and rollback behavior.

Ordinary food choices use `FoodActionPlan` for the exact carried serving,
household loose/pot serving, or resource and full harvest. One check preserves
current custody, reservation/hold rights, household food policy, complete
harvest room and the captured source or pot trip. Passenger consumption carries
the same plan while allowing boat use. Planned and legacy order/care paths share
the native consumption, serving collection and food-harvest commits; application
never chooses a replacement serving or resource.

`ActorCapabilities` checks each action's declared life stages, boat, mounted,
urgent-need and active-order policy against a current read-only actor snapshot.
Systems receive that snapshot per call; it is neither saved nor cached. Reach
keeps the original swimming and carried-load rules, with a named purpose.

`ActorCapabilities` checks the life-stage policy declared by each action
definition. Candidate spelling does not add a second age rule inside that
primitive. Cognition prepares an unplanned ordinary step through its registered
definition once and checks the retained plan before dispatch; application checks
the same plan again. Owner orders declare their command life stages through
`OwnerOrderCapabilities`, using the Action projection shared with typed orders;
the production API uses the existing recipe-goal policy. The former candidate-name
age whitelist is removed, leaving life-stage primitives in `LifeStageRules`.

`Actions/LifeStageRules.cs` owns action age eligibility and the shared adult and
dependent checks used by Town membership, governance, hearings, markets and care.
Medical permission, retained care trips, guardian placement and infant-family
continuity use these same primitives without changing their allowed life stages.
`Actions/NeedThresholds.cs` names the shared food and warmth thresholds, including
food exploration, household delivery and illness exposure/recovery. Source
scanning covers both need properties and the local fullness/warmth variables.
Child routes use
one per-call reach service with an explicit swimming policy and separate
prospective return origin. Native movement and pickup approaches use the same
service from the actor's current position; prospective pickup returns explicitly
keep their selected origin, foot-only policy or additional carried unit.
Project and blacksmith supply and the shared goods query's collection, delivery
and recovery checks use actor-position reach with their original route caches.
Town remedy estimation names its prospective origin for each sequential leg
through the same reach service, with explicit purpose, load and swimming policy.
Order routing uses the same service for cart, custody, delivery, expansion,
field, material, movement, shelter, storage, conversation and tree-planting work.
Its delivery, field, material and shelter helpers retain explicit future origins.
Those orders share one prospective-leg boolean reach query, including the
original in-range shortcut, rather than maintaining separate route predicates.
Route-call scanning covers bare calls, member calls
and conditional member calls, so adapters cannot bypass the source inventory.
The animal world port exposes no unused direct route search; animal action
plans use their existing purpose-specific reach calls.
Learning, market, dependent-care and workstation-supply eligibility use the shared
service's boolean reach query, including the existing in-range shortcut, rather
than site-specific wrappers.
Farming uses that same query with terrain-only eligibility for field planning
and occupancy checks for an actual seed-pickup approach; its unused coordinator
forwarder and field-specific route predicate are removed.
Pure terrain/geography eligibility also belongs to the reach service. It keeps
the existing foot/freshwater checks without building a physical route or checking
temporary occupants; every call names its purpose, load and swimming policy.
Stock-trip and household-tool access checks keep their domain-specific source,
ownership and borrowed-stall rules while sharing the same boolean reach queries
for each physical leg.
Tool-request choices obtain their checked plan directly from the registered
definition when constructed. The sixteen-new-request limit counts only offers
that pass that check, and the former second-pass planner is removed.
Town-project donations likewise count checked plans before applying their four
offers per budget entry and sixteen overall limits. Native selected-choice lookup
does not inherit those presentation caps.
Civic conduct reports count toward their six-report limit only when an offered
filing plan passes its check, preserving report order and all filing variants.
Exploration supply, household tool coverage and tool repair also use actor-position
reach. The source guard in `ActionRuleBoundaryTests` requires no copied age-band
predicate, unnamed need comparison or direct route call outside the shared rules.
Cached existence queries, including the cart-aware route fallback, live in the
same shared reach core. The old source allowance file is removed. Child stock selection and exact-lot rechecks use
the shared goods query before applying the original child load and trip limits.
