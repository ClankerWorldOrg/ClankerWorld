---
title: Assets and map editing
type: architecture
status: active
updated: 2026-11-14
---

# Assets and map editing

This page describes approved assets, paused map editing and building storage history.

## Approved authored assets

The immutable server-approved catalog is loaded at startup. Its reference policy
accepts only an exact normalized `assetId` and lowercase `sha256:<64-hex>` digest;
a missing catalog approves no references. Malformed, ambiguous or unknown-schema
catalogs prevent startup. Device requests cannot edit it or upload asset bytes.
Map editing does not change this catalog or upload assets. The fixture's old
authoring branch is retired; native map editing uses the runtime's own map,
ecology and chunk records. This is not an art-generation pipeline.

## Paused map editing

`WorldMapEditRules.Choices` supplies both the supported native terrain, resource
and camp-object definitions and the server's `EditTools` labels. Signed owner
batches bind the world, observer timeline, run epoch and latest event ID. `WorldRuntime.ApplyMapEdit`
checks pause, bounds, supported kinds, unique tiles and empty placement, then
builds an isolated proposal. Terrain writes update the compatibility tile and
its independent elevation, hydrology, surface and vegetation layers; resource
placements update ecology and chunk manifests through `EnvironmentSystem`.
Map acceptance and strict checkpoint validation run before persistence. Added
objects cannot claim built-in starting-site IDs or replace the original
camp/Town weather anchor.
`WorldStateFile.ApplyMapEdit` saves the proposal before publishing live state,
so a refusal or failed write exposes none of a batch.

Accepted batches are serialized in the existing typed `DeveloperEditEventPayload`
with operation `edit_world`; the event records the complete command for replay.
Exact retained retries succeed without applying again. Once an event leaves hot
history, its stale event precondition refuses another application. No separate
unbounded receipt ledger is introduced. Schema 139 retains `MapAuthoring`, a
bounded set of current terrain overrides and added resource/object definitions.
Reload applies these to the regenerated baseline before checking the map;
compacted edit events are not needed to load an edited world. Earlier alpha
saves are refused and preserved without migration. The number is provisional
until merge.

The authoring observation's optional `EditTools` collection supplies the map
picker's operation names, kind labels and renewable defaults. An empty
collection offers no map edits. The client keeps at most 64 unique preview
tiles and submits them as one retained authoring batch; previews never alter
the observed map. Closing Developer tools, resuming or changing the world/map
context discards the preview. Conflict responses show the server's plain
reason and clear the completed retry record, while an unconfirmed response
keeps the exact request for recovery.

## Building storage history

Building Details receives up to ten recent storage deltas from the existing
retained inventory-event stream. Each committed physical change records its
building, item kind and signed quantity on the existing event; event identity,
time and trade receipts stay intact. The runtime includes direct location
adjustments around kernel operations without recording an already captured
change twice. Net-zero lot splits, ownership changes, reservations, wear and
spoilage add no storage change. Projection scans the retained stream once and
never reconstructs a historical location from a current lot. The records
follow ordinary rollback and event compaction, not a second growing ledger.
