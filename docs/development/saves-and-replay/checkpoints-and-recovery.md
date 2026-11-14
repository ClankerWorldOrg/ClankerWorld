---
title: Checkpoints and recovery
type: persistence-reference
status: active
updated: 2026-11-14
---

# Checkpoints and recovery

Checkpoint history, backups, save branches and permanent deletion.

## Checkpoints, history and backups

Schema 130 adds the required inventory `ArchivedRepairReservationCount`.
Checkpoint compaction keeps the latest 32 completed or released repair-input
reservations, ordered by their deadline and identity, plus the exact receipts
required by retained cart orders and native hearing remedies. Active claims and
other consumption purposes remain intact. Full older repair records enter the
existing digest-addressed archive before the smaller checkpoint is published
or installed in memory. Loading verifies the cumulative count against that
archive; failed writes leave the previous live ledger and save intact.
Inventory transitions and estate handling preserve the count. New repair
identities still derive from the advancing world clock and complete actor/input
scope. Earlier alpha checkpoints are refused and preserved without migration.
The schema number is provisional until merge.

The active recovery checkpoint is encoded and fsync-written after every advanced
one-second tick. Older history is compacted into digest-addressed segments.
Hot event lists are bounded, but long-term segment retention is not yet measured.
[Starting-site reachability measurements](../starting-site-reachability-measurements.md) records native
checkpoint write costs for both supported world sizes; it does not measure
long-term history growth.

Before replacing the active checkpoint, its writer loads the exact encoded
bytes through the current codec. A serialization or validation mismatch refuses
the write and keeps the previous checkpoint. This adds one codec read to each
checkpoint write. Historical-law evidence uses the same Unicode-safe excerpt
rule for inspection and exact-source validation; an emoji crossing the limit
is omitted whole. Source identity, version, conduct time and actual inspection
receipts remain required.

An alpha checkpoint already damaged by a split law excerpt is refused and
preserved. No wording repair or migration is attempted. Return to an earlier
usable manual or recovery save, keeping the damaged file as evidence. Healthy
current-format checkpoints retain their schema and roundtrip behavior.

On private-server startup, `WorldStartupRecovery` holds a refused active
checkpoint without constructing a replacement world. A readable current-format
checkpoint identifies the affected world even if an interrupted selection left
the catalog behind. Otherwise the existing catalog, adjacent autosave settings
or authority identity supply that identity. Pairing and signed recovery status and
restore requests remain available. Middleware refuses other owner-world routes
before dependency binding, and the simulation and catalog warm-up services wait
for recovery. No model work, checkpoint retry, autosave rotation or world selection
runs against the refused checkpoint.

The offered candidate comes from the world's current save branch, ordered by
branch position rather than a possibly corrected clock. If play continues from
an older save and its new branch has no autosave yet, recovery considers only
autosaves at or before that loaded point. Candidate metadata, current-format
checkpoint, world identity, saved tick, model routing, autosave interval and
rotation, and every required history segment must verify. Model preflight includes
the saved routine helper's selected named key as well as agent assignments.
Load World rechecks that key's availability even when its structural checkpoint
proof is cached; a missing named key never substitutes the installation default.
A damaged newer autosave is retained and skipped. No usable
candidate leaves recovery pending; named saves and other worlds are not automatic
fallbacks. An older alpha schema receives a format explanation, never a migration.

The owner signs the offered autosave ID. Recovery rechecks it under the world
mutation gate, verifies that the refused active bytes have not changed, fsyncs a
private `*.damaged.<id>.json` copy, then applies the autosave through the normal
paused runtime, routing, autosave and save-branch boundaries. Other snapshots are
unchanged. Publication remains atomic; a failed operation restores the prior
routing, schedule and timeline and, if checkpoint publication was attempted, the
refused active bytes. Preserved copies remain even after a failed retry. A second
failure during rollback still requires operator recovery.

Successful recovery discards earlier owner challenges before exposing the new
runtime. The client requests a fresh observation timeline and never resumes it
implicitly. A lost response offers a status check rather than an automatic second
restore. Preserved refused checkpoints also remain conservative history roots:
history reclamation refuses to delete segments while any retained root cannot be
verified. This adds no checkpoint fields, schema change or automatic retention
policy. It does not extend the crash guarantee to storage or power-loss behavior
beyond the existing per-second checkpoint.

Named manual checkpoints use a private `.manual` directory and reference the
same history archive. Overwriting a selected checkpoint retains a recovery copy;
new copies have explicit recovery provenance and an opt-in count-based cleanup
preview, described under [permanent deletion](#explicit-permanent-deletion).

Creation, autosaving and overwrite decode and validate the exact serialized
checkpoint before writing save files, publishing metadata or advancing the
timeline. A rejected checkpoint leaves the previous saves and timeline intact.
Law amendment and repeal labels preserve complete surrogate pairs when shortened;
the full structured draft remains unchanged. This adds no schema fields or
migration for previously damaged alpha saves.

Rotating autosaves are a separate
mechanism and must not delete another world's checkpoints, or another branch's.
Updating autosave configuration trims only that configured world, including
rotation off, and counts each branch's autosaves separately.
Rotation and startup recovery share one check of the checkpoint, world identity,
saved tick, model and helper keys, autosave settings, required history and full
runtime restore. Only copies that pass count toward retention. Refused copies
are preserved and reported, so an unusable newer copy cannot displace a usable
predecessor.
The schedule persists a new interval anchor if the server clock moves backwards,
so restart cannot restore the resulting delay. This changes only the adjacent
schedule timestamp; it keeps the saved world tick and existing snapshot files.
Rotation uses the saved branch position to keep that branch's newest copies,
even when earlier copies have future UTC dates. Genuine pre-branch saves still
rotate by UTC; saves with damaged branch fields remain preserved.
The signed action names the world whose settings were opened. Its identity
check, configuration write and rotation share the world-mutation gate with
selection and loading. A request delayed across a world switch is refused
without changing either world's settings or checkpoint copies.

Load and overwrite validate required manual-save metadata before creating a
recovery backup or changing the active world. Malformed JSON, missing save
records or invalid required fields return a controlled conflict and preserve
the original files. The save list skips these same invalid entries.

Manual overwrite first writes an immutable checkpoint generation, then
atomically publishes its metadata pointer with the matching model assignments
and autosave settings. Failure before publication leaves the previous selected
pair intact. Current-format checkpoints whose metadata lacks a generation
pointer remain readable.
After publication, the previously selected checkpoint file is removed; the
separate **Before overwriting** recovery copy keeps its original bytes,
assignments and autosave settings. A locked or unwritable old file stays on
disk with a structured cleanup-pending warning, while the published overwrite
remains successful. Unpublished generations are retained; this retirement does
not sweep unrelated files. Older binaries do not understand this pointer and
must not load newly overwritten
saves. Use a matching pre-upgrade backup for rollback.

Startup recovers an interrupted world selection from the active checkpoint.
It restores that world's model assignments and autosave settings before
publishing the recovered selection in the catalog, so an interrupted recovery
is retried on the next start.

The world catalog keeps a small terrain thumbnail for each world, packed like
the world's own terrain (`terrain-kind-v1`) and at most 96 pixels wide, so Load
World can show it without reading the world's checkpoint. Worlds catalogued
before thumbnails existed get one the first time they are listed, from the
checkpoint that listing already reads. It is a convenience copy, not world
state: an older server ignores it, and a missing or damaged thumbnail shows a globe
while the rest of the world list stays available.

A full recovery backup must keep together:

- The active save and the referenced `.history` archive.
- Named and rotating saves in `.manual`, with each world's branch record.
- The adjacent `.autosave.json` schedule, `.build.json` record and saved-tick metadata.
- The world catalog and its archived worlds.
- Pairing authority, protected provider configuration and usage accounting.

Keep this material private. A world save by itself is not an installation backup.
Trim a rotating snapshot only after a newer usable copy exists. Damaged metadata
entries are isolated from listing so sound saves remain reachable; preserve and
report the damaged file rather than silently removing it.

For an incompatible rollback, restore the matching older application and
pre-upgrade save/history. An older server is not expected to read a newer schema.
The September 2026 internal-identifier reset was an explicitly approved
pre-release fresh-save/new-pairing exception. It never permits deleting saves.
During alpha an older save may stop loading, but it is refused with a reason and
kept. Finished releases follow the migration promise in [Saves](../../game-design/saves.md).

### Which build saved a world

Each named save and autosave records the `gameVersion` and `sourceRevision` of
the server that wrote it, in its `.meta.json`. Overwriting a named save records
the overwriting build; saves from before this have neither field. The first
checkpoint a server run writes also records that build in
`<checkpoint>.build.json` beside the active save, so it can be read without
loading the world. When startup refuses a checkpoint, the recovery status
includes `savedByVersion` from that file, and the game names that version in
its "different save format" message. Each world catalog entry in
`catalog.json` records the build that last wrote or opened that world in the
same two fields, so any version can list who saved a world without restoring
it; the server stamps an entry when it creates, archives, selects or starts with
that world. When a listed world can't be restored and its entry names another
version, its compatibility is `other_version` instead of `incompatible`, and
Load World offers to open it in that version through the launcher. A damaged
save from this build, a world from before versions were recorded, or a missing
model setup stays `incompatible`. These records are diagnostic only: they
never change world state, replay digests or whether a save loads. Unusable
optional build fields read as unknown. Recovery rollback restores the refused
checkpoint's prior build record (or its absence), and a successful retry stamps
the recovering build again
([releasing](../releasing.md#save-and-content-compatibility)). They let the
launcher offer the version that last saved a world
([#1566](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1566)).

### Save branches

Loading an older save and playing on starts a new branch instead of mixing two
histories in one list ([design](../../game-design/saves.md#agreed)). Each save's
metadata records its branch (an opaque ID, a number for display and the save it
started from), its position within that branch and the save it continued from,
with that save's creation time.
A per-world record in `.manual` (`timeline-` plus a hash of the world ID) says
which branch and save the running world continues from. Loading a save moves it
to that save; every new save moves it to the new save. These are save metadata
only: checkpoints, events and replay are unchanged.

The owner's client reads the record for the timeline's **You are here** marker
through the signed `save-timeline` control action
(`POST /api/v1/owner/saves/timeline`). It answers with the save the running
world continues from, that save's branch and time, whether the next save starts
a new branch, and the branch number the next save will use even after deletions.
The answer comes from the same checks saving uses, so the two cannot
disagree, and reading it changes nothing. A server from before this action
answers 404, and the client then leaves the marker out.

A new save continues the recorded branch unless something else on that branch
has a higher position. Positions advance even while paused and survive deletion
of intermediate saves, so equal world ticks do not mix separate histories.
Otherwise the save starts a branch numbered
one higher than any the world has used. Overwriting a slot moves it into the
running world's branch; its recovery copy keeps the old branch and position.
The fork decision uses the loaded save before rebinding it to that recovery copy.

Before loading, the server saves the world being left as **Before loading**, so its
unsaved progress stays on its own branch. After a successful load, the record
keeps a fingerprint of the loaded world after the server's required pause, as well
as the stored checkpoint's fingerprint. It skips the copy only when the world
still matches that loaded state, the stored checkpoint remains readable and
unchanged, and model routing and autosave choices match. Browsing running
autosaves therefore does not create empty branches, while a missing or damaged
checkpoint cannot replace a recovery copy. If loading fails, the branch record
is restored along with the world.

A manual load first records a private `pending-load.json` intent in `.manual`,
naming the selected save, its before-load copy, effective autosave settings and
the previous timeline. The intent stays until checkpoint, model assignments,
autosave settings and loaded timeline all commit. Startup finishes that intent
before opening the active world, including loads within the same world. If a
load fails, its intent switches to rollback before restoring the before-load
copy; an interrupted rollback therefore resumes in the same direction. The
source snapshots remain unchanged. While its intent is pending, the shared save
mutation gate refuses snapshot changes and world switching, including requests
already admitted before shutdown. An unfinished live rollback stops the host;
an unreadable intent or failed recovery stops startup and preserves the files.
Include this intent in an installation
backup; older binaries cannot resolve it, so complete recovery before rollback.
No world schema or replay event changes.

The branch record is written before a new save's metadata is published, so an
interruption leaves it pointing at an unlisted save whose position still keeps the
next save on the same branch. A missing or damaged record never blocks saving:
the next save starts a new branch, with a warning in the log. A save whose own
branch fields are damaged stays listed, without a branch. Saves made before
branches existed have none either; Load World groups them as **Earlier saves**,
and playing on from one starts a new branch. Autosave rotation leaves saves
with damaged branch fields alone, since their histories cannot safely be
grouped; genuinely branchless earlier autosaves still rotate together.
Deleting a world removes its
branch record with its saves.
## Explicit permanent deletion

Signed deletion binds the target kind, exact ID, world ID and (for snapshots)
creation timestamp. It shares the installation mutation gate with world
selection and snapshot writes. The active world cannot be deleted.

Deletion reads verify the metadata's save identity against its filename before
using it. A missing save record or mismatched identity keeps cleanup pending
instead of preventing the healthy server from starting. These checks do not
require playable model settings or a valid display name just to remove an
otherwise identifiable checkpoint.

Whole-world cleanup preserves save IDs recorded in metadata as belonging to
another world, including their unreadable checkpoint generations. It does not
decode those files just to delete the requested world. Leftover files without
such metadata still need a decoded world identity before removal; an
unreadable unidentified file keeps cleanup pending.

Snapshot metadata is renamed to a deletion intent before owned generations
are removed. World removal first moves its catalog entry into pending deletion,
then removes its snapshots and archived checkpoint. Pending targets are not
loadable; startup retries cleanup. Storage failure is reported rather than
acknowledged as complete. Corrupt metadata whose ownership cannot be established
is preserved and can leave cleanup pending. Do not roll back to older binaries
while deletion intents remain: they cannot perform this recovery.

History reclamation verifies all remaining active, archived and manual checkpoint
roots and their digest-addressed chains before removing unreferenced segments.
Unpublished generations conservatively count as roots. Corrupt roots defer history
cleanup, preserving other saves. This is ordinary file deletion, not secure disk
erasure, and does not remove copies in external backups.

Recovery cleanup is a separate signed `POST /api/v1/owner/saves/recovery-cleanup`
action. Its canonical payload binds `preview` or `apply`, world identity, a
count from one to ten and the exact preview digest. It requires the current
world to be paused. There is no background cleanup or persisted enabled setting;
the client initially offers a provisional count of three.

New overwrite backups and before-load copies with a named source record that
source's opaque save ID and a monotonic recovery sequence in adjacent metadata.
Sequence orders copies even when the clock moves backwards. Names never identify
recoveries. Missing, malformed or unclassified provenance confers no deletion
authority and does not prevent loading an intact checkpoint. Explicitly overwriting a recovery makes the selected slot a named
manual checkpoint again. Copies without a named source remain ordinary saves.
The world checkpoint schema and replay bytes do not change.

The preview decodes each classified checkpoint, verifies its world identity and
required history, and performs a full runtime restore without advancing the world
or calling a model. Only copies that pass all these checks count toward the latest
requested number for each source. A copy that decodes but cannot restore remains
preserved and cannot displace a loadable predecessor. The preview additionally
protects the recovery currently continued from;
an older active copy may exceed the requested count. Unverifiable files, manual
saves, autosaves and migration originals are never cleanup candidates. Earlier
unclassified backups remain protected even if their names look like recoveries.
An existing damaged timeline refuses cleanup because the active copy cannot be
verified; it does not block saving a new checkpoint.

The preview digest binds the listed metadata, classified checkpoint bytes,
verification results, timeline and exact kept/removed IDs. Apply rescans under
the shared world-mutation/save-store gate; a mismatch refuses deletion before
any checkpoint is removed. Confirmed cleanup uses the existing durable deletion
intents. Shared history segments remain in place, protecting every retained
checkpoint and migration original. Interrupted cleanup is reported
honestly and startup resumes only those already confirmed deletion intents.
