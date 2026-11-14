---
title: Buildings and projects
type: game-design
status: active
updated: 2026-11-14
---

# Buildings and projects

Building sizes, site choices and shared Town projects.

### Agreed content and building sizes

Computment accepted all unmentioned entries of the initial
[asset-roster review](../content-list.md) and amended specific rows
as recorded here. The roster's numbered entries are the detailed accepted
**base content**. The owner subsequently approved the
[item and resource pipelines](item-and-resource-pipelines.md#item-and-resource-pipelines) on October 1; their
quantities remain provisional. Other open costs, capacities, animation budgets
and invented content are not silently approved. In particular:

- Terrain uses roughly two subtle textures per surface, with most little
  grass/stone/leaf details baked into texture variation. A tile has at most
  one tree. Iron, gold and diamond deposits are visibly distinct
  **ore-bearing outcrops**, not ground veins.
- The farm roster includes **universal grain** (no named grain species),
  **potatoes**, a cultivated leafy green distinct from wild greens that
  restores more fullness, and orchard fruit. Flour is a sellable Farmhouse
  intermediate, including at a Market; cloth, made at the Tailor Shop from
  plant fiber, is likewise a real intermediate item.
- The Blacksmith refines **iron ore into a separate metal item**, then uses it
  to make tools. **Agreed on October 1
  ([#651](https://github.com/compoodment/ClankerWorld/issues/651)):** the
  Blacksmith may also sell spare refined iron from its stock, as the Farmhouse
  sells flour and the Tailor Shop sells cloth. The Blacksmith already sells
  tools directly and takes tool-making requests—no separate Store is required
  for its own products.
  A tool-making request asks that household to use its own materials and normal
  Blacksmith work. Placing or accepting the request takes no payment and grants
  no claim to unfinished goods. Once a real tool is finished, its owner may
  quote an ordinary barter exchange; both sides still choose whether to trade
  and bring their goods to the shop. The request does not lock a price, reserve
  future property or grant private-stock access. Missing ingredients and full
  workplace storage remain visible blockers. A withdrawn request leaves work,
  materials and finished goods with their existing owners. Market stalls and
  shared Town construction follow their separate accepted rules.
- The accepted medical goods include **bandages and medicine**. Their cloth
  and herb/water/fuel supply chains are agreed in [Item and resource pipelines](item-and-resource-pipelines.md#item-and-resource-pipelines); detailed recovery rates
  remain provisional. The October 2 decision stages bandage production, storage
  and trade now; injury causes and bandage treatment wait for the injury stage,
  agreed on October 8 in [Clinic supplies and illness
  care](item-and-resource-pipelines.md#clinic-supplies-and-illness-care).
- Farmhouse **1×1 or 1×2**; adjacent private Silo **1×1**; Blacksmith
  **1×2 or 2×2**; Tailor Shop **1×1 or 2×2**; Workshop **2×2**;
  Restaurant **1×2 or 2×2**; Clinic/healer's shop **1×1 or 1×2**.
  Market's main building is **2×2** with separate **1×1 stalls**.
  **Agreed after the October 1 art review:** the Market building is a hall
  without stalls, and its stalls stand on an open plaza of packed earth around
  it, the same ground as Roads laid as an area rather than as streets.
  [Earlier Market plot size](../../archive/towns-history.md) is recorded in the history.
  For eight stalls computment settled on a
  **7×4** plaza in the October 1 art review: 7×5 was "a bit too big for the
  eight stalls", and 5×4 lost the open column on each side that it needed.
  The stalls stand in two back-to-back rows of four, each facing an aisle,
  with a path from the hall's door down the middle. **Agreed on
  October 1, 2026:** the Town owns the stalls. A new Market comes with a
  starter set; when sellers need more, the Town builds them on the plaza as
  Town building projects, using Town materials. The size of the starter set
  stays provisional. **Agreed on October 8
  ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):** a
  Market's plaza stays 7×4 with at most eight stalls; a Town that needs more
  builds a second Market. Town Hall is **3×4**. These are building/plot
  footprints, not interior rooms.
- Port is **2×4**, rotatable to all four cardinal directions. One tile of its
  four-tile length rests on land; three extend over water. Keep clear docking
  space along both long sides of that three-tile water section. **Agreed after
  the October 1 art review:** a Port can hold up to **six moored boats**,
  three along each long side. A legal land approach and clear docking space
  are required. Boats are communal Town
  property, usable by residents or visitors with permission; the first
  [boat journey](../world.md#first-boat-and-port-travel) needs a completed Port at
  each end. **Agreed on October 4, 2026
  ([#919](https://github.com/compoodment/ClankerWorld/issues/919)):** each of
  the three water rows has one adjacent water dock on each long side, six in
  total. Keep those six tiles clear of construction. Agents request trips at
  a legal land approach and use the [boat queue and reservation
  procedure](../world.md#first-boat-and-port-travel). Construction costs remain
  provisional.
- Combat gear includes a spear, sword, shield and armor alongside the
  dual-purpose axe. Clothing does not change an agent's map appearance; the
  visual treatment of worn armor remains open.
  Maps, written records and books are accepted knowledge goods; law, claims,
  ownership and wills are mainly inspectable **data/gameplay**, not a demand
  for separate physical document objects.

The accepted roster includes proposed-but-now-chosen families such as clay,
pottery, rope, orchard fruit and carry aids. Their sources, processing and uses
are now agreed in the [pipelines](item-and-resource-pipelines.md#item-and-resource-pipelines); numerical balance is provisional.

### Choosing building sites

Agents identify a building need and do the work. The Town layout system offers
**several ranked viable sites** rather than letting an agent search every tile.
The agent can accept or reject; after rejection, they explain what was wrong
and the layout system re-ranks sites. Site ranking should account for
terrain, resources, existing buildings, ownership, access, other Towns, room
for growth and building purpose. Town appearance/layout should vary by
Town/culture. Exact ranking weights are open.

**Agreed on October 8 ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):**
an offer lists up to five ranked sites, and never more than eight. Not choosing
counts as rejecting the offer. A blocked project tries again after 60 ticks.
These numbers are provisional. When no legal site exists, the agent is pointed
to the ways to get one: a household land request or a Council land claim, as
described in [Town land and household use
rights](land-and-roads.md#town-land-and-household-use-rights).

**Agreed, what a household plans:** a household plans only the buildings it
needs for itself: a House, or a Farmhouse, Blacksmith, Store or Tailor Shop it
does not yet hold. It plans at most one of each kind, and only once it has the
materials in hand. Only the household that holds the Farmhouse plans a Silo,
next to its Farmhouse. The **Workshop is the one exception**: a household may
plan it, and once it is built the Town holds it. Other shared buildings use
the Council-approved Town projects below. Any Town resident may still plan the
Warehouse's expansion once its stock is nearly full.

**Agreed on October 16, by the owner in chat, after a playtest showed agents
overbuilding:** a Town grows by what its people need and can actually use, not
because there is wood to spend. In that playtest, four agents in two
households built two Stores, two Tailor Shops, two Restaurants, two Clinics,
two Farmhouses with Silos but no fields, and two Animal Yards on an island with
no animals. Two rules replace the earlier "no Town-wide limit until playtests
show agents overbuilding":

- **A work building needs work it can do.** A household plans a work building
  only when what that building works with exists and the household can reach
  it. For example:
  - a Farmhouse and its Silo only once the household has farm fields, or a
    seed site near the Town it can plant from;
  - an Animal Yard only when there are animals the household can reach and
    tame, and a second yard only when the first is full;
  - a Tailor Shop only with a supply of fibre, wool or hides it can get;
  - a Restaurant only when the Town has food to cook beyond what its
    households eat.

  The same holds for every other work building, using the inputs its own
  recipes need.
- **One of each shared shop per Town until it grows.** A Town holds at most
  one Store, Tailor Shop, Restaurant, Clinic and Blacksmith of each kind for
  every eight residents, rounded up. A second of a kind may be planned only
  once the Town has more than eight residents, a third once it has more than
  sixteen, and so on. Eight is provisional, to be tuned in playtests.

### Shared Town projects

**Agreed by the owner on October 2, 2026 ([#760](https://github.com/compoodment/ClankerWorld/issues/760)):**
an adult Town resident may propose a named shared construction project with
its site and material budget. The legitimate current Council must approve it
through the ordinary proposal and majority procedure before residents may
spend Town materials or start that project. Pending votes follow the Council's
existing membership and ballot rules; proposing a project grants no permission.

This covers Markets and their Town-owned stalls, Town Halls, Ports, street
lanterns and crafting communal boats at a Port. Resident adults physically supply and build the
approved project with real materials. Approval creates no goods, transfers no
private stock and grants no access to household or personal supplies. Private
donations need separate consent and the ordinary physical transfer rules.

Permission is bound to the approved project, site and material budget. A
different site or larger budget needs another approval. Ownership, land rights,
routes, carrying room, storage, work and reservations still apply. Cancelling
or blocking a project must preserve its goods and safely release unspent
reservations. This is a supported construction effect; free-form law text
cannot execute it. The existing resident rule for Warehouse expansion stays
in place. Costs, quantities and work times remain provisional for playtesting.

**Street lanterns, agreed by the owner on October 3, 2026:** agents build
street lanterns beside Roads; the map does not place them by itself. A lantern
needs **no fuel**: once built it lights itself at dusk and goes out at dawn.
There are two designs, a round **stone lamp** with an open flame and a
**hanging lantern** on a roadside post with an arm over the Road
([look](../interface-and-art/pixel-art-and-generated-images.md#pixel-art-and-generated-images)). **Also agreed
on October 3:** a lantern is a **shared Town project** like those above, so the
proposal names the lanterns, their Road-side sites and their material budget,
and the Council approves it before residents build. Costs are provisional
trial values to tune in playtests: a stone lamp takes stone, and a hanging
lantern takes wood and a little refined iron. Where lanterns stand is the
proposer's choice within the ordinary site rules; a suggestion is stone lamps
at Road junctions and hanging lanterns every few tiles along a Road.
**Agreed by the owner on October 7, 2026:** a lantern in an abandoned Town
stays dark at night until someone resettles the Town, and so does the lantern
on a Port's pier.

The intended building roles now include House, Warehouse, Workshop,
Farmhouse, farm fields, an adjacent private farm Silo, optional household-run
Store, household-run Blacksmith with internal work stock and direct sales,
Tailor Shop, public Market and stalls, Town Hall, Port, Clinic, and optional
agent-founded Restaurant. A
Farmhouse processes crops; the household that holds it places fertile fields,
plants seeds, tends, harvests, and sells/trades the produce. Farm count
responds to **Town population and farm yields**; a shortage or reduced yield can justify
more farming rather than a hard cap blocking recovery. Farms plan fields for
the whole Town, as agreed in [Food and replanting](item-and-resource-pipelines.md#food-and-replanting); the
exact numbers are provisional. Farm work stock
is private to its household; its Silo is distinct from the public Town
Warehouse. The Blacksmith makes and sells tools on site and can accept
specific tool-making requests, with stock inside the building. A Store
supports a household selling its products. Goods must be physically carried
to the Store and kept in its own stock before sale; a Store cannot sell from
a remote House, farm, or Warehouse inventory. A Market admits traders from any
Town; a Restaurant buys ingredients, cooks and sells potentially better meals.
Town Hall supports governance and Port supports boats. The physical
stock and barter rules are now agreed in the [pipelines](item-and-resource-pipelines.md#item-and-resource-pipelines), currency in
[Physical trade and production safeguards](item-and-resource-pipelines.md#physical-trade-and-production-safeguards)
and an empty household's property in [Household goods and
departure](households.md#household-goods-and-departure). Other ownership edge cases and
later pricing still need their own decisions. See the
[accepted asset roster](../content-list.md) for specific content and
[current state](../../what-works.md) for what exists in the prototype.

Wood, stone, iron, gold, diamond and many other crafting resources are wanted.
Better tools should gate harvesting/mining more advanced resources, creating
a progression incentive. The agreed initial ladder is **wood tools → stone →
stone tools → iron → iron tools → rarer materials**. Gold, diamond and other
materials are extracted with iron pickaxes and used for ornaments or trade,
as agreed in the [pipelines](item-and-resource-pipelines.md#item-and-resource-pipelines). **Agreed on October 8
([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):** iron is
the top built-in tool tier; any further tier comes from agents'
[inventions](../inventions-and-mods.md). Wood and stone should both be useful for House construction;
specific costs are open.

**Clanker's still-open implementation proposal:** use one incremental layout
service for starter arrangement and later construction requests. Filter
impossible footprints, rank sites by purpose/access/terrain/resources/Town
shape/future growth, and generate a connected Road layout with each accepted
building. The generator should not preselect a fixed lifetime building count.
