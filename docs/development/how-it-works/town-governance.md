---
title: Town governance and land
type: architecture
status: active
updated: 2026-11-14
---

# Town governance and land

This page describes marriage history, Town governance and land cases.

## Towns, building sites and death

### Marriage history

Marriage keeps its original mutual-consent conversation, partnership snapshot
and surname receipt after separation or death. A nullable `EndReceipt` records
the actual committed partnership revocation or death, including its original
effective time. Ordinary `partner_leave` and the paused developer edit both
reconcile the ending immediately; committed deaths reconcile before new
personal choices. An unfinished surname conversation closes without another
model call or a rename. Rejected ticks retain the previous marriage state.

Only a current marriage blocks another proposal or couples player surname
changes. A later marriage requires fresh ordinary face-to-face consent and
its own surname session. Personal context prefers a current marriage and
otherwise the latest ending; public profiles retain every past marriage.
Validation refuses overlapping marriage intervals, an ending without its
committed partnership or death, and inconsistent surname receipts. Current
spouses must share their recorded surname; former spouses may rename or marry
again without rewriting the old result. Later deaths cannot replace an
earlier separation receipt. See [saves and replay](../saves-and-replay.md).

### Town membership

Paused founder setup creates one First Town when the owner accepts its five-building
site. Founders become residents; Start World changes founding state to founded.
Later placement inside the saved border establishes residence; walking does
not change it. A newborn joins its primary caregiver's Town; death removes the
resident. Joining another existing Town later needs that Town's council (below), except deliberate resettlement of an abandoned Town. Owned-building placement establishes household membership.

Founders and added adults arrive at a seeded age from day 15 to day 25 in
day-lifecycle worlds. `SocietyRules.FounderArrivalAge` orders that range once
per world seed and gives the founder in each placement position its day, so
the four founders never share a day, the same seed repeats their ages, and a
founder placed after an undo gets the undone position's age. `AddAdult` draws
an added adult's day from the world seed and the number of agents already
recorded, living or dead, not from the client's agent ID. The saved birth tick
holds the result, so loading never draws again. Year-based development worlds
keep the 18-year adult age.

`TownSystem.TownFounding` offers an explicit personal `found` civic
choice to a living adult on buildable unclaimed land. The stable candidate ID
binds the world seed and current site. Execution rechecks the adult, location,
other Town borders and titles, and private property after earlier choices.
The first layout uses `TownBorderRules.Around`, including the ordinary spare
margin around incorporated household buildings. Another household's building
or field refuses the site. Existing owned buildings and fields retain ownership
and receive footprint use rights under the new title; founding creates no
building, Road or inventory lot. The adult's Town care group moves under the
one-Town rule, and governance starts from its living adult residents. Later
visitors use ordinary admission. A built House alone never founds a Town.

Each `TownRuntimeState` carries its own `TownCouncilState`. `TownCouncilRules`
implements all-adult and representative councils from recorded living adult
residents, independently of geometry and household affiliation. Automatic
representation starts at four adults, so the ordinary four-founder Town opens
its first election on day 1. At three or fewer adults, representation and an
initial all-adult election end; the all-adult council governs until it has four
adults again. Candidate-failure retries use the same four-adult minimum and the
ordinary day-or-material-improvement wait. A separate
`FoodStewardship` remains the household-food steward prototype.

Household food-policy ballots last 120 ticks and can pass through their saved
`ExpiryTick`, inclusive, with a strict majority of the remaining eligible
electorate. After that tick, resolution closes the ballot without changing the
food policy, even if a death reduces the number of approvals needed. Resolution
records its tick and adopted or rejected event, then clears the pending ballot;
save/load preserves the original deadline and votes.

Council candidacy and election ballots enter through fresh-admission action
definitions, after the existing personal provider and admission guards. Their
codecs, planners and named capability policy use the same TownSystem checks for
offers, accepted plans and native application; TownSystem retains all writes.

Town law, policy, proposal votes, government proposals, office consent and
mayoral ballots also have fresh-admission definitions. They reuse the existing
TownSystem planners and checks, preserve bound law wording and original terms,
and apply through the original civic writer after the same personal guards.

Notice visits, reading and relay, Town founding/resettlement and admission have
fresh-admission definitions too. They keep the original notice place/history,
recipient, founding site and dependent group. The visit policy admits non-infants;
existing reason checks restrict child visits to known non-land cases. Child case
visits retain their offered plan and use the same check before native walking.

Town and household land claims/responses, permission transfer proposals/responses
and sale collection/exchange have fresh-admission definitions. Their named
capability policy feeds the same checks used by offers and native entry points.
Admitted plots and terms are bound on the retained plan before checking; the
original TownSystem writer keeps the existing rights, consent and payment steps.

Land-hearing filing, evidence, judge consent/ballots, rulings, reopenings and
property responses use fresh-admission definitions with one named capability
policy and the existing TownSystem checks. Accepted statements, plots and
property terms bind on the retained plan before its original revision, authority
and personal consent are checked for native application.

Non-land filing, evidence, judge consent, findings/reopening and restorative
agreement definitions retain their original case material and authority. Their
capability policy reads the current actor age, preserving child evidence and
reopening rules. Voluntary work and known-report relay retain the selected
contribution or source; pure planners and shared checks serve offered, admitted
and continuing steps, followed by the original TownSystem effects. Existing agreement
consent permits physical continuation; a new agreement needs fresh admission.

Town project proposals and donations have fresh-admission definitions; their
load, work and recovery definitions retain the existing ordinary phase. Each
keeps its original site, budget, selected goods and quantities. Named capability
policies feed the existing checks, including urgent personal returns, before
the original native effects apply. Project names bind on the retained proposal.

Internal project-input delivery, pickup, harvest and waiting share one native
definition. The existing continuation passes its selected input, owner and site
to the planner once, then checks and applies that retained step. It keeps the
public project goal ID and has no personal-action decoder. A dedicated internal
phase requires the typed plan, so it cannot grant fresh property authority.
Native building goals without a selected site and recipe goals awaiting a
workstation have typed goal plans in the same definitions as their placed plans.
Goal checks preserve the original goal IDs and start-only age rule. Existing
native continuation selects and rechecks the physical target when it is ready.
Normal offers keep their selected site/workstation, and subsequent native
material/work steps retain their checks.
Every registered plan now checks through its definition; the legacy check switch
has been removed. Shared age/need/route rules and offer caps remain under audit.

The civic engine keeps final proposal votes, continuing candidate agreements,
opening voter/candidate lists, latest election ballots, cutoff runoffs, settled
seats, fair draw order, ten-day terms and retry snapshots. A failed election may
retry after one world day, or sooner when adult/candidate availability improves;
withdrawals and departures do not themselves reset that wait. Council revisions
cancel pending proposals without altering settled decisions. Admission requests
merge by subject identity; ordinary text requests merge after case/whitespace
normalization. Roster changes or independently supplied material circumstances
allow earlier proposal reconsideration.

`WorldRuntime.Governance` advances each Town on the normal tick path and
rechecks authority when applying choices. `civic|...` actions let actors visit
the public notice place, read posted notices, relay them within interaction
range, nominate another resident, register their own consent and choose proposals
or ballots. Personal-model context includes at most three learned notices, with
270 UTF-16 units per readable notice and 1,024 units in total. Both excerpts keep
complete Unicode characters while the full notice text and read/relay receipts
stay unchanged. A nomination posts a notice; only the named agent's personal response
can add agreement. An adult with no Town, or a resident of another Town, may
request their own admission at a Town's notice place. An adult with no Town may
walk there from anywhere, and a resident of another Town from inside its border,
whenever a tile beside the notice place is reachable on foot. Residents may
request admission of an unaffiliated adult nearby. The optional
`civic_proposal`, `civic_ballot` and `civic_land_tiles` structured response fields are carried only
through admitted choices. Missing, stale or malformed responses cannot supply
votes. A generic private thought or another actor naming a candidate supplies
neither agreement nor approval. No polling provider calls are added, and Jev is
optional.

An adult resident may submit a land claim with up to 64 exact tile coordinates
in one connected plot adjoining the Town's recorded title. This is a bounded
provider payload, not permission to rewrite title. Because the model sees no
map grid, the claim choice names the agent's own tile and up to six unclaimed
tiles beside the Town's title, nearest first. An empty `civic_land_tiles` array
is no plot rather than a malformed reply. The server rejects water,
off-map, duplicate, disconnected and already titled tiles. The saved proposal
uses the ordinary Council majority and notice-reading rules; its identity
comes from the canonical coordinates, so reordering them does not open another
ballot. A successful vote rechecks the whole plot, records title and includes
those tiles in the Town border in the same prepared tick. An overlapping claim
that is no longer eligible is cancelled. Household rights, structures and
inventory are unchanged. Ordinary law prose cannot execute a land claim.

Household land requests use the same bounded `civic_land_tiles` field for
already titled land. The choice names the agent's tile and up to six free Town
tiles nearest first: no use right, pending request, building, expansion, road
or field.
Filing refuses tiles the household already holds and other owners' buildings,
running expansions and fields with no recorded right; another household's recorded
right is contested as a dispute.
A nonconflicting request opens an ordinary `land_use`
Council proposal. Its canonical key includes the household, exact tiles and
optional agreed end date. Filing posts a notice but supplies no acceptance.
Each current adult in the household must learn that notice and explicitly
choose `accept_land_use`; Council votes are separate. Grant settlement rechecks
the current adult roster, competing rights/requests and foreign fields. An
older pending request cannot grant another household's worked field merely
because its Council vote and household acceptance have finished. A dispute leaves the
request pending; refusal, withdrawal, an elapsed requested term and a Council
change that cancels the vote close it, and the household may ask again.
A grant preserves its Council proposal, individual consent records and the
adult roster at settlement. Closed requests remain in the save history but do
not contribute competing claims to the map or Add Agent.

Ordinary and ordered field-site choices and authoritative tilling exclude land another
household holds or has requested; unclaimed land and the farmer's own household
land keep their ordinary physical requirements. New Roads and bridge entrances
exclude every household's held or requested tiles. Routing may still join an
existing Road or reuse an existing bridge on claimed land, but it rechecks
both banks before adding a new crossing. These construction checks do not
change walking permissions or remove existing infrastructure.

`request_expansion_land` derives the extra House tiles from a currently legal
larger footprint, so agents can request them before gathering materials.
Expansion start and completion require a Town-assigned building's added tiles
to have that Town's title. A House needs household use rights on any titled
added tile, even if it has no Town assignment; shared Town buildings need title
and land no household holds or has asked for. An expansion asks the Council
once: while any of its shapes has a pending request, no other is offered. New
construction likewise avoids land other households hold or have asked for, and
Town buildings avoid all of it. Unaffiliated construction on untitled land keeps its ordinary physical
rules and creates no title. Losing permission cancels the
job and releases its materials. Existing expired rights are not silently
removed; ordinary Town advancement opens an expiry hearing and keeps the old
permission provisional until a lawful ruling.

`TownRuntimeState.LandHearings.Transfers` records voluntary transfers of exact
existing permission plots. Published right versions, terms and party snapshots
are immutable. Every current active adult or elder in every source and receiving
household needs an actual notice receipt and a separate admitted personal-model
acceptance. Filing and reading supply no acceptance. Added adults must learn and
accept too; consent belongs to the exact household, so moving households cannot
reuse it for a different party. No routine Council or mayor approval is required.
Expiry, changed source rights, disputed claims and open hearings block completion.
A decline or withdrawal closes the request without transferring permission.
Completion stores the actual final party roster and one replayable bounded
adjustment, preserving original grant terms and all outside pieces. Title,
membership, buildings, crops, inventory and private-building access stay intact.
Owner projection distinguishes current pending rosters from completed-transfer
rosters, showing names, clocks, actual awareness and accepted adult counts. All
pending transfers and eight recent closed ones are projected; the full ledger
remains saved.

`TownRuntimeState.LandHearings` saves each public case's exact plot and right
versions, affected parties, formal notice and response deadline. Equivalent
filings join the live case without resetting its clock. A different plot that
overlaps a live case is refused. Land a settled case covered needs reopening
grounds until its permission terms change; an agreed end reached after that
case's last ruling still opens its expiry review. Material right changes
or new affected adults publish a revised notice with a fresh day. Notice
publication, actual civic receipts and case-file reads are separate records;
reads identify the evidence and rehearing requests actually available to that
adult. Physical relays carry only material their source learned. Owner inspection
does not supply agent knowledge or private memories.
Notice revisions retain their published party snapshots. The shared pure
`TownLandCasePartyRules.CurrentParties` derives current adult responders and Town
representation from the same captured rights, request masks, agents, civic
authority and clock used by the runtime. Death, departure and succession update
current standing without rewriting the original notice. Owner views label these
roles separately, and each ruling projects its actual closure party roster.
A property case whose building or ownership can no longer be captured keeps
its last valid notice and asset snapshot. It cannot transfer stale assets or
publish a new notice without a matching snapshot; current standing and judge
conflicts still apply. Malformed saved notice numbering is refused before
property evidence lookups.
Property requests and hearing outcomes retain complete canonical household
references, including households founded by later-generation native adults.
Runtime admission and saved records still require known households, affected
parties and eligible recipients. Offered civic choices use the existing bounded
household token; property records keep the full ID.
Saved footprint rights and requests retain that same known household reference.
A generated household party key uses the shared derived-identity limit and hashes the complete key when needed,
while its separate household field keeps the complete identity.

Hearing actions enter through admitted personal-model civic choices. Public
statements remain allegations; actual nearby observations and inspected rights,
title, law versions and earlier rulings retain their sources. Ruling-choice
context includes the four most recent inspected evidence records and any required
inspected property record, each excerpt bounded to
160 UTF-16 units without splitting a Unicode character. The full text, its sources
and the inspection records stay saved. The current land
mayor must inspect the file and revalidate authority, conflicts and the response
window before ruling. A permission past its agreed end must be renewed, amended
or ended; it cannot be confirmed or left as it is. A renewal renews every lapsed
permission on the plot for its own household, so several lapsed households need
no chosen winner. Ending is not offered for an unrepresented household. An amend ruling reassigns
existing permissions and the pending requests it heard; other free Town land
stays free. A conflicted holder steps aside for a willing independent
adult elected through the shared civic scheduler for this case only. A valid
acting judge survives ordinary mayor succession; missing authority leaves the
case pending. Reopening needs assessed material evidence or a demonstrated
procedural error, retains earlier rulings and publishes a fresh notice. Grounds
are judged against the latest ruling, and only while the case is settled. A
settled case with no rehearing request offers its file to adults who have not
read its ruling or whose plot's right, title or law has changed since, and offers
its parties nearby observations and a rehearing request; statements and relays
resume while a request is pending. A Council-approved Town filing that
can no longer open when its vote passes keeps the vote and posts the reason.

Bounded permission adjustments retain original grant receipts and replayable
before/after rights. A ruling records the exact covered portions of pending
requests, so resolved tiles stop contributing claims while remaining portions
stay pending. Permission hearings leave Town title, membership, buildings,
crops and inventory unchanged. Owner projection carries all active cases and pending
rehearing assessments plus eight recent settled cases; complete ledgers remain
in the checkpoint. Town, plot and property views use public names, sources and
world clocks. Lifecycle telemetry carries identifiers, status and counts, with
no statements, model replies or private memories.

An explicit `property` land case instead binds a whole existing building and
its physically present shared lots to each notice. `TownPropertyRequest` is
part of the Council's exact filing authorization; a governing mayor uses its
recorded office. Empty membership supplies eligibility, never ownership.
Membership relationship history and birth records identify living former
members even after they join another household. `TownPropertyConsent` records
each person's informed agreement or refusal separately from response waivers.
New assets, quantities or claimants require a fresh notice and new consent.
The independent judge cites the inspected snapshot before `reclaim` or `grant`.
The ruling commits a distinct `property_ruling` footprint adjustment, building owner and actual
inventory transfers together, retaining full before/after transfer receipts.
Containers move with their contents; condition, freshness and physical location
are preserved. Personal and carried goods are excluded. Active work, deliveries
and reservations block completion. A grant needs a prior Town recovery ruling,
an eligible receiving household and every receiving adult's personal agreement.
The owner projection shows readable asset and consent details in the existing
Town, plot and property hearing views. Personal owners keep limited entrance
collection access after recovery and onward granting.

Building inspection labels property rulings separately from permission-only
cases and keeps the current building owner above the historical outcome.
Result-event text resolves the exact ruling ID within the event's Town to choose
property or permission wording. Missing case context uses a neutral land-ruling
description; event prose does not establish ownership or case kind.

`TownRuntimeState.Nonviolent` records non-land allegations, notice revisions,
sources, responses and civil findings separately from permission adjustments.
`TownHearingProcedure` supplies shared response, conflict and case-election
rules. Founding supplies the vacant non-land mandate; standing for election
requires personal agreement. Later scope changes need protected resident
approval. An extension names its existing office and retains that office's
original term; its added authority begins only at the actual handover.

Native completed actions capture applicable law wording, title and membership
at the time of conduct. Actual observations and communication chains determine
which agents can report them; the ledger itself supplies no global awareness.
Case-linked records survive event compaction. A passed `law_case` Council
proposal authorizes the exact report, without deciding its truth. Current
authority, conflicts, notice and the freshest case file are checked again when
an admitted personal-model response executes.

`TownRemedyRules` keeps offers, exact terms, each contributor's informed answer,
agreements and completion receipts. Accepted terms create no forced plan or
reservation over another person's goods. Existing physical delivery and repair
paths supply receipts only after their actual changes commit. One receipt can
credit only one term, and the saved effect cannot exceed that term's remaining
quantity. Declined, unanswered, pending, overdue and completed states remain
distinct. Findings and overdue work never create physical punishment powers.
Goods-return source selection checks each carried lot against the remaining
agreed quantity before ordering eligible lots by identity. The actual transfer
uses that same remaining quantity; a pinned source, reservations, personal
ownership and beneficiary capacity remain binding.

Counteroffers during renegotiation follow the exact prior-offer chain back to
the nearest agreement. After every contributor accepts, the replacement names
that agreement and supersedes its original offer; intermediate countered offers
remain recorded. Only the new remaining commitments are actionable. Earlier
physical receipts stay with the original agreement and cannot earn credit again.
Strict saved-state validation checks the same predecessor chain.

Formal civic acts require an admitted personal-model choice. Built-in decisions,
failed replies and continued intentions supply no votes or candidate agreement.
An explicitly chosen visit can continue moving locally; ballots may be revised
on the agent's ordinary decision cadence.

Long ancestry-based agent IDs use stable SHA-256 aliases in civic model action
tokens. The runtime resolves these against current agents before checking a
ballot; saved candidates and choices retain the actual IDs.

Land-hearing readers, evidence sources and submitters, judges, affected parties,
personal stakes and live or historical election rosters keep their complete known
saved agent identities, including later-generation adults born in the world.
Person rosters retain canonical order and uniqueness without applying the
derived-identity length limit to complete person references.
The shared civic identity check rejects blank, padded and control-character
identities; caller-owned evidence, record and authority keys and prose keep
their existing bounds. Current-world identity, notice and authority checks
still apply.

Saved notice receipts enter a bounded `CognitionSelfContext.CivicNote` excerpt
with read/relay provenance and readable names/world days. An actor receives no
unseen civic dump. Owner observations project each council, its latest eight
proposals, the current election and the latest archived election onto the normal
Godot Towns page. Failed and cancelled outcomes remain visible. The checkpoint keeps eight recent
elections plus elections referenced by government changes. It keeps 32 recent
notices, each agent's three recent learned sources with their relay receipts,
and notices needed by retained elections or other civic systems. Older election
outcomes and notices move to the durable history archive before retirement.
The complete authoritative proposal ledger stays in the checkpoint. Long Town
readouts scroll within the available screen height. A passed structured law
proposal records its scoped wording without creating physical or ownership
powers. Bounded civic lifecycle telemetry records Town identity and
council/vote/status counts without proposal text, notices, names or per-read
polling noise.
