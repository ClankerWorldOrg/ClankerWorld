---
title: Orders and land records
type: persistence-reference
status: active
updated: 2026-11-14
---

# Orders and land records

Saved work orders, land records and civic hearing evidence.

## Order progress and receipts

World schema 49 stores recognized food-order targets, progress, retry
state, cancellation receipts and their exact actor/world identity alongside the
original owner instructions. Loading validates these records together so an
unrelated action or a stale order cannot advance a replacement task. Alpha saves
must use the current checkpoint schema; older saves are refused without
migration and remain unchanged.

World schema 121 records each Town's first completed building time,
including a completed building incorporated at founding. The time remains
after removal, abandonment and reload, so constructing a replacement cannot
repeat the first-building Road link. Loading checks it against the Town's
founding time and world clock and requires it when a Town has buildings.
Inter-Town Road tiles and bridges use their existing saved records. Current
roundtrips and replay preserve the time, complete Road network and original
Town/household land; older alpha schemas are refused and preserved without
migration.

Food consumption progress keeps a fixed-length SHA-256 receipt derived from
the actual consumption's world time, actor and complete lot identity. Valid
inventory splits can lengthen that lot identity without lengthening the saved
order receipt. A completed order retains its progress through reload and
cannot consume or credit the same completed task again.

The last accepted model choice keeps the complete offered candidate ID, just
like the current intention. Inventory-backed actions may exceed 512 characters;
this historical ID has no separate length limit and is never truncated or
replaced with a different action. Loading still refuses blank IDs, control
characters, invalid statuses and inconsistent acceptance times. A later model
failure keeps the last accepted choice independently of its safe fallback.

World schema 65 adds material-gathering orders with a distinct
`TargetMaterialKind`, exact optional source or position, and progress measured
in harvest batches or material items. Saved progress and the last physical
harvest receipt are validated together; mixed food/material targets and invalid
material kinds are refused. A harvest receipt uses the shared derived-identity boundary and hashes
the complete original receipt when needed.
The actor and native inventory lot keep their full identities; shorter receipts
remain unchanged. Current-format reload accepts the short and bounded hashed shapes.
Queueing, cancellation, discovery and partial
quantities retain their state across reload. Older alpha saves are refused and
preserved unchanged; no migration is added.

World schema 66 adds `store_material` orders using the same bounded
material target. The destination is the current household House, so source,
food, guardian and coordinate target fields are refused. Progress counts storage
loads or exact item quantities and requires a committed personal-relocation receipt
with a fixed-length identity, even when the inventory lot identifier is long.
Replay preserves partial storage, queued work and cancellation without moving
goods again. Older alpha saves are refused and preserved without migration.

World schema 67 adds `collect_material` orders for the same material
catalogue. Their source is selected through ordinary personal-goods collection
rules; schema 74 adds an optional coordinate constraint below. Food, guardian
and resource identity targets remain invalid. Progress
counts collected loads or exact item quantities, and a bounded committed-move
receipt prevents replay from duplicating pickup. Unavailable goods and full
carrying space preserve the remaining task. Older alpha saves are refused and
preserved without migration.

World schema 69 adds `repair_equipment` orders with a bounded
`TargetEquipmentKind` and progress counted in finished repairs. The equipment
work record has an optional `OrderInstructionId`, which must refer to that
actor's active repair task and match the actual lot kind. Its saved work counter
and material reservations retain their ordinary validation. Completion credits
a bounded receipt only after the real repair consumes its inputs. Cancelled or
replaced orders cannot retain live repair reservations. Replay covers partial
work, queues, cancellation and exact material costs. Older alpha saves are
refused and preserved without migration.

## House tools, estates and land claims

World schema 86 introduces crude House tool behavior. The two new
item kinds keep ownership, physical custody, condition and reservations in
ordinary inventory lots; their recipes use existing saved projects and
production jobs. Current saves resume supply, paid work and tool use without
duplicating output or wear. The separate House-tools content package keeps
the existing House identity. Older alpha checkpoints are refused and left
unchanged; there is no migration or inferred tool history.

Schema 53 saves wills with several heirs. An estate keeps its household
default beneficiaries and adds, for an accepted will, the named heirs in order,
the split, the exact quantity of each frozen lot each heir receives, and any
final words. Frozen lots retain their original building storage when present,
so escrow and Town or legacy communal inheritance cannot claim an unrelated
House as storage. Town stock retained in a private House must match a settled
estate's frozen lot ID or provenance, kind, quantity and original storage,
and its owner must be the deceased resident's recorded Town. This validates
physical custody without changing House ownership or collection permission.
The deceased archive records the Town the agent lived in. It also retains the
committed death tick and final physical position. The owner observation exposes
that position and the `death-tick` decision factor; the client derives map grave
markers from them. Marker selection uses SHA-256 parity of the saved person ID,
placement uses current legal open ground, and expiry uses the reported calendar.
No grave placement, fade state or removal event is saved. Reloading recomputes
the display from the same death records without changing checkpoints or replay. Loading
checks that only an accepted will has heirs and a division, that the division
covers every frozen lot exactly with no other lots, that a held vessel's
contents go to the vessel's heir, that person heirs are known agents and Town
heirs existing Towns, and that final words are already normalized. Final words
become private memories only at settlement, so a current-format save taken
between the will and settlement replays the same transfers and memories. Older
schemas carrying these records are refused; no migration is added.

Schema 58 adds exact land-claim coordinates to Council proposals. A passed
claim and its title record must agree on Town, tiles and settlement time;
loading refuses a claim title without approval or a passed claim without its
title. Pending, refused and cancelled claims hold no title. Proposal identity
uses ordered coordinates, and title identity is derived from the proposal ID.
Prepared-tick rollback removes votes, titles and border changes together;
continuing a current-format checkpoint preserves the vote window and cannot
apply an accepted claim twice. Older alpha schemas are refused and preserved;
no migration is provided.

Schema 59 adds household land request status, separate adult consent, the
Council proposal link and the adult roster at grant settlement. A grant's
original rights receipt must retain matching plot coverage, grant time and agreed end date;
loading rejects missing approval or consent evidence and orphaned Council
land-use proposals. Closed requests remain history without competing claims.
Prepared-tick rollback and current-format reload preserve approval progress and
commit the final grant once. Older alpha schemas are refused without migration.

Land requests retain the complete canonical identities of their known requester
and household, including native-born adults who form their own household and
whose family identity is longer than a request key.
Filing still requires an active adult or elder household member who resides in
the Town. A filed request supplies no adult consent, Council vote or land-use
grant. Request keys keep their 128-character limit; the saved shape is unchanged.

## Field, planting and collection orders

World schema 72 adds field orders and their optional `TargetCropKind`;
schema 77 adds an optional exact tile below.
`FarmFieldWork.OrderInstructionId` binds work to its actor's active field order.
Restoration validates that link, action, crop, work time and ordinary seed/tool
state; a cancelled, queued, unrelated or missing instruction cannot retain bound
work. Only finished work earns a bounded receipt and one completed field. Replay
covers partial planting, queues, released seeds, real tool wear and household
harvest ownership. This version follows the integrated guardian-placement schema 71. Older alpha saves
are refused and preserved unchanged without migration.

Tree-planting orders reuse the current order fields for a bounded count,
optional exact tile and `plant_tree`, `plant_broadleaf`, `plant_conifer` or
`plant_orchard` action. Only a newly created sapling earns one tree and its
canonical `tree:plant:planted-tree-{x}-{y}` receipt. Seed collection and travel
retain zero progress. Strict restoration rejects unsupported fields, invalid
counts and malformed receipts. Native replay covers physically harvested
seeds, personal storage and shared stock, planting travel, exact occupied
tiles, completed quantities, queues and cancellation. No schema field or
migration is added.

World schema 73 adds `repair_tool` orders using the existing
`TargetEquipmentKind` field. Validation restricts the action to supported tool
kinds and repair counts. Only a completed inventory repair earns a bounded
`repair:tool:` receipt. Replay covers partly completed quantities, preparation,
queues and cancellation without charging the materials twice. This version
follows integrated field-order schema 72. Older alpha saves are refused and preserved
unchanged without migration.

World schema 74 allows an optional `TargetPosition` on collection orders.
Coordinates keep the existing bounded integer validation; an off-map target is
a valid instruction that waits with a reason. The exact tile survives queued
work, travel, partial pickup, cancellation and reload. Runtime selection and
execution both recheck the lot's current position along with ordinary personal
collection permissions, so moved or depleted goods cannot redirect the order.
This version follows integrated tool-repair schema 73. Older alpha saves are
refused and preserved unchanged without migration.

## Land hearings and transfers

Schema 76 saves each Town's land-hearing ledger: plot and right-version notice
revisions, affected parties, public evidence and provenance, explicit responses,
actual file reads, case-only candidate consent and elections, adjudicator terms,
rulings and rehearing assessments. Notice publication does not become a receipt,
and a read records the evidence and rehearing requests actually seen. Original
grant rights remain available for the Council receipt checks; versioned bounded
adjustments reproduce current permissions without destroying that receipt.
Request-resolution pointers identify the ruling and exact tiles decided, leaving
only unresolved portions as competing claims. Loading validates the case's notice,
source, read, authority and adjustment links rather than inventing missing evidence.
Notice party snapshots remain historical; current response standing is derived
from the captured world, while each ruling retains its actual closure parties.
Readers, evidence agents, judges, notice and ruling parties, personal stakes and
live or historical case-election rosters retain complete known person identities,
including later-generation adults born in the world. Person rosters keep canonical order and uniqueness.
Unknown or malformed person references are refused;
caller-owned keys and prose retain their existing limits. This identity validation
fix changes no schema or saved field.

The same ledger saves voluntary transfer requests with immutable exact plots,
right versions, published party rosters and terms. Actual notice receipts remain
separate from individual accept or decline responses and their contemporaneous
household rosters. A completed transfer retains its final adult rosters and
exact permission-adjustment receipt. Loading validates that completion against
every required adult's informed acceptance and the unchanged original grant terms; incomplete,
declined, withdrawn or invalidated transfers cannot carry a completed adjustment.
Pending consent requirements are derived from living adults, so a new adult must
personally accept before completion. These records preserve original Council
grant receipts and Town title. A goods sale also retains its immutable price
and actual payment receipt; only the agreed goods payment changes physical
property ownership.

Voluntary-transfer filers and the person rosters in proposals, individual
responses and completed receipts retain complete known person identities,
including later-generation adults born in the world. These rosters still require canonical order and unique, known,
well-formed people. Transfer keys, permission keys and other bounded fields
keep their limits. This validation repair changes no schema or saved field.

Beneficiary household references in civic replies, transfer records and resulting
land-use rights retain complete known household identities, including households
founded by later-generation native-born adults.
Unknown or malformed household references are refused. Transfer keys, permission
keys, evidence references and other bounded reply fields keep their limits, as
does the provider response-body limit. Each affected adult still separately learns
and accepts the exact current terms before permissions change. No schema or saved
field changes.

Property requests and end, renew, amend, reclaim and grant hearing outcomes also
retain complete canonical household references. Known-household and affected-party
checks, personal agreements, inspected evidence and independent judge authority
still apply. Household references are not limited by caller-owned key lengths;
unknown, whitespace-padded or control-character references are refused. This
validation repair changes no schema or saved field.

These records share prepared-tick rollback with permissions, requests, civic
receipts and events. Current-format reload and continuation retain pending
windows, rulings and transfers without applying a decision twice. Reopening
preserves the earlier ruling and current rights until a new correction is committed. Older
alpha schemas are refused and preserved without migration.



## Further work orders

World schema 77 allows the existing bounded `TargetPosition` on field
orders. A saved running field job must be at that tile as well as matching the
order's actor, action and crop. Queueing, partial work, exact progress and seed
reservations survive pause and reload; a rejected tick cannot leave work or
progress behind. An unavailable target waits without selecting another field.
Older alpha saves are refused and preserved unchanged without migration.

World schema 80 adds `collect_food` orders using the existing
`TargetFoodKind` and optional `TargetPosition` fields. Food targets are generic
or one of berries, fruit, wild greens, cultivated greens and the seven named
prepared foods. The named-food catalogue extends the same fields and receipts;
raw grain and potatoes remain invalid food targets. They use
`food_items` for exact quantities or `collection_loads` for default pickups,
with the same bounded `collect:personal:` receipts as material collection.
Validation refuses mixed material, equipment, crop or resource targets, wrong
progress units and unearned receipts. Queues, interruptions, cancellation and
partial pickups retain their state across replay and rollback. This version
follows admission schema 78 and field-location schema 77. Older alpha
checkpoints are refused and preserved without migration.

World schema 82 adds `collect_equipment` using the existing exact
`TargetEquipmentKind` and optional source tile. Only the five supported garment
and carrying-aid kinds or 13 tool kinds are accepted. These tasks use
`equipment_items` for explicit quantities or `collection_loads` for default
pickups, with bounded personal-pickup receipts. Validation refuses mixed target
categories, unsupported equipment, wrong units and unearned progress. Physical
condition, source, queued work, remaining quantity and cancellation survive
reload and rollback. This version follows Council-election schema 81, food-collection schema 80
and lantern schema 79. Older alpha checkpoints are refused and preserved
without migration.

World schema 83 adds `store_equipment` with the existing exact
`TargetEquipmentKind`. Equipment storage permits the five garment/carry-aid
kinds and 13 tool kinds, with `equipment_items` for explicit quantities or
`storage_loads` for default tasks. It uses bounded personal-storage receipts.
Validation refuses mixed targets, locations, unsupported kinds, wrong units
and unearned progress. Partial storage, queued work, cancellation and equipment
condition survive reload and rollback. This version follows equipment-collection
schema 82, Council-election schema 81, food schema 80 and lantern schema 79.
Older alpha checkpoints are refused and preserved without migration.

World schema 85 adds `produce_item` orders with an exact recipe and
output kind, an optional requested site, and the selected building, project
start and production-job identities. A production job's optional
`OrderInstructionId` records which order started it; matching a recipe and
start time alone cannot adopt an unrelated job. Progress counts finished output
items or whole batches. Loading validates recipe yields, cross-record ownership and
job references, pause state and completion receipts together. Queued work,
partial progress, cancellation and survival pauses retain their state through
reload and replay. This version follows marriage schema 84 and equipment-storage schema 83; older alpha checkpoints are refused and preserved
without migration.

World schema 87 adds exact personal-goods and borrowed-return orders.
`TargetItemKind` is separate from gathering and repair subjects. Storage and
return tasks save the selected House identity, household owner and position;
returns also save their source lot. The destination binding is complete or absent,
and incompatible actions, subjects, quantities and receipts are refused.
Finite progress, queued work and cancellation survive reload without switching
House or changing ownership. This version follows crude House tools schema 86 and production-order schema 85; older alpha checkpoints are refused and preserved
without migration.

World schema 88 adds `deliver_stock` with exact purpose, item and
building kinds. Optional `DeliveryRoute`, `DeliveryLotId` and `DeliveryQuantity`
retain the selected native route and active shipment. The destination reuses
the complete building/owner/position binding; its owner may be the receiving
household or Town according to the route. The active lot and quantity are
paired, and final-delivery receipts use a distinct bounded identity. Loading
validates the action's subject, route and binding together. Queued tasks,
partially completed quantities, carried shipments and cancellation survive
reload without crediting earlier pickup or redirecting goods. This version follows
custody-order schema 87; older alpha
checkpoints are refused and preserved without migration.

World schema 89 adds construction and expansion order bindings.
Construction keeps its exact definition, household, site, project start and
unique building identity, with retained completion evidence for the actual
paid placement. Expansion keeps the original building identity, owner,
position and revision, the selected target anchor and footprint, and its
order-owned expansion job. Validation checks these links rather than
inferring completion from a building that happens to exist. Completed work
survives later building removal; unfinished work, cancellation and survival
pauses remain replayable. This version follows delivery-order schema 88. Older alpha checkpoints are
refused and preserved without migration.

World schema 90 adds shelter target bindings and completion records.
Building targets pin identity, definition, owner, anchor and placement time;
natural cover pins its actual destination. The same building binding can name
a Town Hall for a homeless own-Town resident's storm refuge; its fields and
the current schema are unchanged. Live refuge permission is checked when
work continues, while a completed arrival remains historical. Shelter completion
records the arrival time and position. Fire completion also references the unique actual
completed one-wood fuel reservation. Validation checks action-specific shapes
and payment evidence without requiring historical cover, permission or a fire
to remain available forever. Unfinished targets are revalidated when work
continues. This version follows building-order schema 89; older alpha checkpoints are
refused and preserved without migration.

## Non-land hearings and remedies

Schema 91 adds the required non-land case ledger, protected non-land mandate
scope and retained-term extension receipts. It saves exact allegations,
historical conduct and law context, actual observation and communication
sources, notice and response history, independent adjudicator authority,
findings, voluntary offers and each contributor's consent. Completion effects
refer to native physical receipts rather than mutable stock or the event log.
Loading rejects missing, duplicate, future or inconsistent evidence and
authority links. Historical records remain meaningful after law changes,
departure, death, consumption or removal of the physical target. Prepared-tick
rollback includes these ledgers alongside the actual goods or work. Earlier
alpha checkpoints are refused and preserved; no inferred case history or
migration is added.

Accepted remedies retain the nearest earlier agreement through their exact
prior-offer chain, including successive counteroffers during renegotiation.
The original agreement's own offer becomes superseded; intermediate countered
offers keep their history. Strict loading rejects a missing or unrelated
predecessor instead of reconstructing obligations or moving earlier physical
receipts into the replacement. Previously written accepted renegotiations with
a lost predecessor are refused and preserved. That repair changes no saved
fields or event shapes.

Schema 97 adds `CarriedAvailableQuantityBefore` to native public-service
receipts. It snapshots the usable personal stock of the donated kind before
transfer, across eligible carried lots and excluding reservations. Source-lot
quantity and availability remain separate evidence of the actual transfer.
Loading requires the recorded carried total to cover that source's available
quantity and leave the four-unit reserve after donation; other receipt kinds
cannot carry this field. The total participates in the receipt digest and
retained completion effect. No historical stock is inferred or backfilled.
Earlier alpha checkpoints are refused and preserved without migration.
