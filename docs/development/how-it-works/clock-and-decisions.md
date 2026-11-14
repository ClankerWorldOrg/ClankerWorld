---
title: Clock and asynchronous decisions
type: architecture
status: active
updated: 2026-11-14
---

# Clock and asynchronous decisions

This page describes the clock, model requests and owner instructions.

## Clock and asynchronous decisions

Restoring an existing world, including an isolated proposed tick, starts with
its saved world systems. It does not generate a disposable initial ecology,
chunk set or weather state. New worlds still generate those systems normally;
ordinary loads still validate saved data and check deterministic map identity.

The server supports only the world runtime. Configuring the removed
`fixture` world mode refuses startup with a clear error. The scripted one-agent
fixture and its persistence/projection branches are gone; map generators,
terrain codecs and routing remain shared by the native world.

Saved world systems contain weather, ecology and chunks. The unused prototype
faction/law, currency and culture records are removed. Physical goods,
ownership/access checks and actual Town laws keep their existing systems.
Currency and culture remain intended features to build against those systems.
The test-only PNG asset normalizer is also removed; mod import, canonical asset
IDs, reservation budgets and the server-owned approved-asset catalog remain.

The current server aims for one tick per real second. New worlds save 360 ticks
per day and a 40-day year with four ten-day seasons; lifecycle thresholds are
3/15/45/60 days. Newly created playable worlds start at 06:00 on Spring 1,
Year 1. A saved 90-tick calendar offset sets that clock while elapsed world
time still begins at zero, preserving founder setup, seeded ages and elapsed
deadlines. Calendar dates and daily conversation allowances turn over at the
displayed midnight. Load can affect real-time pace. The old development calendar
is not silently reinterpreted; the observation carries the saved clock values.
Its calendar pace includes the saved season lengths and clock offset, so the game names dates
such as Autumn 2, Year 1 from the world's own calendar instead of a copy. With
no season lengths, from an older server, the game shows numeric dates.

Model requests are dispatched after a committed tick and resolved at a later
tick boundary. The unresolved queue entry is saved; the HTTP task is not save
authority. Admission checks the request ID, provider/run epochs and current
candidate legality. An epoch is a generation marker that makes replies from
an earlier configuration or run obsolete. Other agents continue while one waits.

Cognition rejection and ignored-failure events use the current request's world
tick, or the matching retired request's observation tick when none is active.
After restore, where request records are absent, they use the last retained
event's world tick, then the saved intention's tick if the event window is
empty, or zero when neither exists. Run epochs are never used as timestamps.

The OpenAI-compatible adapter can complete one omitted empty final field of a
five-field civic candidate ID when the completed ID exactly matches a uniquely
offered choice, with no other offered ID sharing that prefix. An already exact
offered ID keeps its meaning. The adapter does
not guess a subject, a nonempty choice or multiple missing fields, and the usual
epoch and candidate-legality checks still admit the completed reply.

Owner instructions are suggestions (**Suggest** on the agent card,
`Suggestion`) or orders (**Order**, `Order`). The next ordinary personal
planning request for that agent can include the exact original words with an
outside-observer label. The request contains only messages for its target
agent. Guidance bypasses Jev's routine route, while adults still use their
normal planning assignment or inherited world planning provider. A child
without an explicit personal-model choice and an agent whose planning model is
set to deterministic stay local; neither receives a forced model call.

A submitted message is checked before it changes the world. Its idempotency key
and issuer ID must each be at most 128 characters with no control characters,
its kind must be a suggestion or an order, and its text must be at most 512
characters. A request that fails these checks is refused with a validation
error; the world, its message numbering and its save stay unchanged. These are
the same limits a save applies, so an accepted message cannot leave the world
unable to save.

`OrderTask` holds common progress and an `OrderTaskDefinition` union. Each of
the 50 supported kinds has its own record in `OrderTaskDefinitions.cs`, with
only its target and binding fields. Shared families keep their shape checks on
those records; save validation checks common progress and links to other state.
Task-specific values exposed to runtime and display callers are read-only
projections. Parsers and binding updates construct the matching union record.

`OrderTask` stores its lifecycle as an `OrderStatus` enum. Its one transition
operation keeps blocked reasons and retry waits together and refuses to reopen
closed work. The string status is derived for saves and observations. Runtime
completion comes from that status; only suggestions need a separate completion
set. The checkpoint's completed-instruction list is derived when exported.
`InstructionStore` indexes active orders by agent and submission sequence;
pending-order lookup and replacement use that queue. Restoring a checkpoint
rebuilds it, and replacing an instruction updates it in the same operation.
The runtime retains every active order and the six newest closed orders per
agent, matching the game. It also retains at most six cancellation retries per
agent. Retiring an order removes its order-only construction evidence and
links from native jobs, projects, boat requests and written artifacts; actual
goods, consumed reservations, buildings and native outcomes remain.
Submission receipts and request hashes stay separate so retrying a forgotten
submission returns its original receipt and cannot start work twice. They do
not retain the original task or message text.

## Order completion and pending decisions

`read_knowledge` uses the native held-artifact read. `TargetItemKind` may
constrain records, maps or books; `TargetKnowledgeArtifactId` binds an exact
artifact resolved from a complete ID, lot ID or unique title. An unspecified
source is chosen from eligible personally held items when the step executes.
An explicit source cannot be replaced by another available item while blocked.
The native read returns its actual number of newly learned sites and recipes.
A positive effect stores `KnowledgeReadCompletion` with the reader, bound
source, tick, sorted newly learned site coordinates and sorted recipe identities,
then credits one task through a deterministic receipt covering both collections.
A missing, foreign, reserved or uncarried source, no remaining capacity for any unknown contents
in the item, existing writing work or an account with no new contents
blocks the task. It uses existing provenance and partial-capacity rules for
each ledger; writing, sharing and another person's reading cannot credit it.
Later firsthand site updates preserve the historical completion. Only the
bound artifact identity reaches the order projection; completion evidence
stays with authoritative saves.
`talk_to` resolves a complete person ID, full name or unique first name with
Unicode normalization, excluding the addressed actor. It keeps `TargetAgentId`
even if that person is renamed or becomes unavailable. The native invitation
binds `TalkConversationId` only after normal range, route, provider and
allowance checks. The task holds queued work until that exact conversation
closes, then records its outcome and one attempt with a deterministic receipt.
Refusal is a finished attempt, not consent. Linked orders preserve ordinary
model choices for wrap-up, ending and mutual resumption; an order never forces
an affirmative choice. A replacement talk task also lets an already active
conversation finish before sending its own invitation. Urgent survival remains available. Active links keep
closed history from being pruned before completion is credited. The owner
projection reports the actual phase and saved outcome to the card.

`propose_marriage` recognizes "Propose marriage" and "Propose marriage to my
partner" for adults. It reuses the conversation-order path, binds the current
accepted partnership's other person, and checks `AgentMarriageRules.CanPropose`
without changing eligibility. An initially missing partner leaves the order
blocked; once selected, the target never changes to another partner. Only the
addressed person's ordinary conversation request receives the bounded
`RequestedActivity=propose_marriage` context. The invitation, marriage wrap-up
and surname choices remain personal-model decisions. Speech changes no world
state. Refusal is an honest completed attempt; an ordinary close without a
marriage proposal records `not_proposed`. Accepted mutual marriage consent
holds the task until the linked native surname session completes, then records
`married` and one deterministic `marriage-order:` receipt. Active links retain
the original consent conversation until credit. The owner projection follows
surname status while consent is accepted but incomplete. Cancellation removes
the task without undoing consent, renaming anyone or forcing resumption.

Recognized orders complete only when their requested legal action
actually progresses. Default gathering counts one harvest; explicit quantities
count goods acquired or food consumed. Food-source travel finishes on arrival within
interaction range of the requested food site; exact-tile travel requires the
tile itself. Counted travel is refused.
An unrelated action, blocked movement or unavailable food leaves the instruction
pending, including across reload. A recognized new order replaces the current
non-queued task unless `Queue` is true, preserving the queue. Pending selection
and saved work bindings prefer that current task, then queued tasks in submission
order. Cancel
retains an idempotent receipt, including a no-op cancellation of a closed order.
Receipt-only owner changes supersede a concurrently prepared tick too.
Cancellation matches the complete native agent identity to its saved order,
including agents born in the world. The 128-character bounds apply to caller
keys, issuer, world and order IDs, not native target identities; control
characters are refused in every cancellation identity.
Suggestions do not block orders. A suggestion completes only after the
addressed personal model accepts a request containing it; local choices and
provider failures do not claim it was heard.

New suggestions and recognized active orders trigger one fresh cognition request. Its prompt
marker only prevents a new request every tick; it is not a read receipt. The
same pending message remains available on the agent's later ordinary planning
requests until a personal-model result is accepted. After the fresh request,
`NeedsCognition` applies its usual rules. For example, active agents reevaluate
every 30 ticks, and idle agents reevaluate when their legal choices change or
after 300 ticks. A blocked order therefore cannot request a paid model call on
every tick.

When a model decision is still running, a newly queued choice or observer
message remains pending after its older reply is accepted. The next prepared
tick refreshes that work against the resulting world before another request
starts. Ordinary planning waits while the agent is busy talking; pending work
remains available after the conversation. It also waits for a life-event
identity reply, then uses the accepted identity in its refreshed observation.
Ordinary changes to the clock or need values do
not by themselves request another paid decision. Pending work is kept only for
a choice the earlier request lacked, a change in urgent food need or warmth, or new
observer guidance; choices that merely disappeared leave the accepted reply
valid. An agent whose reply was accepted takes no extra waiting routine in that
tick, and pending work with no call in flight is rebuilt before it is sent.
Work for an order that finished before any reply carrying it was accepted is
still sent unchanged, so the agent can acknowledge it; a pause, a load or newer
guidance rebuilds that work without it. A suggestion carried by a reply that is
set aside because its order has since finished stays open for the next fresh
request. Pending observations survive
save/load and are refreshed for the resumed world before dispatch. The usual
request, provider, conversation and legal-choice checks still reject stale
replies.

Local order steps can continue while a model reply is pending. An accepted
decision and a local continuation do not execute the same order twice in one
tick. If local work finishes first, a valid reply to that exact original
request can still record that the model heard the message. Its old action is
discarded before execution; cancelled or replaced tasks cannot use this path.
Urgent survival uses the normal unrestricted legal candidates before
resuming the task, and repeated eating respects the normal fullness threshold.
Gathering and collecting household food check free carrying capacity before
starting a step. A provider fallback leaves the order blocked for a bounded
normal retry; it cannot strand the task permanently on `safe_idle`.

Pending personal-model work also preserves already chosen non-order physical
tasks. `ApplyContinuingIntentions` continues existing repairs, accepted lessons,
field work, projects and bounded travel. Outstanding social or civic choices do
not by themselves stop an existing project or lesson while its model is waiting;
the normal physical access, illness, consent and fresh-choice guards still apply.
Important civic acts, family and ownership changes, new projects and lessons
cannot be repeated through the pending-task path. Existing civic, Market,
ornament and donation intentions continue only their ordinary physical walk.
Built-in urgent food/warmth takes priority; otherwise a waiting adult may feed
or tend a needy dependent already within interaction range, using actual goods
and the normal care transition without accepting a new caregiver relationship.
A waiting routine and an ordinary continuation take at most one action per
agent in a tick, and an admitted reply excludes that agent from waiting routines.
These transitions use the prepared tick and the existing save/replay format;
no provider reply, paid retry or failure event is fabricated for an ordinary delay.

The owner snapshot sends every open message, plus the six most recently
submitted closed orders and, separately, the six most recently submitted closed
suggestions for each agent, whether or not a personal model heard them. An
order the game could not act on, or one carried out by local rules, therefore
still appears on the agent card as closed and not heard, and heard suggestions
cannot push the latest closed orders out. **Your messages** shows up to four
messages per agent, newest first by submission but always preferring open
messages over closed ones, then lists them in the order they were sent. Newer
closed messages therefore cannot hide an order that is still waiting. The
card's order line and the **All orders** reader read the same snapshot: the
oldest open order is the current one, the other open orders follow in
submission order, and closed orders come newest first. The client keeps no
order list of its own. The save keeps every message; only the snapshot is
bounded.

Pause, quit and loss of presence cancel external work without inventing an
answer. Restore can retry a still-relevant saved decision. Synchronous fixture
methods remain for isolated tests; they are not the live scheduling path.

Tick-boundary faults in live play discard the failed proposal before publishing
its checkpoint. The server retries isolated copies with groups of agent actions
withheld, bisecting only after a failure. Received replies stay frozen across
these trials, and trial worlds make no checkpoint writes. If withholding one
actor's action alone makes the entire tick and save check succeed, the server
refuses that action, makes the actor wait and cancels their current order. A
saved refusal blocks the same choice for that actor for one world day. The
Event Log explains the refusal, Developer tools show the cumulative count,
and the server records a bounded check name without exception text or paths.
Healthy ticks make one attempt and one checkpoint preparation and write.
The strict setting `ClankerWorld:Clock:StrictTickFailures` keeps test and CI
servers halting on the first failure. Global faults or failures that cannot be
isolated to one action hold the last good state and pause the world.
Active checkpoint I/O/access failures retry only the retained save while paused;
success still requires a later explicit Resume. Other faults latch for operator
inspection. Startup corrupt-save failures remain separate. Logs use bounded
category/type/tick fields, not raw exceptions or file paths. Do not restart a
held process before preserving its unsaved state. Filesystem fault injection
does not establish arbitrary mid-tick rollback or crash durability.

The Godot map keeps unsaved local motion per agent, animal, boat or
handcart marker. Each short reported move retargets a one-second linear glide
from its current drawing; it never predicts a future tile. A separate local
quarter-second phase selects the approved walk frames and one-pixel bob for
agents/animals. The same `WalkingMotion` code feeds the implemented reference
animation. Pausing freezes elapsed glide and pose time; a paused
relocation, a move longer than three tiles, a new world or a checkpoint rewind
snaps and clears the phase. Wrapped targets use the nearest world copy and
camera/zoom changes retain intermediate tile coordinates. Pointer targets,
names, conversation badges and the selected agent card follow the drawn
control. Despawn removes the local entry. The server's positions, routes,
observations, clock and save schema are unchanged.

The Godot World Info page refreshes its date, season, camera-local weather,
soil moisture and calendar separately from its eight count tiles. Advancing
clocks and changing local conditions keep unchanged count nodes in place,
even with the panel closed or its Towns page selected. Current observations
still refresh both sections, so opening World shows the latest data at once.
Changed counts, map dimensions or theme rebuild the count grid and its F1 hint;
this cache is display-only and does not change observation cadence or saves.
