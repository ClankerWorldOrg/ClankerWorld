---
title: Households
type: game-design
status: active
updated: 2026-11-14
---

# Households

Household membership, goods, departure and resident capacity.

### Household membership

**Agreed with the owner on October 1:**

- An agent belongs to **at most one household at a time**. They can also have
  no household. Visiting, helping or trading with another household does not
  grant membership or access to its private stock. Household membership and
  family ancestry are separate records.
- An adult joining an existing household through an ordinary move needs
  **every current adult member's agreement**. This includes relatives joining
  from another household. The separately agreed Add Agent exception remains:
  placing an agent on household property joins that household without a
  consent step. It still needs a valid free resident place under the agreed
  capacity checks; consent and room are separate requirements.
  An ordinary request is bounded: one refusal, or no answer within the usual
  proposal window, ends it, and that household is not asked again for a while.
  **Agreed on October 8 ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):**
  the wait after a refusal is two world days, a provisional value.
  Standing nearby grants nothing; a pending request grants no membership or
  access to the household's stock or shelter.
- An adult may **leave voluntarily without the household's permission**.
  Personal goods, shared stock, access and dependent care follow the
  [agreed departure rules](#household-goods-and-departure).
- **One adult may start a household alone**; a partner or second founder is
  not required. A newly added homeless adult still seeks suitable existing
  household housing first. If none is suitable or accepts them, they may form
  their own household and pursue a House through the ordinary materials,
  terrain, access and land-use rules. Founding a household creates neither a
  free House nor building materials.

The household's existing limit of one planned House remains separate from the
number of people in its family. Splitting into two households does not erase
parentage or other family relationships. Care during a living adult's departure
is agreed below. Town affiliation follows the
[Town membership rules](town-membership.md#town-membership). When a dependent loses their last
active primary caregiver, the agreed care sequence is in [Agents and
families](../agents-and-families.md#dependent-care-after-loss-of-the-last-active-primary-caregiver).
Any move between Towns still follows the admission rules in issue
[#602](https://github.com/compoodment/ClankerWorld/issues/602).

### Household goods and departure

**Agreed with the owner on October 1, answers 10A–14A:**

- **Ownership and location are separate.** A person may keep recorded personal
  belongings in the House without donating them to the household. Shared food,
  materials and household equipment remain household property. For example,
  Rowan's own coat remains Rowan's beside the shared grain in House storage.
  Carrying a borrowed household axe does not make the axe personal property.
- **Moving out ends the old household membership.** This applies to voluntary
  departure and overcrowding displacement. The person keeps their personal
  belongings and the departure allowance below; they get no automatic equal
  share of shared stock. Shared goods and buildings remain with their recorded
  household owner, including when its last member leaves. Moving out does not
  settle abandoned-property claims or transfer buildings to a new household.
- **Personal belongings remain retrievable after departure.** The former
  member retains limited access to collect their recorded personal goods,
  without needing to rejoin or obtain a new permission for each collection.
  Ordinary shared-stock and cooking access end with membership. Retrieval is
  physical, follows carrying limits and may take repeated trips; it grants no
  remote transfer or access to other people's goods. Borrowed goods keep their
  original owner and must be physically returned rather than silently becoming
  the departing person's property. Leaving itself needs no household vote.
  **Agreed on October 11
  ([#1370](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1370)):**
  an agent may also recover their damaged or spoiled personal belongings.
  Recovery keeps their physical ownership and ordinary access, reservation,
  route and carrying limits; it does not require the goods to be usable.
- **A small food allowance supports the move.** The initial trial is up to
  **two ready-to-eat food portions**, from available **unreserved** household
  stock, for both voluntary departures and evictions. Existing care and work
  reservations are respected. Missing food is not created. The allowance is
  allocated once for the departure and collected physically within carrying
  limits; repeated requests, collection trips or a saved-and-resumed move
  cannot multiply it.
- **Care continues when a caregiver leaves.** A departing sole caregiver
  remains responsible: dependent children move with them unless another capable
  adult explicitly accepts their care. An accepting destination must have room
  for the whole moving care group under the resident rules. Travel and care
  happen physically; no child is left alone, teleported or assigned to an adult
  who has not accepted. If no home is ready, the housing task remains visible
  while that adult continues care. Parentage is unchanged. The overcrowding
  timer still cannot expel a child alone or remove their only caregiver.

**Agreed on October 8 ([#1241](https://github.com/compoodment/ClankerWorld/issues/1241)):**
when a household has no members left, its buildings and shared goods stay its
recorded property; nobody gains them by moving in. The Town may open a land
case for them under the [land hearing rules](laws-and-enforcement.md#land-hearings-and-rulings), with
the usual notice, and a ruling may pass them to the Town, which can then grant
them onward, for example to a household that needs a House. Former members who
are still alive keep the property unless they agree: a ruling cannot pass it
to the Town without their consent.

See [what works today](../../what-works.md#life-work-and-society) for departure,
collection, care-group moves and relocation in the current build. The agreed
response to losing a dependent's last active primary caregiver is described in
[Agents and families](../agents-and-families.md#dependent-care-after-loss-of-the-last-active-primary-caregiver).
Cross-Town moves after accepted care still follow Town admission rules in
[#602](https://github.com/compoodment/ClankerWorld/issues/602). Wills are agreed
in [Agents and
families](../agents-and-families.md#starting-agents-families-and-life-stages);
other guardianship cases remain separate choices in [Agents and
families](../agents-and-families.md#still-to-decide).

### House resident capacity and relocation

**Agreed with the owner on October 1, answers 5A–8A and 9B:** three ordinary
resident places or four with a dominant family **per tile of the House
footprint**. These are total resident limits, not separate family and outsider
allowances. This replaces unlimited House residency. Storage quantities and
construction costs remain provisional; they are separate from these approved
resident counts.

| House footprint | Ordinary resident limit | With a dominant family |
| --- | ---: | ---: |
| 1×1 | 3 | 4 |
| 1×2, either orientation | 6 | 8 |
| 2×2 | 12 | 16 |

- **Count permanent residents, not people currently inside.** Traveling
  residents keep their places. Every child, including an infant, takes one
  place from birth. Invited visitors and temporary storm shelter do not create
  residence, household stock access or a place in the count.
- **A dominant family gets the larger limit.** One recorded
  domestic family unit must contain at least two residents and more than half
  the House's permanent residents. The unit is partners and their
  children, including siblings who belong to that unit; marriage is not
  required. Each person has one primary unit for this housing calculation,
  separately from their full ancestry. Taking a partner creates a new unit;
  it does not merge both ancestral family trees. A shared surname, household
  membership or distant ancestor does not by itself qualify. Nonresident
  relatives do not count toward dominance. A tie gives neither family
  priority. Extended ancestry does not automatically join that domestic unit.
  **Owner decision, October 1:** when parents separate or a parent starts a new
  relationship, younger children follow their recorded primary caregiver's
  domestic family group for this count. This changes domestic-unit membership
  only; it does not change anyone's household or residence, and it never merges
  ancestry records.
- **Displace only when there is no room.** In a 1×1 House, two partners, their
  baby and an unrelated adult can use all four places. A second baby makes
  five, so the crowding must be resolved by completed expansion or relocation.
  Becoming a family majority does not itself expel anyone. Admission considers
  all residents after the move: a newcomer cannot use a family bonus their
  own arrival would remove. Ordinary adult newcomers still need unanimous agreement;
  family status alone cannot bypass it.
- **Births always complete, even if the home is full.** Before preparation,
  the initiating parent names a primary caregiver and intended household, and
  the accepting parent chooses a named caregiver-and-home option. Either
  parent's current household can be chosen. The newborn joins that caregiver's
  actual household at birth, even if it changed after agreement; the other
  parent's residence does not automatically change. A full or unavailable
  home creates a visible housing need, not a lost or rejected baby. A House
  that exceeds its limit is marked overcrowded and accepts no further
  voluntary residents. Once an
  overcrowding case opens, a birth or age change does not restart its deadline.
- **Adults relocate; dependents are not sent away alone.** Volunteers go
  first, then the most recently admitted eligible unrelated adult, with a
  stable tie-break for equal arrival times. Without a dominant family, use
  that same volunteer-then-arrival order without favoring a family. A dependent
  moves with a caregiver when needed, and an arrival cannot gain a place by
  displacing people in its destination House. Recheck current residents and
  the completed footprint before acting so a death, departure, expansion or
  new family relationship cannot cause an unnecessary eviction. Continue
  relocating eligible adults only until the remaining residents fit.
- **A sole caregiver is not given notice, agreed on October 2.** If the only
  adult who could be asked to leave an overcrowded House is the sole caregiver
  of a dependent child, nobody gets notice, and the caregiver and child are not
  given notice together as a group either. The House stays visibly overcrowded
  with a plan to expand or split, and the caregiver may still choose to leave
  with the child.
- **One unpaused world day of notice for displaced adults** is the initial
  trial period. During notice they look for a suitable
  accepting household with room; otherwise they may form their own household
  and build normally. If notice ends with no home ready, the eligible adult
  leaves residence and the old household membership, and continues a visible
  homeless housing task. Immediate storm
  shelter is still possible under the guest rules. No person or goods are
  teleported to a new home. Goods, collection access and care follow the
  [departure rules](#household-goods-and-departure).
- **Late forced replacements get time to look, agreed on October 4
  ([#909](https://github.com/compoodment/ClankerWorld/issues/909)).** If the
  adult holding a notice becomes ineligible, an eligible replacement inherits
  the case's deadline while it is still in the future. A forced replacement
  notified at or after that deadline instead gets one fresh unpaused world
  day. This does not restart other residents' notices or the overcrowding
  case. Volunteers always inherit the original deadline.
- **Expansion can solve crowding.** The household may plan its next supported
  footprint for a storage or housing need, under the ordinary terrain,
  overlap, land-use, materials and work rules. Reserved space or an unfinished
  project creates no resident places yet and does not restart the notice
  period. A completed expansion cancels any remaining relocation that is no
  longer needed. Adults who already left do not automatically rejoin.
- **An all-family full House needs a family housing plan.** A birth beyond
  the current footprint's family limit permits temporary overcrowding.
  Suitable adults arrange expansion or, if the House cannot expand further,
  a split into another household and House while preserving dependent care.
  With no safe care arrangement, legal site or materials, the home stays
  visibly overcrowded and blocked;
  it cannot admit additional voluntary residents. Do not apply the unrelated
  adult's notice deadline to a child or to remove their only caregiver.
  This adds no housing prerequisite for starting pregnancy, automatic birth
  cap or change to the agreed continuity safeguard. At 2×2, a seventeenth
  family resident therefore needs a housing split, not an invented larger
  base-game footprint.
- **Add Agent respects resident capacity.** It retains the agreed consent
  exception but needs a valid free resident place. A full-house placement
  explains the blocker instead of bypassing capacity or silently evicting
  someone. Placement on a household's other property likewise cannot avoid
  its House's resident limit. **Agreed on October 8
  ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):** Add
  Agent is also refused on a tile that cannot be built on, is occupied or is
  disputed, and on top of another agent unless the tile is a House.

| Case | Agreed result |
| --- | --- |
| Three unrelated adults in a 1×1 House; another wants to join | No free resident place until expansion completes. Seek another household or build if none is suitable. |
| Two partners and one unrelated adult in a 1×1 House; first baby | Four residents with a family majority. Nobody is displaced. |
| Two parents, one child and one unrelated adult in a 1×1 House; second baby | Five temporarily. Completed expansion can keep everyone; otherwise the unrelated adult relocates, leaving four family residents. |
| Four family residents in a 1×1 House; another baby | Birth completes. Expand or arrange a housing split with safe care. |
| Eight family residents in a 1×2 House; another baby | Birth completes. A finished 2×2 expansion gives sixteen family places; otherwise arrange another home. |
| Sixteen family residents in a 2×2 House; another baby | Birth completes. No larger standard footprint; a family housing split is needed. |
| Two unrelated adults in a 1×1 House; one has a baby whose other parent lives elsewhere | Baby joins the primary caregiver. Three residents fit; the other parent does not automatically move. |
| Four residents in two equally sized family units in a 1×1 House | No family majority. A voluntary fourth admission is refused; an existing birth-related case needs expansion or relocation with no family favored. |
| A resident is away gathering; a guest is invited | The traveler still occupies their resident place. The guest gains shelter only. |

The resident counts, family scope, birth exceptions, notice and departure
rules are agreed. The existing
[housing-admission work](https://github.com/compoodment/ClankerWorld/issues/464)
and [storage-expansion work](https://github.com/compoodment/ClankerWorld/issues/463)
remain their original implementation slices. See [what works
today](../../what-works.md#life-work-and-society) for resident limits, admission,
birth placement, expansion and relocation in the current build.
