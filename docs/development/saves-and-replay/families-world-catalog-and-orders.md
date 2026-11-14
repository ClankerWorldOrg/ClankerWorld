---
title: Families, world catalog and orders
type: persistence-reference
status: active
updated: 2026-11-14
---

# Families, world catalog and orders

Birth model choices, guardians, world selection, government and order lifecycle.

## Birth model choices and guardians

Schema 33 records a child's personal-model role at birth, provider endpoint, model
ID, installation-local key-slot ID for a model, and selection reason.
An Anthropic birth choice uses the endpoint identity `anthropic-messages-v1`
in the same record, with no schema change. The birth record has no thinking
level, so the child's model starts at its default.
Matching parent assignments are disclosed as agreement; when they differ, the
parent who began the family plan is the tie-break. The provider store preserves that
choice if its key is unavailable after moving a save or deleting a key, so the
child idles on built-in choices until setup is restored rather than using a
different paid model. A child without an explicit parental model remains
unconfigured, and worlds without a saved birth choice do not acquire one by
inference. An owner's later personal-model choice, including an explicit
no-personal-model setting, is saved separately from the historical birth
choice. That setting survives ticks and reloads; an explicitly unconfigured
child uses built-in choices instead of the world default. Infants make no
personal-model calls; normal presence, budget and admission checks still apply
after infancy. Living and deceased profiles validate the descriptor against its
recorded birth and schema. While provider storage is being recovered, the Model
panel keeps showing the selected provider and model with a setup message.
Birth bindings and owner role overrides retain the complete generated
agent ID. A valid long-seed newborn can bind both roles, including when
reloading a birth saved before provider storage recovered; no identity is
truncated or replaced. Repeated recovery preserves later owner overrides and
the separate historical birth descriptor.
Shutdown completes pending newborn bindings in installation provider storage
before acknowledging a saved world. Frozen bindings are staged before a prepared
birth becomes visible, so shutdown during tick publication retains the original key.
A failed binding refuses shutdown for a
paused retry, retaining the frozen original default key even if that default
changes. Key bytes never enter the world checkpoint; its format is unchanged.

Schema 50 saves an unresolved dependent-guardian search, including its current
relative, household or Town stage, start tick and offered adults. Each stage
keeps the earlier groups, and the offers are brought up to date at the end of
every tick, so a save always matches the households it was made with. Acceptance is
an explicit adult action that changes the saved current primary caregiver.
The original search format placed a child only in a completed House with room
in the same Town. Schema 71 adds the pending physical placement described below.
Loading validates the search stage, times and adult references. Replaying
from a pending request reaches the same acceptance opportunities and preserves
the single guardian-needed event. Older alpha saves without this state are
refused; no migration is added.

Schema 71 saves a dependent's pending guardian placement separately from the
accepted care relationship. It records the caregiver, exact relationship and
revision, start time, collecting or escorting stage, selected household and
Town, and the House's instance, definition, placement time and anchor. A
blocker may preserve accepted care while a home or route is unavailable. These
records grant no membership, reserve no House place and never substitute a
teleport for movement.

Loading checks the care authority, references, stage and saved destination as
one placement. A House that disappeared or changed ownership can leave a
pending destination; the runtime refreshes it rather than discarding accepted
care. The normal runtime rechecks availability and capacity on retry and
arrival. Completion changes the child's household and Town together and
clears the pending record; cancellation also clears it without changing birth
history. When completion changes household, an unfinished delivery to the former
House restores the original household owner and clears its delivery destination,
retaining the exact quantity and physical carrier. It earns no delivery credit or
remote storage transfer. Save/load retains intermediate travel and blockers, and replay must
reach the same membership, position and lifecycle events. Earlier alpha saves
are refused and preserved; no migration is added.

## World catalog and selection

The private catalog archives each world's checkpoint. It saves the active world
before a paused switch and keeps world IDs, names, seed and settings separate.
Creation/selection require signed owner requests and leave the selection paused.
If creation fails, selection rolls back only the phases that committed, then
removes the newly added catalog entry and its snapshot. A checkpoint replacement
that failed does not require another write to that same blocked path, and an
autosave change that never committed does not need rewriting. Existing worlds
remain catalogued and the failed creation can be retried. If rollback itself
fails, the new entry and checkpoint remain available for operator recovery.
An interrupted catalog update is recovered against the active checkpoint before
the server starts hosted services or accepts requests.
A corrupt leftover checkpoint encountered during deletion keeps cleanup pending
without preventing the healthy active world from starting. Unverified files and
the deletion intent are preserved; later recovery can finish after the file is repaired.
The paired authority identity belongs to the installation, not the selected
simulation world.

Load World checks each inactive checkpoint against the current runtime before
marking it compatible. The server keeps a process-local structural proof,
including the history head and saved helper selection, for each catalog identity.
A SHA-256 hash of the checkpoint bytes identifies the proof. File replacement,
catalog-identity changes and a server restart force a new structural check.
Every list still verifies the referenced history chain and current model
credentials; selection reads and restores the checkpoint again before changing
worlds.
Checking a changed checkpoint can therefore still be slow, especially on a
large generated map, while repeated unchanged lists avoid rebuilding worlds.
After a server start, a background task makes the structural check for each
inactive checkpoint, so the first list can reuse it; it reads checkpoints only
and changes no save.

Manual load shares the world-mutation lock with world selection. Its pause/world
checks, checkpoint restore, routing/autosave restore and rollback finish before
selection can archive the active world. This serializes concurrent operations; it
does not add a cross-file crash journal.

Founder placement restores the prior in-memory world and provider configuration
if its checkpoint commit fails. World selection and founder/add-agent setup
share a transaction lock while restoring checkpoint and model routing. This
handles ordinary operation failures; process termination between separate
files and a second failure during rollback still require operator recovery.
It does not make every owner endpoint a multi-file transaction.

Start World persists its completed setup while time is still paused, then resumes.
A failed checkpoint write restores the paused pre-start setup so a fresh signed
retry can succeed after storage recovers.

Windows provider configuration uses current-user DPAPI. Validated legacy JSON
migrates atomically to the protected envelope; damaged or wrong-user data is
not overwritten with empty state. Unix uses private permissions. Forgetting a
key removes it from the current store, not backups or the provider account.
Protected Windows files are not portable key exports. This is protection at
rest, not protection against software already running as the same user.

## Town government

World schema 55 adds each founded Town's laws and government record.
Law versions bind adoption, amendment and repeal to passed Council proposals,
with original scope and effective dates. Resident changes save original and
remaining electorates, final votes, queues, approval and handover deadlines.
Mayoral records save consent for specific mandates, every completed/interrupted
round, repeated top ties, retries, winners and separate land/ordinary terms.

Loading rejects unsupported arrangements, invented majorities, changed opening
rosters, malformed ballots, unsupported winners, overlapping mandates and terms
without a completed election. The approved arrangement must follow a completed
resident handover. Current-format saves preserve pending windows, accepted
choices and distinct mandates. Paused and rejected ticks do not advance or
partly apply civic work; replay does not reroll ties or duplicate authority.
Older alpha checkpoints are refused and preserved; no migration is added.

World schema 125 starts every new Town with vacant land and non-land
mayoral duties under ordinary Council authority. Each vacancy records the
Town's founding tick and has no holder, term or election. The first election
needs personal consent to both duties, rather than a government-change vote.
Current-format saves retain an empty office, joint consent, queued elections,
ballots and the separate terms after election. Pause, rejected-tick rollback
and paired native replay cover founding, election and case-only succession.
Earlier alpha checkpoints are refused and preserved without migration.

World schema 81 records the exact initial Council election forced by a
protected government change, or an explicit null when it forced none. The field
is required in the current format. Loading rejects missing or cross-Town
elections, duplicate ownership, unrelated renewal/replacement elections and
links inconsistent with approval or settlement. A runoff keeps the same
identity while its current round opening moves forward. Current-format replay
preserves forced attempts and ordinary elections independently; old alpha
schemas are refused and preserved without migration or inferred ownership.

## Order lifecycle

Orders retain their existing saved status spelling. Runtime status is an
`OrderStatus` enum changed only through the task transition operation; closed
orders cannot return to active work. The runtime keeps a completion set only
for suggestions and derives order completion from status when exporting the
existing checkpoint list. The status foundation changed no saved fields or schema.
Schema 134 bounds closed orders to the six newest submissions per agent,
keeping all active work. Cancellation retries are also bounded to six per
agent, ordered by world tick, event ID and idempotency key. A retired order can
no longer be cancelled through its old ID. Compact submission receipts and
request hashes remain authoritative: a duplicate submission returns its original
receipt, even after its task and text have retired, and conflicting reuse is
refused. These receipts contain no status and do not duplicate completion state.

Retirement drops order-only construction evidence and clears optional owner
order links on native work and written artifacts. It preserves the actual
inventory, consumed reservations, buildings, artifact contents and native work
outcomes. Retired exploration purposes are cleared in live and deceased records,
while their recorded paths and discoveries stay intact. Construction completion
uses the actual placed building even when its order receipt retires in the same
action. Loading validates retained order links and rebuilds the active index.
Earlier alpha schemas are refused and preserved without migration. The schema
number is provisional until merge.
Current-format reload, cancellation and rejected-tick checks retain the same
physical effects and receipts.

Schema 135 stores each task's `definition` with an explicit `kind` discriminator
and only that kind's fields, beside common progress and the unchanged status
spelling. Missing required targets, unknown kinds and fields belonging to
another kind are refused during decoding. Each task record owns its shape
validation; native receipts, authority and cross-system bindings keep their
existing checks. Current-format progress, cancellation and replay remain the
same. Earlier alpha schemas are refused and preserved without migration; this
version is provisional until merge.
