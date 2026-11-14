---
title: Commit and restore
type: persistence-reference
status: active
updated: 2026-11-14
---

# Commit and restore

Commit, rollback, restoration and current-format replay requirements.

## Commit and restore rules

Canonical state is the accepted state used by the simulation. A replay checks
that recorded events reproduce its expected results and digests.

- Stage tick changes before accepting them; rejected work leaves no partial state.
- Version and atomically replace saves after committed changes.
- Validate externally loaded state and map identity before accepting it.
- Reject unsupported, mismatched or corrupt data visibly; preserve the original
  file and valid current world instead of silently substituting content or keys.
- Include replay coverage when state, events, schemas, generation or replay
  semantics change, so a save in the current format still loads and replays.
- During alpha, older saves do not have to keep loading, and no migration or
  old-save handling is written only to keep one working. The rule above still
  applies: a save that cannot load is refused with a reason and kept. Finished
  releases promise forward migration later, as described in
  [Saves](../../game-design/saves.md). The cutoff and removal audit are recorded in
  [issue #487](https://github.com/ClankerWorldOrg/ClankerWorld/issues/487).
- Keep build revision, release labels and telemetry out of canonical digests.
- Never infer compatibility merely from the public game version or file age.

## Conversations and saved choices

The world checkpoint stores the conversation cursor (revision, status,
next speaker and consent/interruption state) together with the full admitted
history: up to six accepted public turns and one accepted wrap-up, including
the actual listener IDs for each. Each later speaker receives the committed
public history so far, not an unaccepted reply; the wrap-up request receives
the six public turns. Each participant's current-day allowance is saved too.
A live provider request is never saved. On restore, an accepted unfinished
conversation becomes suspended and cannot spend again until both participants
make fresh resume choices, including when a saved suspension contained one
person's earlier choice. A pending invitation keeps its original deadline and
requires normal acceptance, which counts against the invitee's daily allowance.
Conversation records use world schema 35, following schema 34's fields
and ground harvest lots. No migration for older alpha saves is added solely to preserve
compatibility.

World schema 120 adds optional goods prices to voluntary permission
transfers and actual goods payment to their settlement receipts. The notice and
consent token include the immutable price. A paid request stays pending after
all current adults accept until physical payment and permission settle together.
Receipts retain the buyer, seller adult, meeting place and exact inventory
transfer event IDs and quantities; retained events must match the accepted terms.
Current-format checks cover pending and completed reload, declined sales,
refused-step rollback and paired byte-identical continuation. Older alpha saves
are refused and preserved without migration. This number is provisional until
merge.

World schema 106 adds `talk_to` order bindings: stable `TargetAgentId`,
optional `TalkConversationId` and the completed `TalkOutcome`. Validation binds
an active task to one actual ordinary invitation from its actor to its target;
several tasks cannot share an invitation. Completion requires a closed outcome
and the matching deterministic task receipt, with no duplicated progress.
Completed and cancelled task links may outlive bounded closed history.
Restored accepted conversations remain stopped until both people make fresh
resume choices; proposals retain their original acceptance and deadline rules.
Replay checks cover normal generated-map approach, proposal and completed
reloads, halfway resumption and rejected invitation/completion ticks. The
number is provisional until merge. Older alpha saves are refused and preserved
without migration.

World schema 107 adds `read_knowledge` task bindings. The optional
`TargetKnowledgeArtifactId` is stable across waits; `KnowledgeReadCompletion`
records the actual read tick and at most nine distinct, sorted newly learned
sites. One deterministic receipt credits a successful task. Validation checks
the target kind, frozen written membership and the actor's actual read
provenance at that tick. A later personal observation may update the site
without undoing its completed read task; historical completion can outlive a
removed artifact only after all credited sites have later accounts. Active
missing targets remain blocked. Completed tasks cannot be credited again.
Tests cover native paid artifacts and ownership, paired replay/reload, urgent
food, cancellation/replacement, exact targeting, full/partial knowledge limits,
malformed progress and rejected read ticks. This number is provisional until
merge. Older alpha checkpoints are refused and preserved without migration.

World schema 117 adds `propose_marriage` orders using the existing
`TargetAgentId`, `TalkConversationId` and `TalkOutcome` fields. A selected partner
stays bound; an order without a partner must have neither a conversation nor
progress. Linked orders identify one actual ordinary invitation between their
actor and target. Completion requires a deterministic `marriage-order:` receipt
and the actual closed outcome: `refused`, `proposal_declined`, `not_proposed` or
another native unsuccessful outcome. `married` additionally requires completed
native marriage and surname records. Retained marriage consent also verifies
closed order outcomes after ordinary history is trimmed. Accepted consent without a finished surname
leaves progress at zero. Existing marriage validation protects both consent and
surname receipts. Reload stops accepted ordinary or surname conversations,
clears one-sided resume choices and requires both personal models again.
Cancellation and replay preserve actual consent without duplicating task or
marriage effects; rejected ticks admit neither. The number is provisional until
merge. Older alpha saves are refused and preserved without migration.

World schema 118 adds typed default estate laws to Town law drafts
and version history. The bounded Town share, exact prospective wording,
Resident-duty scope, Council proposal and historical version must agree;
missing shares, mismatched effects and overlapping rules are refused.
At settlement, the deceased resident's saved Town and the law history at their
death derive the default division. No extra mutable estate-policy snapshot is
saved. A valid will wins, and adoption, later amendment or repeal cannot change
an earlier death's division. The number is provisional until merge; schema 117
and older alpha saves are refused and preserved without migration.

World schema 119 adds the `Permanent` flag to private self-subject
experience records for player renames. Each keeps the accepted name, original
world tick and world-day wording. Same-tick rename IDs include a saved world-event ordinal,
so repeated changes and retries remain distinct and deterministic after reload.
Loading refuses a rename record whose permanent marker is missing or false,
that is tombstoned, public, cross-owned or dated in the future. The source
history remains saved independently of bounded model/UI lists and compaction
scores. Name and memory publish together, including both changed spouses;
refusal and retry preserve checkpoint bytes. This number is provisional until
merge. Earlier alpha schemas are refused and preserved without migration.

World schema 84 adds required marriage records and conversation kinds.
Each marriage retains its accepted partnership snapshot and the ordinary
conversation's separate mutual marriage consent. Its surname session admits
at most four alternating choices from the original surnames that fit both
accepted names within the existing 48-character limit. Later player renames
cannot invalidate those choices while the session is unfinished; loading
refuses such a checkpoint too. Marriage eligibility and saved acceptance names
also check the limit after Unicode normalization, so an accepted receipt cannot
have an empty surname choice list. Invalid replies, failed calls, pause and
cancellation admit no turn. A completed receipt records the result, completion
time and whether the seeded draw was used; validation rechecks the draw against the seed and consent identity.
Receipts survive ordinary conversation compaction, including a pending session
closed because a participant became unavailable. Active unfinished sessions
still require both partners' fresh resume choices after loading. Later player
surname changes retain the original result and record the latest player
change separately; validation requires both spouse names to match the current
surname while that marriage is current. Tick rollback keeps both names, consent and surname history unchanged
and can admit the completed reply later without issuing the call again.
Marriage schema 84 follows equipment-storage schema 83. Older alpha files
are refused and preserved without migration.

Routine history compaction validates the compacted checkpoint without applying
load transitions. It preserves the live conversation cursor, consent and pending
turn admission identity. Resume with compaction likewise keeps the conversation's
existing pause state; only an actual load suspends it as restored and clears
previous resume choices. A failed checkpoint write leaves the live state unchanged.

## Family, messages, trade and health

World schema 40 saves each agent's explicit domestic family unit
and primary caregiver, plus the caregiver, intended home and actual birth home
for an agreed parenthood plan. Birth records retain the caregiver and actual
household. House resident counts and limits are derived from active household
members, recorded family units and the completed building footprint; they are
not separately mutable counters. Current-format roundtrips preserve pending
family decisions and births without inferring a family group from ancestry or
household membership. Older alpha checkpoints need not load; no migration is
provided.

World schema 41 records each owner's exact pending message, target,
submission identity, whether a personal model observed it, and its optional
short reply. The one-fresh-decision prompt tick is scheduling state, not a read
receipt. Accepted replies stay attached to the original message ID; pause,
reload or a stale result cannot transfer them to a newer message. Current
schema saves require the instruction and completion records and validate their
target, ordering and tick bounds. Earlier alpha instruction records need not
load; no message migration is provided.

World schema 45 records each physical shop exchange beside its inventory
offer: the shop, holding household, customer, transaction position and time,
item kinds, seller who completed it and any cancellation reason. Inventory
offers retain the exact quantities and lot reservations. Pending exchanges and
Store delivery lots survive save/reload without granting customer access to
private stock. Earlier alpha saves need not load; no shop-state migration is
provided.

World schema 47 records household departures, their original household, care group and once-only food allocation, plus optional physical inventory custody separate from ownership. Personal House storage retains the personal owner even after membership ends. A recorded departure allowance may also remain in a Store belonging to the former household until physical collection. Validation binds its lot, owner, edible kind and quantity to the departure; unrelated personal Store stock remains invalid. Death and successive settlements preserve that allowance through the archived departure, exact frozen lot and settlement IDs and matching Store location. Accepted wills may use the saved household fallback when a named heir is unavailable. Validation caps the combined Store portions at the original departure allocation. The same Store’s validated prior-household transfer history preserves the binding after Town reclamation and an onward grant, without authorizing unrelated personal stock. Every departing care-group member's unfinished House delivery restores household ownership and clears the former destination while preserving its quantity and physical carrier; leaving earns no delivery credit. Reload preserves collection rights, borrowed carried goods, the care group and unfinished housing task without awarding another allowance. Paused private jobs retain the original owners, exact input reservations and pause time; a leaver's own cart build is cancelled instead, releasing its carried materials. Their reserved workstation or expansion footprint stays occupied; a remaining authorized member can resume at the physical site with the same materials and remaining duration. Unavailable materials cancel the preserved job and release its remaining commitments. Invalid custody, departure records and unavailable carriers are refused. Older alpha checkpoints are refused and preserved; no migration is added.

World schema 51 lets each living agent record named adult medical
permissions and an active medicine course. The course binds its patient and
caregiver to the actual completed inventory reservation for the consumed dose,
including the supply lot, owner and start time. Strict validation checks
permission, identity and progress; it refuses a mismatched or already closed
receipt. Completed or interrupted courses close that receipt's medical purpose
without releasing or refunding goods, so renewed permission cannot restore a
spent effect. Death cleanup preserves completed receipts while releasing live
claims. Archived physical profiles retain permission history but cannot retain
active treatment. No migration is provided; older alpha saves are refused and
preserved.

World schema 93 adds a caregiver's optional `MedicalSupplyTrip`, naming
an accepted patient, their last-seen map position and the start tick. It keeps
an already started medicine fetch and return trip through reload without
exposing the patient's current distant location or health. Validation requires
a living adult caregiver, an accepted living patient, a map position and a
non-future start time; deceased profiles cannot retain trips. Permission loss,
death, successful treatment or finding the remembered place empty clears the
trip. Supplies remain ordinary inventory, and treatment still consumes one
actual usable dose beside the patient. Earlier alpha saves, including schema
92, are refused and preserved without migration.

World schema 52 adds an optional exact ornament-lot selection to the
existing personal equipment record, without introducing combat equipment or a
second inventory. Current-format checks reject a foreign, reserved, stored or otherwise
ineligible selected unit and preserve intermediate refining, diamond setting,
gifts and barter. Removing or giving the ornament clears the selection while
keeping the actual item; death and estate handling retain the property without
an active selection on an archived profile. Older alpha saves are refused and
preserved; no migration is added.

World schema 54 introduced optional admission records to each Town,
one per passed admission proposal of that Town. The Town resident lists stay
the only record of membership; an admission record says what one approval did to them:

- `approved`: a resident asked for the newcomer, and the approval waits for the
  newcomer to accept before its deadline. It keeps the newcomer's Town at the
  time of the vote, and acceptance is refused if that has changed.
- `admitted`: membership changed. It keeps the Town the newcomer left, if any,
  and the sorted IDs of the newcomer and the dependent children who moved.
- `lapsed`: the approval could not be applied, with the reason `unavailable`,
  `already_resident`, `affiliation_changed`, `joined_elsewhere` or, in schema 78,
  `acceptance_expired`.

Schema 78 gives a sponsored approval one unpaused world day for acceptance. Its
deadline is derived from the saved passed proposal's original `SettledTick` plus
the configured `TicksPerDay`; no separate deadline field is stored. The approval
expires at that tick, and its admission record keeps the lapse reason and actual
settlement tick. Pause, save and reload preserve the remaining world time. An
adult's own passed request still admits them immediately. The owner projection's
optional `AcceptanceDeadlineTick` is derived display data, not checkpoint state.
Earlier alpha saves are refused and preserved without migration.

A passed admission is settled as soon as the council decision or roster change
that passed it is saved, so every passed admission has exactly one record and
reload and replay never apply an approval twice. Loading, and the runtime's own
state check, refuse a passed admission without a record, a record without a
matching passed admission proposal, a repeated proposal, an unknown status or
lapse reason, an unsorted or incomplete moving group, a decision time before the
vote settled or after the saved world time, and an approval still waiting on
someone who is already a resident there. Older alpha saves are refused and
preserved; no migration is added.

## Load and recovery boundaries

Checkpoint decoding enforces declared non-null members and required constructor
fields before runtime validation. A missing society, cognition or inventory
object is invalid data, not an unexpected null-reference fault. No saved list
holds empty entries, so decoding refuses a null entry in any list in the
checkpoint before the typed records are read; the serializer's non-null checks
cover members, not list entries. A saved world size other than Small or Medium
is refused too, since only those sizes can be created. Compatibility
assessment marks that inactive world incompatible while retaining healthy list
entries. Selecting it fails before replacing the active world; the damaged file
stays available for recovery. Optional fields retain their declared defaults.

Signed pause, resume and rename retries persist the requested state before reporting
success, including when the in-memory value already matches after a failed
write. Storage failure remains an error. A paused world's Resume stages the
running checkpoint under the runtime gate, saves it, and only then commits the
running state in memory. A failed write leaves the original pause, epoch and
events untouched; a fresh signed retry can recover without relying on a later
tick. Resume still requires a started world and valid usage allowance. Other
recovery paths do not resume time implicitly.

Before a potentially committed create/select/rewind request, the client invalidates
older observation requests and asks for a fresh baseline. It keeps the last
confirmed view when the server advertises observer timelines; legacy servers clear
the held observation. If the receipt is lost, reconnect starts from a fresh
baseline while retaining normal regression and terrain-identity checks within
that timeline. Continue does not resume an uncertain world switch.
Other connected devices detect that change through transient observer metadata
and fetch a fresh baseline too, including for same-world rewinds. The runtime
captures that metadata with the committed state; it is never written into a
checkpoint and does not change saved world identity or replay. See
[observer recovery](../device-pairing.md#recovering-after-another-device-loads-a-world)
for ordering, cache and retained-request boundaries.

The internally captured proposed tick reuses its committed map, declarative
content, world systems and world-content simulation snapshots by reference.
An environment write replaces its immutable snapshot; committing transfers the
environment system, while discarding the proposal leaves committed state alone.
Restore initializes saved services directly without running new-world genesis.
External loads still fully validate state and regenerate the map; this shortcut must not weaken input
validation. Missing/corrupt referenced history must fail closed. See Issues for
current defects rather than treating these requirements as proof of every load path.

World and manual-checkpoint selection preflight the actual referenced history
before replacing the active save. Save-list metadata alone is not enough to
establish that the checkpoint can load.
