---
title: Validation, weather and events
type: persistence-reference
status: active
updated: 2026-11-14
---

# Validation, weather and events

Validation, weather episodes, developer edits, animals and world events.

## Validation boundaries

Ordinary conversation handles are generated from the proposal tick and a
SHA-256 digest of the ordered complete participant IDs. Participant references
remain intact and must identify known people. Handles and derived turn IDs use the shared derived-identity bound;
turn/request binding checks still reproduce the exact generated identity.
The unsaved scheduler choice token also hashes its complete actor, handle,
revision and status; its existing bound and stale-choice comparisons remain.

Use focused tests for migration, replay identity, interrupted writes, referenced
history, old calendars and rejected restores as relevant to the change. Record
real Windows/server verification separately from fixture and CI results.
The full check commands are in [build and test](../build-and-test.md).
Long-term save support and retention remain [design questions](../../game-design/saves.md#still-to-decide).

## Regional weather episodes

The prototype adds optional `RegionalWeather` data to world systems: topology,
local climate, condition, start/end ticks and each region's earliest next storm.
Absent data stays absent on load, preserving visible daily weather. The first resumed tick imports that weather; a current
storm retains its original daily start so migration cannot extend its duration.
Ordinary imported conditions reserve a conservative half-day storm-free window.
New worlds start with episode data. Saved episodes resume without rerolling;
all transitions use the same prior neighbor snapshot.

Custom weather weights retain their saved values, including valid totals up to
`int.MaxValue`. Climate conversion and episode bonuses use wider arithmetic to
avoid overflow. Existing episodes and the save format stay unchanged; future
transitions from extreme profiles use the corrected probabilities. Current
saves round-trip and continue deterministically under those rules.

Episode version and bounds are validated, including topology against the saved
map. Episodes originally arrived with private-save schema 26 and world-systems
schema 2. Current checkpoints require world-systems schema 4 and the current
world schema; earlier alpha saves are refused without migration.
Regional weather remains optional: absent or null episodes stay absent on load,
and the field is omitted when null. Saved episodes resume without rerolling;
the first resumed tick initializes missing episodes. Future weather and events
follow these rules without changing past recorded history.

Advanced generation saves optional forest, mountain and river presets. Missing
fields mean Normal. New-world water defaults do not alter saved water values.
Non-default maps require a build that understands their options and validates
their generated identity. Every generated world saves the visibility algorithm
(terrain) version, now 2, and, when trial targets apply, the selected candidate
attempt.
Restore regenerates that exact attempt, checks the saved map manifest, and does
not rerun candidate selection or silently change the saved map. The attempt
defaults to 0 for historical saves. A save whose map no longer matches
deterministic regeneration is refused for load; restore leaves the source save
file available for recovery or an explicit future migration.
## Developer edits

A paused developer edit changes existing need, inventory, skill or partnership
state and appends one `developer_edit` event whose typed payload carries the
complete `WorldDeveloperEdit` command. Existing save validation still applies
to the entire proposed world;
readers supporting the same checkpoint schema can load the representation,
though they do not offer
the edit UI or describe the new event kind.

The event ID supplies deterministic lot and relationship IDs. Reapplying its
command to the same paused baseline reproduces the checkpoint; replay tests
also compare resumed ticks after a save/load roundtrip. The event carries the
world ID and expected latest event ID. An exact retry found in hot event history
returns already applied without another grant or event. After history compaction,
the old event precondition refuses that retry instead of applying it again.

The server writes the validated proposal through the checkpoint's atomic file
replacement before accepting it in memory. A failed write preserves the live
state and the prior save, and the original command can be retried. Tests cover
this rollback, stale/wrong-world refusals, field binding in signed requests,
and the generated-world path for every supported edit category.

## Animal checkpoints

World schema 96 requires the complete animal-world record. It stores
exact animal identities, species, sex, birth/death ticks, physical positions,
household and yard, paid care deadlines, unfinished products and pregnancies,
reserved births, named permissions, taming work, supply trips, riders and
leaders. Products and fitted saddles reference real inventory lots and exclusive
reservations. Trade offers retain the exact animal, adults, receiving yard and
payment lot; native animal orders retain the exact bound animal ID. Milk is
ordinary inventory stock, and Store or Market purchases retain their normal
inventory offers, reservations and stock receipts. There is no milk-offer
field, vessel exchange or milk-specific lineage state in schema 124.

Supply-trip actors, riders, leaders and named permissions
retain complete known person IDs, including native descendants. Reload checks their actual person references, unique roles and
physical custody. Animal and herd identities use the shared derived-identity limit; animal names
keep their separate 512-character text limit; unknown, duplicated and malformed person references are
refused. This changes validation, with no new field, schema or migration.

Current-format replay and rollback cover arrival, paid care, collection, birth,
production jobs and attachments. Reload validates required animal fields,
physical saddle/product custody, household/herd limits and vessel contents.
Ordinary milk barter covers twenty consecutive whole-lot resales without ID
growth. Pending and completed Store purchases cover server-file reload,
acceptance, refusal, paired exact-byte continuation and refused-tick rollback.
Ordinary offer validation rejects unknown parties and inconsistent references.
Water jugs accept only fresh water; milk may remain loose or enter a food pot.

An interrupted tick or refused durable developer edit restores both animal and
inventory state. Older alpha checkpoints are refused and preserved; no animal
inference or migration runs.

## Event presentation

Owner event observations include server-formatted `Text`. It is derived from the
same captured state as the event slice and is not stored in accepted history.
The snapshot refreshes potentially visible descriptions from its current names
and relationships. A transient 2,048-event observer window preserves descriptions
for connected clients after saved history compaction; a load or world switch
resets it. Renaming still updates existing rows. The game displays this text
without parsing event details. Event observations also carry the typed payload
for fields the game uses directly, such as model status and weather. Diagnostic
detail is not serialized in event observations. Descriptions remain derived
and do not change checkpoint bytes. `EventLogPolicy` classifies every accepted
kind once for the server and game. Routine chores and stale automatic choices
remain in the event stream and archive. Event Log refusal coalescing uses complete
typed actor/subject references and the saved calendar's day boundary; no saved
field, event payload or schema changes.

## Typed world events

World schema 136 stores accepted events as event ID, world time and a
kind-discriminated payload. The payload carries an explicit actor when there is
one, its captured position, and only that kind's subject references and values.
The event log looks up the complete actor ID. It never guesses an actor from
colon prefixes, and IDs and canonical content references remain complete.
Unknown kinds, foreign fields and missing required payload fields are refused.
Text and diagnostic detail are derived; they are not authoritative saved fields.
Earlier alpha schemas are refused and preserved without migration. Strict reload
and matched continuation retain accepted event order, payload data and positions.
