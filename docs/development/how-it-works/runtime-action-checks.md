---
title: Runtime action checks
type: architecture
status: active
updated: 2026-11-14
---

# Runtime action checks

This page describes runtime systems and retained action checks, including civic and trade choices.

`GroundOccupancy` is a read view over the current map, content, placed buildings,
fields, roads, bridges, Town projects, Markets and animals. Its named layers
retain each source's enumeration order. Callers combine the layers in their
existing order and add resident positions or ownership constraints for that
query. Unions reserve capacity from cheap layer counts; unknown-size layers
are read once, and duplicate points keep their first position in the order.
Original building footprints, expanded footprints and protected Port
tiles remain separate choices. Occupancy is recomputed from current snapshots;
no layer cache, saved field or tick phase is added. The immutable declarative
content snapshot supplies one read-only definition index shared by these queries.
Checked footprint layers share one definition lookup; project layers share the
active-building-plan selection while keeping footprints, entrances and Port docking tiles in order.
To add a new obstacle kind, define its layer here and explicitly select it in
the queries whose rules need it.

The approved runtime refactor is tracked in
[#1377](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1377). The runtime
coordinates the simulation through a partial class. `Conversations/ConversationSystem.cs`
owns conversations, daily budgets and marriages in one immutable
`ConversationState`. Every write replaces the affected read-only collection
and its snapshot; the runtime commits the system with one assignment.
Other areas move in separate reviewed steps.

`Farming/FarmingSystem.Fields.cs` owns household fields in an immutable `FarmingState`
and handles field work, crop planning, validation and forestry. Field writes
replace the read-only snapshot. Trusted preparation captures and shares that
snapshot; each proposal has a separate system, and commit transfers it with one
assignment. `IFarmingWorld` supplies the current inventory, residents, land,
routes, environment and event sink per call, through an adapter that stays on
the coordinator. Field and tree orders retain their existing adapters and
private forwarders. Tree planting updates the coordinator's map and the
EnvironmentSystem's ecology and chunks through those world services.

The shared farm demand calculation keeps the first field and its initial
planting eligible while a household has no crop established. Ready food still
reduces the Town shortage used for further fields and replanting. This same
demand check governs offers and retained-plan validation.

`Animals/AnimalSystem.AnimalActions.cs` owns animals, trade offers and physical supply trips
in the existing immutable `AnimalWorldState`. Care, breeding, products, wild
forage, permissions, trade, travel and animal orders run in this system. Writes
replace the snapshot; trusted tick preparation gives each proposal a separate
system sharing that snapshot until a write, and commit transfers the system
with one assignment. `IAnimalWorld` supplies current residents, inventory,
goods queries, routes, ecology and events per call through a coordinator
adapter. The system retains no world or runtime reference. Existing runtime
methods remain private forwarders, including animal-order entry points;
checkpoint capture, identities and tick-phase ordering stay unchanged.
Milk consumption and collection of animal products or wild hides carry typed
plans with their exact stock, owner, quantity, animal, pickup position, route
and any required dismount. One check runs at offer, admission and apply. The
product goods query accepts only the selected ready-product reservation;
collection releases it through the existing animal operation before moving
that stock. A changed stock or animal produces the standard choice refusal.
Animal supply pickup also retains its source, quantity, complete vessel
contents, yard and care target. Its check uses the same supply-demand rule as
offering, and filters before the twelve-choice cap. A successful pickup starts
the existing physical supply trip with the selected load and its stock hold;
trip completion remains a separate operation. Care, taming and saddling now
retain their ordered input lots and quantities, preparation pickup or mount,
and work duration. Taming also retains its yard and current work count. The
same check verifies physical access, named trip holds, meal reserves and reach
at all three action boundaries; native commits keep their existing identities.
Retained animal trips also bind their carried load, family, yard and delivery,
work or release step. A work step carries the same exact work plan. Partial or
already completed work releases its original trip without spending cargo;
delivery checks the named trip hold, fixed quantity, room and route. The trip
query authorizes its matching hold; after native release, that query refuses
the remaining load without moving or consuming it.
Animal mounting and leading also retain their target and selected yard place.
Dismount choices and work/product preparation share a captured cargo plan:
base capacity, current load and exact loose units or complete vessel families.
Checks refuse changed cargo rather than dropping a different load. Native
ride-loss cleanup uses the same planning and commit operations. Dropping a
trip-held load cancels that trip before splitting its stock; dropping unrelated
cargo keeps the trip and its promise.
Animal permission and trade choices retain their recipient, offer, receiving
yard, payment lot and any rider cargo preparation. Shared checks run before
caps and at admission and apply; fresh personal-model consent remains required.
Payment queries use exact uncommitted private stock and match native transfer
access. Acceptance retains its planned leading preparation.

Handcart attach, park and give choices retain the exact cart, cargo family,
attachment and recipient. One check verifies that selection at offer, admission
and apply. Parking only detaches the cart and remains available when cargo is
promised; attaching and giving check the existing cart and cargo access rules.
Cart pulling retains the attachment, cargo and building destination. An absent
route remains a wait choice with the existing movement-blocked events; changed
stock, attachments or destinations receive a typed refusal.
Cart loading and unloading retain the exact source, cart families, physical
place and quantity. Checks share existing loading permissions and food rules,
and match the native cargo operations' hold access and capacity. Order and Civic
callers keep their named-cart and maximum-quantity limits.
Cart repair retains its exact cart and ordered carried material lots, while
repair-input pickup retains its source and cart target. The shared equipment
pickup check verifies access, capacity and reach; repair rechecks its selected
inputs before the existing native commit and receipt.
Cart build-material collection retains its recipe, Blacksmith, stock, place and
quantity. Planning and checking share the full-set eligibility rule; pickup
rechecks the same material and its carrying route back to the selected Smith.
Boat request choices retain their Town, Ports, approach and a boat route proving
geography. Queue and cancel choices retain the exact waiting request. Shared
checks run at offer, admission and apply; native queue processing still assigns
a free boat and dock, including requests made while the destination is full.
Medical permission choices retain their caregiver, prior and resulting consent
lists and any interrupted dose identity. One check runs before the caregiver
cap and at admission and apply; fresh personal-model consent and historical
revocation remain required.
Medicine treatment also retains its patient and exact dose source and place.
The shared dose query matches native reservation access; public treatment keeps
its existing results and lower illness threshold, while offered care uses the
shared medical need threshold.
Medicine collection retains the patient, last-seen position, original trip and
selected pickup dose. Return travel keeps that recorded destination and carried
dose; the shared check never looks up a distant patient's current position.
Conversation choices retain the selected person or conversation revision and
participants. The same check prepares existing pure transitions at offer,
admission and apply; the conversation system keeps its native writers.
Dependent-care authority choices retain the proposal or relationship receipt,
guardian search and intended household/family. The same check runs at offer,
admission and apply; owner guardian orders carry that plan too.
Guardian acceptance also retains the selected House definition and placement
tick, so rebuilding with the same ID requires a fresh choice.
Practical care retains the dependent, caregiving receipt, position and selected
serving or food preparation source. Waiting routines use that same plan; native
feeding, warmth and illness relief remain in their existing writers.
Guardian travel retains the selected child, accepted caregiver/care revision
and exact home. Shared checks allow collection and escort phases to progress,
including infant following, while refusing a changed authority or destination.
Parenthood choices retain their partnership, original request, continuity
deadline and selected caregiver/home. One check controls offer, admission and
application; native preparation, birth and continuity maintenance remain.
Partnership choices retain the selected person or exact relationship and consent
revision. The shared check runs at all three boundaries; existing Society
transitions and marriage cleanup remain their native commit path.
Housing admission choices retain their request, House and adult electorate.
Other members' answers can accumulate without changing that selection; a changed
House, electorate or request receives the same check at all three boundaries.
The House's definition and original placement tick stay bound if its ID is
reused. Expanding that same House still uses the current capacity checks.
Learning choices retain the student, teacher, skill, request and communal lesson
site. The same check governs offer, admission and apply; native continuation may
advance the same agreement while a model reply waits. A pending withdrawal can
still stop that agreement and preserves its latest progress.
Household leave/founding choices retain their household, moving care group and
exact loose-food allowance. Native membership, custody and paused-work effects
use the retained selection; automatic displacement plans its own native step.
Allowance selection and rechecks respect the native transfer's stock holds.
Paused household work retains its job, original worker/time, material reservation
IDs, workstation and selected knife. The same check runs at all three boundaries;
blocked travel waits and expansion checks on arrival keep the native behavior.
Household replacement-care choices retain the child, household and original
primary-caregiver receipt. Shared checks refuse changed care before the existing
Society transition grants responsibility to the selected adult.
Relocation volunteers retain the House, selected adults, replaced notices and
inherited deadline. The shared check refuses a changed selection; the native
notice writer and deadline reconciliation keep the original timer behavior.
Tool-request consent choices retain their Smith, recipe and exact request.
Shared checks run at offer, admission and apply, while fresh accepted personal
model consent remains required for placement, acceptance, refusal and withdrawal.
Pending withdrawal permits progress within that same request and preserves the
current job, offer and payment state when it acts.
Tool-request visits retain the chosen Smith, recipe and route. The shared check
uses that exact selection at offer, admission and application; the native
movement writer keeps its original interaction range and arrival behavior.
Tool commission continuation retains the request, worker, project, Smith and
recipe. Shared checks bind that agreement before the existing project writer
continues its native production step.
Finished-tool collection retains its request and Smith; an on-site quote also
retains the exact finished output, payment and quantities. Shared shop checks
recheck those lots' current availability and stock promises before the native
writer opens the exchange, allowing ordinary processing without choosing other stock.
Civic notice-place visits retain the Town, public destination and route. Their
shared check covers residents and eligible visitors, including children who
actually know a non-land report or case, before the native civic action or its
ordinary walking continuation; arrival still asks for a fresh act. A child's
visit grants no adult civic rights, and urgent food or warmth still comes first.
Council candidacy responses retain the chosen Town, nominee or remainder-term
deadline and the actor's existing agreement. A shared check preserves personal
consent while other residents' registrations can accumulate in the same tick.
Council ballots retain their election round, voting window and eligible
candidates. Structured model choices are bound to that plan at admission;
the same selected IDs are checked again before the native vote writer runs.
Town admission choices retain the applicant, prior affiliation and dependent
group; acceptance retains the exact approved proposal. Shared checks cover
self requests, sponsorship and personal acceptance before membership changes.
Town founding and resettlement retain the selected site or abandoned Town and
the founder's prior affiliation and dependent group. The native writer uses
that selection after the shared check confirms it remains available. Founding
records adopted buildings at the actual founding time.
Public notice reading retains the Town board and recipient and reads its
current contents, including notices published during native council processing.
Saving may archive older notices while a read is pending without invalidating
the selected board.
Relay retains the exact learned notices and recipient. The shared check confirms
the selected history remains available; receipt ordering and sources stay the same.
Proposal votes and withdrawal retain the original proposal, voting window and
electorate. Shared checks allow other votes to accumulate while refusing a
settled, cancelled or changed proposal before its native response applies.
Law adoption, amendment and repeal retain the chosen scope, titled site or
law version. Model wording is bound at admission; the same selected site and
version are checked before the native law-proposal writer runs.
Supported estate defaults retain their law version and share; boat-access
proposals retain their resolved visitor. Their shared checks run before the
native Council proposal writer, with visitor caps applied after eligibility.
Protected government proposals retain their original arrangement or exact
change, voting window and electorate. Shared checks allow other votes and
personal acceptance of the same added duties to accumulate before the native
proposal, withdrawal or final vote writer runs.
Withdrawal follows the same request as its vote opens, preserving the current
voting progress in the native writer.
Personal office choices retain the selected consent, mandate or incumbent
term. Additional duties retain their exact proposal and use the existing
current-term consent check before the native government writer runs.
Mayoral ballots retain their exact contest round, window, electorate and
resolved candidate. The same check allows other ballots to accumulate and
refuses a changed round before the native mayoral vote writer runs.
Town title-claim proposals retain their offered nearby plot options and bind
the admitted exact tiles once. The shared native claim validator checks that
selection before the existing Council and title writers use it.
Replies without a plot are refused before proposal submission.
Nearby plot suggestions do not restrict the selection. Before the reply binds
its exact plot, availability follows the Town's current adjoining unclaimed land.
Household land responses retain the exact request, plot and terms. Their
shared check allows other household consents to accumulate while checking
the actor's personal eligibility before the native consent writer runs.
Opening the request's Council ballot does not change its personal response terms.
Household land filing retains the household and binds the admitted exact
plot once; expansion filing retains its House and selected extra tiles.
Plan checks and native submission share the existing plot refusal validator.
Replies without a household plot bind an empty selection and are refused before submission.
Expansion filing keeps its selected tiles and waits while a feasible alternative
shape overlaps the household's pending request, using native availability rules.
Permission-transfer proposals retain the source terms and eligible beneficiary
plots, then bind the admitted target, exact plot and goods price once.
The shared native transfer validator checks those terms before publication.
Accepted sale collection retains its household stock, exact pickup quantity
and notice place. The same check verifies consent, usable goods, reserves and
carrying room for that selected load before collection.
Sale travel retains the notice place and household role. Payment retains its
exact personal goods lots and quantities and the actual seller at that place;
arrival or a newly arrived seller requires a fresh personal payment choice.
The native exchange publishes its goods and permission changes together.
Knowledge collection and storage retain the artifact, owner, origin and selected
House. Shared checks verify the same custody, permissions, room and physical
route before native transfer or relocation; personal ownership stays intact.
Voluntary knowledge reading and sharing retain the exact held artifact
contents and recipient. One check verifies custody, unknown contents and sharing
range before native learning; automatic traded reading retains its existing path.
Knowledge writing and copying retain the selected contents, source and exact
material pickup or carried input lots. One check validates that same selection
before native reservation; continuation retains the original project and claims
while work progresses. Copy timestamps refresh on the retained content only.
Original writing requires its selected fact versions to remain recorded, including
valid earlier accounts; an unavailable version requires a fresh choice.
Shared-food policy proposals and votes retain the original steward, policy
and ballot scope. One check validates the same personal authority and response;
other votes accumulate while native expiry and living-majority resolution run.
The ordered food-policy definition owns the codec, planner and named capability
policy and calls the same TownSystem check used by offers and native entry points.
After that check, TownSystem alone applies the retained proposal or ballot vote.
Shop offer responses retain the original shop binding and barter terms.
The same check verifies the buyer or current household responder before native
cancellation, waiting or acceptance; changed offer state refuses reuse.
Shop buyer choices retain the selected shop, owner and either an exact goods
and payment quote or a Restaurant meal/ingredient trip. One check validates that
same selection; a trip discovers private stock only on arrival, before a fresh
quote choice reaches the original native offer writer.
Urgent hunger can interrupt an owner order while preserving an admitted
personal shop-food quote or response and its original terms.
Direct barter retains the chosen counterpart and one-unit goods, or the
original offer and personal response state. Shared checks revalidate that same
selection before the native offer, cancellation or acceptance writer; arrival
still waits for the other adult and receiving room.
Market quotes retain the exact seller occupancy, goods and personal payment.
Open-offer responses retain the original exchange terms, claims and arrival
phase; shared checks refuse a changed offer before the native response writer.
Market stock loads, deposits and collections retain their exact lot, owner,
quantity, origin, stall, occupancy and arrival phase. Shared checks preserve
surplus reserves and physical spoiled-stock collection, including jug families,
before the original inventory move or deposit receipt writer runs.
Market borrowing and leaving retain their selected stall, household, personal
qualifying stock and original occupancy. Shared checks keep borrowing at a fresh
personal arrival and send leaving to the same native cancellation/release writer.
Released Town project recovery and return retain the exact parcel, quantity,
release receipt and Warehouse, or the carrier's actual ground-drop position.
Checks preserve one carrying space for food and terrain-only return eligibility
while a pedestrian briefly blocks movement; urgent carriers can set goods down.
Town project work retains its selected hammer and exact delivered budget.
Gathering retains the selected source, harvest tool and yield, or its required
shared-tool pickup; the resulting goods remain personal until a fresh donation.
Both use one check before their native work or harvest transition.
The selected tool keeps its recorded owner even after its carrier leaves that
household; native carried-input custody and hold checks still govern its use.
Town project pickup and delivery retain the selected Town-owned lot, exact
load, current custody and approved destination. One check verifies the same
load against current project need, carrying room and both physical route legs
before the native pickup or delivery writer runs.
Town project donations retain the exact personal lot, contribution quantity,
approved scope and destination. Travel keeps that selected contribution;
arrival requires a fresh personal choice before the native donation transfer.
The same check refuses changed scope, stock or remaining material requirements.
Town project proposals retain the selected footprint, doorway, material
budget and Port, with an admitted name bound once. The shared check verifies
current resident authority, native site and demand rules, and proposal retry rules
before the retained project reaches its original Council writer.
Physical non-land work retains its agreement, exact remaining term, selected
return lot or service load and destination. Repair uses the retained native
cart, tool or equipment plan, then continues its existing reserved work.
Return stock uses the transfer's inventory access rule, so another task's
promise prevents both a new return choice and an already selected return.
Known-act relays retain their listener, personal statement and source chain.
Voluntary non-land terms retain their named contributor, beneficiary, target,
quantity, duration and reasons. Personal responses retain the exact noticed
offer and allow other contributors to accept. Shared native feasibility and
refusal checks validate the retained terms with the IDs that the current
publication counter will mint, so another accepted repair cannot be mistaken
for the proposed contribution. Acceptance reserves no stock or starts any work.
Non-land findings and reopening retain the exact case material, independent
judge and response parties. Admitted citations resolve against the retained
file and reasons bind once. Shared native validators check the same finding,
reopening grounds or assessment before the original history and notice writers.
Non-land judge consent and resignation retain the exact personal consent or
case mandate. Ballots retain the opening round, rosters and selected candidate
while allowing ballots to accumulate; the same check verifies case knowledge
and the native election deadline before the original writer runs.
Native roster progress preserves an original ballot while its selected voter
and candidate remain eligible in both the opening and current rosters.
Non-land case material and responses retain the exact file revision, selected
known sources and historical law, legitimate response party or nearby recipient.
Relays retain all evidence, response, finding and reopening references actually
read by the source. Child submissions use the same native standing rules.
Non-land filing retains the known act, historical law, acquired source, filing
role and actual ordinary office or Council revision. The same check verifies
current source knowledge and legitimate response support before the retained
report reaches the original public-file or authorization writer.
Land hearing filing retains the household, governing office or Council
revision and binds the admitted plot, requested outcome and statement once.
Its shared check uses the native plot, filing and proposal-retry validators
before the retained terms reach the original case or Council writer.
Hearing inspection retains the current case file and noticed revision.
Statements and personal responses retain their submitted text and party identity;
responses still check the actor's current authority when another adult leaves.
Observations retain the visible plot and physical description. Relays retain
the recipient and the evidence/reopening material the source already read.
Case-judge consent and resignation retain their exact case revision and
consent or mandate. Case ballots retain their round, opening rosters and
candidate while allowing current ballots to accumulate or be revised.
Offering, planning and native lookup share the bounded token of the complete
case-revision and ballot-round pair, including long references from founded Towns.
A withdrawn candidate or departed voter does not invalidate other selections;
the retained voter and candidate must remain in the original and current rosters.
The same checks verify current standing and the native round deadline.
Ruling plans retain the noticed revision, judge, affected permissions and
applications, cited material and bound outcome/reasons. Property rulings
retain the actual building/goods snapshot. Shared native refusal validation
checks those inputs before the original ruling and property writers publish.
Reopening plans retain the settled ruling, selected request, personal grounds
or assessment and exact cited evidence. The same native refusal checks verify
standing, informed independent assessment, established grounds and plot access.
An accepted property rehearing retains its new-notice material snapshot.
Property filing retains the building, ownership/recipient, recorded goods,
plot and governing authority. Its shared nonthrowing capture check validates
that material before publication. Personal consent retains the exact noticed
snapshot and previous personal response, reusing the native consent validator.
Before binding, household filing availability follows current affected plots;
the original nearby suggestions do not restrict the exact reply selection.
Filing plans are constructed only at the notice board.
Transfer read/consent/withdrawal choices retain their exact request terms
and household party. Shared checks allow other responses to accumulate and
check notice knowledge before the native response writer runs.
