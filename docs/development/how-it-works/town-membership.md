---
title: Town membership and government
type: architecture
status: active
updated: 2026-11-14
---

# Town membership and government

This page describes Town admission, government and elections.

**Town admission** (`TownSystem.TownMembership`). `TownRuntimeState.ResidentIds`
is the only record of Town membership; household, House and position alone never
change it. After every saved council decision, every Town roster change and on
every tick, `SettleTownAdmissions` reads each Town's passed admission proposals
and records exactly one `TownAdmissionRecord` per proposal, rereading the Towns
after every change:

- A request the newcomer made for themselves is their consent, so it is applied
  at once.
- A request a resident made is saved as `approved`, and the newcomer is offered
  an `accept_admission` civic choice once they have learned the result notice.
  `TownAdmissionDeadlineRules` derives the acceptance deadline from the passed
  proposal's original `SettledTick` plus the world's configured `TicksPerDay`.
  Acceptance is open only before that deadline. At the deadline the approval
  lapses with `acceptance_expired`; paused time does not advance the window.
  Accepting needs the same Town they had when the council voted, and is not
  offered while their own request elsewhere is undecided.
- Applying an admission moves the newcomer and their care group: the living
  infants, children and adolescents they are primary caregiver for who share
  their current Town. The group leaves the old Town and joins the new one in the
  same step, the newcomer's other open approvals lapse as `joined_elsewhere`,
  and both councils' rosters follow their recorded adult residents.
- An approval for someone who has died, already lives there or changed Town
  lapses instead.

An adult has one undecided request of their own at a time. A refused or
withdrawn request for the same newcomer waits one unpaused world day unless the
council changed, and the choice is not offered during that wait. An adult with an
open request or approval may also ask households in that Town to take them in,
but household admission stays a separate decision and grants no Town membership.

`TownMembershipText` describes recorded membership, the rights it gives (council
seat or vote, in-person Warehouse collection) and the admission status in one
bounded line. The agent's `CognitionSelfContext.TownMembershipNote` includes only
pending, approved, lapsed, refused or cancelled admissions the agent learned from
notices. Its shared approval selector excludes expired approvals and keeps the
original notice knowledge gate. A known open approval and its acceptance choice
state the deadline and remaining world time. The owner's agent card reads public records;
its `town-membership` decision factor has an optional derived
`AcceptanceDeadlineTick`, so the client can format the actual world date and
time without depending on the bounded Council proposal display. No deadline is
saved separately or added to an agent's knowledge by publication alone. A later
ordinary request can be approved and accepted again. `town_admission`
telemetry records the Town, outcome, previous Town and counts only.

`TownGovernmentState` stores scoped law versions, protected resident processes,
mayoral consent and contests, and separate land, ordinary and non-land mandate terms.
`TownGovernmentRules` coordinates them with the existing Council engine.
New Towns initialize two vacant office records, `land` and `non_land`, with
their founding tick and no holder, term or election. The initial arrangement
keeps ordinary Council authority. While the whole office is vacant and no
protected change is pending, the personal registration choice covers its
combined duties. Joint consent opens the existing vacancy election, and its
winner receives both separate mandate records under the same election and
twenty-day term. Empty offices supply no judge. Paused founder setup updates
the Council roster without running elections or advancing time.

Law adoption consumes passed structured Council proposals once; amendment and repeal
bind their base version, so a stale passed proposal cannot overwrite a later law.
Amendment and repeal labels shorten the display to 256 UTF-16 units, reserving
one for an ellipsis and omitting a whole surrogate pair when it crosses the
boundary. The structured draft retains the full wording; saved validation
reconstructs the label with the same rule.
Territorial applicability uses formal title records, including a saved site
subset, rather than the drawn Town border. Law text grants no physical powers.

Residents propose government changes at the notice place, and the choice to
seek a mayoral office appears only while an office exists, a contest is open or
a change that creates one is pending, keeping these choices out of every model
call. Government votes preserve their opening electorate and final votes. Later adults
wait; deaths and membership departures remove voters and ballots. Equivalent
requests share a process; different requests queue with fresh opening lists.
Incumbent Council revisions cannot cancel this ledger. Approved transitions
retain incumbent authority until all required successors are ready, with a
three-day deadline. Ordinary handovers require mayoral successors only for
mandates added by the target arrangement; explicit replacement requires every
targeted mandate. A retained vacancy follows its existing succession rules.
Explicit all-adult government disables automatic representation, and explicit
elected government seeks three representatives above three adults. The initial
arrangement uses the four-adult threshold.
Each transition records the exact Council election it caused to open, including
an attempt that fails immediately. Only that election waits for the transition
or is cancelled when it lapses. An ordinary election keeps its own result and
retry policy when population changes during an unrelated handover.

Mayoral contests bind consent to exact mandates and ballots to a contest/round
opening token. A new contest starts only with an eligible willing candidate,
so an empty vacancy adds no daily history or notices. Existing contests retain
their normal failure and retry rules. Every deciding round needs a positive
vote; tied leaders repeat with fresh voters, without a random draw. Saved round
records preserve votes, ties and interruptions. Scheduled Council voting takes
priority; other Council
contests wait while mayoral voting runs. A cancelled transition cannot later
seat its dependent contest. A completed transition cancels any dependent
contest it no longer needs. Separate mandate records preserve a governing
leader when a land mandate ends, and a governing vacancy temporarily restores
all-adult authority without changing the approved succession arrangement.

The normal personal-model path supplies all proposals, consent, withdrawals,
resignations and votes. The runtime revalidates current eligibility, actual
notice knowledge and the exact round before admitting an action. Owner
observations add current mandates, the latest eight government processes,
current/latest mayoral contests and the latest sixteen laws; saved history is
not pruned. Bounded civic telemetry records transition kinds and counts without
law wording, notice text or personal model payloads.
