---
title: Towns and boats
type: persistence-reference
status: active
updated: 2026-11-14
---

# Towns and boats

Town projects, founding, abandonment and communal boats.

## Council-approved Town projects

Schema 68 adds a required, non-null `Projects` list to each Town and an optional
typed `Project` payload to a civic proposal. An empty list records that no
construction has been approved. The first supported payload is
`TownHallContent.Hall3x4()`: its exact definition identity, normalized name,
site, south doorway and 24 wood / 12 stone budget are bound together. The
material amounts and ten-unit work target are provisional gameplay values.
No additional Council or vote authority is saved: a shared job references the
original passed proposal in that Town's full canonical governance ledger.

The records retain the approval tick, stage, work done, last transition, blocker,
completed building ID and every actual load's contributor, source and resulting
lot IDs, quantity, pickup/delivery times, exact reservation and release history.
Each material receipt keeps the identity derived from its exact delivery. The
normal owner removal command records a completed Hall's removal time while
retaining the consumed receipts; a missing Hall without that record is refused.
The supplied query counts usable Town-owned stock reserved on the approved
ground site, or that project's completed consumption receipts. Carried promises,
expired or released claims and cumulative deliveries whose goods are no longer
there do not stand in for paid materials. Private harvesting and an accepted
personal donation remain separate transitions.

Validation for these records must reject a missing or duplicate approval
binding, unsupported or altered payload, invalid times/stages/work, duplicate
load identities, incorrect custody or quantities, mismatched material claims,
unpaid completion and a completed building that does not match the approved
Town, definition, footprint and doorway. Legal site checks use actual Town title
and existing household rights and pending requests, independently of the
visible border. A blocked live site releases unused material claims; the goods
retain their actual location and Town owner rather than being recreated or
returned by a counter. Only a pending household land request keeps a project
blocked; any other site failure saves it as `cancelled` with its reason and
released claims, and a cancelled project no longer protects its site. A
completed Hall remains bound to its original paid receipt history.
The owner observation projects the retained `RemovedTick` to the client without
changing the saved stage or receipts. Towns displays a removed asset as retained
construction history, and historical project event names resolve from complete
retained project IDs even when their actor is absent from the observation.

Current-format restore and replay must keep partial multi-load supply, consumed
receipts, shared work and civic knowledge without duplicating approval, stock,
donations or the Hall. Discarding a prepared tick must leave no transfer,
reservation, progress, building or event. Late replies must recheck the current
project, actor authority and physical goods. Approval in the owner observation
comes from the complete proposal ledger even after it leaves the eight recent
results. `TownProjectRuntimeTests` and `TownProjectSaveValidationTests` cover
generated-world gathering and donation, genuine Warehouse loads, intermediate
restore/replay, discarded prepared ticks, stale votes, retained removal and
coherently altered receipt/source references. Earlier
alpha schemas, including 53, are refused visibly and preserved without migration.

## Founding another Town

The personal `found` choice records an additional `TownRuntimeState`, its first
layout title, retained household footprint use rights and the founder's Town
care group using existing saved fields. The schema is unchanged. Existing
buildings keep their household owner and gain the new Town assignment; stock,
bodies and care do not move. Council and government initialize from the new
adult roster. The `town_founded` event uses a pipe-separated Town ID, founder,
previous Town and care-group count; initial First Town events keep their format.

The choice binds the current physical site and is rechecked when applied.
Competing or delayed replies cannot claim another Town's land, move membership
twice or supply free property. [TownFoundingTests.cs](../../../tests/ClankerWorld.Tests/Simulation/World/Towns/TownFoundingTests.cs),
part of `TownMembershipTests`, covers generated-world
founding, care-group membership, existing owned and foreign Houses and fields, claimed
land, competing and delayed model choices, exact replay/reload and discarded
prepared ticks. Routine Windows playtesting remains pending.

## Abandoned Towns and physical salvage

Abandonment is derived from a founded Town's empty recorded living-resident
roster. Schema 99 requires a nullable `AbandonedSinceTick` on each Town: the
latest empty-roster transition records its world tick, and revival clears it.
It survives count-based event compaction, so building decay starts after a
full season rather than when its event leaves hot history. An unknown age
does not imply a full season. Loading refuses a future or pre-founding tick,
or an abandonment tick on a lived-in Town; older alpha saves are refused and
preserved without migration. Children, travelers and residents
without a home still count. A last departure or death records `town_abandoned`;
the first new resident records `town_revived`. The Town record, laws, civic
history, buildings, land title, household use rights and infrastructure stay
intact.

The personal `resettle` choice requires a living adult physically inside the
abandoned Town. It moves that adult's Town care group under the one-Town rule,
without moving bodies or changing household membership, care or property.
Governance refreshes from the new living adult roster; prior offices are not
restored. `town_resettled` records the deliberate change. Later arrivals use
ordinary admission.

Public salvage operates on exact communal lots on the ground or in the
recorded Town's Warehouse. Pickup rechecks current abandonment, physical
distance, reservations and carrying room. Container families move together;
salvaged handcarts keep their ground location until pulled. Transfers retain
condition, provenance and stock history and record `town_stock_salvaged`.
Revival neither confiscates prior pickups nor permits a stale public choice
to collect more stock.

[TownAbandonmentTests.cs](../../../tests/ClankerWorld.Tests/Simulation/World/Towns/TownAbandonmentTests.cs),
also part of `TownMembershipTests`, exercises strict current-format roundtrips, identical
live/restored continuations before and after membership or stock transfer,
discarded prepared ticks, competing pickups and a delayed model reply after
revival. No migration or inferred historical abandonment is added.

## Ports and communal boats

World schema 92 adds required, non-null `BoatTransport` state. Empty
boat and request lists record that no asset or trip exists. Each boat binds a
Town, one completed paid boat project, its current water position and either a
real mooring or one saved journey. Every completed boat project must retain its
physical asset, completed asset ID and exact consumed wood, rope and refined-iron
receipts. A missing boat or invented removal record is refused.

Journeys retain one passenger, origin and destination Port IDs, reserved dock,
cardinal water path, current index, start and next movement ticks, blocked-arrival
start time and return direction. Requests retain sequence, traveler, owning Town,
Ports, status, boat and settlement time. Validation rejects mismatched payment,
owner, passenger or request, duplicate physical or incoming dock claims, invalid
water steps and an active passenger separated from the boat. Waiting requests
hold no boat reservation; terminal history remains saved.

Schema 108 adds `OrderTask.BoatTravel`, containing the stable
destination Port ID and latest request ID, and optional `BoatTripRequest.OrderInstructionId`.
Validation binds each ordered request to the same instruction, passenger and
destination, disallows overlapping retries, and requires the latest actual
`arrived` request for a finished order. Unknown Ports, changed destination
tiles and invented progress are refused; a removed Port remains identifiable
through its paid construction record. Cancelled orders may retain an underway
or settled journey, but never an unused waiting request. Native arrival
receipts survive event and request-history compaction: each saved order’s latest
bound request stays live, while older retries and unrelated closed requests can
be archived. Events or retired sequence ranges alone cannot prove completion.
Schema 103 requires non-null `RetiredRequestRanges`, empty until the first
retirement. When more than 40 requests are closed, checkpoint compaction keeps
all waiting and underway requests, the latest 40 closed requests by sequence,
and each saved boat order’s latest bound request,
matching the existing owner view. The older full records go into
`ClosedBoatRequests` in the same digest-addressed history segment as event
prefixes. The segment is fsync-written before the smaller checkpoint is
published or installed in the live runtime. Preparing a plan or failing either
write leaves the live queue unchanged. The ordinary server saves after each
advanced tick; a runtime used without persistence keeps its records until saved.

Retired sequence ranges are ordered, disjoint and merged. They account for each
issued sequence exactly once together with the live requests, while preserving
gaps for older waiting or underway travelers. Retirement never alters journey
bindings, paid boat/Port records, cargo or ownership. Later closing an older
trip archives its exact final record and merges adjacent ranges. A checkpoint
with retired requests requires an archive head. Loading and saved-world
compatibility checks verify that its reachable segments contain exactly the
retired sequences, each once with a closed outcome. Missing, unrelated, corrupt
or inconsistent archives refuse loading and preserve the checkpoint. Strict reload,
paired continuation, refused prepared ticks, failed archive/checkpoint writes,
arrival, blocked return and passenger death are covered by native checks.

Council boat permission is an explicit typed field on its proposal draft and
adopted law version. The exact known visitor or standing grant, canonical rule,
original vote and adoption/repeal history must agree. Matching freeform text
cannot supply the field or permission. Generic amendments cannot turn a grant
into a different authorization. Deceased profiles retain their actual boat ID;
aboard estate cargo retains unique ground roots and follows the boat until landing.

Capture, prepared-tick commit, codec restore and owner projections retain these
records together. Native checks build both Ports and the boat through personal
choices, real votes, supply and work, then exercise travel, mid-voyage replay,
queues, cancellation, permissions, blocked arrival and damaged current-format
records. Earlier alpha checkpoints are refused visibly and preserved without
migration.
