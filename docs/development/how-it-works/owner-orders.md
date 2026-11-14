---
title: Owner orders
type: architecture
status: active
updated: 2026-11-14
---

# Owner orders

This page describes the order grammar and physical order actions.

`ParseOrderTask` reads a complete, bounded grammar for eating food,
seeking a food source, harvesting food, gathering supported raw materials,
storing personal raw materials or equipment, collecting personal raw materials,
ready-to-eat food or equipment, repairing supported personal
clothing, carrying aids and tools, household field work, and moving to an exact
tile, attaching/parking owned handcarts, supported non-food production recipes,
a named face-to-face talk attempt and one held knowledge-artifact read. Harvest and food-source travel orders must name a supported kind or resource; explicit resource names must match a complete
identifier and the requested kind. Unsupported
objects or operations, mixed tasks, unknown explicit targets, and invalid
quantities or leftover words are rejected as not understood rather than mapped
to a nearby candidate. A recognized order retains the player's original text and the
understood action, but the simulation still checks legal choices and requires
the requested physical effect before recording progress. Names in the prompt
do not create map knowledge. Optional observer replies are tied to the exact
message ID and stored separately from private thoughts and conversation
speech. Local deterministic decisions do not mark messages as heard.

Food-source orders can use food directly observed within normal gathering
range even when the agent's map memory is full. Food kinds and exact resource
or coordinate targets still apply; distant sites require that agent's own
knowledge. Observation does not bypass availability, reachability or the
whole-load carrying checks.

Urgent food interruptions admit drinking usable carried milk while retaining
the pending order. Milk's ordinary freshness, reservation, ownership and
physical custody checks still apply.
They also admit the buyer's legal ready-food shop quote,
Restaurant meal visit and continuation of an open food purchase. The same
check is used for selecting the interruption and filtering model choices.
Raw ingredients, equipment and a seller's response cannot use this food
exception; payment, carrying room, physical arrival and consent still apply.

Material orders save the requested kind separately from food targets. They use
known resource facts or observation within normal interaction range; a named
unobserved site first requires physical travel. Untargeted orders may use normal
exploration with a saved requested-material purpose when none of their known
matching sources is reachable. Inaccessible
facts stay in the knowledge ledger. A reachable source still requires the
normal tools and carrying room; explicit resource or tile targets do not
substitute another source. Gathering uses the existing tool pickup, whole-load capacity,
inventory, tool-wear and ecology transitions. Only a returned physical harvest
receipt advances progress. One load is the default; explicit quantities count
actual output, including a final whole load that exceeds the requested amount.
Discovery, tool collection and movement never count as harvested goods.
Only the observed resource site joins map memory; walking there adds no facts,
so the journey cannot fill the agent's bounded ledger before arrival.

`Move to tile (12, 4)`, `Go to (12, 4)` and `Travel to (12, 4)` create a
`move_to` order with a saved `TargetPosition` and one arrival. The common
unoccupied-route finder and `MoveToward` enforce walking rules, occupancy,
travel cooldowns and illness delays. House destinations check current household
membership or a saved guest invitation. An eligible Warehouse user can share its
exact tile with other occupants, using recorded Town access including abandoned-Town
salvage. Residents can also share their Town's supplying or working project site.
These destination exceptions keep stock ownership, reservations and action
permissions intact; other occupied exact destinations stay blocked.
An unavailable destination stays blocked;
it is never substituted. The order completes only when the actor occupies the
exact target tile. Repetition, quantities and extra task words are rejected.
Arrival records one firsthand fact, for the destination tile. Submission,
waiting and the walk itself add none, so a long trip cannot fill the agent's
bounded map memory.
Children and adolescents can walk under an order; infants still cannot take
instructions. The normal queue, cancellation, stale-reply and survival rules
apply without a separate model request for each step.

The strict guardian-order form is `Become guardian for <full name or exact ID>`.
It resolves one active child with an open search and saves that child's ID as
`TargetAgentId`. Only an adult can carry out this task; renames cannot retarget it.
`CanAcceptGuardian` is checked again for each step, and acceptance goes through
the existing dependent-care transition. Completion requires the actual primary
care assignment. A search that closes first leaves the order blocked rather
than replacing its accepted guardian. Queue, cancellation, stale-response
checks and urgent survival interruptions use the common order lifecycle.

Storage orders reuse the material, equipment and goods catalogues and normal personal-storage
eligibility. `StorePersonalGoods` serves both ordinary choices and orders: it
walks to the House entrance, then uses `InventoryAuthority.Relocate` to preserve
ownership, condition and provenance. Reserved goods, promised deliveries,
container contents, food and selected equipment are excluded.
House capacity already promised to collected inbound deliveries is unavailable
to personal storage and borrowed returns, at both choice admission and the
physical move. Whole vessels must fit with every content lot in the remaining
space. Only a committed relocation receipt advances the order; its identity is hashed to a fixed length
because split inventory identifiers can grow. Walking and survival actions earn
no storage progress. Default tasks count one stored lot, while explicit quantities
limit the final relocation to the remaining amount. Repetition keeps waiting
for further matching personal goods or space until cancelled. The destination is
bound once to the current household's House identity, household owner and listed
position, before travel. An optional requested coordinate must match that House.
Each step rechecks the binding, so removal, reassignment, movement or departure
blocks the task instead of retargeting it. Named foreign buildings remain unsupported.

`store_equipment` uses the existing exact `TargetEquipmentKind` and the same
garment/carry-aid/tool subjects as collection. Complete equipment names are read
before material names, so "stone pickaxes" cannot select raw stone. Explicit
quantities count `equipment_items`; default orders count `storage_loads`.
Selection and execution both use `PersonalStorageLots`, preserving the exclusions
for worn items, reservations and borrowed or promised goods. Storage does not
equip or repair goods. Model guidance and client task text name equipment storage.

Collection orders use `PersonalGoodsAwaitingCollection` and the shared
`CollectPersonalGoods` action. The actor must own the lot, which cannot be
carried, reserved in full, promised for delivery or inside another container.
Storage must belong to the current household or one recorded in that actor's
departures. Ground lots use normal pickup range. The nearest reachable eligible
lot is chosen, with stable identity ordering for ties. An optional source tile
filters this same set by its current physical position, at both selection and
execution. Moving goods away or exhausting the tile leaves the remaining order
blocked; it never falls back to another location. Building storage uses the
building's listed position. Physical pickup preserves ownership, condition, provenance
and reserved portions, with the final quantity capped by carrying space and
the requested remainder. Only the committed relocation earns progress, using
a bounded hashed receipt. Former-household collection grants no other access.
Vessels must fit with their entire contents before selection as well as execution.

Routine personal recovery uses the same current pickup-range and unoccupied-route
check before offering a lot and again before collection. An unreachable lot keeps
its ownership and location without blocking reachable belongings. Routine
priorities and identity ordering still apply among eligible reachable lots.

`collect_goods` and `store_goods` use an exact `TargetItemKind` from a separate
logistics catalogue. They do not broaden the gathering or repair subjects.
Explicit quantities count `goods_items`; default tasks count collection or
storage loads. A vessel relocation moves one whole container family, preserves
its ownership and credits one vessel, never its contents as extra progress.

`return_borrowed` uses the same named goods plus the existing material/equipment
subjects. It binds the carried source lot and its owning household's exact
House before travel. The shared borrowed-return helper rechecks custody,
ownership, reservations, whole-vessel space and the destination at execution.
Explicit returns select carried borrowed goods independently of ordinary milk
stocking alternatives. A possible Store or market destination does not exclude
carried milk; committed deliveries and active animal-supply trips still do.
Only a committed relocation receipt earns `goods_items` or `return_loads`.
Cancellation never undoes completed movement or changes goods ownership.
When an explicit storage or return moves the exact hoe or sickle used by that
agent's ordinary field work, it first interrupts that work through the normal
cancellation transition. The new order can then move the tool without leaving
a stale field-work reference in the same checkpoint. Spare tools and unrelated
field work are unaffected.

Food collection uses the same path with action `collect_food`. A separate
bounded subject parser accepts generic food or exactly berries, fruit, wild
greens or cultivated greens, without resolving natural-resource aliases. The
lot filter uses ordinary edible-food eligibility and any exact `TargetFoodKind`.
Default orders count one `collection_loads` pickup; explicit quantities count
`food_items` and cap the last pickup at the remaining amount. Shared food
collection, harvesting and eating never credit these orders. Model guidance,
observer activity and task labels identify the action as personal food pickup.

Equipment collection uses `collect_equipment` and the existing exact
`TargetEquipmentKind` catalogue for five garments/carrying aids and 13 tools.
The parser tries complete equipment names before material names, so "stone
pickaxe" remains a tool while "stone" remains a raw material. The same personal
pickup path preserves condition and never equips or repairs the goods.
Explicit quantities use `equipment_items` and cap the final relocation; default
orders count `collection_loads`. Shared and borrowed equipment never enters
this order's eligible personal-lot set.

Repair orders save a separate `TargetEquipmentKind` for basic clothing, padded
coats, rain cloaks, baskets or sacks. The parser refuses unsupported equipment and
explicit sites. Orders filter the normal worn-item rules by this exact kind
and use `RepairEquipment` for planned pickup and repair start, then
`ContinueEquipmentRepair` for saved work. Ordinary repair chooses the first feasible worn item, preferring the equipped
carry aid when its materials, carrying space and private work site are usable.
Ordinary repair carries its selected item and material lots through one plan check;
orders and civic repairs use the same planned pickup order, repair-start commit and saved continuation.
A repair work record links to the active instruction; only the returned completed
repair advances its item count, with a bounded receipt derived from the actor,
start time and lot identity. New orders release any previous repair's unspent
inputs before starting their own work. Cancellation or replacement releases
reservations immediately. Survival interruption follows ordinary repair rules:
release unused inputs and restart unfinished work when the order can resume.
Save/reload retains a running repair's work counter and exact reservations.

Tool repair orders use `repair_tool` and the same `TargetEquipmentKind` field,
restricted to the 13 supported tool kinds. The parser requires the material and
tool name. `RepairableTools` shares ordinary private Blacksmith, material,
carrying and route checks. Both ordinary repair choices and execution require
positive remaining condition; fully broken tools need replacement. Orders
additionally require personal ownership. Ordinary repair retains its
borrowed-household behavior. Order preparation protects every matching worn personal tool from
spare-cargo storage and refuses gathering that would break a requested tool.
Preparation retains the quantity needed for one repair across usable carried
material lots. Excess units may be stowed to collect another ingredient; live
reservations, promised cargo and protected tools remain unavailable for stowing.
`RepairTool` performs physical pickup, gathering, spare-cargo storage
and walking, returning a repaired lot only after the real inventory transition.
That return alone earns one repair, with a bounded receipt derived from actor,
time and repaired lot identity. Stacked worn lots split into individual repaired
units. Preparation has no reservation or timed job to unwind: cancellation keeps
already collected goods and spent tool wear. Remaining quantities and queued
work survive reload; survival can interrupt before repair resumes.

Field orders use `till_field`, `plant_field`, `tend_field` and `harvest_field`,
with an optional `TargetCropKind` limited to the three existing crops. Explicit
quantities require the word "field" or "fields" and count finished work sites.
Selection uses household access, physical routes, usable tools and actual
planting stock without the ordinary food-demand preference. It never substitutes
a different named crop. An optional `TargetPosition` filters both new tilling
sites and existing fields; it never redirects unfinished quantities to another
tile. `ApplyFieldCandidate` performs normal walking and stock
collection; `StartFieldWorkCore` binds new work to the instruction. Only a
completed result from `ContinueFarmWork` credits a bounded receipt. Cancellation
releases unused seed reservations and removes a partly tilled field. Ordinary
work retains its existing priorities and behavior; urgent survival cancels the
current work under normal field rules and the order resumes its remaining count.
Saved work must match the actor's active instruction, action, crop and any
explicit tile.
Validation refuses links to another agent, task or equipment kind.

Delivery orders use `deliver_stock` with a separate purpose, exact item kind
and requested building kind. Bounded `haul`, `supply`, `deliver`, `donate` and
`stock` commands select existing native policies for household/farm stock,
workstation inputs, spare House food, Town Warehouse surplus and Store stock.
Their plans preserve the ordinary source, demand, reserve, capacity, access
and route checks. A requested farm-storage destination filters legal choices
before applying the default Farmhouse/Silo preference.
The Silo replenishment fallback applies those same constraints: it supplies
only grain to the household's Farmhouse, never a different requested item or
stock already stored at the destination. An unavailable requested item leaves
the order blocked without binding an unrelated shipment or earning progress.

Town Warehouse donations retain four usable personally carried units of each
resource kind across all eligible lots. Storage and collection splits do not
multiply that reserve. Reserved, borrowed and promised delivery stock cannot
satisfy it. Each transfer still uses one exact source lot, its unreserved
quantity and the destination's available room.

The order binds a native route, building identity, owner and listed position,
then the exact carried lot and requested-kind quantity. Partial pickup derives
the lot handle through the same `DerivedId.Create` factory as the inventory
transition, preserving the binding when the complete split identity is hashed.
Household pickup uses the existing ownership transfer and `DeliveryBuildingId`
promise; a Town
Warehouse never becomes a household delivery destination. A matching carried
shipment can be explicitly bound before its final delivery. Continuing a bound
shipment does not repeat ingredient-demand selection, because inbound stock
already reduces that demand. Each deposit rechecks custody, access and room.

Personal building and crafting projects finish a valid carried household
delivery before acquiring missing inputs. When its destination cannot fit the
whole load, the project retains its existing blocked state and cargo instead
of retrying a capacity-impossible harvest. Ordinary project reconsideration
still applies, and delivery rechecks available room before it resumes.

Typed committed movement effects provide the actual transfer identity and
quantity. Only final delivery credits `goods_items` or `delivery_loads`, never
pickup, walking, an inventory difference or an unrelated event. A selected
load waits if its destination no longer has room for it. Whole-vessel plans
sum every matching contained resource lot, preserve the entire family and
reject a move that would exceed a finite request. Only the existing grain/flour
path may take part of a vessel's contents. Cancellation keeps real cargo and
delivery promises; it does not refund ownership or revive old progress.

Content rollback checks saved production and delivery orders against the
remaining definitions before changing the registry. A package removal that
would invalidate an active or historical order is refused without changing
the checkpoint; unrelated package removal remains available.

The production catalogue includes every shipped named-meal recipe, including
the 2×2 Restaurant's exact variant definitions. House and Restaurant prefixes
disambiguate shared output names; the larger Restaurant uses `restaurant 2x2`.
The three simple-meal recipes name their ingredient rather than treating
different inputs as interchangeable. Existing whole-batch validation and
order-bound production jobs account for their two-serving yields.

Named-meal inputs extend the existing House/Restaurant `workstation_input`
routes. Supply still checks current recipe demand, source cooking reserves,
whole vessels, access and room. Prepared-food nouns extend `collect_food`,
borrowed returns, spare House-food delivery and Store stocking. Personal
storage continues to exclude loose edible goods. Loading allows prepared
food targets only on eating or food-collection orders, retaining the separate
item targets on returns/deliveries and rejecting raw-crop food targets and
mixed fields. No order fields, receipts or checkpoint layout change.

Building orders use separate construction and expansion adapters. A
`construct_building` order selects one active household building definition,
then binds its household, exact site, project start and unique instance
identity. Its own agent project performs normal material acquisition,
travel and work. The catalogue includes the exact shipped 1×2 Restaurant,
using its normal 8 wood and 2 stone cost and household ownership rules.
Only successful placement creates the retained construction
receipt and credits the order. The receipt preserves the actor, order,
definition, owner, site and paid-material evidence after the project is
replaced or the building is removed; an unrelated existing building is never
completion proof.

An animal-yard construction order checks for the household's completed House
before choosing a site or starting a project. A missing House blocks the order
without reserving or consuming yard materials, matching ordinary planning.

The construction adapter can replace an unpaid ordinary project rather than
waiting on the project the operative order itself paused. It creates a fresh
order-bound project, with no inherited work or placement credit. A Store-stocking
order instead keeps that ordinary project while its typed delivery proceeds;
ordinary automatic Store stocking still waits for unfinished projects. Both
paths wait for a live paid production job to finish, retaining its reservations,
goods and history. Projects bound to another order or an accepted tool-making
request keep their existing checks. The shared eligibility check adds no saved
state, and neither adapter adopts the ordinary project's payment or output as
order completion proof.

An `expand_building` order binds the original building, definition, owner,
position and revision, plus the chosen next footprint and anchor. It then
links its real expansion job through `OrderInstructionId`. Normal permission,
need, placement, cost and reservation rules remain authoritative. Only that
job's successful completion earns progress; retained completed-job history
keeps the proof if the building is removed later. Urgent survival pauses
ordered work before completion, and cancellation releases only the order's
own unfinished work and unused reservations. Shared expansion pickup excludes
carried household lots, just like ordinary shared-stock collection. Cancelling
an expansion and leaving the household preserves a borrowed delivery in its
former worker's custody; another worker cannot collect it until it is physically
returned. The current worker's own delivery and available ground or stored
materials retain their usual checks. Neither adapter adopts an unrelated
project or job.

The `ordered-building-` identity prefix belongs to the internal construction
path. Saved building orders also reserve their bound instance identities,
including after cancellation or removal. Public placement cannot reuse them;
only an order's own construction completion can place its pending identity. This
prevents replacement buildings from satisfying old bindings or mixing new
material payments with retained completion receipts. Ordinary paid rebuilding
by the same agent chooses a fresh, deterministic instance identity when its
previous identity belongs to retained order history. It still pays the normal
cost and cannot satisfy the earlier order.

Shelter orders use `seek_shelter` for one protected arrival and `tend_fire`
for one real ignition. Their saved binding pins a natural-cover position or
the exact building, owner, anchor and placement generation. Own and invited
homes are known targets; other buildings must be locally observable. An
explicit distant coordinate is approached before its shelter facts are
inspected. Firsthand map facts record only passable tiles. Inspecting an
impassable shelter or fire target can record the legal approach tile, but
leaves the unusable order blocked without adding an invalid target fact or
interrupting saving. Natural cover uses the existing bounded local search.

Arrival requires live native coverage and permission at the actor's actual
position; it does not certify warmth recovery. Ignition uses native fuel
acquisition and consumption, then records the newly created completed
`heating_fuel` reservation for one owned wood. Each fire completion has its
own reservation. Gathering uses known or locally observable wood; an absent
usable supply leaves the task blocked. Historical completion survives weather
changes, fuel expiry or building removal; unfinished work rechecks the bound
target and access.
Food survival can interrupt these tasks, but cold does not suppress the
protective action. Children may seek shelter; tending fires remains adult
work. The parser refuses quantities, repetition, durations and recovery
promises unsupported by these single-task adapters.

Production orders use `produce_item` and retain the exact recipe, output kind,
chosen work site and owned project/job identity. The job's optional
`OrderInstructionId` proves that the instruction started it. The bounded catalogue resolves
supported product names without interpreting arbitrary recipe text or model
output. `output_items` counts actual produced units and accepts only whole
recipe-yield multiples; `production_batches` counts one task by default or an
explicit number of batches. A named site constrains the same normal access and
workstation checks. Unavailable inputs or sites block the saved task.

The order advances its bound agent project through ordinary preparation,
travel and production, even though ordinary project continuation yields to an
active instruction. Starting or waiting for a job earns no progress. Only a
committed completion of the exact bound job credits its physical outputs, with
a production receipt preventing duplicate credit. Outputs retain ordinary
recipe ownership. Each reusable vessel in a recipe output is a separate
quantity-one lot; a batch of jugs, pots or handcarts never becomes one stacked
vessel. The first item keeps the usual output lot ID, and additional vessels
have deterministic unit IDs. Handcarts remain personal ground items; other
vessels use the recipe's normal owner and storage location.
Cancelling or replacing an order releases its unfinished
job's inputs without adopting or cancelling another project. Survival pauses
the bound job before it can finish and resumes its remaining duration after
the actor returns. Recipe, project, job and order references are validated
together on restore. Content removal checks retained production orders,
including cancelled history, against the remaining definitions before changing
the registry. Retired generic cooking recipes are excluded; the catalogue uses
the exact active named-meal recipes described above.

An order with no recognized action is closed when it is submitted, with an
`instruction_not_understood` event
(`<agent ID>:<instruction ID>`), which the Event Log shows. This preserves the
active and queued recognized orders. A closed unsupported order requests no
decision and does not block later instructions. Suggestion interpretation stays
separate from the strict order grammar.
