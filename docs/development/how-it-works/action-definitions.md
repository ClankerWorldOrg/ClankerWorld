---
title: Action definitions
type: architecture
status: active
updated: 2026-11-14
---

# Action definitions

This page describes typed action definitions and their checks.

Typed action definitions bind a choice codec, plan, capability policy, shared
check and application to those ordered rows. Waiting uses one definition in
both phases. Offering checks its new plan; admission and application check the
retained plan through the same definition. Each action family in the ordered
tables now owns its plan check; the type index resolves retained plans back to
that definition. Legacy prefix sinks only consume unsupported spellings after
the typed rows have had their turn.

Civic definitions share one application method for retained plans sent to
`TownSystem.ApplyTownCivicCandidate`. Council ballots retain their selected
ballot argument; food policy and physical work keep their own application paths.

Reply payloads that supply names, terms, evidence or tiles bind those fields
onto the retained plan at admission. Admission checks all replies against the
prepared world, then application checks each plan against the changes made by
earlier replies. A refusal uses the existing `choice_refused` event and a plain
reason. Native project-input steps also plan their selected lot, quantity and
route once before checking and applying it.

Offer limits count checked choices. Tool-request placement and visiting,
project donations and public-report filings apply their limits after the
definition accepts the plan. Invitation, ornament, animal, Port and Town-project
offers likewise filter eligibility before their final offer limit. Bounds on
history, observation context, land-tile payloads and layout searches retain
their separate purposes. Continuing work retains its existing job, lesson,
repair or trip identity and reservations. Owner orders keep their typed task
and existing step executor; their command age policy uses the shared
capability check.

Child social and household-helping actions also use one typed definition. Their
existing selected person, resource or load reaches the same native effect after
the definition rechecks it; legacy entry points share that check and effect.
Town Warehouse surplus and abandoned-stock salvage each own a definition with
their existing capability policy. Both retain the resolved Town, stock, family
and load in the plan; hashed salvage requests keep their original identity.
Household recovery, hauling and spare-food storage also own separate
definitions. Their existing urgency policies and selected physical steps share
one codec, plan check and native effect with legacy entry points.
Field work, tree planting and farm-stock hauling bind their existing shared
capability policies and plan checks to typed definitions. Field and tree rows
call the Farming system's native effects directly after those checks.
Food consumption, shared-food collection, food gathering, fire and warmth also
bind their codecs and existing capability policies to typed definitions. Their
offer, admission and apply paths check the same selected serving, source,
fuel or protection; legacy entry points share the native effects.
Garments, carry aids, equipment repairs and each household-vessel action also
own definitions. Their selected item, repair inputs or vessel family reaches
the same native effect after its shared check; each vessel keeps its existing
urgency policy.
Smith delivery and gathering, tool collection and repair, rare mining,
workstation supplies and Store stocking also own typed definitions. Their
selected source, tool, stock, family and load pass the same check before the
native effect; Store stocking has its own row beside the other shop actions.
Recipe projects also own a typed definition. The capability policy is resolved
from the retained recipe, preserving the garment exception during urgent cold.
A codec-matched recipe row precedes the construction row; invalid legacy
targets retain their existing project rejection through the fallback handler.
Construction and expansion also own typed definitions. Construction resolves
its warmth policy from the retained design and keeps legacy site-less requests
on their existing fallback path. Both retain selected sites, footprints and
material steps through the same check before native application.
Household building preparation and project assistance also own typed
definitions. They keep the requested material, selected recipient, source,
tool and load through their shared check and native effect; legacy callers
use the same eligibility and effect.

Choice sources retain their original adult, child and urgent-needs groups,
including both Market calls and their different gates. Exclusive boat and
conversation choices, order restriction and refused-action filtering stay at
their existing points. Construction sources share the same per-call tool,
warehouse and rejected-site context. Action routes retain ordinal prefixes,
exact identities and their order on either side of age validation, repair
cancellation and continued field work. Registration tests pin both lists against
the original coordinator order; native equivalence checks compare the resulting
saved bytes, events and observation digests.

Shop buying and responses, personal barter, and Market stock, exchange and
stall occupancy own their codecs, planners, capability policies, checks and
native application in ordered action rows. Market application retains its
fresh personal admission guards and call order. Policies follow the retained
stock kind or response, preserving urgent-food collection and warm-up exits.

Ornament choices and making room for food own their codecs, planners,
capability policies, checks and native application in ordered action rows.
Ornaments remain limited to fresh personal admission; their walking
continuations cannot wear or give items. Food cargo keeps its selected stock
families, quantities and orchard reservations through application.

Conversation choices own their typed codec, planner, capability policy, check
and native application in the ordered action row. ConversationSystem retains
the shared preparation check and native writers; successful conversation
application keeps the existing cancellation of equipment repair.

Tool request consent, visits, commission work and finished-tool collection own
their typed codecs, planners, capability policies, checks and native application
in ordered action rows. Consent remains at fresh personal admission; the work
step keeps its existing fresh-choice guard. Request receipts, shops, jobs and
collection quotes stay bound through application.

Animal work, products, travel, supply, trips and agreements own their codecs,
planners, capability policies, checks and native application in ordered action
rows. AnimalSystem keeps the planners, shared checks and native writers.
Agreements apply at fresh personal admission after the existing provider and
order checks; missing supply-trip loads keep their existing cleanup.

Boat requests and waiting, and handcart control, materials, repair, cargo and
travel own their typed codecs, planners, capability policies, checks and native
application in ordered action rows. Their retained plans keep the selected
ports, boats, carts, stock families and quantities; boarding and hitching keep
their existing mounted-actor restrictions.

Personal collection, storage, borrowed-item returns and house-guest permissions
own their typed codecs, planners, capability policies, checks and native
application in ordered action rows. The retained custody mode or invitation
source keeps the existing limits on boat travel; automatic borrowed-item
returns keep their age exception.

Housing, learning, departure, replacement care, relocation and household work
own their typed codecs, planners, capability policies, checks and native
application in ordered action rows. Their retained plans keep the selected
people, Houses, lessons, departure allowances and paused jobs.

Medical supply, treatment and consent own their typed codecs, planners,
capability policies, checks and native application in ordered action rows.
Consent applies only in the fresh-admission phase after the provider and
admission checks; continuing intentions still cannot change permission. The
supply definition checks both retained pickup and return plan types.

Partnership, parenthood and dependent care and travel own their codecs,
planners, capability policies, checks and native application in the ordered
action rows. Policies follow the retained care or consent step. Guardian travel
keeps its placement refresh before planning a legacy request; ending care runs
before age validation only while a guardian placement task exists.

Exploration owns its typed codec, planner, capability policy, check and native
application in the ordered action rows. Its policy follows the retained plan
for automatic travel, preserving the different limits on model choices.

Knowledge writing, reading, sharing and artifact storage own their codecs,
planners, capability policies, checks and native application in the ordered
action rows. Their retained plans keep the selected contents, supplies,
recipient and artifact custody through acceptance and application.

Recipe choices retain their selected workstation, owner, available input lots
and production knife. A running project keeps that selection; a restored
project binds its next step once from current state. Native job creation
reserves the selected lot quantities with the existing reservation identities.
Inputs acquired later are selected once before that reservation step.
Recipe plan references have a live lifetime. Tick preparation copies entries
for the same project and order into the trial runtime; successful commit
copies those entries back without replacing the live cache or saving it.

Household construction preparation retains the selected building need, source,
tool, harvest quantity and household return destination. The same check
rechecks the selected building's reachable work inputs, load and route before
the native harvest; changed work inputs or tree seed yield refuse that stale
selection.
Project assistance retains the requesting project, recipient and exact personal
lot or material harvest. Its shared contribution query keeps physical stock
availability and inventory order; transfers use that selected lot and quantity.
Its hold-access check leaves stock promised to another task with that task.
Construction preparation and assistance share one harvest and tool check.
Construction choices retain their selected design, site, building identity,
ownership and available material lots. Their shared check also rechecks the
selected building's reachable work inputs. Matching project/order plans are copied
through tick preparation and commit without entering saves. Material payment
uses the selected quantities and existing reservation identities; supplies
acquired later are selected once when the payment inputs become available.
A builder whose selected materials become unavailable gets fresh construction
choices. The paused goal still prevents another member from duplicating that kind.
Expansion choices retain the selected building's placement identity, entrance,
revision, ownership, target position and footprint. Available payment lots and
quantities are retained
through native job reservation; later deliveries are selected by their own
physical step. Reservation identities and completion retain their native order.
Each expansion material step retains its delivery or pickup lot, quantity and
physical destination, or its harvest source and tool. The shared expansion
check validates that selection before the native transfer or harvest applies.
Private projects plan each input acquisition step before applying it. Delivery
and Warehouse pickup retain their lot, quantity and destination; gathering
retains its source and carried tool, or the shared tool pickup needed first.
These steps reuse the contribution, equipment pickup and harvest checks while
keeping the native waiting stages and transfer events.
House guest choices retain the selected House, household and named guest.
The same check validates membership, invitation state and the proximity needed
to invite; revocations retain their existing remote behavior. Invitation caps
follow filtering. Direct access requests keep their idempotent results.
The original placement tick distinguishes a rebuilt House. Expanding the same
House may move its anchor without changing its guest permission.
Personal collection, storage and borrowed returns retain their selected lot,
quantity, custody and House or pickup position. Stored and returned goods also
retain the House's definition and placement tick, so rebuilding the same ID
requires a fresh choice. Vessel plans retain their
contents too. Model choices and native custody operations share the same check
and mover; explicit orders keep their extra gear and delivery protections and
interrupt ordinary field work before moving its tool.
Exploration plans retain the original trail and purpose and select only the
next visible neighbor or a recorded return waypoint. The same check validates
that origin, trail, need and step; application uses the selected target while
keeping native start, discovery, completion and interrupted-movement events.
Waiting choices carry a server-only plan through the same capability check.
Their native idle or age-rejection outcome stays unchanged, including fallback
and boat text. Permitted waiting still follows existing repair cancellation
and farm-work continuation before it records an idle event.
