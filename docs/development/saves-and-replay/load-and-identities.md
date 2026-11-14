---
title: Load and identities
type: persistence-reference
status: active
updated: 2026-11-14
---

# Load and identities

Load bounds, save browsing, identities and advisory space checks.

## Stored-data bounds and model settings on load

`SavedFile` checks the length on the same open handle before allocating or
reading: checkpoints have a 256 MiB limit, individual history segments 64 MiB,
the world catalog 16 MiB and adjacent metadata 4 MiB. It also refuses a file
that grows during the read. Oversized files stay on disk. The checkpoint
writer checks its encoded checkpoint and new history segment before publication;
there is no trimming, migration or replacement world. An oversized active file
stops startup with a size explanation rather than loading it into recovery memory.
An oversized timeline refuses save publication before creating a recovery copy
or another save. An oversized diagnostic `.build.json` stays untouched; its
best-effort build recording does not fail an otherwise valid checkpoint save.
Save creation, overwrite, load, world selection and timeline inspection share
one `save_too_large` response with the size explanation.

The world holds at most 256 content package records. Each manifest permits 256
definitions and asset reservations, 128 dependencies and 64 capabilities. Display
names have a 128 UTF-16-unit printable-text limit; capability names have the same
limit and contain no whitespace. Each definition payload has a 256 KiB UTF-8
limit, with 2 MiB of payload across the manifest. Manifest decoding is capped at
16 MiB. Proposal and strict restore share these checks. Earlier alpha saves over
the bounds are refused and preserved.

The signed `content/remove` action removes only quarantined content, while
paused. It validates and saves an isolated proposal before installing it. Ordered
governance audit receipts remain, including a removal receipt; the package's
payload and reservations are no longer retained as a package record. A removed
package ID cannot be proposed again. Active content must first pass the existing
rollback checks, including retained physical and order references.

Before loading a named save or selecting another world, the game reads its model
settings without applying them. A changed set of agent assignments or routine
helper is shown with model names and installation key labels. Confirmation is
bound to the selected identity and exact saved assignments, thinking levels and
helper. The signed load carries that approval; changed settings require a new
preview and confirmation. Missing approval refuses before recovery copies,
timelines, provider assignments or checkpoints change. Keys themselves remain
installation state and never enter previews, approvals or saves.

These bounds and approval checks add no world fields or replay events. Existing
strict reload, history verification, durable load intent and rollback remain
required.

## Developer map edits

Paused terrain, resource and camp-object edits persist one validated proposal
before changing the live world. A failed write preserves the prior checkpoint
and live map, layers, ecology and history. Accepted batches use the existing
`developer_edit` event with operation `edit_world`; its value retains the full
world/event/epoch-bound command for deterministic replay and exact retry.
Current-format saves retain edited terrain and resource/chunk manifests through
strict reload and continuation. Schema 139 stores bounded current terrain
overrides and added resource/object definitions in `MapAuthoring`. Reload checks
the regenerated baseline with those overrides, including after history
compaction. Earlier alpha saves are refused and preserved; no migration runs.
The schema number is provisional until merge.

## Browsing saves from Load World

The signed `POST /api/v1/owner/worlds/saves` read takes a catalog world ID and
returns its saves, timeline position, calendar and world time. It does not
activate or write that world. The client uses the same timeline, list and
save-description controls as Save World. A changed world selection cancels the
read and discards late replies. Loading first uses the existing world selection
and manual-save load operations, preserving the current world and keeping the
loaded world paused. No checkpoint format or branch-creation rules change.

## Stock holds

World schema 132 introduced optional typed holds to inventory lots and matching
authorization to reservations. Each hold records a purpose and an exact holder;
the inventory core checks both, including vessel families. Splits keep their
effective holds when goods leave a vessel, and a saved reservation rechecks its
authorization before consumption or barter settlement.

World schema 133 requires matching holds for saved custody and promises.
Delivery holders are destination buildings, borrowers and
supply-trip holders are their actors, and Market holders are occupancy IDs.
Strict world validation checks each hold against its corresponding saved promise
and refuses a missing or inconsistent link. Hold lists have unique entries in purpose/holder order and
participate in checkpoint bytes and inventory digests. Empty lists are omitted.
Current-format reload and continued transitions retain the holds; refused
operations leave the supplied checkpoint unchanged. Earlier alpha worlds are
refused and preserved without migration. This number is provisional until merge.

## Choice plans

Child action plans are transient server data and add no checkpoint fields.
The typed `choice_refused` event records the actor, their name, activity and
plain refusal reason when a selected person, resource, lot or quantity becomes
unavailable. World schema 137 adds this event payload. Earlier alpha saves are
refused and preserved without migration.

## Building doorways

Doorway movement adds no saved fields or schema version. Strict validation
checks the building's effective footprint and its recorded entrance, or the
default south doorway when none is recorded. The doorway must be on buildable
ground, directly beside the footprint and outside every other building.
Older alpha layouts with sealed doors are refused and their files preserved;
no migration moves a building or invents another entrance. Native placement,
strict reload, refused-tick and paired continuation checks cover the rules.

## Mod content identities

Content IDs use the 512-character import limit in
`ContentPackageRules.MaximumContentIdLength`. It covers package and local IDs,
schema kinds, complete canonical definition and asset IDs, and resource and
workstation references in building/recipe payloads. The complete canonical ID
must fit; its digest, separators and version count toward the limit. A package
with a longer ID is refused before its proposal record or event is created.
Existing typed-payload, dependency, authority and activation checks still apply.

Construction candidates allow the ID plus their action prefix and optional
site coordinates. Inventory, recipe projects, frozen estate lots, will
bequests, land-sale prices and payment receipts retain complete identities.
Long will goods labels are shortened to the model's display budget and include
their offered `item:N` key; the reply maps that key to the original frozen lot.
Resource knowledge and its model context use the import limit and local-ID
syntax, including periods.

No saved fields or schema change. Native boundary checks retain complete
checkpoint bytes through reload and matched continuations, including completed
inheritance and actual land-sale payment. Earlier alpha saves containing
over-limit mod IDs may be refused under the new import validation; they are
preserved, and no ID shortening or migration is performed.

## Bounded derived identities

World schema 123 applies one 128 UTF-16-unit limit to generated
identities assembled from other identities. Short values retain their spelling;
long values hash the complete original value through `DerivedId`. This bounds
native descendants, family relationships, inventory and ordinary barter splits,
work reservations and receipts, memories and public Town record handles.
The separate provenance and ownership references remain complete. Numbered
Town handles retain a counter and validate by exact recomputation against the
original scope. Earlier alpha saves are refused and preserved; no migration
runs.

Schema 126 adds action refusals and their cumulative count. A refusal records
the complete actor and candidate, refusal tick, one-day retry boundary and a
bounded check name. The matching world event retains the accepted refusal for
history and replay. Active cooldowns are authoritative saved state; they are
not inferred from display text. Earlier alpha formats remain refused, with no
migration. The version is provisional until merge.

Strict reload and replay must preserve the bounded handle, complete source
links, reservations, payment, ownership and personal recall. Display names,
mod content identifiers and parsed action payloads retain their own contracts.

The [generator inventory](../derived-identities.md#executable-generator-inventory)
describes the native transition, factory, save and recall evidence.


Typed memory, reservation, offer and receipt handles retain their existing JSON
string spelling, including strings in saved input lists. Save readers retain
existing nonblank spellings; validation applies each record's original rules.
This preserves legal long or control-containing inventory keys while keeping
the stricter belief, receipt and repair-material checks. Complete provenance
references keep their own limits. No fields, schema, spelling or migration
change. See [Derived identities](../derived-identities.md).

## Save-space warning

The server samples available free space on the active checkpoint, manual/autosave,
world-catalog and history-archive volumes every five seconds, resolving existing parent
directories, symlinks and mounted volumes. The initial advisory level is below
1 GiB. Any known low volume warns even if another cannot be read; space is
unknown when a query fails or the sampling batch started at least thirty seconds ago.
The authenticated `save-disk-status` read returns bounded status and byte
counts, without filesystem paths. The client polls independently of world
refreshes and reads again before a manual save or overwrite. A failed status
read cannot prevent submission. This installation read remains available while a
world baseline or startup recovery is pending; world mutation controls keep their
normal readiness guards. The recovery card also reads the advisory before its
explicit autosave restore.

The monitor owns no world or save lock. No free-space query runs on the
checkpoint path, and even a stalled query cannot hold up emergency recovery.
Space is advisory rather than a reservation: a later write can still fail.
Warnings do not authorize cleanup or deletion, and no warning state enters
checkpoints, replay events or save metadata. The world schema is unchanged.
[Releasing](../releasing.md) owns version selection and rollback gates.
