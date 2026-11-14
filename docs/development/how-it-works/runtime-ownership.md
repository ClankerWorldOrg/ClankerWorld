---
title: Runtime system ownership
type: architecture
status: active
updated: 2026-11-14
---

# Runtime system ownership

This page describes runtime system ownership and validation.

## Runtime systems

`Runtime/WorldRuntime.AgentRegistrations.cs` binds choice sources and
action routes in explicit, read-only ordered tables. Animal, farming, civic and
conversation adapters register their entries beside their existing private
forwarders. Every binding receives the current coordinator per call; no entry
captures a world or a prepared system. Remaining areas bind their current rules
in the same tables until their own extraction. Follow-up work is tracked in
[#1826](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1826).

AnimalSystem checks local animal identities, permissions, custody pairs,
pregnancy, taming, product and payment shapes before test mutations publish.
The save check reuses that contract and keeps society, inventory, map and
calendar links. Stale offers and trips still retire at their existing cleanup
point; a temporary external link is not treated as a malformed animal record.
Wild-herd population limits apply after advancement and during save validation,
so a birth consumes its reserved place in the existing mutation/event order.

FarmingSystem checks field ordering, one work site per worker, crop stages and
work progress at construction and before test mutations publish. Save validation
reuses those local checks and keeps fertility, ground occupancy, household and
worker authority, calendar, tools and actual seed-reservation links. Forked
field state and derived fertility keep their existing lifetime.

TownSystem checks local Town, founder, food-policy and land-record contracts
before test publication. It keeps live insertion order and canonicalizes only
at the existing checkpoint boundary. Save validation reuses those checks and
keeps population, map, clock and land-authority links. Multi-Town roster updates
finish before global membership consistency is checked. Council forms, proposals,
elections, notice order and rooted civic relays also have one local contract
checked before Town publication in tests. The boundary keeps population,
eligibility and calendar links. Admission outcomes, household land-request consent and
settlement shapes, and project stages and material deliveries use local
TownSystem contracts too. Publication permits separately staged Council,
population and inventory changes; the save boundary checks their completed
approvals, membership, physical receipts and sites. Main-round support and a recorded draw remain
historical when candidates withdraw; current candidate and winner eligibility
still updates. Government and law record contracts run at publication too. A
deciding government-change vote remains valid until the existing advance step
settles it; that completed step checks its resulting status and Council authority
before returning. The save boundary keeps resident, Council-decision, land-title
and calendar links.

Land and civil hearing ledgers check their file completeness, revision order,
retained evidence, responses, consent history and internal read provenance before
test publication. Their population, Council-notice, map and current-time links
remain at the boundary. Case election rounds, local judge mandates, land rulings,
civil findings and reopening records also check before publication. Save checks
retain current population, historical office authority, law and calendar links.
Findings are checked before reopening grounds follow their revision references.
Remedy offers, exact personal consent, agreement history and credited physical
quantities also have local contracts. Current-time expiry and overdue status,
actual notice knowledge and physical-source links stay at their existing boundary.
Property snapshots and consent, voluntary-transfer terms and payment receipts,
and land-adjustment identities and internal provenance also check locally. Case
identities are unique before adjustment lookups. Map
geometry, live rights replay, population, actual notices and inventory events
remain external links.

`Production/ProductionSystem.cs` owns production jobs, their sequence, tool
requests and retained recipe plans. Named operations start, pause, resume,
finish and cancel work, including household departures and owner-order steps.
Request retirement removes its job links in the same transition. The old
checkpoint still combines these records with construction data; its production
view is read-only. Tick preparation copies live recipe plans, and commit
transfers the production system with the saved state. Local production
invariants run before mutations publish in tests. Production completion uses
`IProductionWorld` per call for current content, society, buildings and named
inventory, learning, order-credit and event operations.
Recipe planning and checking read `IRecipeWorld`; production admission uses
`IProductionStartWorld` to reserve the selected inputs and bind the resulting
job. Local job validation has one implementation in the system, used both
before test mutations publish and by checkpoint validation. The remaining
production save checks compare jobs with current recipes, buildings and time. Tool-request identity,
status, bounded text and internal links are checked by ProductionSystem before
test publication. Retained history bounds run after request maintenance prunes
its terminal records, preserving the original retirement point; checkpoint
validation reuses these checks and retains external authority and time links.

`Construction/ConstructionSystem.cs` owns construction plans and admission,
expansion plans and lifecycle. Its `BuildingStore` is the only writer of
placements, ownership, entrances, expansion records, guest access and paid
construction receipts. Other systems request named operations for these
records. Civic setup and founding use named building operations rather than
replacing the combined simulation snapshot. The existing checkpoint combines
the building store with production state; trusted preparation retains immutable
records and copies live construction plans. Local record checks run before
test mutations publish and at the save boundary, where the remaining checks
compare records with content, map, society, inventory, orders and time.

`Community/CommunitySystem.cs` owns personal knowledge records and coordinates
lessons, skills, practice, household admission, departure, relocation, paused
household work, belongings and trust. Its per-call ports read the current world
and request named operations from the systems that own other records. Local
knowledge, lesson and housing checks run before test mutations publish; save
validation uses the same checks and retains links to people, households and time.
`Agents/AgentStore.cs` is the only writer of living and deceased physical-agent
records. Named component operations preserve captured input records and detached
preparations until their original commit points. Creation, death, equipment
rollback and trusted tick preparation keep existing record and insertion order.
The semantic writer scan rejects physical-agent writes outside this store.
Local project, equipment, survival, exploration, departure and child-model
contracts also run before agent publication in tests. Production, community
and cognition own their component contracts; AgentStore owns equipment and
survival values. Save checks reuse those contracts and retain clock, map,
household, population and inventory links. Detached preparations still wait
for their original publication point.

`Family/FamilySystem.cs` owns saved population continuity and family rules for
partnerships, parenthood and births, dependent care and guardian travel, medical
consent and treatment, and child social/helping actions. Its per-call ports use
current world queries and named society, inventory and other-system operations.
Local family component checks run at the physical-agent store boundary in tests.
`FamilyWillService` owns live will requests and cancellation reasons; it stays
with the live coordinator when a prepared tick commits. `FamilyTurnState` owns
scratch guardian-action marks and resets each tick. Saved continuity alone
transfers with prepared state. Will reply validation handles declared contract
failures without hiding other failures in an empty catch-all.

`Markets/MarketSystem.cs` owns shop ledgers, quotes and demand, personal barter,
public-stall borrowing, stock, exchanges, construction/removal and history
retention. The saved town system contains this system, so its ledger transfers
with prepared saved state. Named town operations publish market records while
retaining the existing town transition. Narrow per-call ports provide current
queries and named inventory operations for custody, holds, accepted payments,
receipts and retirement. Local market checks run before test writes publish;
source guards limit record writers to the market system and its town projection.
Existing action definitions and retained plans keep their ordering and checks.

`Transport/TransportSystem.cs` owns handcart attachments, boats and their request
queue, roads, bridges and traffic evidence. It also owns retained transport-plan
checks, journey and cart lifecycles, road generation and detached boat-history
retention. Per-call boat, handcart and road ports read current world views and
request named inventory, building, order-credit and event operations. Saved
transport transfers together on commit; the road-deck view is rebuilt at the
same original points. Local identity, attachment and progress checks run before
test mutations publish and are reused by save validation. Existing geometry
fixtures target the system through their existing reflection sites.

`Orders/OrderSystem.cs` owns owner instruction submission, cancellation, task
selection, execution, progress, interruption and native-work bindings. Its
`OrderStore` is the only writer of instruction, task, binding and receipt records
and completion markers. Purpose-named operations preserve captured input records
and detached preparations until their original commits. Separate per-call ports
cover selection, survival, lifecycle, delivery and each task family; inventory,
production, building, boat and physical-agent changes use named owner commands.
Public ingress keeps its existing validation and gate ordering. Saved order
state transfers as one system; store-local checks run before test writes publish
and at save ingress, while save validation retains cross-system links.

`Cognition/CognitionSystem.cs` owns live model, conversation and life-moment
requests, their outcomes, provider settings and memory-compaction telemetry.
The coordinator holds stable read-only request views for host scheduling; only
the system mutates their backing queues. These live objects stay with the host
when prepared saved state commits. Per-call ports handle observation creation,
reply admission, continued work, candidate selection, memories and identity;
provider entry keeps the original runtime-gate guard. Physical cognitive
components still publish through AgentStore, which runs system-local model,
life-moment and thought checks in tests. Save validation reuses those checks
and keeps time/epoch links. Identity reply validation handles declared contract
failures; unexpected validation failures surface instead of an empty catch-all.

MarketSystem checks local ledger IDs, stall slots, borrowing lifetimes, receipt
sequences and internal receipt/trade references before test publication. Save
validation reuses these checks and keeps inventory, building, society, map and
clock links. Every ledger replacement uses the same check.

`Kernel/InventoryStore` is the only source writer of inventory lots,
reservations, barter offers/proposals, holds, storage deltas, events and
checkpoints. Named preparations preserve captured immutable records and remain
detached until their existing publication point. InventoryAuthority retains its
public command API and delegates publication and record/link validation to the
store. Custody holds, canonical ordering, history receipts and codec copies keep
the same evaluation and commit order. A semantic guard covers both record
construction and updates. `World/Inventory/InventorySystem` owns the runtime
inventory commands. Its named factories capture a checked operation without
running it; the runtime publishes the command inside the existing society
gate. Four system ports accept the closed command type instead of arbitrary
inventory callbacks. Capacity checks, market holds, storage receipts and
equipment rollback keep their original transaction order. Per-call query ports
provide the operation-specific world reads; returned IDs are recorded at the
original assignment point.
