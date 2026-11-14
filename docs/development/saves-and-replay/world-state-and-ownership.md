---
title: World state and ownership
type: persistence-reference
status: active
updated: 2026-11-14
---

# World state and ownership

Saved state ownership, world identity and physical and social records.

## Separate state by ownership

Production jobs, their sequence and tool requests are captured from the
production system in the existing save fields. Retained recipe plans remain
live state and are copied during trusted tick preparation. The production
system checks local job shape; save validation checks job links to recipes,
buildings and the world clock. This ownership split changes no save shape.

Construction captures placements, expansions, guest access and completion
receipts from one building store. Local record validation belongs to that
store; loading still checks links to content, terrain, people, inventory,
Town records and orders. Live construction plans copy across trusted tick
preparation and stay outside checkpoints. Existing construction save fields
and event order are unchanged.

Living and deceased physical-agent records are captured from AgentStore;
community knowledge is captured from CommunitySystem. Trusted preparation shares
immutable physical records until a named store operation replaces one. It keeps
local detached preparations and rollback commit points. Local lesson, housing
and knowledge checks are shared with save validation, which still checks their
links to society, inventory and the clock. These ownership changes preserve
existing save fields, event ordering and replay bytes.

FamilySystem captures population continuity in the existing field. Family
components remain physical-agent records written through AgentStore, with local
family checks shared by test writes and loading. Live will requests and their
cancellation reasons stay in FamilyWillService and never enter a checkpoint or
transfer from a prepared runtime. Guardian action marks are per-tick scratch
state. Family ordering, provider decisions, birth food and consumed medical-dose
receipts keep their existing save and replay behavior.

Shop trades are saved by MarketSystem, contained in the saved town system.
Town market records still use their existing fields and named town operations.
Preparation restores the shop ledger before the next tick, and commit transfers
it with the town system. Canonical ordering, stock holds, inventory receipts,
recent-history boundaries and event order are unchanged. Local market record
checks are shared by test writes and save validation; cross-system validation
still checks buildings, people, authority, stock and time.

## World and installation identity

World state includes its seed and generation options, clock, agents, accepted
events, Towns, content locks, model/slot assignments and autosave choices.
Cognition diagnostics retain their world ticks through export and restore.
Late replies and ignored failures use known observation or saved world time;
run epochs are generation markers, not timestamps. This changes new diagnostic
events without changing the schema or rewriting events already saved.
New generated worlds have a deterministic identity derived from their seed,
selected generation options and accepted map digests. Different maps can reuse
a seed while keeping separate checkpoints, save timelines and owner-action
contexts. A world name is not part of this identity, so renaming the same
generation choices does not permit a duplicate. Loading keeps the saved
identity, including existing generated worlds whose identity was their seed.
Duplicate checks use those older worlds' saved generation choices even after
planting changes their current resource manifest.
Loading a named save requires both its seed and saved world identity to match
the selected world. A save from a different same-seed map is refused before
creating a backup or changing its timeline, runtime or settings.
Installation state includes device authority, provider credentials and usage
accounting. Saves store slot IDs and model choices, never API-key bytes. An
agent's model choice can carry a thinking level, written only when one is
chosen. Provider files from before Anthropic was added load with an empty
Anthropic record; the provider file's schema stays 3.

World schema 127 and world-systems schema 4 remove the unused prototype
faction/law, currency and culture collections and the culture-tag bound.
Native legacy-map starts previously wrote world-systems schema 2, while generated
founder starts wrote schema 3; both now write schema 4.
Current saves retain weather, ecology, chunks and the native Town law and
ownership systems. Creation, play and replay keep their existing behavior.
Earlier alpha checkpoints are refused and preserved through the normal
unsupported-format path. There is no migration or additional compatibility
implementation.

The October 1 terrain tuning changes deterministic generation for new worlds.
Loading retains the saved map and current weather episode; it does not replace
either with a freshly generated map or a new weather roll. Earlier alpha maps
may fail the existing regeneration checks and are refused and preserved; no
terrain migration is provided. New saves with default weather store a null
profile list to select the reduced built-in preset. Explicit profile lists
keep their configured weather weights.

Checkpoint encoding still performs full map acceptance. Only the derived
starting-site reachability traversal may be reused when its actual topology and origin
remain unchanged. Loaded maps start without that process-local cache, and
borrowed-data edits require a fresh traversal. Placement, layers, canonical
digests, generated-map checks and atomic checkpoint publication remain required.
No cache state or new fields are saved; the checkpoint schema and canonical
bytes are unchanged. [How it works](../how-it-works.md) owns the runtime cache boundary, and
[Starting-site reachability measurements](../starting-site-reachability-measurements.md) records the
matched native save/reload, rollback and continuation comparisons.

Society validation builds temporary indexes over the complete retained belief
history. An owner's source turn can belong to only one correction lineage;
known agents and both sides of correction links are still checked, and
cyclic chains are refused. Encoding, decoding and recording or correcting a
belief use the same integrity rules without per-belief whole-ledger scans.
No belief, confidence, reporter, source turn or memory-compaction reference is
removed. The indexes add no saved fields or schema change, and valid checkpoints
retain their canonical bytes.

## Physical goods and work

World schema 60 adds exclusive physical handcart attachments. A cart
and its cargo are existing inventory lot relationships, with the cart's ground
position retained while pulled or parked. Loading verifies one cart per puller,
one puller per cart, living ownership, condition and shared position. In-flight
crafting and repair inputs use the normal exact inventory reservations. Current
roundtrips retain loaded parked carts and mid-journey hitches; rollback retains
the previous physical position and every cargo quantity. Earlier alpha saves
are refused and preserved; no migration is added.

Current cart repair orders retain their stable target in a typed cart definition
and their completion receipt in common progress. The receipt binds the instruction, actor, cart
and native repair tick; reload requires that tick's three exact consumed wood,
iron-fitting and rope reservations. Real carried supplies, the broken cart's
cargo and pending order survive reload and refused-tick rollback. Cancellation
keeps collected goods and creates no unfinished repair reservation. The original
cart-repair change kept the schema unchanged; a build without those orders refuses saves
containing that action and keeps the original file.

World schema 31 records an agent's learned skills and each lesson's
skill instead of a work role. Skills retain their first learning time and
optional teacher ID, including in deceased profiles. Loading validates those
references and times, and rejects null entries in living or deceased skill
lists as damaged checkpoint data. Current lesson progress and skills survive pause,
save/load and replay. Newly completed lessons bound oversized gratitude-memory
keys with a SHA-256 digest of the complete key, retaining the `lesson-gratitude:`
prefix and complete saved participant references, text and source tick. Short
keys stay unchanged. This adds no field, schema or migration; existing memory
records are not rewritten when loading.
Schema 32 adds saved household requests and recent refusals
for adults without an authorized home. These states are validated and survive
save/load and replay. Older alpha lesson records need not load; no migration is
provided. Saved skills grant no ordinary action permissions. Matching skills now shorten
work with a provisional 20% reduction, without a new saved field or schema.
Production, expansion and field jobs keep their recorded deadlines or remaining
work across reload; the bonus is applied when a new job starts. Projects and
repairs use their actor's saved skill with existing progress. Native paired
continuation retains outputs, material costs, full completion receipts and
current-format checkpoints; refused ticks keep the preceding state.

Workstation recipes that output fresh water are refused before reserving
materials or creating a job: their output path has no destination jug, and
inventory requires fresh water inside a water jug. Autonomous production and
workstation supply planning skip those recipes, including mixed-output ones.
A supported current-format checkpoint can retain a running job admitted by an
earlier build. When it becomes due, it cancels and releases its remaining reservations without consuming
inputs, wearing its tool or creating output. The cancellation saves and
replays normally; no schema change or water-production feature is added.

World schema 34 adds household field tiles and their crop/work state.
The saved inventory also records a ground position for physical harvest lots.
Field ownership, work inputs, growth times and replanting reservations are
validated together with inventory and map geometry. Current-format roundtrips
retain intermediate work, carried deliveries and planting reserves. Fertility
is derived from the seed and immutable map layers rather than saved per tile.
Generated maps use their saved candidate attempt to recover the same rainfall
as generation. Correcting that calculation changes derived fertility and
future crop yields for candidate attempts 1 and 2, including after reload;
existing fields, checkpoint bytes and the schema are unchanged.
Older alpha saves need not load; no field or orchard migration is provided.

Town-wide farm planning derives its food shortage and each farm's field target
from the current living resident roster, assigned Farmhouses and usable,
unreserved resident or household food. These inputs already survive save/load;
no cached target, additional record or schema change is introduced. Restored
farms recalculate the same budget before choosing new work. Existing field
work and planting reservations retain their ordinary validation and ownership.

Schema 37 adds authoritative garment and carrying-aid lot IDs and timed repair
work, with exact material reservations. Loading checks the selected goods are
single, unreserved units physically carried by their recorded owner; repair
work also needs its private work site and exact live inputs. Broken gear and
existing overloads remain valid property. Carry capacity and weather protection
are derived from the selected lots rather than saved as a second authority.
An unpaid household recipe may also keep a paused plan that requires a fresh
choice after its inputs become unavailable; validation only accepts that flag
on a paused project without a linked job. Older schemas carrying equipment
records are refused. No migration is added.

World schema 38 retains a building's definition identity in completed
or cancelled expansion history after an owner removes that building. A
building with active production or expansion work cannot be removed or
reassigned, and a household's last Farmhouse stays assigned until its field
work finishes. Town membership, building assignments and physical inventory
locations are validated together; older alpha saves need not load and no
migration is provided.

## Continuity and Town governance

Schema 46 adds the low-population continuity rule: whether it was on at the
last check, so its Event Log transitions are not repeated after loading, and
each eligible couple's deadline for saying "not yet" to a child. A parenthood
plan may also be in the new `postponed` stage. Loading refuses a checkpoint
without this state, couples while the rule is off, unknown or unordered
partner IDs, duplicate couples, and a deadline more than two world days after
the saved clock. Current-format roundtrips keep the flag and deadlines, and a
replay from a postponed plan reaches the same plan and checkpoint. Older
schemas are refused. No migration is added.

The current continuity trigger counts eligible adult couples rather than
non-elders, without changing the saved flag or deadline shape or the private
schema version. A loaded world applies the current trigger on its next tick,
records a transition only if the flag changes, and preserves deadlines for
couples still held by the procedure. Historical headcount events keep their
original explanation. Eligible-couple transitions, rejected ticks and replay
are checked with the current save format.

World schema 138 extends continuity state with each held single adult's fixed
partnering deadline, optional chosen partner and whether they said not yet.
The single list is required,
ordered by adult ID and empty while the rule is off. Loading refuses unknown
adults or chosen partners, self choices, duplicate or unordered entries, and
deadlines beyond two world days. Choices may become unavailable before the
next maintenance check; pairing rechecks the same eligibility used by the
continuity couple count. The typed `continuity_partnership_formed` event keeps
both adults. Current-format reload, refused-tick rollback and paired exact-byte
continuation preserve choices, deadlines and the resulting native partnership
and family records. Earlier alpha saves are refused and preserved without
migration. This version is provisional until merge.

World schema 48 adds independent saved Town governance. It records the
current council and fallback cause, term/retry schedules, personal full-term or
remainder-term candidacy agreements, proposal identity/windows/final votes,
current election and settled history, ballot revisions, runoff eligibility,
recorded fair draw order, posted notices and actor-owned read/relay receipts.
A founded Town missing its governance, duplicate votes, unsupported winners,
invalid civic references or inconsistent proposal thresholds are refused as
bad checkpoint data. Founder setup may have no civic state until Start World.

Current-format roundtrips preserve windows and accepted choices, including
paused proposals, independent two-Town decisions and election ballots. Prepared
ticks rejected before commit leave no civic change. Delayed replies revalidate
the exact current contest/proposal and actor authority; cancelled votes cannot
revive after owner membership changes. Draws use a named world-local PCG stream
with unbiased selection and save the actual order, so loading does not reroll
an accepted outcome. Older alpha saves are visibly refused and preserved; no
migration is provided. A passed admission changes Town membership at most once
(schema 54); it never grants stock or household access.
