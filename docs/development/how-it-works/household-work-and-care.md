---
title: Household work and care
type: architecture
status: active
updated: 2026-11-14
---

# Household work and care

This page describes tool requests, medical care and household production.

### Blacksmith tool-making requests

A tool-making request records bounded demand for an existing Blacksmith recipe.
A customer can have one active request; the world keeps up to 32 active requests
and 32 terminal records. Placing, accepting, refusing or withdrawing one requires
a fresh accepted personal-model choice. Jev, fallback, repeating intentions and
owner orders cannot make those commitments. Routine supply and already accepted
production continue through the existing household project machinery.
The four personal commitment choices rank below safe idle for the built-in
chooser, so it does not repeatedly select an action that cannot execute.
Walking to a known Blacksmith, continuing accepted work and opening a quote
for a completed tool retain their routine priorities. Opening the quote does
not accept payment or transfer goods; the existing barter consent checks apply.

Planning rejects unrelated workstation recipes before seller and route queries.
Fresh consent uses the same complete eligibility rule.

The accepted worker produces with actual household-owned inputs at the named
Blacksmith and its normal output-space reservations. The finished tool remains
household property. The request references the real production job and output;
it creates no future inventory, advance payment or exclusive customer title.
An eventual quote uses the existing completed-goods business barter offer and
its acceptance, meeting, stock-room and exact transfer checks. Withdrawal or
interruption does not undo work or change the owners of materials and goods.
A request stops when the customer or the shop becomes unavailable, when no
adult is left in the selling household, or when the customer joins that
household. If the worker leaves, the request stops; a housemate may still
resume the paused job as ordinary household work.

The observation projects at most eight requests per actual building, preferring
active requests and recent transitions. The relevant customer or household
member gets a scoped note of at most 256 characters; public blockers are at most
160. Notes expose request progress, never unrelated private stock or raw model
dialogue: the worker's own errands and storage problems appear only as "still
preparing" or "waiting for a work site". Owner cards show the requester, existing recipe and current blocker;
the actual building supplies its name.

**Clinic supplies and illness care** use the normal household building,
workstation supply, ecology and recipe paths. `clankerworld-care-v1` adds a
1×2 Clinic costing 10 wood and 4 stone, bandage recipes at the House and Tailor
Shop, and a medicine recipe at the Clinic. One cloth makes two bandages in
eight base work ticks; two medicinal herbs, one fresh water and one wood make
two medicine in sixteen. These quantities and times are provisional. Herbs
come from reachable renewable patches. Ingredients must arrive at the actual
workplace; medicine reserves water from a real reusable jug and leaves the
vessel intact. Injury causes and bandage treatment remain deferred.

The [#749](https://github.com/ClankerWorldOrg/ClankerWorld/issues/749) fix
returns empty household pots and jugs from a workstation to the household's
House using physical pickup and the existing delivery path. It keeps inventory
ownership and reservations authoritative and checks carrying room, the walking
route and destination space. As with other household deliveries, the hauling
adult holds the vessel during the trip and delivery hands it back to the
household. A save during the trip retains the same vessel and delivery.
Automated checks cover the return path.

Medical permission is admitted only from a fresh, accepted, non-fallback
`LargeLanguageModel` choice by the adult patient. Jev, owner orders, failed
replies and continuing intentions cannot grant or revoke that authority.
The patient may permit at most 16 living named caregivers. While a permission
slot remains, the candidate list offers up to 16 eligible nearby alternatives,
independently of the remaining slot count. Granting still checks the current cap.
Self-treatment is allowed, and a dependent's effective accepted `Caregiver`
relationship supplies their existing authority. Treatment checks living adult
caregivers, permission, local patient observation and actual usable medicine.
An unrelated household's stock must be bought through ordinary barter first.
Models receive no distant patient's hidden health or location through care. A
caregiver starting an offered medicine fetch records that locally observed
patient and their last-seen position in `MedicalSupplyTrip`. The same intention
continues to accessible stock, collects one real dose, then returns to the
recorded position. Leaving interaction range does not cancel the fetch.
Treatment still requires finding the consenting patient nearby; a patient who
has moved out of sight is not tracked remotely. Permission loss or death ends
the trip. Returning to an empty last-seen location, observing that care is no
longer needed, or starting treatment also clears it. Current-format reload
preserves both outward and return travel without reserving or inventing a dose.

Starting medicine reserves and consumes one actual dose through the inventory
authority. Its completed reservation binds the patient, caregiver, owner and
start time. The provisional course lasts twenty world ticks and removes
75 illness basis points per tick. Maintenance ends an interrupted course
without refunding the dose, including death, permission withdrawal or loss of
dependent-care authority. Closing the receipt's medical purpose prevents a
spent effect from being reattached after permission is renewed. Death releases
live reservations while preserving completed consumption receipts. Permission,
active progress and closed receipts survive current-format save/reload; a
paused world advances no treatment time. See [saves and replay](../saves-and-replay.md).
Automated checks cover this path; the
[Windows playtest](../../../playtest/alpha-1.md) is still pending.

Death archives the last physical state, frozen age and the Town the agent lived
in, then removes the active actor. Existing personal inventory is frozen in
estate escrow, and `SocietyEstate.BeneficiaryIds` records the household default.
[Wills](wills.md#wills) explains the final will and how the estate is divided.

New proposals for Shelters, Storehouses, Cooking fires and Stone hearths are
retired. Existing buildings, projects and recorded proposals remain for old-world
compatibility. Approved owner building designs stay active but are not household
kinds, so agents do not plan them; they wait for shared buildings. House fires supply heat. General invention is later Workshop work.
Fire-tending selects an unlit hearth the adult can reach, using the same
household access and interaction distance as movement. Selection is repeated
when tending begins, so changed occupancy can redirect the adult to another
hearth or leave warmth-seeking available. Fuel is consumed only at the hearth.

Clothing comes from a household's Tailor Shop (`clankerworld-tailor-v1`), which
replaced the Weaving frame and its "Woven clothing" recipe outright. The shop
weaves 3 fiber into 1 cloth in 20 ticks and sews 2 cloth into 1 clothing in 24
ticks. Its 1×1 size costs 8 wood and 2 fiber to build; all of these are provisional
values. First-Town setup stores each starting agent's garment in their
household's House. A package is staged for older worlds on the first tick, but
the settlement package's digest changed when the Weaving frame was removed, so
saves made before this change are refused.

Garment and carrying-aid selection uses personally carried goods, actual
household stock or permitted Town Warehouse stock. Having no household does
not make a person's own ID a shared-stock owner. Their goods left in a House
or on the ground remain physical collection choices until picked up, with
the usual access, travel and carrying-space checks. Equipping shared goods
still collects one unit at its location. Save validation requires every
equipped unit to be personally owned, carried and unreserved.

The five alternative household building sizes have separate shipped packages;
their original definitions and first-Town defaults keep their identities.
Each alternative includes companion recipes bound to its own exact building
definition. Their package identities cover the complete building, recipes and
dependency ranges, including the Smith's Ornament recipes and the Tailor's
Care recipe. First-Town setup activates them after their dependencies. Normal
runtime staging requires every declared dependency to be active at a compatible
version and never restages a package already recorded, including a rolled-back
or quarantined one.

The ordinary household planner, paid construction, full-footprint site checks,
workstation reservations and physical supply paths serve both sizes. Exact
recipe-to-building equality stays authoritative. Personal handcart planning
chooses the recipe bound to the household's actual Blacksmith, including when
checking work already running or materials the adult keeps for that cart.
Storage still follows `BuildingStorageRules`; production capacity remains one.
The current sizes and provisional costs are listed in
[What works today](../../what-works.md#household-building-sizes).

Workstation recipes use only stock already at the building. A household
building without its own dedicated hauling (every kind except the House,
Farmhouse and Blacksmith, including the Tailor Shop, Clinic and Restaurant)
is kept stocked by the `supply_workstation:<item>` choice. It is offered to an
adult of the holding household while the building holds less of an input than
two batches of the
largest recipe that needs it, counting loads already on their way. The adult
delivers what they carry, picks up the household's spare stock from its House
or private farm or cooking stock (the existing delivery step then carries it
in), or gathers from a reachable source. House pottery and named cooking also
use this supply path. A vessel and its contents move together only when the
whole family fits the person and destination; loose inputs use four-unit loads.
Protected planting reserves stay unavailable. Grain already delivered to a
House or Restaurant is left there rather than hauled back into farm stock.

Restaurant ingredient purchases count only household stock the worker can
collect. Household goods on an actively borrowed Market stall stay on sale and
do not reduce ingredient demand. After borrowing ends, reachable goods may
reduce that demand under the normal pickup, source-reserve and capacity checks.

House cooking and Restaurants use the named recipes in the
[agreed food pipeline](../../game-design/towns.md#food-and-replanting). Generic
food-to-food production is refused. Production reserves exact usable on-site
inputs and room for its net storage growth; finishing consumes the reservations
and stores two named servings, leaving water jugs intact. A runnable named meal
has priority over input replenishment, so one shared jug cannot shuttle between
House and Restaurant indefinitely before anyone cooks. The trial cooked-food
reserve is two servings per living household resident across prepared kinds.
Recipe planning counts only physically present household members for that
cooked-food target, reading current society records without separate lookups.
Only loose meals and contents in usable vessels count toward that target;
finished meals trapped in a broken pot do not suppress replacement cooking.
A recipe that improves already prepared food, such as Restaurant meals from
bread, checks its own finished dish so an existing bread reserve cannot hide
that choice. Meals give 40% fullness, stew and fruit/berry porridge 50%, and
Restaurant meals 60%; decorated porridge and Restaurant meals also improve
nutrition. These values remain provisional.

Concrete meal names are retained for dietary variety. Raw grain, potatoes and
flour keep their freshness in the current trial. Bread and cooked meals spoil.
Protected odd-rate goods use a stable two-tick cadence, combined with the pot
cadence, rather than rounding their spoilage rate to zero.
