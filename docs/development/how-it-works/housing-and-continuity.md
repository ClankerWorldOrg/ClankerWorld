---
title: Housing and population continuity
type: architecture
status: active
updated: 2026-11-14
---

# Housing and population continuity

This page describes household departure, housing and population continuity.

**Household departure and personal custody** (`HouseholdDeparture`). Ordinary decision candidates allow an adult to leave without a vote, store or collect their own goods, return borrowed household tools, explicitly accept replacement care, and found a solo household only when no suitable existing home can currently be asked. Membership exits and admissions include the complete primary-care group. The same completed House-capacity calculation checks all incoming residents; children never apply alone. Forced displacement refuses sole caregivers, including a guardian whose dependent lives in another household. Voluntary departures keep the existing care-group rules.

`InventoryLot.OwnerId` records property; optional `CarrierId` records physical custody without donation. Personal goods may remain in House storage after departure. The once-only food allowance also remains at its source when allocated from a household Store; its departure record permits only those portions to remain personal Store stock until collection. Exact frozen lot and settlement IDs preserve that physical allowance through death and successive inheritances, including household fallback when a named will heir is unavailable. The total portions remaining in Stores cannot exceed the recorded departure allocation. If a Town reclaims the empty household’s Store or grants it onward, its validated prior-building transfer history retains the original household binding and the former owner’s limited collection right. `InventoryAuthority.Relocate` preserves ownership, condition, provenance and reservations while moving an unreserved quantity. A stored personal lot is collected physically at its building's entrance, with carrying limits, under current household membership, a recorded departure's limited collection right, or a settled will's bequest to the actor. A child who moved in a caregiver's care group uses that recorded departure too once old enough to collect, including after the caregiver dies. The lookup reads living and archived departure records; it creates no child departure or additional food allowance. The bequest must match the lot's ID or provenance, kind and frozen storage location. This lets an heir retrieve inherited goods from another household without granting membership, House entry or access to shared stock. Recovery from the ground or a former household remains a routine errand. Collecting a map or field record from the current home stays available as a deliberate choice, but ranks below idle for the built-in chooser so it does not immediately retrieve knowledge goods it has just stored. Other goods retain their normal collection priority, so the built-in chooser stores only maps and field records; storing other belongings is a deliberate choice, because routine collection would fetch them straight back. Borrowed tools retain the lender's owner ID while carried and are returned physically. Shared delivery loads retain their owning household on departure. Shared buildings, stock and job records are never reassigned to the new household. A departing worker's private production and expansion jobs pause with their existing owners and reservations, except work for the worker's own goods, such as building their handcart, which nobody else may finish: it is cancelled and its reserved materials are released and stay with the worker; their previous work plan is retained on the departure record instead of resuming under a new household. A remaining member can take over paused work at its physical site, using the same still-available committed inputs and remaining work time. Private materials held by the former worker are not reassigned; these keep the task blocked. Held reservations keep their exact owner and stock, receive a new deadline only on resumption, and are released if the materials become unusable; canceled job records retain the original property owner.

Each departure allocates at most two unreserved ready-to-eat portions once. Ownership changes at allocation while the existing storage/ground location stays fixed. Saved departure records retain the allocation and collection right; retries with no current membership cannot allocate again. Caregiver IDs and ancestry stay unchanged. Dependents follow the caregiver in physical steps, and a traveling caregiver waits when a dependent falls behind. Housing, ownership, collection and care facts use normal personal-model observations and player inspection; no extra acknowledgement request is made.

Departure food selection uses the shared `Holdings` query, excluding unusable
stock, delivery promises and food on actively borrowed Market stalls. It keeps
the existing loose-food rule and leaves other people's carried loads alone.

Work-tool choices use the same physical collection checks. Collecting the
actor's own stored or ground tool relocates it into carried custody without
transferring ownership, including after household departure. A borrowed
household tool retains its household owner; a Town Warehouse tool transfers
to the collecting actor. Each pickup still rechecks access, unreserved stock
and carrying room at the source.

Production jobs capture their owner when the original inputs are reserved. Completion uses that saved owner, including at a public workstation when the worker leaves or forms a household during the job; membership changes cannot redirect the finished goods.

**Housing requests** (`HousingState`). An agent's saved `Housing` record
holds its current blocker and, for an adult, any pending request, recent refusals
or move-out notice. Each tick `MaintainHousing` resolves
requests, then recomputes the blocker and appends `housing_blocked` when it
changes. The blocker codes are `no_household`, `no_authorized_home` (the
household can plan or is building a House), `missing_materials`,
`no_legal_site` (the household has the build costs but `TownLayoutService`
ranks no site), `awaiting_answer` and `overcrowded` (the household's House
has more permanent residents than places). The state is explained on the owner's
agent card and sent to the agent's own model as a `housing` line in its self context.
An adult with no household, or with a currently valid move-out notice, is offered
`household_ask:{household}` for each other household that holds a House in the
same Town, has room for the entire moving care group, has an adult who can answer
and has not refused within the last two world days. Asking records that household's
adult members in the request, which expires after 120 ticks like other
proposals. Adults who join the household or reach adulthood while it is pending
must also answer; existing answers are retained and adults who die or leave no
longer need to answer. Each current adult is offered `household_admit:{applicant}` and
`household_refuse:{applicant}`. During ordinary planning, an unanswered housing
choice pauses project and lesson work. While that participant's personal-model
reply is pending, an already chosen project or agreed lesson can continue under
its normal physical and urgent-need checks. This records no housing answer and
grants no membership; an ongoing lesson retains its progress and learned skills.
One refusal by a living member ends the request; when every living
member has agreed, `SocietyRules.JoinHouseholdCareGroup` records the membership and
`household_joined` is appended. A refusal or an unanswered request is remembered
as a refusal for the cooldown. The request grants nothing while pending: stock,
shelter and route rules still check household membership. A resident with notice
leaves through `DepartHousehold` before joining, preserving the
[departure rules](../../game-design/towns/households.md#household-goods-and-departure) for goods,
food allowance and care. Before that exit, live relocation eligibility and
destination capacity are checked again. A cancelled notice also cancels a
resident's pending request elsewhere; an adult who already left keeps seeking
a home.

**House resident capacity** (`HouseResidentCapacityRules`). A completed House
provides three permanent-resident places per footprint tile, or four per tile
when one explicitly recorded domestic family unit has at least two residents
and a strict majority of the House's residents. The unit is saved separately
from ancestry. A partnership changes these units only when it is accepted or
when an accepted partnership ends. Withdrawing, refusing or expiring an
unaccepted proposal leaves each person's existing unit and the resulting
House limit alone. Traveling residents and infants count, dead people and invited
storm guests do not. Joining a household is offered only when the proposed
resident fits after their arrival is counted. The server checks again after
unanimous admission, and Add Agent checks the selected household property
before placement. A birth always goes to the primary caregiver's current
household, even when that puts the House over its limit; the building card,
agent context and the newborn's saved housing status show the resulting need.
An unavailable House is recorded the same way without delaying an agreed birth.
The birth still needs food and an unoccupied, buildable tile for the newborn:
near an accessible shelter, or near the primary caregiver when there is none.
Losing a House does not bypass the food or consent checks. This status gives
dependents no adult admission or construction choices. House expansion can
start for a
storage need or when there is no resident place, but added places use only the
completed footprint. Unfinished expansion does not reserve room for another
resident.

The birth food gate and contribution selector exclude household food on an
actively borrowed Market stall through the same boundary as ordinary goods
pickup. Their queries capture both the society checkpoint and Town state;
owner cards and private continuity guidance use that same captured state.
Ending borrowing makes the stock eligible again. The reserve remains two
ready-to-eat portions per active household member plus four, and the transaction
still spends four actual portions. No collection or trade is performed by birth.

**Overcrowding relocation** (`HouseRelocationRules`, `RelocationNotice`).
Selection uses the completed House footprint and active permanent residents.
Volunteers come first, then existing notices and the latest eligible arrivals,
with ordinal agent IDs breaking equal arrival times. Forced selection protects
the dominant domestic family; when no family has a majority, the arrival order
does not favor a family. The majority is checked again after each selected
departure, so a family that gains it part-way is protected from then on.
Sole caregivers are ineligible for the notice timer,
including when their dependent lives elsewhere. Capacity is recalculated after
each proposed departure, and a departure that would not reduce overcrowding is
skipped. Selection stops when the remaining residents fit.

`Housing.Relocation` stores the household, original notice tick, fixed deadline
and selection reason. The initial period is one world day. Pausing and loading
do not consume or restart it. A forced replacement inherits a still-future
deadline; when notified at or after it, they receive one fresh world day from
the current world tick. Volunteers keep their original deadline. Other residents’
notices, births, age changes and unfinished expansion do not restart a notice. Reconciliation cancels obsolete notices after changes in
residents, family, care or completed capacity. Admission and departure actions
recheck that eligibility before acting on a saved or delayed choice.

At expiry, each still-eligible adult leaves through the ordinary departure
transition, in the order selection planned, rechecking remaining need before
the next exit. No location or
inventory is transferred remotely. The once-only food allowance, personal
collection rights, borrowed goods and paused work retain their existing rules.
An adult without a new home keeps a visible housing task. An overcrowded House
with no eligible adult remains blocked while its members arrange expansion or
a voluntary household split; sole caregivers are never forced out with their
children. The split is offered only when the House cannot grow: no expansion
is running, and either no larger footprint fits or its extra land is neither
the household's to use nor free Town land it can still ask the Council for. While only that land permission
is missing, the housing line names it as the next step, and the expansion
land request prefers a footprint whose extra land no other household holds or
has asked for. Built-in rules do not found a household while the adult still
has a home, so the notice period can end in a completed expansion or an
accepted request. Agent observations and owner inspection show resident
counts, notice reason and time, pending requests and expansion state.
`relocation_notice` and `relocation_cancelled` record changes without
repeating them on reload.

**Guardian placement** (`GuardianPlacementState`). An adult's explicit
acceptance records primary care separately from the child's move. When a child
cannot yet join that adult's household, their physical state keeps a pending
placement tied to the exact accepted care relationship and its revision. The
guardian needs a recorded Town and a completed household House with room;
acceptance creates neither a House nor a resident place.

The guardian first reaches the child, then accompanies them to the selected
House through ordinary movement. Collections keep acceptance order among
currently reachable children; an unreachable collection does not hold up another
accepted child. An ongoing escort stays together, and blocked collections retry
after the available journeys finish. The guardian waits for a child who falls
behind. Urgent food and warmth needs may interrupt the journey without
removing accepted care. Capacity, current care authority, Town membership and
the House's identity are checked again before placement. Only arrival together
commits the child's household and Town membership in the same world transition,
using the existing rule that dependents follow their accepted primary caregiver.
The destination guardian is already a Town resident; the move does not invent a
Council admission proposal or give an unrelated adult membership. Parenthood,
birth records and property ownership do not change.

Pending placements retry after temporary blockers clear. A change of caregiver,
death or the child reaching adulthood ends the old placement. The owner's
agent card and People section distinguish accepted care, a blocked home and
travel through existing observation notes. `guardian_placement_pending`,
`guardian_placement_completed` and `guardian_placement_cancelled` record the
placement lifecycle separately from `guardian_assigned` and `guardian_needed`.
The guardian's model housing note keeps the accepted task and current blocker
within a 256-unit text budget without splitting Unicode characters. The complete
child name and accepted care and House references stay saved.

**Continuity rule** (`PopulationContinuity`). The owner's answer on
[#1266](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1266) replaces the
original headcount trigger with eligible couples. The provisional threshold
is four couples, retaining the original scale of eight adult partners for
playtesting. A couple may still say "not yet" for two world days
(`2 × TicksPerDay`). The checkpoint saves whether the rule was on at the last
check and, per eligible couple, the tick at which their "not yet" ends. A new
world appends `continuity_rule_on` when it is created; each tick
`MaintainContinuity`, after `MaintainParenthood`, counts distinct accepted
partnerships whose living, physically present adult partners each have a
household and are not close relatives. It compares whether that count is
below four with the saved flag and appends `continuity_rule_on` or
`continuity_rule_off`, with typed eligible-couple count and threshold fields, only when
it changes. Having an infant does not remove a couple from this risk count.
The Event Log displays the count and reason; historical `non_elders:{count}`
events retain their original headcount explanation.

While the rule is on, the two-day procedure holds only eligible couples where
neither partner is a parent or caregiver of a living infant. This infant check
is separate from the risk count. A couple first gets a deadline when the
procedure holds it, and loses that deadline when it stops meeting these checks
or the rule turns off. Until the
deadline, `parent_postpone:{owner}` replaces both `parent_decline` and
`parent_cancel`, and moves the plan to the inactive `postponed` stage; refusal
candidates are neither offered nor applied. A couple held by the rule may
propose while caring for an older child. At the deadline the server moves an
existing plan to `preparing`, or creates a new plan for the partner who last
asked (else the first by ID), and appends `continuity_plan_proceeded`.
A new plan records that parent
as its primary caregiver and their current household as its intended home.
Resuming an accepted or postponed plan preserves its original initiating
parent, request tick, selected caregiver and intended home. Birth still follows
the caregiver's current household, including after a move or when its House
is full. A plan past its deadline does not expire: it waits for the usual food,
shelter and readiness checks.

Single adults who meet the same parent eligibility check also receive a saved
`ContinuitySingle` deadline, optional chosen partner and postponement choice. `partner_choose` and
`partner_postpone` use the existing partnership action plans, including checks
at offer, admission and application. Choosing or postponing never resets the
deadline. Already recorded choices are not offered again unchanged; ordinary
work keeps precedence in built-in decisions. Once both adults' deadlines pass, mutual choices take precedence;
otherwise combined directed trust selects the strongest eligible pair, with
ordinal IDs breaking ties. Availability is checked again after every pair,
so a taken, deceased, elder or close-relative choice cannot force that pair.
The rule uses the native partnership proposal and acceptance operations,
including exclusive partnership and domestic-family records; its deadline
replaces voluntary trust and retry requirements. Outstanding proposals for
newly paired adults are withdrawn. `continuity_partnership_formed` records both
adults and explains the pairing in the Event Log. Ending a partnership starts
a fresh single deadline while the rule remains on.

Each partner's self context carries a `continuity` note saying the rule and
the hours left; held single adults receive the partnering rule instead.
Loading checks ordered, known adults and chosen partner references, unique
single entries and couples, no held entries while the rule is off, and no
deadline more than two world days ahead. Turning off clears both sets of
deadlines and all partner choices. These events are logged as
`family_activity`; the two transitions are also Event Log lines.
