---
title: Town membership and borders
type: game-design
status: active
updated: 2026-11-14
---

# Town membership and borders

Town membership, borders, abandonment, salvage and founding.

### Town membership

**Agreed with the owner on October 1, answers 15A–18A:**

- **Residence is recorded membership in one Town, or none.** A journey, visit
  or border crossing does not change that record. Household membership,
  permanent House residence, physical location and Town membership are separate
  facts. Rowan can gather near another Town for three days while remaining a
  resident of the original Town.
- **Homeless residents keep their Town membership.** Leaving a household,
  eviction or losing a House does not alone remove Town membership, ordinary
  eligibility to collect communal Warehouse stock or adult voting rights.
  The person can seek another accepting household or build normally while
  remaining a Town resident. Available stock, physical collection, carrying
  limits, access and ordinary construction rules still apply.
- **An ordinary adult newcomer needs Town approval.** The current council or
  other legitimate Town government accepts them. Household admission is a
  separate decision: an accepting household alone cannot grant Town membership,
  communal stock access or a vote. A homeless newcomer may request Town
  membership before finding a House. A request or model claim is not approval;
  walking into the border does not register a resident. The council's voting
  procedure remains a separate governance choice.
- **Dependent children follow their primary caregiver.** A newborn joins that
  caregiver's Town without a separate admission vote; the other parent's Town
  membership is unchanged. Approval for a caregiver moving Towns with dependent
  children covers the care group. Keep the group's membership consistent with
  actual caregiver placement, preserve parentage and count everyone as a
  resident. Children gain no adult council or voting rights before adulthood.
  The household still needs agreement and places for the complete care group;
  Town approval creates no free House, private access or extra resident places.

**Agreed by the owner on October 3, 2026
([#871](https://github.com/compoodment/ClankerWorld/issues/871)):** when a
resident asks the council to admit an adult nearby who has no Town, the
council's approval waits **one unpaused world day** for the newcomer to accept.
If they have not accepted by then, the approval lapses; paused time does not
count. Anyone may ask the council again later through the ordinary proposal
rules. There is no separate decline choice. An adult's own request is
unaffected: it admits them as soon as it passes. The implementation is tracked
in [#887](https://github.com/compoodment/ClankerWorld/issues/887).

The separately agreed New World and Add Agent placement rules still initialize
Town membership: the first four agents belong to the first Town, and confirmed
Add Agent placement uses the recorded household property/Town precedence. Those
setup exceptions do not turn later travel into membership or bypass the agreed
House capacity checks.

The [initial council and elections](councils-and-elections.md#town-laws-and-governance) use these recorded
adult residents rather than the agents physically inside a border. Admission
to an abandoned Town follows the [revival exception](#borders-abandoned-towns-and-salvage).
The approved membership work is tracked in
[#602](https://github.com/compoodment/ClankerWorld/issues/602); council voting and
election procedures follow separately in
[#601](https://github.com/compoodment/ClankerWorld/issues/601).

### Borders, abandoned Towns and salvage

**Agreed with the owner on October 1, answers 19A, 20A, 21B, 22A and 23A:**

- **Removing a building does not shrink the border automatically.** Keep the
  existing boundary and room for rebuilding; assigned new construction may
  expand it under the existing layout rules. A formal boundary change does
  not by itself change Town membership, title, household use rights or the
  owner of buildings and goods.
- **A Warehouse's recorded Town controls ordinary access.** If a formal
  boundary change leaves the Warehouse outside that Town's border, its residents
  may still collect stock under the usual physical pickup rules. Do not suspend
  access merely because of geometry or grant it to a neighboring Town.
  Reassignment remains a separate validated change; it cannot erase stock or
  bypass the agreed removal/reassignment safeguards. Moving one updates both
  Towns' assigned-building records and leaves their borders unchanged.
- **Owner building changes preserve physical state.** The owner may remove a
  placed building or reassign its household/Town owner through a signed action.
  Removal and reassignment are refused while the building has stored or
  inbound stock, or active production/expansion work. Completed work history is
  retained after removal. Removing a building keeps its Town border and Roads;
  changing a private building's household owner never changes its Town title
  or assignment. Removing or reassigning a House clears its shelter-only guest
  invitations. An action against an outdated owner record is refused.
- **An empty Town permits public salvage of unreserved communal stock.** When
  no recorded living residents remain, any agent may physically collect that
  stock, including from its Warehouse, within carrying and availability limits.
  This is the owner's chosen exception to resident-only communal access. Check
  the current abandoned state and reservations when pickup actually happens;
  no missing goods are created and no reserved lot is silently released.
  Visiting or salvaging creates no Town membership, council vote or access to
  private household goods/buildings. Salvage does not erase the Town's identity,
  stock history, claims or existing ownership records.
- **One adult may explicitly resettle an abandoned Town.** The adult must be
  physically there and choose residence, rather than merely visit or collect
  salvage. Record the deliberate membership change, preserving the one-Town
  limit and dependent-care rules, revive the same Town identity and restart its
  council from the returning adult residents. No previous council approval or
  second adult is required for this first-resident exception. Later ordinary
  newcomers need the restored government's approval. Resettling grants no free
  House, materials or ownership of old private buildings; ordinary housing,
  care, construction and rights rules still apply.
- **Revival ends public salvage access.** Remaining communal stock again uses
  ordinary resident eligibility, checked at the next physical pickup. Goods
  already collected legitimately stay with the person carrying them; revival
  neither duplicates those goods nor confiscates them. A stale salvage plan
  cannot collect new stock after the Town is occupied unless it now has normal
  resident access.
- **The Town's existing laws survive abandonment.** The restored council may
  amend or repeal them through the agreed [council
  process](laws-and-enforcement.md#town-law-scope-and-changes). Old councillors or offices are not
  resurrected automatically.
  The laws remain social rules that can be broken; their persistence does not
  rewrite fixed world facts or grant agents knowledge they have not learned.

For example, a visitor may collect five available unreserved logs from an
abandoned Town without joining it. An adult later chooses to settle there;
the remaining logs become resident-only communal stock again, the same Town
revives, and its protected-grove law still exists until validly repealed.
Private coats, Houses and household equipment retain their recorded owners.
The owner resumed implementation of these rules in chat on October 6, choosing
abandoned Towns, physical salvage and resettlement as the next gameplay work
in [#410](https://github.com/compoodment/ClankerWorld/issues/410). This replaces
the earlier decision to park that work. See [what works today](../../what-works.md)
for the connected game path and its pending hands-on checks.

**Founding a new Town, agreed on October 8
([#1238](https://github.com/compoodment/ClankerWorld/issues/1238)):** one adult
may found a new Town on unclaimed land away from other Towns, in the same way
one adult may resettle an abandoned Town. The adult must be physically there
and choose to found it; no minimum group, House or Warehouse is needed first, and their old
Town's council does not have to approve. The game checks the site before
recording the Town. The founder's Town membership moves to the new Town, so
they still belong to only one Town, and dependent children follow them under
the ordinary care rules. The new Town holds title to the land of its first
layout, as the first Town does, and starts with an all-adult council; later
newcomers need that council's approval. Founding creates no free House or
materials. This also settles a House built outside every Town: it is an
ordinary household House that belongs to no Town, and its adults may found a
Town there under this rule.
