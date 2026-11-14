---
title: Formats and maps
type: persistence-reference
status: active
updated: 2026-11-14
---

# Formats and maps

Current save formats, generated maps, compatibility and saved clocks.

## Current formats and older worlds

Schema 128 removes prototype names from the save
([#1364](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1364)): map objects are
saved as `mapObjects`, the checkpoint header is
`clankerworld.world-runtime/v2`, and the active save is `saves/world.json`
instead of `saves/private-world.json`. A save under the old file name is
not found, so the game starts without it and leaves it on disk. An older
checkpoint opened by path is refused by schema with the usual reason and left
unchanged; there is no migration.
The same schema saves the game design's names: `agents`, `deceasedAgents`,
`agentId` and `targetAgentId` instead of `inhabitants`, `deceasedInhabitants`,
`inhabitantId` and `targetInhabitantId`; `fullnessBasisPoints` instead of
`hungerBasisPoints`; `foodStewardship` instead of the world's `council` for
the food-policy vote; `shopTrades` instead of `businessTrades`; and, for each
Town, `council`, `landRecords` and `nonLandHearings` instead of `governance`,
`landHearings` and `nonviolent`. An instruction's understood task is saved
as `task` instead of `order`. Event kinds follow: `agent_*` instead of
`inhabitant_*`, `barter_*` instead of `settlement_trade_*`, `shop_trade_*`
instead of `business_trade_*`, `food_policy_*` and `food_steward_changed`
instead of `council_*`, `hearth_content_staged` and `starting_resource*`
instead of the `settlement_*` names. Generated IDs use `{world}:agent:` instead
of `{world}:inhabitant:`, and `direct-barter:`, `shop-trade:`, `partnership:`,
`care-relationship:` and `trust-memory:` instead of `settlement-trade:`,
`business-trade:`, `settlement-partnership:`, `settlement-care:` and
`settlement-trust:` ([derived identities](../derived-identities.md)). The
provider-configuration file, which keeps model keys outside the world, moves
to its own schema 4 for the `AgentId` of each model assignment; an older file
is upgraded in place, so saved keys keep working.
The starting households are saved as `household:first` and
`household:second`, and their starting goods are named after them, such as
`food:first` and `wood:second`. A meal from stored food is recorded as
`rations`, spare supplies set down outside a House are recorded as `pile` in
`spare_cargo_stored` and `household_delivery_recovered` events, the chunks of
generated maps name the generator `noise-drainage`, and the small test map's
cooking fire and path are saved as `cooking` and `path`. Decision intentions
check observations against a `clankerworld.world-observation/v2` digest.

World schema 98 adds optional structured storage changes to committed
inventory events: a building ID, item kind and nonzero signed quantity.
Records have unique, ordered building/item pairs and retain their original
event identity and time, including through archive compaction. Strict loading
refuses malformed records. The observer projects only recent retained facts;
reading Details changes no stock, permissions or agent knowledge. Earlier
alpha schemas are refused and preserved without migration or history backfill.

World schema 95 adds a required routine-helper choice, model name and
optional credential-slot ID (OpenAI for Decisions, TypeSafe for Jev). API-key bytes remain installation-owned.
Loading rejects invalid helper/model/slot formats or inconsistent availability;
missing installation keys cannot be invented from a save. The existing helper
routing revision invalidates earlier requests, and switches preserve memory
sources and scores. Current-format roundtrips and resumed replay retain the
helper and typed model. Earlier alpha schemas are refused and preserved without
migration.


## Map generation and physical travel

Generated geography now saves a hydrology revision for new worlds. A missing
or zero revision keeps the previous lake/river algorithm, including historical
resource placement and map identity. Revision 1 is selected by both normal
New World preview and Create World. Existing saves are not regenerated with it;
there is no in-place lake shrink or river rewrite. Older builds need not accept
new-revision worlds.

The terrain rules for sand, groves and hills
([#461](https://github.com/ClankerWorldOrg/ClankerWorld/issues/461)) change the
surfaces, vegetation and resource layout generated from a seed, for every
hydrology revision. There is no generator switch and no migration: a generated
world saved before them no longer matches regeneration and is refused with "The
world map does not match deterministic regeneration." Its file is kept.
The save format and schema number do not change. As with any unloadable active
world, the server will not start until that save is moved aside. Hills are drawn
from the saved elevation and water layers, so nothing extra is saved for them.

Removing the forced polar sea rows at the north and south edges
([#2367](https://github.com/ClankerWorldOrg/ClankerWorld/issues/2367)) also changes
generated maps in the same way: there is no generator switch or migration.
Existing save files keep their maps; restore never rewrites them to the new
terrain. A saved map that differs from deterministic regeneration is refused
with the same message, and its file is kept. The save format and schema number
do not change.

Mountain massifs ([#683](https://github.com/ClankerWorldOrg/ClankerWorld/issues/683))
change elevation, rivers, climate and resources for every generated world, so
the terrain version saved in `GeographyOptions` (`balancedVisibilityVersion`)
moves from 1 to 2 for all climate modes. Generation and loading accept only the
current version. A world saved with version 1 is refused when its checkpoint is
read, with "This world's map was made by an older terrain generator, before
mountains formed massifs. This build cannot rebuild that map, so the world is
not loaded; its save is kept." The file is left unchanged, the world list marks
it as unable to load, and there is no migration. The save format and schema
number do not change; the hill band still comes from the saved layers.

Private checkpoint v2 stores verified 64×64 terrain-byte chunks. Map acceptance
and checkpoint writing share the complete-grid count, bounds and uniqueness
proof; the writer fills terrain bytes during that same walk. Borrowed tiles
are rechecked on every call. The old v1
per-tile format is refused, and generated maps must match the current generator
and package checks; there is no historical generator or package fallback.
Damaged chunks are rejected without replacing the save. Restore also removes
Road tiles inside validated saved building footprints, leaving other Roads and
state intact. The repair applies once and may expose an already broken Road
connection; it does not reroute Roads or create bridges. Back up older saves
before an upgrade.

The terrain codec and map acceptance share per-layer length and declared-byte
checks. Each boundary retains its own failure messages.

World-systems resource manifests are separate from the terrain-byte chunks.
Loaded or changed manifests require full structural validation and an exact
canonical digest. Unchanged immutable manifests may reuse that result in a
process-local weak cache; current-world bounds and duplicate coordinates still
require validation. No cache state is saved, and the checkpoint schema, JSON
field order and canonical bytes remain unchanged. See [How it works](../how-it-works.md)
for ownership and [Chunk validation measurements](../chunk-validation-measurements.md)
for exact checkpoint/replay comparisons.

Bridges are saved in the same checkpoint as the Road tiles they join, so a
Road never ends at a river without its bridge. Loading checks every bridge
against the map (a legal one- or two-tile river span between buildable banks,
matching its ID and design), refuses overlapping decks, a bridge landing on a
building, resource or map object, and a Road bridge whose two entrances are
not both Road tiles. It does not need the Town or building that caused the
bridge. Traffic evidence must be recent, within its per-agent bound, for real
unbridged crossings of one or two river tiles, and any open wade must match
where that agent stands, in either water tile of a two-tile crossing. A save
that fails these checks is refused with a reason and kept. Two-tile wading and
its evidence use the existing fields and crossing IDs, so the schema number
does not change. An older build refuses, and keeps, a save that places an
agent, a map memory, an exploration path or traffic evidence in a two-tile
river.

Swimming uses existing physical position, warmth and travel-cooldown fields;
there is no new save field or schema version. Live and archived positions may
lie in a wide river or lake. A sea position still requires the saved boat
passenger evidence. Loading does not apply the starting warmth, illness or load
gate to someone already in swimming water: they must be able to reach shore.
Refused ticks commit no movement or warmth loss. Older builds refuse and keep
a checkpoint that places an agent in swimming water.

Scouting waypoints record already walked steps, rather than permission to
repeat those steps now. Loading checks each ordered edge against the current
bridge map or against the same terrain with only bridges built strictly before
the outing's `LastOutingTick`. A bridge built on the outing's start tick is
excluded from that older graph because it may have appeared later in the same
tick. Bridges that predate the outing remain, so an impossible sideways step
across an existing deck is not excused by removing it.

For a deceased agent's archived outing, the first graph includes only bridges
built at or before `DeathTick`; a bridge built on a later tick cannot make a
fabricated old step legal. Record bounds, coordinates, times and discovery uniqueness remain
checked. Saving and loading keep the recorded path, visits and discoveries
without clearing them to hide a topology change. Actual scouting and return
movement always use today's bridge map, with its legal axes, detours and
blocked-return behavior. These checks use existing timestamps and add no saved
fields, schema change or migration.

## Housing, names and the current schema

World schema 64 adds a move-out notice to an adult's `Housing` record:
the current household, original notice tick, fixed deadline and selection
reason. A pending request to another household is permitted while that notice
is held. Loading validates the adult, membership, reason and time bounds;
malformed notices and unsupported earlier schemas are refused and preserved.
Live care, family and capacity changes may make a notice obsolete, so the
runtime rechecks them before admission or displacement instead of treating a
stale notice as authority to move someone.

Current-format roundtrips retain notice deadlines, volunteer replacements,
housing requests and the ordinary departure's collection rights and once-only
food allowance. A forced replacement inherits a future deadline; one notified
at or after it receives a fresh day from the current world tick. Its saved
notice tick and deadline describe that new period. Volunteers retain the
original deadline, and other residents’ notices do not restart. Pause/load,
births and unfinished expansion do not restart the saved period. Replay must
produce the same cancellation or departure without duplicating events or goods.
Only completed footprints add resident places. Sole caregivers are protected
from timed displacement even when their dependent lives in another household.
No older-save migration or backfill is added.

World schema 70 introduced the chosen-name marker. The current standalone
`clankerworld.society/v4` and `clankerworld.society-runtime/v4` envelopes require
`HasChosenName` for every agent. This separates a chosen identity
from a temporary label after automatic naming has ended. Loading rejects a
missing marker, chosen names still awaiting naming, or duplicate normalized
chosen first names across living and deceased agents. Open and closed
placeholders may share first names. Choosing an identical placeholder text is
a real rename; closing automatic naming preserves its unchosen marker.

Current-format save/reload and history compaction preserve these distinctions
and the existing one-retry queue. Child surname checks happen when a chosen
name is admitted, rather than being reconstructed from mutable parent names
on load. Earlier society envelopes and private schemas are refused and their
files preserved; there is no name inference, migration or silent renaming.

The alpha accepts only the current world checkpoint schema, currently
`WorldRuntime.StateSchemaVersion` 139. The minimum supported schema is
the same value, so older alpha checkpoints are refused with a reason and left
unchanged; no world migration runs. The current schema also includes
a bounded model-attempt status and exact last accepted model choice per agent, plus
building footprint revisions, reserved expansion jobs, House guest invitations,
learned skills and skill-based lessons, birth-model choices, household fields
with ground harvest lots, bounded conversations with daily allowances, personal
equipment with timed repairs and exact reservations, reusable container lots
with their contents, locations, owners and reservations, explicit domestic
family and caregiver/birth-home records, owner messages with whether a personal
model heard them and any short reply, tool-lot links for saved field and
recipe work, per-agent life-moment identity opportunities with their outcomes,
connected Town-title plots, household use rights and pending use requests,
physical shop exchanges beside their exact inventory offers,
the continuity rule's state with each eligible couple's deadline, food-order
targets, progress, retry state and cancellation receipts, staged
guardian-search records with their offered adults and pending physical
guardian placements, medical permission and
consumed-dose progress, selected personal ornaments, and wills with up to
three named heirs, exact divisions and final words, Town admission records,
Town laws and government, concrete last-meal names for nourishment and
dietary variety, bounded tool-making requests linked to ordinary production
and barter, exact land-claim coordinates on Council proposals, household
land requests with their Council proposal and each adult's consent, physical
knowledge writing with exact material reservations and completed-artifact
receipts, exact-tile movement orders with their destination and arrival
receipt, overcrowding move-out notices, material-gathering targets and receipts,
typed Council-approved Town-project plans, shared construction progress and
exact physical delivery receipts, and paid Market layouts, stall borrowing,
stock receipts and physical barter records.
Land records are checked against the saved map, Towns, households and one
another before load. Building reassignment moves only existing footprint use rights;
connected remainder plots keep their holder and original grant terms. Split
records get deterministic unique IDs, and whole-plot moves retain their IDs.
The building and rights change under the same world lock; rejected or stale
requests change neither. No save-format change or migration is needed.

The food repairs use the existing field, inventory, reservation and delivery
records; they add no migration or new save fields. New planting claims remain
active until the work consumes the seed or an interruption releases it. An
existing current-format finite planting claim may still expire: field
maintenance then cancels the unfinished planting and retains the unconsumed
seed. Reload preserves physical food and vessel locations, exact active
claims and in-flight delivery pointers. Recovery clears those pointers only
when the real stock is set down; a discarded prepared step does not move it.
[Earlier schema changes](../../archive/saves-and-replay-history.md) record the historical compatibility table.

### Tool-making requests

Saved tool requests keep their requester, selling household, actual Blacksmith,
existing recipe, status, worker and real production/offer links. Active and
terminal lists remain bounded; selected production plans retain the exact
request identity. Recipe identities retain the full canonical content ID,
including IDs longer than 128 characters; request validation adds
no separate recipe-name limit. Withdrawn no-job history keeps that identity even
after its package is rolled back. Loading checks these links against the
canonical production and barter records. Every linked job must retain its exact
input reservations,
matching the whole recipe and selling household; completed work requires
completed consumption receipts. Ready work must bind a completed job, and an
offer must name that job's actual output and the same customer and shop.

Offered, fulfilled and terminal purchase history must agree with the actual
open, settled or cancelled inventory offer. A late withdrawal cannot turn an
already settled purchase into a withdrawn request. Retiring bounded terminal
history also clears its job link, so a job cannot retain an orphaned request.
Withdrawal creates no refund or ownership transfer: materials, unfinished work
and an unbought tool remain household property. A request is neither an
inventory lot nor payment authority. Reload must preserve in-progress work and
offer status without double production, duplicate payment or new ownership.
Earlier alpha checkpoints are refused and left unchanged. Current-format
integrity and byte-exact roundtrip remain required.

Other compatibility fields remain separate for simulation, envelopes, content,
assets, generator and network contracts. Change the field whose semantics
changed; a cosmetic game-version bump is not a migration.

## Saved clock and calendar

The saved clock/lifecycle values govern loaded current-format worlds. Restore validates matching
society/world-system calendar values rather than silently assigning the newest
calendar pace.

Newly created playable worlds save `CalendarOffsetTicks = 90` with 360 ticks
per day, placing elapsed tick zero at 06:00 on Spring 1, Year 1. The offset
must be nonnegative and less than one day. World-systems schema 3 originally
introduced nonzero offsets. Current checkpoints require world-systems schema 4
for both zero and nonzero offsets. An absent offset means zero and is omitted
when writing a zero-offset current-format checkpoint, preserving its saved
clock and roundtrip bytes.
Restore uses the saved value, not the new-world default. Ages, setup guards,
action deadlines and elapsed durations keep using the original world tick;
calendar dates, daylight and daily allowances use its offset calendar.

Night ([#673](https://github.com/ClankerWorldOrg/ClankerWorld/issues/673)) adds no
saved darkness field: time of day is derived from the saved tick, ticks per day
and calendar offset. Current-format saves retain their saved clock; nights and
their chill on outdoor warmth are derived from it without re-simulating
recorded history. A world saved during dawn reloads at the same darkness and
advances to the same bytes as the live world, which `SurvivalTests`
checks. Seasonal night length
([#891](https://github.com/ClankerWorldOrg/ClankerWorld/issues/891)) is derived the
same way, from the saved tick and the saved season lengths, so it adds no saved
field either: current-format saves retain their saved season, and from the
next tick their nights follow that season.
