---
title: Buildings and animals
type: game-design
status: active
updated: 2026-11-14
---

# Buildings and animals

Building types, animal care, products, breeding and horse travel.

## Buildings, land, Towns, and animals

### Agreed

- Each building **type has a few supported shapes of its own**, rather than
  choosing freely from every shape/material/floor/state combination. There is
  initially **one standard appearance per supported design**; duplicate houses
  of the same type and size may look alike. The decided footprints are listed
  below and in the [accepted asset roster](../content-list.md). Agents
  may invent new designs, including unusual shapes if the art and world rules
  can represent them; larger, irregular, or multi-floor buildings are therefore
  possibilities, not automatic unlocks. **House** replaces Shelter as the
  residential building. It has household-exclusive storage/inventory, usable
  by household members while physically inside; food is kept here. It provides
  cooking, storm refuge, childcare and household property functions. Agents
  outside the household may enter **when invited**; invitation does not grant
  access to the household's private inventory. Invited guests may shelter from
  storms but get no access to the House's stock or cooking. Any adult member of
  the household may invite a named guest and revoke that invitation later.
  Invitations are saved and last until revoked. **Agreed on
  October 1:** a House has a permanent-resident limit that scales with its
  footprint and a dominant family's needs; the [capacity and relocation
  rules](households.md#house-resident-capacity-and-relocation) replace the earlier unlimited
  resident count. Invited visitors and temporary storm guests do not take
  resident places. The House has no bed or sleep-recovery
  role. A selected building exposes inspectable occupants, stock and ownership in a panel
  ([quick card and Details](../interface-and-art.md));
  there are **no visible/enterable room interiors**.
- **Building expansion:** a House starts at **1×1** and can expand to **1×2**
  or **2×2** when its household needs more storage or resident places.
  Completed expansion increases storage and the agreed resident limit. A Warehouse starts
  at **2×2** and can expand to **2×3** for more storage. A Store may be **1×1**
  or **1×2**, likewise tied to storage. **Agreed on October 8
  ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)), with
  provisional values for playtesting:** Houses, Warehouses, Stores,
  Blacksmiths, Tailor Shops, Restaurants and Clinics store 64 units per
  footprint tile, and an animal yard holds 16. A building counts as nearly full
  at 80% of its storage. Houses, Warehouses and animal yards can expand. A
  House expansion costs 4 wood per extra tile, a Warehouse expansion 4 wood
  and 2 stone per extra tile, and a yard stage the [animal
  pipeline's](#animal-pipeline) 8 wood and 2 rope. Other construction costs and expansion triggers other than the
  agreed Warehouse rule (any Town resident may plan its expansion once its
  stock is nearly full) belong in the full content catalogue; do not invent
  those values yet.
  **Agreed on October 10
  ([#1369](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1369)):**
  private farm Silos also store 64 units per footprint tile. A 1×1 Silo holds
  64 units.
- Non-residential buildings have distinct physical occupancy, workstation,
  storage, and other type-specific limits. Agents can reserve space when
  practical, queue or choose alternatives when full, and retain the blocked
  goal for later retry. The simulation owns that memory; Jev can help choose an
  alternative but is not responsible for remembering the task. **Agreed on
  October 8 ([#1242](https://github.com/compoodment/ClankerWorld/issues/1242)):**
  civic buildings, such as the Town Hall, Market and Workshop, are open to
  everyone, including visitors from other Towns. Shops, such as the Farmhouse,
  Blacksmith, Store, Tailor Shop, Restaurant and Clinic, serve any customer,
  visitors included. Each building has one workstation, used first come, first
  served. Entering a building does not open its stock: Warehouse stock stays
  resident-only, a Port's boats still need permission and household stock
  stays private.
- Building sites should come from understandable legal options considering
  access, terrain, resources, land-use rights, and Town context. A Town holds
  title to its formally claimed land; households can receive recorded rights
  to use particular sites without owning the land itself. Land rights can be
  disputed; permission, transfers and case procedures are agreed below.
  **Agreed on October 8 ([#1256](https://github.com/compoodment/ClankerWorld/issues/1256)):**
  before any currency exists, a household can sell a use right for goods
  through the ordinary consent-based transfer; money prices come later with a
  [Town currency](item-and-resource-pipelines.md#physical-trade-and-production-safeguards). Agents
  can later buy/sell transferable property or rights through valid processes.
  Roads help travel and influence site choice.
- The world system must validate hard physical building constraints such as
  terrain, footprint and overlap with existing objects/resources/buildings.
  Access, ownership/claims and Town context also matter, without turning
  every agent-created law into an unbreakable physical rule. Agents request a
  building; Town planning ranks legal sites; agents accept or reject rather
  than choosing tiles.
- **Housing priority for a newly added adult:** when the adult has no
  home, seek suitable existing household housing first; start a new House
  project only if none is suitable. Joining, leaving and starting a household
  follow the [agreed membership rules](households.md#household-membership) and
  [House resident limits](households.md#house-resident-capacity-and-relocation).
  Only a household can plan a House. The adult is told
  the real blocker: no household, no House their household holds yet, missing
  materials or no legal site.
- **Town** is the name for every such place; there are no village or city
  place classes. The first Town is created during paused New World setup, and
  its four starting agents
  are Town residents with access to its Warehouse. Agents found later Towns
  under the [founding rule](town-membership.md#borders-abandoned-towns-and-salvage). Later
  membership follows the [agreed Town rules](town-membership.md#town-membership). Towns have generated,
  inspectable borders with room to grow. The border follows the
  Town's assigned buildings, includes spare space around them, and expands
  when new buildings join that Town. **Agreed after the September 30 road
  review:** the border keeps about **three tiles** of spare land beyond every
  building and road (tuned in playtests) and follows the Town's shape rather
  than a rectangle, so there is room, and a reason, to build inside it.
  Removing a building does not automatically shrink the existing border;
  later construction may expand it. A Warehouse keeps its recorded Town access
  if its footprint is outside that Town's border. Moving a Warehouse to a
  different recorded Town updates that building assignment but does not move
  either Town's border or transfer land title. Reassigning a private building
  changes its household owner while preserving its recorded Town assignment.
  Town identity and current resident membership are separate: the founding
  roster is historical provenance, while each living resident belongs to at
  most one recorded Town.
  When its last resident leaves or dies, the Town becomes **abandoned**, not
  erased. Its identity, buildings, border and infrastructure remain in the
  world, and later residents can revive that same Town under the
  [agreed abandonment and salvage rules](town-membership.md#borders-abandoned-towns-and-salvage).
- **Warehouse** replaces Storehouse. It is communal: it holds actual
  inspectable resource stock at its location for agents/households resident in
  its recorded Town. Food belongs at home instead. Ordinary access is for
  residents, with the agreed abandoned-Town salvage exception below, checked
  when they pick something up; what someone already
  carries stays theirs. Use [recorded Town membership](town-membership.md#town-membership),
  including homeless residents. The recorded Town controls access even if a
  formal border change leaves the Warehouse outside its border.
- **Household buildings and who may use them:** work roles do not decide what
  an agent may do. One household holds each household building: the Farmhouse,
  Blacksmith, Store and Tailor Shop. Any adult resident of that household may
  use it. The Farmhouse and Blacksmith are household-held, **not communal**: the
  holding household works there and sells directly from its own building. A
  building built after setup is held by the household that builds it. A
  Farmhouse household may also build a Silo and an optional Store, and a Tailor
  Shop household may build an optional Store; Stores are optional. An agent
  cannot give itself access to a household building its household does not
  hold. The two starting households receive the Farmhouse and Blacksmith
  automatically, as recorded in the [starter Town
  setup](../agents-and-families.md#agreed-starter-town-and-remaining-choices).
- **Removing or reassigning a building** that holds stock or jobs is refused
  until the stock is moved.
- **Workshop** is communal and held by the Town. It remains for agent
  inventions/mods and is usable by outsiders. Its footprint is **2×2**. Its
  mechanic/code is a **late-development phase**:
  first establish a functioning simulation and broader base asset set, then
  build the invention/mod loop on that foundation. This is staging, not removal
  from the intended finished game. Remove separate Cooking fire and
  Stone hearth buildings; House handles cooking. Remove Weaving frame and
  bed/bedroll content.
- **Tailor Shop** is a household-held building, **1×1 or 2×2**. It turns plant
  fiber into cloth and cloth into clothing, and cloth is a real item that can be
  held and traded. The Weaving frame and its "Woven clothing" are removed
  outright as soon as the Tailor Shop works, with no legacy transition; old
  alpha saves need not keep loading, and no migration code is written for them
  (see [Saves](../saves.md)). Its recipe numbers, costs
  and work time are **provisional** and will be tuned in playtests; this chapter
  does not fix them. The holding household sells clothing
  directly from the building, as the Blacksmith does, and does not need a Store.
- **Town Hall** is a **3×4** building that the Town raises as a [shared Town
  project](buildings-and-projects.md#shared-town-projects). **Agreed on October 8
  ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):** in
  government it is only a notice place, where agents post and read notices;
  every Town governs from its founding, with or without one. **Agreed on
  October 8 ([#1237](https://github.com/compoodment/ClankerWorld/issues/1237)):**
  a resident with no House may shelter from a storm in their own Town's Town
  Hall. Natural cover such as a forest or tree gives only partial protection
  ([The world](../world.md#survival-and-exploration)), so a House stays the best
  shelter.
- **Roads and bridges:** the world system generates infrastructure, not the
  player or individual agent. The full agreed road rule is below; the starter
  Path becomes a Road in intended content.
- **Livestock and mounts** belong in the finished game. Hostile predators do
  **not** belong in the current plan, though they may be revisited later.
  The accepted base roster is **chickens (eggs), sheep (wool), cows (milk), and
  horses (mounts)**. Animals belong to households: care uses
  physically reachable feed and water, and collected products enter local
  stock rather than a global pool. Permission to ride a horse does not transfer
  its ownership. If care is missed, production and riding stop first. Breeding is
  autonomous but must have a population cap, not a player-triggered breeding
  action. The owner approved the complete [animal pipeline](#animal-pipeline)
  on October 6, 2026. Slaughter, hunting and predators are excluded.

### Animal pipeline

**Agreed by the owner in chat on October 6, 2026:** the complete proposal for
arrival, yards, care, products, breeding, old age and horses below was approved
with “i approve, go”. This extends [#166](https://github.com/compoodment/ClankerWorld/issues/166)
and resumes [#433](https://github.com/compoodment/ClankerWorld/issues/433).
All quantities, capacities, costs, times and lifespans below are provisional
playtest balance. No starting livestock is supplied.

#### Arrival, homes and ownership

New maps have small seeded groups of untamed chickens, sheep, cows and horses
on suitable land. An adult with a household and free animal space may approach
and tame one specific animal with two feed portions, one fresh water and a
short task. Taming assigns household ownership. An animal yard is a household
outdoor area on land the household may use, planned through ordinary legal
site selection. A House is required. Its 2×2 design holds four animals and
may expand to 2×4 for eight. Each stage costs eight wood and two rope.
Young animals occupy places; an expected birth reserves a place. The yard has
sixteen cargo units of supply storage. Animals wander inside it and adults
can lead them along legal routes when relocating them.

Animal care, ownership and riding permissions are distinct. Any adult household
member may care for its animals; a named outsider needs a separate permission.
Nearby adults may explicitly agree to sell or give an exact animal, with exact
payment and receiving-household space checked. The animal must be dismounted
before transfer. Transfer ends old permissions and preserves physical position:
the buyer leads the animal home. Household abandonment does not make private
animals public salvage. Inheritance follows the ordinary property rules.
Paused Developer Tools may add animals; each direct edit is recorded in the
Event Log.

#### Care and products

One feed portion is one unit of grain, wild greens or cultivated greens.
Adults bring feed and actual water-filled jugs to the yard. Care consumes
carried supplies or physically delivered yard stock and protects existing food,
planting and production reserves; it never consumes distant House stock.
Jugs remain after their contents are used.

| Animal | Feed per day | Water per day | Product with care |
| --- | --- | --- | --- |
| Chicken | 1 | 1 | An adult hen lays one egg per day. |
| Sheep | 2 | 2 | An adult sheep supplies two wool every three days, collected by shearing. |
| Cow | 2 | 2 | An adult cow supplies two milk per day; no separate lactation cycle. |
| Horse | 2 | 2 | An adult is available for riding and carrying. |

Missed care pauses production, breeding progress and riding. Restoring care
resumes unfinished progress. Neglect does not kill animals in this version.
Untamed animals forage from reachable wild greens and drink from reachable
fresh water, never private crops or goods. Each untamed herd has an eight-animal
cap. An animal retains at most one ready product batch before production pauses.
Collection requires reaching it and having receiving space. Eggs and milk
spoil; wool, hides and leather do not. Collected products belong to the
household and move physically into House, Restaurant, Tailor Shop, Store or
Market stock. **Agreed by the owner in chat on October 11, 2026:** milk is an
ordinary stored good, like eggs. Collecting it needs receiving space but no
jug, and it is carried, stored, sold and cooked like other goods, with no
record of the jugs or sales it has passed through. Jugs hold water only.

| Pipeline | Trial recipe and destination |
| --- | --- |
| Cooked eggs | House or Restaurant: two eggs + one wood → two cooked egg portions. |
| Milk porridge | House or Restaurant: one grain + one milk + one wood → two milk-porridge portions. |
| Rich Restaurant meal | Restaurant: one bread + one egg + one cultivated greens + one wood → two rich meals. |
| Wool clothing | Tailor Shop: two wool + two cloth → one padded coat. |
| Leather | Tailor Shop: one hide + one fresh water + one wood → two leather. |
| Leather sack | Tailor Shop: two leather + one rope → one leather sack, carrying 32 units. |
| Saddle | Tailor Shop: two leather + two cloth + one rope → one saddle. |

#### Breeding and old age

Animals have male/female sex. A compatible adult pair sharing a yard breeds
autonomously when cared for and there is space. Each pregnancy or hatching
cycle produces one offspring and reserves its place. A household holds at most
eight animals, including young and reserved births. New breeding also needs
enough delivered supplies for existing animals and the offspring. There is a
two-day recovery period after birth. Care shortages pause breeding progress.

| Species | Birth or hatching | Adulthood | Old-age lifespan |
| --- | --- | --- | --- |
| Chicken | 2 days | 3 days | 30 days |
| Sheep | 4 days | 5 days | 45 days |
| Cow | 6 days | 7 days | 60 days |
| Horse | 6 days | 7 days | 60 days |

Old age is the first animal mortality rule. Naturally deceased sheep, cows and
horses leave one collectible hide; chickens leave none. Remains and hides keep
their recorded owner. Hunting, slaughter and meat are excluded.

#### Horse travel and inspection

A cared-for adult horse with a saddle carries one adult rider at twice walking
speed with eight additional cargo units. Legal terrain, blocked corners,
occupancy and route rules still apply. Household adults may ride. A named
outsider requires revocable riding permission, which grants neither ownership
nor household-stock access. Dismounting leaves horse and cargo at their actual
position; losing care or permission safely stops further riding. Horse-drawn
carts and transporting horses by boat remain later work.

Each animal has an inspectable name, species, age, household, location, care,
ready product and permissions. Sprites show young/adult animals, movement and
mounted horses with eight directions. Agents plan supply trips, care,
collection, crafting and trading through ordinary decisions. Suggestions and
orders use those same physical rules. The Event Log records taming, care,
collection, births, transfers and deaths.
