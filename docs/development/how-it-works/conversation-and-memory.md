---
title: Conversation and memory
type: architecture
status: active
updated: 2026-11-14
---

# Conversation and memory

This page describes conversations, memory, written knowledge and exploration.

Each agent retains up to eight private thoughts and exposes up to sixteen recent
non-forgotten social memories to owner inspection, including deceased profiles.
A separate belief record retains firsthand/hearsay/inference evidence and
superseded corrections. Inspection never broadcasts these records or turns
them into public events. Validation indexes the complete belief history by ID,
owner and source turn, resolving correction roots once. Source-turn checks
use those indexes instead of rescanning every belief for every heard turn.
Known-owner, reporter, subject and bidirectional correction checks still run;
unrelated duplicate turns and dangling or cyclic correction chains are
rejected. The indexes are rebuilt for each validation, are never saved and
discard no beliefs or provenance.

Ordinary conversation is a server-controlled activity, not free-form world
commands. The normal provider router requires an explicit personal planning
assignment for the current speaker and sends only that speaker's identity and
the accepted public history. A proposal and acceptance each use that agent's
saved daily allowance. The server admits at most two pending conversation calls
per world tick and six alternating public turns per conversation, followed by
one wrap-up. Responses must match the saved conversation revision, run epoch,
request and speaker; stale or malformed replies are not admitted. Public turns
carry a bounded list of agents who were actually close enough to hear. A
separate memory extraction gives those listeners hearsay claims tied to the
turn ID, with duplicate and owner checks. Listener and belief-owner references
retain complete native agent IDs, including longer identities created by
birth; they do not share the length bounds for caller-provided belief IDs and
prose. Canonical identity and known-owner checks still apply.
Personal-model memory subjects and belief reporters use the same stable hash
alias for a longer native identity, as map-knowledge discoverers already do.
Short identities remain unchanged. Saved social-memory and belief ownership,
subjects and reporters keep their complete identities and evidence links.
Memory salience indexes retain the same complete native owner identities while
validating each indexed source against that owner's existing records.
Helper score replies retain these owner IDs too; admission requires the exact
requesting agent and one of its offered source records.
Private thoughts, provider payloads
and unaccepted replies never enter that history. Prose has no world effect.
Ordinary dialogue can propose mutual trust or, for eligible partners, marriage
during wrap-up. Either structured effect requires both participants to accept
the same proposal.

The saved revision, status and next-speaker fields are the conversation cursor.
The checkpoint keeps the complete admitted history (up to six public turns and
one accepted wrap-up), and each later provider request receives the committed
public history so far. The wrap-up request receives the six public turns; a
pending answer is never included or saved.

Conversation participants and speakers retain their complete native agent
IDs too, through proposal, personal model routing, requests and saved history.
These references use canonical identity and known-world participant checks
instead of prose length limits, including for longer identities created by birth.
An ordinary conversation's generated handle combines its proposal tick with a
SHA-256 digest of the ordered pair of complete participant IDs. This keeps the
handle and derived turn IDs bounded without shortening either participant reference.
The scheduler's choice token hashes the complete actor, handle, revision and
status too. Its size guard stays independent of native identity length, while
the same exact context checks still reject replies for an old choice.

Owner snapshots include every unfinished saved conversation and the 16 most
recent closed conversations, ordered by last update and then ID. New snapshots
and reconnects use the same projection, including each session's admitted public
turns. This display bound does not change the 64-session save limit or mutate
conversation records.

Saving by itself keeps an unaccepted invitation pending, with its original
deadline and the inviter's saved daily allowance. It has no current speaker;
only acceptance assigns the first turn. When the 64-session save limit is
reached, the oldest closed session is removed before a new one starts. A
listener's private hearsay claim, its correction links and any memory-compaction
reference remain saved. Only the link from that private claim to the removed
public turn is cleared; its statement and speaker attribution stay private to
its owner, while the old turn text leaves the shared conversation history.

Pausing, disconnect, a provider failure, urgent need, separation or an
unavailable participant interrupts or closes the activity according to its
state. A save contains accepted bounded turns and daily allowances, not a live
provider task. Restore leaves accepted active conversations suspended and clears
any earlier resume choice; no model request starts until both participants choose
to resume again. A pending invitation retains its original deadline and still
requires normal acceptance and the invitee's daily allowance. Both agents receive
the same public wrap-up and proposed effect before accepting. Lessons wait while
either participant is talking. Conversation logs record only purpose, bounded
turn count, latency and usage totals, including reported usage for rejected speech.

Personal requests retrieve at most four own-memory/belief excerpts and sixteen
recent own-map facts. Social memories protected by `SocietyMemoryArchiveRules`,
including final words and permanent rename experiences, take precedence, newest first,
including before the 256-source scan limit; ordinary sources retain their
relevance and salience ranking. The owner's bounded 16-entry Memories projection
also keeps permanent entries first and marks them Permanent. All rename source
records remain saved; bounded lists do not archive or delete them.
Excerpts use at most 160 UTF-16 units and keep complete Unicode characters;
the saved source text and evidence stay unchanged.
An existing Jev routine call may score up to twelve previously unassessed
ordinary records; permanent entries are excluded from scoring and cannot fade
through a low assessment. Candidates must fit the writer's latest-256 scoring
window, including scores already saved for protected sources, so the helper
does not repeatedly assess entries the index cannot retain.
Only linked salience/confidence values are saved.
The same Jev or OpenAI Decisions request may include a `memory_summary`
choice over source-derived recent, earliest or varied extracts, plus
`keep_records`. It adds no separate paid request or free-form model prose. Local retrieval works with
Jev off. On an unpaused world-day boundary, `SocietyMemoryArchiveRules` moves
ordinary experiences and beliefs at least three world days old and below 2,500
importance basis points out of the active ledger into required owner-private
archive lists. Unscored records count as zero. These constants are provisional.
Archive entries retain the complete original record and archive tick; ordinary
recall and memory-scoring candidates read only the active ledger. Typed life,
relationship, skill and commitment records, records marked `Permanent`, durable cooperation/final-word
receipts and world-event-backed beliefs stay active. Active orders, projects,
lessons (including their teachers), requested or preparing parenthood plans,
relationship proposals, barter,
conversations and outstanding model calls protect their owners; surname
conversation turns stay protected after closing. A completed, postponed or
cancelled parenthood plan does not protect its owner's ordinary old records;
other open work and protected kinds still do. Correction links and source
validation include both ledgers, while trimming old dialogue clears obsolete
turn references in both. The daily rule makes no provider call, runs with Jev
and the routine helper off, and commits or rolls back with the native tick.
Its event contains only archived experience/belief counts. Deceased historical
profiles inspect both ledgers without sharing that evidence with living agents.
Authoritative life,
relationship, skill and order state is unchanged. Automatic experience capture
remains unfinished.

`SocietyMemorySummaryRules` offers four to twelve uncovered ordinary archived
records owned by that actor, at least three world days old and below the same
importance cutoff. Corrected, permanent and event-backed records are excluded.
Each option combines two labeled excerpts into at most 160 characters and is
shorter than its complete source batch. The helper chooses only an offered ID;
the server saves its exact extract and canonical typed source links. It can decline
or omit the choice. Invalid, failed, cancelled, stale or personal-model replies
cannot write a summary. The server rechecks the batch and attribution before
applying an awaited reply. No original record is deleted or replaced.

Saved owner-private summaries compete with active source records for the same
four-excerpt recall budget, including the deceased agent's final will request.
Each recalled summary includes bounded source
attribution: subject, firsthand/hearsay/inference, confidence, speaker, event and
correction marker, resolved from the owner's original ledger. A later correction
marks the source corrected without changing the saved extract. Switching the
helper off prevents new summaries but preserves existing recall. The summary
event contains only owner and source count. Timing and usefulness remain
provisional; see the [memory summary playtest](../../../playtest/alpha-1.md).

Childhood talk, play and learning record owner-private experiences whose keys
fit the same 128-character memory boundary used by recall and Jev scoring.
When the complete actor/target/time key is longer, a SHA-256 key retains the
action prefix used by the social cooldown. The saved owner and subject keep
their actual identities. Short keys and existing saved records stay unchanged;
records with oversized or malformed keys remain excluded from recall.

The three-person candidate limit counts reachable people who contribute an
available talk, play or learning action. A person whose applicable actions are
all on cooldown consumes no slot; distance and stable identity still order the
eligible targets. The 120-tick per-action cooldown and age restrictions apply
both when offering and when executing an action.

Project assistance records gratitude with a key that fits recall's 128-character
boundary. Oversized keys hash the complete requester/helper key with SHA-256,
retaining the `project-gratitude:` prefix used by social standing and durable
memory protection. Short keys, full saved owner/subject references, gratitude
text, source time and material-help trust effects remain unchanged. Existing
oversized records are retained without rewriting and remain excluded from recall.

Exploration records personal knowledge; it does not create free inventory.
Each completed outward or return scouting step observes its actual arrival tile;
blocked steps do not observe the requested destination. An actual observation
of a previously known tile refreshes that agent's terrain
and resource account when it changes. Planting also records the planter's own
work. Unchanged observations do not add another fact or learned event, and
changes do not teach distant agents. The current ledger keeps one fact per
agent and tile, within its existing 128-site limit.

Written artifacts and unfinished writing keep the exact facts captured when
writing started. Earlier observations are retained only while those snapshots
need them, bounded to 81 versions per agent (eight nine-site artifacts and one
nine-site writing project). Copies preserve the held source's account even if
the reader observes a change before or during copying. The reader must already
know every source site and recipe; copying does not replace their current observations.
Cancelled writing prunes versions with no
remaining snapshot. Earlier versions validate provenance but are not offered
as the agent's current map knowledge.

Households make paper at an authorized House from physically delivered fiber
and fresh water in a reusable jug. The provisional batch uses two fiber and
one water to make two paper in sixteen work ticks, leaving the jug intact.
The writing reserve counts healthy household sheets and sheets collected or
reserved by eligible writers. Paper on an actively borrowed household Market
stall stays on sale and does not prevent making replacement paper.

An adult can write a field record, draw a map or bind a book from
contents they have actually learned. A record holds at most one site and one
recipe, a map at most nine sites, and a book at most nine sites and nine
recipes. Recipes may be the only contents of a record or book.
The provisional writing costs are one paper
and four work ticks for a record, one paper and six work ticks for a map, or
two paper, one cloth and twelve work ticks for a book. Each artifact holds at
most nine sites. Writing reserves real personal materials and creates one
distinct physical lot only when the work completes. Saved progress and
material reservations survive temporary interruptions and reload. If the
writer or copying source becomes unavailable, or the artifact limit is
reached, the unfinished work releases its unused supplies. The agent's Profile
shows the writing or copying progress.

Completed production adds a recipe account only to the actual worker, bounded
to 128 recipes per person. It retains the recipe definition, completed job,
original worker and learned tick. Reading or sharing a held written work adds
only its recipes, retaining that original production evidence and the actual
written source. Copying requires all source contents already learned and fresh
paid writing materials; it preserves the source's account. It grants no skill
and changes no worksite, tool or recipe permission. Model self context and
Profile cards show at most the person's 16 newest recipe accounts. A household
or Town holds no shared recipe ledger.

The bounded owner catalogue recognizes `write_knowledge` for records, maps
and books, with an explicit count up to 1,000 or repeat-until-cancelled.
Orders use the native fact selection, duplicate-account, access, carrying and
paid-work rules above. A matching ordinary writing job can be adopted; another
kind or a copy remains intact and blocks the order. Each owned live project
binds the exact current instruction and the instruction binds that project.
Only its completed physical artifact credits one unit. Cancellation and
replacement release only the bound project's unspent inputs. Counted or
repeating work waits for new learned contents after the current account has been
written. Queuing and urgent interruptions use the existing order lifecycle.

The catalogue also recognizes `copy_knowledge` with bounded counts and repetition,
using the native held-artifact copying path. The adult must own and
carry the exact source and know all of its sites and recipes; order handling does not read
it or teach missing knowledge. The source stays pinned during supply collection
and writing. Source loss releases unused native reservations and clears the
project binding, while keeping the source pointer for a later retry. A completed
paid copy carries the instruction ID, credits one unit and clears both live
pointers. The native duplicate-account and artifact limits still apply.

Reading or sharing a held artifact teaches only its recorded sites and recipes to the
actual recipient, retaining the original discoverer and the source artifact.
Copying needs the source, learned contents and new writing materials; sharing
does not create another physical copy. Barter transfers the existing lot and
teaches its recipient, without granting access to unrelated knowledge or
anyone else's private stock. Two artifacts of the same kind can be exchanged
when each records sites or recipes its recipient has not learned; the existing consent,
ownership, reservation and delivery checks still apply.

Scouting has no fixed eight-step turn-back. `explore` continues a curiosity
outing; the separate `explore_return` choice starts a return to its origin.
These choices use the existing 30-tick reconsideration cadence and ordinary
guidance-triggered requests, not a new request for each movement step. An
ongoing return can also continue through `explore`, preserving its waypoints.
The starting warmth estimate keeps its eight-step trial budget and grows with
the recorded return distance. If that budget no longer permits more outward
travel, only the return choice remains; existing urgent survival rules still
interrupt either leg.

`explore_for:resource:<kind>` and `explore_for:terrain:<kind>` offer purposes
from the agent's food needs or missing inputs of an unpaid, unbound project.
Supported material needs can offer Forest for wood or Mountain for ore; no
unseen resource location chooses the purpose or ranks neighboring steps.
Curiosity remains available outside active projects. `ExplorationState.Goal`
saves the bounded purpose and, for untargeted food/material orders, the exact
instruction ID. Strict loading checks supported targets and the matching
owner, action and requested kind; an explicit resource or coordinate keeps
its existing precedence and never gains a substitute.
An already chosen ordinary outward purpose can keep moving while its next
personal model reply is pending; the saved purpose identity, live offered
candidate and ordinary survival/movement checks still apply. Owner activity
summaries name an untargeted order's current physical purpose while retaining
the earlier model intention ID; local order execution need not replace it.
Returning, interrupted, blocked, cancelled and replaced goals do not label a
current search.

An order search hands back only when its normal task candidate is usable,
including actual observation or personal knowledge, source availability,
tools, carrying room and an open route. An ordinary resource purpose likewise
uses current observation or owned knowledge and the normal source checks.
Project-material searches also require the harvested load to return to
household storage or the actual worksite, so an unusable source across water
does not suppress or finish the search. A terrain purpose finishes only on an
actually reached matching tile. A full
personal fact ledger does not block either observation or invent a saved fact. Finishing clears the
outing path and purpose, retaining discoveries and recording
`exploration_goal_found`; it awards no harvest progress. Return, blocked-return
abort and interrupted movement clear the purpose. Cancellation cannot make
an old order purpose authoritative for a later outing. Urgent interruptions
retain the pending goal and instruction through the existing lifecycle.

Outward scouting checks building walls, occupied destinations and both diagonal corner tiles
before ranking neighboring exits. An attached cart also restricts exits to
ordinary legal cart steps: cardinal movement without unroaded mountains.
If no legal outward exit remains, the scout
uses the existing return path instead of repeatedly targeting a blocked corner.
Only completed movement adds a visited tile.
An outward path never repeats a tile and is bounded by the map's tile count.
The recent visited/discovery lists keep at most 256 entries; the existing
personal knowledge limits are unchanged. Validation rejects looping or
oversized saved paths and retains historical bridge checks for actual steps.

If intervening legal movement interrupts outward scouting, a new outward path
starts at the actual position without inventing missing steps. On the return
leg, recorded visited tiles are waypoints: the actor routes from its current
position around occupants without restarting the outing. Completion requires
reaching the original start, including after reload. A return that remains
blocked still ends through the existing bounded wait/abort behavior. Visited
facts remain personal knowledge.
