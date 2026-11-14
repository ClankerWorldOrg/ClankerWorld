---
title: Production and markets
type: architecture
status: active
updated: 2026-11-14
---

# Production and markets

This page describes production inspection, household work, barter and Markets.

## Building production inspection

The owner snapshot derives `AvailableRecipes` from active content matched to
its placed building's exact definition, including workstation-size variants.
Crop recipes and the retired generic-food and bedding transformations are
excluded from the workstation list. Each entry carries its registered name
and input/output quantities, rather than a client-maintained recipe table.

Owner observations retain every running or paused production job and the
newest 32 terminal jobs by start tick and identity. The checkpoint also retains
older jobs still needed as recipe, project, tool-request, food-origin or owner-order
evidence; unreferenced older work moves into the durable history archive.
Production jobs project the same recipe facts and `HeldInputs` grouped from
the job's own active inventory reservations. Completed or released
reservations contribute nothing; building storage remains a separate view.
Godot Details refreshes these facts even when work progress stays unchanged.
Missing fields from an older server leave recipe facts unavailable rather than
fabricating them. The recipe-facts projection changes neither world state nor agent
knowledge; the production-retention format is described in
[Saves and replay](../saves-and-replay/memories-and-history.md#bounded-production-history).


A recipe project that finds its work site busy waits with the blocker "Waiting
for a free work site". While anyone waits, no one else is offered a new recipe
for the same workstation design, so the waiting agent gets the next turn
instead of losing it each time the site frees. Field work uses its own physical
tile and cannot be started through a zero-input crop recipe.

Recipes do not depend on a role or on personality or aspiration text. An adult
resident is offered field work only when their household holds a Farmhouse,
and a workstation recipe only at a building their household holds or at a
communal one, which has no holding household. A Farmhouse or Blacksmith that no
household holds is nobody's workstation. First-Town setup gives the Farmhouse
to the first starting household and the Blacksmith to the second. The offline
check in `HouseholdBuildingUseCoverageTests` builds generated worlds the normal
way and runs two world days with the built-in rule-based chooser and no model
calls, failing if these offers disappear or cross households. The owner's direct
production request checks the same workstation ownership and age. Crop
recipes are refused; crops need tilled land, a hoe and real planting stock.

Recipe planning classifies workstations through the immutable canonical-ID
definition lookup and shared household-layout rule. Site selection checks the
actor’s current walking route, ownership and unused production capacity. An occupied or inaccessible workstation
does not hide another reachable one. Movement’s existing diagonal and household
access rules still apply. If no site is reachable, an existing project uses its
blocked/reconsideration path rather than travelling toward an unreachable tile.

Recipe preparation and production must use the same actor/building owner.
Direct production requests use the same age restrictions as autonomous choices:
only adults and elders may start workstation recipes or field work. A request for an
infant, child or adolescent is refused before reserving inputs or changing jobs.
Household workstation inputs must be present at the actual building; stock
elsewhere in the household is not on-site stock. Missing inputs block the
project under its existing retry rules, without granting another household's
materials or implicitly transporting remote goods.

Construction input selection checks the builder's current unoccupied route
to each stocked permitted Warehouse, using movement's exact destination,
diagonal, shared entrance and attached handcart rules. If none is reachable,
preparation continues to its existing material-gathering fallback. Collection
still rechecks authority, available stock and carrying room at the Warehouse.

Ordinary workstation supply choices and execution check the adult's current
native route to each destination before selecting its missing input. An
occupied earlier workshop does not hide a reachable House or another workshop.
The route is checked again as the actor moves; reaching a shared household
building can make another delivery possible. Supply still uses actual carried
or accessible stock, ownership, receiving space and carrying limits.

Blacksmith input hauling checks the actor's current unoccupied pickup route
and the source-to-shop route before selecting household or permitted Town
Warehouse stock. An occupied earlier lot does not hide later reachable stock.
The same selection is repeated when hauling or planning a supply order; exact
lot/item targets, carrying limits and receiving-space checks still apply.
Stock suppresses Blacksmith gathering only when the current delivery selector
can use it with cargo room, receiving space and the required routes. This also
applies to ore mining, harvest preparation and supply orders. Disconnected stock
keeps its owner and position while the adult gathers reachable local inputs.

Material harvest planning keeps room for the required carried tool as well as
the gathered goods and any tree seed. If a tool's full bonus exceeds the actor's
total carrying capacity even after other cargo is set down, it uses the existing
four-unit harvest instead. Bonuses that fit keep their full yield; current free
space, reservations and loaded return routes still govern the actual harvest.

If an unpaid household recipe remains blocked for 60 ticks and no household
member has an actionable way to supply its missing ingredients, the runtime
pauses its saved plan and stops trying to continue it automatically. The adult
can choose other work. After ingredients return to the building, choosing the
recipe again resumes the saved plan. This does not interrupt a running
production job.

Barter choices and offer creation require both agents to be adults or elders.
Infants, children and adolescents cannot receive an offer that reserves their
belongings while they have no legal trade response. The society transaction
checks age before reserving either party's stock. Household and organization
parties keep their existing inventory rules. Previously saved offers retain
their normal withdrawal and expiry behavior; loading does not rewrite them.

Personal barter keeps one usable carried unit of each ordinary item kind in
reserve across the owner's eligible lots, rather than requiring two units in
the chosen lot. Reserved, worn, contained, delivered, stored or uncarried
units cannot supply that reserve. Artifacts and unworn ornaments retain their
single-unit exception. Each offer still reserves one actual unit from each
named lot, and settlement requires both parties' independent acceptance.

A completed personal barter records a public relationship memory for each
trader about the other, alongside the usual trust increase. New memory keys
retain `trust-memory:` and use a SHA-256 digest of the complete directional
key when it would exceed personal recall's 128-character limit. Short keys
remain unchanged; saved owners, subjects and summaries retain their complete
values, so native-born traders can recall the exchange too. Later trades retain
one memory per direction, recognizing both original and bounded saved keys.

**Household shops** bind an inventory barter offer to its actual business and
holding household in `ShopTradeState`. Offers use unreserved goods in that
building and payment already carried by the adult customer. The inventory
remains the only authority for quantities, acceptance and lot reservations;
the business binding keeps the transaction location and readable outcome.
Positive net incoming space is held for both parties, alongside production and
inbound delivery space. Both traders must reach the shop before the household
accepts and the inventory transfers anything. Payment is placed in that exact
building; the purchase becomes personal cargo. Cancelled, expired or
invalidated offers release their reservations without transporting goods.
Buyers compare usable tool tiers and garment protection in the current weather.
Their best usable tool in each family, equipped clothing and carrying aids, and
the active equipment repair target are excluded from payment.
Customers receive transaction access only. Ingredient-shopping and Restaurant
meal trips may cross Town borders. Planning uses the public building kind and
menu, the customer's own demand and carried payment, and a reachable route;
exact stock and terms are checked only after arrival. Restaurant demand follows
the customer's household-owned Restaurants, even when they live in another Town.
Visiting or buying grants no Warehouse stock or private household access.
Store stocking first moves actual
surplus into carried delivery lots, then uses ordinary household hauling to
reach the Store. A remote House, field or Warehouse is never sale stock.
Store stocking keeps one available unit of each adult's best usable work tool
per family. Extra units in that same carried lot can be stocked, just as
separate spare lots can. Equipped garments and carrying aids stay protected.
Food stocking
keeps two usable servings of each food kind for the adult, or two per
living member when taking household stock. Each reserve is counted once across
eligible lots with the same owner and food kind, including when an order is
bound to one lot. Unreachable, reserved, spoiled, contained or other owners'
stock cannot satisfy the reserve.
Optional shelf restocking waits behind gathering materials needed by household work.
Rates, the eight-unit shelf target and four-unit carried loads are provisional.
Blacksmiths can sell real refined iron for another household's tool work.
Currency remains later work. The Clinic sells actual medicine
and bandages through the same inventory and physical business authority.

Released Town construction loads retain their Town owner and exact material
history. Resident recovery moves only available ground quantities to a reachable
Town Warehouse with actual room, leaving one carrying space for food. A split
load gets a released custody record in the original project's delivery ledger;
this does not revive its construction commitment or reservations. Urgent food or
warmth permits immediate set-down of released carried goods. A temporary path
obstruction while returning waits rather than dropping and recollecting cargo.

**Markets** use the same inventory authority with separate saved paid-building
and occupancy records. The Council-approved starter project pays for the 2×2
hall and only two 1×1 stalls on the fixed 7×4 plaza, in slots 0 and 4. When
every standing stall is borrowed and no further stall is proposed or under
construction, the next unused fixed slot may be proposed as a separate
Council-approved Town project. A standing Market's site tiles count as occupied
for other buildings, Town project sites, expansions, fields, tree planting and
household land requests. The provisional starter budget is 24 wood,
8 stone and 4 fiber with 10 work units; another stall costs 4 wood and 2 fiber
with 3 work units. General plaza growth has no implementation or agreed rule.
Physical stock receipts retain the personal or household owner. Each receipt
keeps complete native source and destination lot IDs, including long
split/move histories and a partial deposit's derived destination. Its hashed
identity and exact inventory event bind that history to the real deposit.
A named active adult borrows a stall while they remain inside the hall-and-plaza area;
leaving, household change, death or removal ends borrowing and releases
unfinished offer claims without transferring leftovers.

Market live records retain recent outcomes and the exact borrowing, deposit,
open-offer and payment bindings still needed by actual stock. Receipt sequences
survive retirement; an explicit boundary allows closed source history to leave
without granting selling authority. Closed inventory offers and claims retire
with their Market records, while paid construction/removal and physical property
remain intact. The [Market save rules](../saves-and-replay/markets-and-trade.md#paid-markets-and-stall-trade)
explain live bindings and archive/recovery behavior.

Loads, borrowing, deposits, collection and barter mutations need a fresh
accepted, non-fallback personal LLM choice; their candidates rank above
`safe_idle`, so built-in rules never pick them. Continued intentions walk only;
owner orders do not authorize these mutations. A member carrying their own
household's goods may return them to its House through `household_return`, and
the household hauling, farm stock and planting routines skip stock on a stall
while a member of that household borrows it. Explicit `market_collect` also
protects household stock from other housemates while borrowing is active;
the borrower may collect it, and personal owners may retrieve their own goods.
The same choices are checked again before applying a mutation or continuing
travel, using current borrowing.
Usable loose surplus (the food
reserve counts the owner's other usable stock of that kind), actual
carrying and stall room, active claims, current household rights and protected
equipment constrain the offered choices. During urgent food need, Market candidates
include only collection of edible food the adult may legally retrieve. This
filter runs before the candidate limit; normal Market choices retain their
existing order. Collection still needs a fresh personal LLM choice and the
usual ownership, reservation, route and receiving-space checks.
The owner-order survival classifier revalidates an edible collection against
the same Market choices, so urgent retrieval can interrupt an unrelated task.
It grants no new stock access or trade consent; the original order and its
progress remain available after the emergency. An interrupted order executes
collection only for the actual fresh admitted personal LLM choice. A matching
accepted intention can continue walking to the stall between decisions; it
cannot collect through the automatic continuation path.
Ordinary collection uses physical unreserved quantities, so spoiled stock can
be reclaimed from a current or former stall without deleting or refreshing it.
Urgent food collection additionally requires usable food; spoilage never
turns property recovery into a survival action. Execution resolves the current
choice again before moving its authorized quantity.
The provisional one-for-one quote is
an actual `Inventory.Offers` exchange. Buyers may belong to any Town or have no
Town membership; walking into the Market and completing a purchase change
neither their household nor their Town. The named seller accepts only after
both people meet at the stall. Purchased stock
becomes the buyer's personal cargo; payment is physically set down as the
seller's household stock, including payment for personally owned goods. A
later stall borrower cannot sell an earlier borrower's stock. Its recorded
owner, or a current member of the owning household, may physically collect it.
Live inventory ownership remains authoritative after inheritance or collection.
Customer access remains limited to the named transaction. See the
[Market save rules](../saves-and-replay/markets-and-trade.md#paid-markets-and-stall-trade) for the
saved layout, stock and offer checks.

Missing-input demand checks the buyer's actual production owner, keeping each
household's available materials separate. Blacksmith iron and ornament-input
purchasing reserves exclude household stock on an actively borrowed Market
stall. Released household stock counts again, preventing redundant purchases.
A nonterminal recipe plan marked
`RequiresFreshChoice` may choose a Market purchase while remaining paused; an
actively continuing plan keeps the adult at its work. Resuming the recipe
still requires its ordinary choice and physical ingredient-delivery rules.

`WantsFieldPlantingStock` reuses the actual field and planting-stock checks for
grain seed, cultivated-green seed and loose potatoes. The adult must belong to
a household holding a Farmhouse, have a usable hoe and have no urgent survival
need or actively continuing project. That household must need food, and the
adult must be able to reach its idle `Prepared` or `Harvested` field without
another person's planting claim. A usable personally carried planting unit,
accessible household stock or that field's reserved replanting lot satisfies
the same-kind need. The exact one-unit purchase remains personal cargo until
ordinary field work consumes it; a held unit suppresses further same-kind seed
quotes even when the seller still has stock. Other goods may still be wanted.
Demand creates no future seed buffer or access to another household's stores.
