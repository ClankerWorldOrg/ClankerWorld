---
title: Animals and horse travel
type: architecture
status: active
updated: 2026-11-14
---

# Animals and horse travel

This page describes animal care, trade and horse travel.

## Animals and horse travel

The built-in animal package adds a household animal yard and the egg, milk, wool,
leather, leather-sack and saddle recipes to their actual House, Restaurant and
Tailor stations, including larger Restaurant and Tailor variants. Generated
worlds seed small wild groups near public forage and fresh water. Each animal
has an authoritative identity and location; it never draws from private crops
or household stock while wild. Wild forage selection checks an unoccupied route
to the patch's interaction area, using the same live people and animal blockers
as movement. A crowded nearer patch does not hide another reachable patch within
the existing search radius.

Wild drinking selection likewise checks the complete unoccupied route to a
fresh-water shore before choosing it. Shores retain their distance, row and
column order and twelve-tile search radius. A crowded approach to an empty
shore does not hide an alternate reachable shore. Selection and movement share
the same live people and animal blockers and route search; drinking and care
durations remain unchanged.

Forage checks that radius before looking up stock through the current ecology
state's ID index. Feed consumption replaces the ecology state, so the next
animal sees the remaining quantity. Distance and ordinal-ID ordering stay the
same; missing source records remain unavailable.

Adults tame, care, collect, supply, lead, saddle, mount and dismount through
ordinary revalidated choices. Native orders bind an exact animal name or ID.
Order progress follows the completed animal action, including when fetching a
supply at the animal completes that action in the same tick.
Care spends actual unreserved grain/greens and jug water at the animal or yard;
food, planting and workstation reserves stay protected. Its ready-to-eat reserve
counts only usable vessels or loose food, excludes household stock on actively
borrowed Market stalls, and still subtracts freshness, condition and reservations.
Ending stall borrowing makes that stock eligible again. The existing two portions
per active household member and four extra for active parenthood are unchanged;
non-edible grain does not spend the food reserve. For care away from the
actor's tile, input selection accepts only physically carried supplies. This
keeps the existing supply path collecting feed and enough usable jug water before
approaching the animal, including after a partial pickup. Water may be split
across jugs or content lots; fetching counts the usable water already carried
and rechecks each whole jug against reservations and the loaded route to the
animal. Care and saddle pickups use `PickupCarryCapacity` for the animal's
current position, keeping the whole vessel family within that route's load limit.
At the animal's tile, permitted local yard stock remains usable directly.
Supply pickup leaves household sale stock on an actively borrowed Market stall.
Once borrowing ends, that stock can be collected through the normal ownership,
reservation, capacity and travel checks. Physical supply trips retain the
owning household. Cancelling or replacing an animal order releases
only a supply trip matching its actor, animal and action, without relocating,
transferring or spending its cargo. Executing a new animal order also discards
an unrelated retained trip, including one loaded from a previously cancelled
task, before selecting supplies. Care-order admission can inspect legal replacement
supplies despite unrelated trip tracking. A matching active trip keeps its normal checks.
A trip isolates its selected feed quantity from
the source, including an already carried stack, so delivery cannot donate the
unselected remainder. Reusable jugs travel whole with their contents. One held
product batch waits for local collection;
milk is relocated as ordinary household goods carried by the collecting adult.
Collection needs space for the complete batch and does not fetch a vessel.
Products then use ordinary stock hauling, recipes and trade.
`drink_milk` uses the same carried-milk selector for its offered choice and
execution. It excludes milk assigned to a building delivery, keeping promised
goods available for their destination. Optional food-pot contents require a
usable container; milk cannot enter a water jug.

When care is due, its native order candidate requires complete safe inputs, a current care
supply trip or an obtainable supply. Missing inputs use the existing blocked
order status with a reason naming feed and jug water. Candidate and blocker queries
change no state, and the existing order schedule resumes the task when its
physical supply path becomes available.

Cared adult pairs breed automatically when their yard has a place and delivered
supplies cover existing animals and the offspring. Feed and water inside a
broken vessel do not satisfy that prerequisite; healthy loose feed and usable
alternative vessels still count, under the existing quantity and reservation
rules. Pregnancy reserves one place;
young animals and reservations count toward eight per household or wild herd.
Missed care pauses progress. Only old age kills animals; an owned sheep, cow or
horse leaves one household hide at its actual death position. Untamed animals
leave one hide bound to their remains until a nearby adult collects it; this
does not grant access to household-owned hides. Abandonment does
not turn private animals into public salvage.

Animal gifts, sales and named outsider permissions need fresh, admitted personal
model choices. Built-in fallback and forced orders cannot give personal consent.
Receiving space and the exact payment lot are checked again on acceptance;
transfer clears old permissions and preserves position for physical leading.
Dead animals offer no new permission, gift or sale choices. Existing care and
riding permissions may still be revoked, including after the recipient moves
away.

Care for a different animal first ends the actor's existing ride through the
normal dismount operation, then approaches the care target on foot. That
preparation earns no care-order progress and spends no feed or water. The
horse and reserved saddle keep their position and ownership; excess cargo
uses the existing dismount rules before care rechecks its actual inputs.

A cared adult horse has one real reserved saddle and one adult rider. Movement
uses the normal legal route and occupancy rules, halves walking cost and admits
at most two legal steps per tick. A mount adds eight cargo units. Care or
permission loss ends riding; dismount puts unreserved excess cargo at the actual
position without changing its owner. A partially held loose stack can shed
only its unreserved units, preserving the original claimed source; a held
vessel or its contents protect the whole vessel family. Cart attachments and boat travel exclude
ridden or led animals. Godot projects and draws the authoritative animal state
with young/adult headings, mounted horses, yard art, inspection and event text.

Building Details lists animals whose current observed positions lie within
the building footprint, using the same `GameUiText.AnimalDescription` as tile
inspection and hover help. It refreshes with the snapshot and grants no animal
ownership or care permission.

Collection from another animal and saddling another horse prepare with
`EndAnimalRide` on their own tick, without product transfer, saddle fitting or
order credit. The following native action rechecks permissions and foot
capacity; existing product and fitted-saddle reservations remain held during
preparation, and existing dismount rules preserve excess cargo.

Carried milk drinking is offered with ordinary survival choices, including
for children, and passes the final child age gate. The selector checks
ownership, physical custody, freshness and reservations; drinking consumes one
portion. Infant self-feeding and adult-only animal work remain age restricted.

Milk uses ordinary Store stock, Market stock receipts and inventory barter.
These paths retain ordinary food reserves, protect actively borrowed stalls
and recheck custody, receiving space and reservations at execution. Store
stocking includes reachable loose ground goods, so released Market stock uses
the same selector as other household supplies. Store and
Market purchases use the existing buyer offer and seller acceptance or refusal.
There is no milk-specific offer ledger or exchange identity derivation.
Whole-lot barter retains the existing lot identity; partial transfers use the
shared inventory transfer rules. Spoilage retains milk's existing rate.
