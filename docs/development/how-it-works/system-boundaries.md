---
title: System boundaries and runtime contracts
type: architecture
status: active
updated: 2026-11-14
---

# System boundaries and runtime contracts

This page describes system interfaces, caches and runtime contracts.

Fertility uses `Derived` keyed by the map snapshot, with a cache shared by
prepared farming systems for the same world seed. A changed map gets a new
value. The verification switch rebuilds and compares its terrain and rainfall
inputs on cache hits. Positions, reservations and planting obstacles remain
current-world queries; no fertility or cache data enters saves or digests.

`Environment/EnvironmentSystem.Physical.cs` owns the immutable `WorldSystemsState`
snapshot, including ecology, chunks and weather. Named writes handle harvesting,
animal forage, tree planting and starting resources. Weather advances at its
existing tick phase, and the runtime commits the system with one assignment.
Map and declarative content remain owned by the coordinator. Production and
building records come from their systems. Trusted tick preparation shares the
immutable map, content, environment and combined simulation view by reference
until a write replaces one; external loads retain full validation and map
regeneration checks. Restore initializes saved services directly instead of
creating founders, physical state or genesis events first.

Environment operations receive `IEnvironmentWorld` or
`IEnvironmentAgentWorld` per call. A live coordinator adapter supplies the
current map, weather anchor, resource availability, resident state, inventory,
order cancellation, death recording and event sink. The system retains no
runtime reference, and its adapter stays on the committed coordinator. Existing
private environment and exposure methods remain forwarders.

`Civic/TownSystem.cs` owns the food stewardship, complete Town records,
Town land titles, household land-use rights and requests, and founder setup in
one immutable `CivicState`. Membership, abandonment, admission, first-Town setup,
land grants, hearings, transfers and sales run in this system alongside Town
governance, government and nonviolent law. Every write replaces its affected
read-only collection and snapshot; one central Town write records abandonment
and revival. Checkpoint capture reads these records directly. Trusted tick
preparation shares the captured civic snapshot until a write replaces it, and
commit transfers the system with one assignment. External loads keep their full
validation and canonical ordering. The admission loop's recursion guard is live
system state and never enters a checkpoint.

Civic operations receive the current world through an adapter on each call,
and the adapter stays on the committed coordinator. Founder placement and setup
keep their public coordinator locks. Add Agent validates its placement and applies
model configuration under the same runtime gate before inserting the resident,
so a tick cannot invalidate the placement between those steps. Existing private
methods remain forwarders.
Town writes need only the clock and event sink; land consent needs those services
and current household adults through `ICivicLandConsentWorld`. Other operations
query current physical state or request inventory, society, road, construction
and property transitions through `ITownWorld`. No system retains the adapter
or a runtime reference.
The civic-history index uses `Derived` with the immutable governance snapshot
as its weak key. Its notice, knowledge and recent-receipt lookups survive tick
copies; replacing governance invalidates the lookup. Physical needs, positions,
inventory availability and remedy admission remain current-world queries.

A system owns its saved state, its rules and a small `I…World` interface for
the services it needs from other areas. Saved state is an immutable record
replaced on writes; a hot mutable collection instead replaces a `Version`
token on every write. Pending model calls and other live work stay outside
saved state. The runtime keeps the existing tick phases and calls each system
at the point where its code runs today. Moved private methods remain one-line
forwarders while callers and reflection-based tests still need them. A tick
commit assigns each extracted system rather than copying its individual fields.

Conversation operations receive `IConversationWorld` for each call. A private
runtime adapter supplies the current society snapshot, resident positions and
needs, calendar, boat presence, provider eligibility, talk-order protection,
trust, name/memory transactions and the event sink. The system retains no
runtime reference. Its adapter stays live on the committed coordinator, so a
tick commit cannot bind future effects to the disposable prepared runtime.
Provider invocation, pending turns, request Guids and provider-route checks
stay in the runtime. Admission checks the route at the original point before
publishing dialogue, listener beliefs or marriage effects.

`Runtime/WorldEventLog.cs` owns the ordered world events, next event ID, history
floor and archive head. Its hot list is exposed read-only and every append
replaces its `Version` token. Restore copies the saved events into a separate
log; committing a prepared tick transfers the log with one assignment. Event
positions still use an explicit point first, then the longest matching living
or deceased agent ID prefix. The sink receives the current world per call
and retains no coordinator reference.

`Runtime/AgentQueries.cs` supplies resident age, household, interaction,
stock-position and urgent-need rules. Its live adapter supplies the current
society, positions, distance, building storage and warmth calculation. The
adapter stays on the coordinator across tick commits; physical queries are
never cached. Ground occupancy uses the named layers described above; the
goods query is described below. Snapshot-keyed
derived values must be pure, unsaved and absent from digests. Immutable
snapshots can pass through the trusted tick copy by reference; replacing a
snapshot invalidates its derived values without letting a discarded tick
change committed caches. Positions, reservations and occupancy remain live
query overlays.

The shared `Derived.cs` helper weakly caches a pure value by snapshot identity.
Cache misses pass the existing factory directly to the BCL cache; verified hits
still recompute and compare the complete derived value before returning it.
`SocietyAgentIndex` uses the immutable agent-list identity for an
ordinal ID dictionary behind the existing `SocietyCheckpoint.GetAgent`
method. Inventory-only snapshot changes can reuse the index; identity, age,
name or household writes replace its key. Missing and duplicate IDs retain
the linear query's `Single` behavior, and malformed inputs still pass through
the existing validation. Its fallback runs separately from valid lookups, and
verification compares an immutable copy of the complete agent references.
The index changes no checkpoint fields or ordering.
Conversation
lookup indexes each participant's earliest open session, preserving created-tick
and ID ordering. A separate derived checkpoint view keeps the original ID-sorted
conversations, budgets and marriage records. Trusted tick preparation shares
that immutable view through a separate system instance; writes on a refused
proposal cannot alter the committed snapshot. Actual loads still perform the
existing suspension transitions. `DerivedVerification.RecomputeOnHit` compares
every cache hit with a fresh computation when enabled; the test assembly enables
it at startup. Derived values and the switch never enter checkpoints or digests.
The [conversation equivalence probe](../conversation-system-equivalence/README.md)
checks native public turns and listener memories alongside the general tick
comparison.

Every step preserves these contracts:

1. Keep every seeded SHA-256 hash input byte-identical: tag, seed, IDs and tick.
2. Keep tick phase order, repeated phases and ID-sorted decision and turn application.
3. Keep candidate-list order and action-prefix routing; choices feed saved observation digests.
4. Keep dictionary insertion order and collection types where a first match has meaning.
5. Append events through the shared sink at the same point; call order determines IDs.
6. Keep checkpoint bytes, sorting and null-versus-empty choices; do not change the schema.
7. Separate saved state from pending calls and other live work across the tick swap.
8. Keep request Guids live-only and introduce no random values into world state.

The architecture tests classify every runtime instance field in
[`runtime-fields.json`](../../../tests/ClankerWorld.Tests/Architecture/runtime-fields.json)
as snapshot state transferred on commit, live work retained by the coordinator,
or scratch/derived diagnostic state. The `saved` category also includes derived
values transferred with that snapshot, such as fertility and Road bridge decks;
restore rebuilds them from saved inputs. Event handlers are live work too. They check
the complete transfer list, and invoke the native commit with a prepared
runtime to compare its encoded checkpoint and transferred values.

The C# source scan records writer files for every agent component in
[`agent-writers.json`](../../../tests/ClankerWorld.Tests/Architecture/agent-writers.json).
Construction writes all components, including defaults; collection replacement,
insertion and removal have their own entry. New writer files fail the test.
Removing a writer also fails until the baseline is lowered. Review a baseline
change with the code it permits. The scan uses the pinned SDK's C# parser and
type binding; it rejects binding errors except declarations whose regex
implementation the real build generates.

Before moving an area, check current open PRs that edit its methods and the
goods-query steps tracked in #1366. Add ready overlapping PRs as blockers and
tell draft authors which branch will move their code. Move one area, retain
forwarders, remove its dead `checkpointSchemaVersion = StateSchemaVersion`
assignments, extend these guards and document its boundary here. Run the
[tick equivalence comparison](../build-and-test.md#compare-tick-equivalence)
against the exact base and include the result in the PR. Later steps extend
the writer ratchet to the state they move.


The [runtime action checks](runtime-action-checks.md) describe ground occupancy, and [Finding goods](finding-goods.md) describes the goods query.
