---
title: Goods and production
type: game-design
status: active
updated: 2026-11-14
---

# Goods and production

Food, materials, tools, care goods, production and trade.

## Item and resource pipelines

### Agreed on October 1

The owner approved these complete pipelines on October 1, 2026. They describe
intended gameplay; [what works today](../../what-works.md) says how much of it
the game has. Sources, production
sites, uses and ownership rules below are agreed. Recipe quantities, yields,
work times, storage sizes, wear, spoilage and effects are provisional and are
tuned in playtests. See [what works today](../../what-works.md) for current play.

Every pipeline covers **source → gather or grow → carry → store → process →
use or sell → replenish or replace**. Named goods replace generic raw `food`
and universal crop `seed` where those hide different resources. A simple
cooked meal remains a specific item with a generic meal icon.

#### Food and replanting

| Source | Product and complete path |
| --- | --- |
| Berry bush | Gather berries, carry home or into sale stock, eat fresh or sell; the bush regrows. |
| Wild greens patch | Gather wild greens, eat fresh or cook; the patch regrows. |
| Grain field | Plant grain seed, tend, harvest grain and replacement seeds, reserve replanting stock, carry grain to private Silo/Farmhouse stock, then make porridge or mill flour. |
| Potato field | Plant potatoes, tend and harvest, reserve planting potatoes, then cook or sell the remainder. Potatoes are both the crop and planting stock. |
| Cultivated greens field | Plant cultivated-green seed, tend, harvest greens and replacement seeds, reserve replanting stock, then eat, cook or sell. Cultivated greens nourish more than wild greens. |
| Orchard tree | Plant orchard seed, let the tree mature, then harvest fruit and some orchard seeds in autumn. Eat or sell fruit and reserve seeds for more trees. |

Berries, fruit and greens provide immediate food. Grain and potatoes normally
need processing or cooking. Fields pass through prepared, planted, growing,
ready and harvested states. Farmers keep enough planting stock for the next
crop before offering a surplus for sale. Grain seed, cultivated-green seed,
wood-tree seed and orchard seed are distinct; planting potatoes remain potatoes.

**Agreed on October 8 ([#1248](https://github.com/compoodment/ClankerWorld/issues/1248)):**
farming does not wear out the soil, and a field keeps the fertility of its
land. A field that nobody works for a full season goes back to ordinary grass.

| Finished product | Trial recipe | Work site and purpose |
| --- | --- | --- |
| Flour | 1 grain → 1 flour | Farmhouse; a real intermediate that can be stored, carried and sold separately. |
| Simple meal | 2 potatoes/greens + 1 wood → 2 meals | House; everyday household cooking, using potatoes, wild greens or cultivated greens. |
| Porridge | 1 grain + 1 water + 1 wood → 2 servings | House or Restaurant; accessible grain-based nourishment. |
| Berry or fruit porridge | 1 grain + 1 berries/fruit + 1 water + 1 wood → 2 servings | House or Restaurant; named improved meals that can be eaten or sold. |
| Bread | 2 flour + 1 water + 1 wood → 2 servings | House or Restaurant; keeps longer and travels well. |
| Vegetable stew | 1 potato + 1 cultivated greens + 1 water + 1 wood → 2 servings | House or Restaurant; a substantial vegetable meal. |
| Restaurant meal | 1 bread + 1 cultivated greens + 1 wood → 2 servings | Restaurant; better nourishment and dietary variety, sold on site. |

Harvests stay at their field until collected; they do not appear remotely in a
Silo. Grain and planting stock enter private farm storage. Ready food goes into
House or business stock. Raw grain, potatoes and flour keep their freshness in
the current trial. Greens, bread and cooked meals spoil, with a usable storage
pot slowing the loss. These storage and nourishment rules remain provisional.
Food stays out of the
communal resource Warehouse.

Farm production responds to population, expected yield and stored reserves.
Shortages can justify more fields or another farming household. The target for
playtesting is roughly two substantial meals per agent per day, leaving time
for other activities; this is not a fixed nutrition or fullness-drain formula.

**Agreed on October 8 ([#1235](https://github.com/compoodment/ClankerWorld/issues/1235)):**
farms plan fields for the whole Town they feed, not only for their own
household. The Town's food need, less the food already held in reserve, is
split across its farming households, so a Town with one farm still grows food
for everyone. The exact formula and numbers are provisional.

#### Materials and useful destinations

| Material | Source and processing | Uses |
| --- | --- | --- |
| Wood | Fell trees with an axe, carry wood, recover tree seeds and replant. Loose fallen wood can also be collected by hand. | Buildings, expansion, fuel, tool handles, carts and boats. Initially one wood item; separate logs and planks wait until they create useful decisions. |
| Stone | Extract from a finite outcrop with a wooden pickaxe and carry it. | Stone tools, building foundations and expansion. |
| Iron | Extract iron ore with a stone pickaxe; at the Blacksmith, a trial 2 ore + 1 wood makes 1 refined iron. | Iron tools, fittings, carts, weapons and armor. Ore and refined iron remain separate items. |
| Plant fiber | Gather fiber plants or reeds; the plants regrow. | Cloth, rope and baskets. |
| Cloth | Tailor Shop; trial 3 fiber → 1 cloth. | Clothing, sacks, bandages and later book covers. |
| Rope | House crafting; trial 3 fiber → 1 rope. | Sacks, carts, boats and later construction recipes. |
| Clay | Dig a clay bank and carry clay. | Fired storage pots and water jugs. |
| Pottery | House crafting; trial 2 clay + 1 wood → 1 vessel. | Reusable storage pots and water jugs, with separate item identities. |
| Fresh water | Collect from a river or lake with a reusable jug and carry it. | Used in current porridge, bread and stew, Clinic medicine-making and animal care. |
| Gold | Extract gold ore from a gold-bearing outcrop with an iron pickaxe, carry it to the Blacksmith and refine it with fuel. Ore and refined gold remain separate goods. | Ornaments, gifts and trade goods. |
| Diamond | Extract from a diamond-bearing outcrop with an iron pickaxe. | Trade goods or a stone set into an ornament. |

The Blacksmith makes gold ornaments, optionally set with a diamond. Agents may
keep, wear, gift or sell them. This is the agreed destination for rare materials;
it does not select gold as a universal currency or add a diamond tool tier.

An ornament remains a real owned item when worn. Wearing it changes neither
protection nor carrying capacity, and does not change the agent's map appearance.
Removing it leaves the same unit in carried stock. A named gift or barter moves
actual goods to a nearby recipient through the existing inventory authority;
foreign, reserved or promised goods remain protected. Production and shop
exchanges use the same local-stock, receiving-space and private-access rules as
other Blacksmith work. Ornament deterioration is not part of this stage.

The ornament trial uses 2 gold ore and 1 wood to refine 1 gold in 24 work ticks;
2 refined gold make 1 gold ornament in 24 work ticks; 1 gold ornament and
1 diamond make 1 diamond ornament in 28 work ticks. All these quantities and
work times remain provisional. Wearing, removing and giving an ornament are
deliberate choices through a fresh accepted personal-model response. Jev,
fallbacks, repeated intentions and owner orders do not make those choices.

A storage pot slows spoilage for the food within its limited capacity, a trial
8 food. A water jug holds a limited quantity of water, a trial 4 fresh water.
Both remain after their contents are used; filling, emptying and carrying them
must preserve the vessel and its goods. A filled vessel moves as one family
only when its entire load fits and moving every ingredient leaves the source's
needed reserve intact. Active claims still prevent its movement.
Adults dig clay from a finite clay bank
and make either vessel at a household House. Houses and Restaurants consume
fresh water from these jugs for porridge,
bread and stew, leaving the jug ready for refilling. The Clinic also consumes
delivered fresh water to make medicine and leaves the jug reusable. Animal care
also uses jug water, as the [animal pipeline](buildings-and-animals.md#animal-pipeline) describes.

#### Tools, clothing and transport

| Equipment | Production and function |
| --- | --- |
| Crude wooden axe and crude wooden pickaxe | Any household's own House, using wood; the same tree and stone gathering as wooden tools, but worse. |
| Wooden axe and wooden pickaxe | Blacksmith, using wood; basic tree and stone gathering. |
| Stone axe and stone pickaxe | Wood and stone; better speed and durability. The stone pickaxe unlocks iron extraction. |
| Iron axe and iron pickaxe | Wood and refined iron; better speed and durability. The iron pickaxe unlocks rare deposits. |
| Wooden hoe | Wood; prepares and tends fields. |
| Iron hoe | Wood and iron; performs field work faster. |
| Hammer | Wood and stone; helps construction and repairs. |
| Wooden sickle | Wood; speeds crop harvesting. |
| Iron sickle | Wood and iron; harvests faster than the wooden sickle. |
| Knife | Iron; speeds food preparation and suitable crafting work. |

Tools wear through use. The Blacksmith repairs worn tools with some of their
original materials; broken tools need replacement. Hand collection of loose
fallen wood keeps replacement wooden tools possible after the last axe is lost.

**Agreed by the owner on October 3, 2026
([#917](https://github.com/compoodment/ClankerWorld/issues/917)):** an adult can
make a crude wooden axe or a crude wooden pickaxe at their own household's
House, using wood. Fallen wood gathered by hand is enough, so no Town, shop or
Blacksmith is needed. A crude axe fells trees, and a crude pickaxe extracts
stone. Both do these jobs less well than the Blacksmith's wooden tools: slower,
shorter-lived or both. The Blacksmith therefore stays the place for better
tools. An agent who starts far from any Town can still gather wood and stone,
build a House and later a Blacksmith of their own. Crude tools wear like other
tools. The recipe, work time and how much worse crude tools are remain
provisional trial values.

| Product | Production and effect |
| --- | --- |
| Basic garment | Fiber → cloth → Tailor Shop; trial 2 cloth → 1 garment. Carry it home, wear it, and repair wear with cloth. |
| Padded coat | More cloth at the Tailor Shop gives stronger cold protection. |
| Rain cloak | Cloth and fiber at the Tailor Shop reduce rain exposure. |
| Basket | Fiber and rope, crafted at home, give a modest carrying increase. |
| Sack | Cloth and rope at the Tailor Shop give a larger carrying increase. |
| Handcart | Wood, iron fittings and rope at the Blacksmith allow large loads, especially on Roads. |

Baskets and sacks use one equipped carrying slot. A handcart is a visible
object that an agent pulls, parks, repairs and transfers ownership of. Worn
clothing still does not change the agent's map sprite. Exact carrying amounts,
cart movement costs, protection and repair quantities are tuned in playtests.

The handcart trial uses 1 refined iron to make 2 fittings, then 4 carried wood,
2 carried fittings and 1 carried rope at a household Blacksmith to build one
personal cart. Its 32 cargo units are separate from the puller's carried load.
The owner must reach it to attach, load, unload, park, repair or give it away.
Loading household goods still requires that household's permission and physical
pickup. Giving a parked cart transfers that cart and its contents together,
without changing access to any other property. Carts carry loose goods;
filled pots and jugs remain separate carried vessels in this trial.

A pulled cart follows cardinal legal foot routes and avoids unroaded mountains.
Road travel has the ordinary foot movement cost and less cart wear; other
routes take an extra movement wait. A broken cart stops at its last position
with its load intact. Unloading can move goods into the owner's available
carrying space or set them down as owned ground goods, including when the cart
or contents are damaged. One carried wood, fitting and rope repair the cart
where it stands. These recipes, limits and rates remain provisional.

**Agreed on October 8 ([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):**
the built-in transport is the handcart, the saddled
[horse](buildings-and-animals.md#horse-travel-and-inspection) and the [first
boat](../world.md#first-boat-and-port-travel); nothing else is built in.
Horse-drawn carts are reconsidered after the horse playtest.

The current equipment trial allows 8 cargo units without an aid, 16 with a
basket and 24 with a sack. The equipped garment and aid each occupy their own
slot; other goods, delivery loads and vessel contents count as cargo. Stored
or ground goods do not count as carried. Broken aids give no extra capacity.
When an aid breaks or is replaced, excess cargo and the old aid stay real
property; the agent must make room before collecting more.

The House trial uses 3 fiber for a rope and 3 fiber plus 1 rope for a basket.
The Tailor trial keeps 3 fiber per cloth and 2 cloth per basic garment, uses
4 cloth per padded coat, 2 cloth plus 1 fiber per rain cloak, and 2 cloth plus
1 rope per sack. Only the equipped garment protects against weather, with
less protection as it wears. Garments wear during exposure and carry aids
wear during travel with cargo. Adult household members repair garments and
sacks at their Tailor Shop with 1 cloth, or baskets at their House with
1 fiber and 1 rope. Eight work ticks restore 60% condition, up to full
condition. Interrupted repairs release unspent materials. These quantities,
capacities, wear rates and repair effects remain provisional.

#### Care and later goods

| Product | Complete path |
| --- | --- |
| Bandage | Fiber → cloth → cut bandages at a House or Tailor Shop → store, carry or trade. Injury treatment is deferred under the October 2 decision. |
| Medicine | Gather medicinal herbs → carry to Clinic → herbs, water and fuel make medicine → administer to an ill agent. |
| Clinic treatment | The Clinic stocks its own medicine and bandages. A patient visits or a caregiver carries supplies to them; treatment consumes the required goods. |
| Eggs | Household chickens with reachable feed, water and care produce eggs; collect into local House/Restaurant stock, cook or sell. |
| Milk | Household cows with feed, water and care produce milk; collect, drink, cook or sell. |
| Wool | Care for household sheep, shear them and carry wool to the Tailor Shop for warm clothing. |
| Leather | Naturally deceased livestock provide a hide; the Tailor Shop processes it into leather for durable clothing and carry gear. |
| Horse transport | Household horse, feed, water and care → permitted rider or cargo use. Permission does not transfer ownership. |
| Boat | Wood, rope and iron fittings → construct at a Port → communal Town boat → carry agents and goods between completed Ports. |
| Spear and sword | Wood and iron at the Blacksmith → equip → combat use → repair or replace. |
| Shield and basic armor | Wood, cloth and iron at the Blacksmith → equip → protection → repair or replace. |
| Maps, records and books | Fiber and water make paper; write actual learned knowledge, bind where needed, then carry, read, copy or sell. Cloth may supply a book cover. |

Bandages are intended for the later injury stage; medicine supports gradual
illness recovery. Neither is an instant full-health restoration. Medicinal
herbs, paper, hides and ornaments are approved
additions to the catalogue. The [October 6 animal pipeline](buildings-and-animals.md#animal-pipeline) settles animal care, breeding,
old age, products and mounts, with provisional balance. Combat and invention
work retain their later staging; these item pipelines do not settle their
detailed actions. See [combat](../agents-and-families.md#combat) and
[inventions](../inventions-and-mods.md).

#### Clinic supplies and illness care

**Owner decision on October 2, 2026:** "Defer injury causes; build supplies now."
This stage makes, stores and trades bandages, but does not create injuries or
apply bandages to a patient. Medicine uses the existing illness system and
supports recovery gradually. The injury stage is agreed below. See
[current availability](../../what-works/life-work-and-society.md#life-work-and-society) for the build and
pending Windows checks.

**Injuries, agreed on October 8
([#1267](https://github.com/compoodment/ClankerWorld/issues/1267)):** the injury
stage uses injury states rather than simple health-point damage; how fights
cause them is in [Combat](../agents-and-families.md#combat). At first injuries
come only from fights; rare work accidents come later. Bandages and the Clinic
treat injuries through the agreed [Clinic treatment](#care-and-later-goods)
path: a patient visits, or a caregiver carries supplies to them, and treatment
consumes the goods. Only an untreated or repeated severe injury can kill.
Recovery rates are provisional.

The first Clinic is a household-held 1×2 building. Trial construction uses
10 wood and 4 stone. At a House or Tailor Shop, 1 cloth makes 2 bandages in
8 work ticks. At the Clinic, 2 medicinal herbs, 1 fresh water and 1 wood make
2 medicine in 16 work ticks. Water must arrive in an actual reusable jug;
using its contents leaves the vessel intact. Costs, quantities, time, stock
targets and recovery rates remain provisional.

Patients may use their own medicine. An adult patient explicitly chooses a
named caregiver through a fresh, accepted personal-model decision and may
revoke that permission. A purchase, household membership, kinship, helper
choice, failed model reply, repeated intention or
owner order cannot supply that agreement; neither can Jev or a fallback choice.
A dependent's effective, accepted caregiver relationship supplies its existing
care authority; another relative
or household adult does not acquire it automatically. Treatment needs the
actual usable dose at the patient and does not give the caregiver access to
another household's stock.

An outside customer buys real Clinic goods through the ordinary local barter
rules. Buying a dose grants possession of that dose, not treatment permission
or private work access. Care does not reveal distant patients' unseen health,
location or private thoughts to an agent's model. Supplies, permissions,
consumed doses and gradual progress survive saving and reopening. Interruption
ends the remaining effect without refunding the consumed dose, and permission
renewed later cannot reuse it. Pausing freezes treatment time. Effect rates and
course length remain provisional; hands-on acceptance is still pending.

#### Physical trade and production safeguards

The Farmhouse sells produce, seeds and flour stocked at its location. The
Blacksmith sells tools and spare refined iron, and takes tool-making orders.
The Tailor Shop sells clothing, cloth and sacks. Restaurants buy ingredients
and sell finished meals; Clinics stock care goods and sell treatment. An
optional Store receives goods by actual delivery before selling them. Market
sellers carry goods into their stalls, trade with agents from any Town and
carry remaining goods away.

The first Restaurant version buys missing inputs for the recipes its household
needs, with a provisional reserve for two batches. Usable stock already there,
actual incoming deliveries and supplies an adult can physically collect reduce
the shortage. Supplying one workplace must preserve the ingredients another
still needs. An adult pays only with their personally owned carried goods, and
cannot spend a needed Restaurant ingredient in a way that increases its
shortage. Restaurant adults may visit ingredient shops, and any adult may visit
a Restaurant for meals, including in another Town
([#1242](https://github.com/compoodment/ClankerWorld/issues/1242)). The trip
reveals no remote private stock. Exact goods and
terms are checked after arrival, and both traders must meet before settlement.
The House remains the heating hearth; Restaurant wood is a cooking input.
Meal prices remain provisional barter terms tied to the recipe.

**Agreed on October 1, 2026:** a seller may use any empty Market stall and keep
it until they leave. The Town does not assign stalls. Leaving frees the stall
for another seller; goods keep their recorded owner and must be
carried away or transferred through an actual trade.

**Owner answer on October 1, 2026:** the Town owns the stalls that sellers
borrow. The Town adds stalls when sellers need them, using Town materials.

Initial trade uses barter offers naming exact goods and quantities. Both sides
bring their goods to the transaction. The buyer receives purchased goods
personally; payment becomes seller-household stock at that location. No
universal starting money or gold standard is chosen.

**Currency and land prices, agreed on October 8
([#1256](https://github.com/compoodment/ClankerWorld/issues/1256)):** money
comes in two steps. First, a household can sell a land use right for goods
through the existing consent-based transfer, so land has a price in goods
before money exists. Later, a Council may approve a Town currency as a
[shared Town project](buildings-and-projects.md#shared-town-projects), minted from real metal. Coins are
physical goods that the game checks like any other item, they use the same
transaction system, and accepting them stays voluntary. No world starts with
money, so the unused starting Copper wallet is removed.

Ownership and physical location remain inspectable across every transfer.
Production reserves actual inputs and output space. Full storage, missing
materials, unavailable tools and blocked transport give a readable reason.
Guests and customers gain only the access their invitation or transaction
grants. A Store cannot sell goods from a remote House, farm or Warehouse.
