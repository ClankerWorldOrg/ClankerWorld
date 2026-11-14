---
title: Orders for work
type: product-status
status: active
updated: 2026-11-14
---

# Orders for work

Instructions for production, building, repairs, fields and shelter.

Production orders use the existing recipes for goods such as rope, baskets,
cloth, clothing, sacks, tools, flour, pottery and care supplies. Examples are
"make two sacks", "mill flour" and "make two batches of house bandages". They use
ordinary recipe ingredients, household access, work sites, preparation and
production time. Goods keep the recipe's usual ownership and location.
The default is one batch. Explicit item quantities must be an exact number
of whole batches; the game refuses a count that would require making extra
items. Only completed production advances the displayed item or batch count.
Bandages distinguish "house bandages" from "tailor bandages". These names
select exact recipes; an ambiguous plain "bandages" request is not understood.

An optional "at (12, 4)" pins the work site to that tile. The task keeps its
chosen recipe and work site while it waits or repeats; it does not redirect
to another building when access or materials disappear. Queueing, replacement,
cancellation and save/reload retain the remaining count. Cancellation releases
unused reserved ingredients; already finished goods remain. Urgent survival
pauses the current production job and its remaining work before the order
resumes.

Named cooking orders cover all shipped House and Restaurant recipes. Simple
House meals specify potatoes, wild greens or cultivated greens. Other recipes
name the work site: "cook house bread", "cook restaurant berry porridge" or
"prepare restaurant meals". The larger Restaurant uses "restaurant 2x2" in
the recipe name. Every batch makes two servings, so "cook four house bread"
requires two paid batches; odd serving counts are not understood. Plain
"cook porridge" is ambiguous and does not replace the current order.

Cooking ignores ingredients inside broken vessels and uses healthy on-site
alternatives. The broken pot or jug and its contents stay unchanged.

House and Restaurant ingredient supplies use their native demand, cooking
reserves, carrying and whole-jug rules. Named prepared foods can be collected
as personal property, returned when borrowed, delivered as spare carried food
to the House, or stocked in a Store. These actions preserve the existing
ownership and food reserves. The nonfood personal-storage action still excludes
loose meals; named-food consumption and wild-food gathering keep their separate
commands.

Building orders cover one House, Farmhouse, Blacksmith, Tailor Shop, Silo,
Clinic, Store or Restaurant at its starting size (including the 1×1 Store
and 1×2 Restaurant) through the
ordinary household construction path. "Build a
Clinic at (12, 4)" requires that exact site; without coordinates the agent
chooses a legal site and keeps it. An animal yard requires a completed House;
its order waits without spending materials until that House exists.
Normal one-per-kind limits, Silo prerequisites, material ownership, carrying, travel and construction work
still apply. An existing building or somebody else's project never counts as
the ordered completion.

Construction uses Town Warehouse supplies only when the builder can reach
them. A blocked stocked Warehouse does not stop the builder from gathering
reachable materials elsewhere; its stock stays in place.

A construction order can replace the agent's unpaid ordinary building or
crafting plan. It starts fresh construction work and pays the normal costs;
the earlier plan's work does not count toward the order. A production job
already holding reserved inputs finishes first. Other agents' projects and
work bound to another order or an accepted tool-making request keep their
existing checks.

"Expand my House" and "expand my Town Warehouse" request one next supported
size under the normal access, need, space and cost checks. The task pins the
original building and chosen expansion, then follows its real reserved
materials and work. A changed building or lost access blocks or cancels that
work rather than applying it somewhere else. Both forms accept an explicit
count of one; larger counts and repetition remain unsupported.

Only successful placement or a completed expansion advances progress.
Completed work remains recorded even if the building is later removed.
Cancellation releases unused expansion reservations and ends only the task's
own unfinished work; collected goods stay where they are. Expansion cannot
take borrowed material from another living agent's carried load, including
after that agent leaves the household. Physically returned stock becomes
available again. Urgent survival pauses the work. Queues, cancellation, partial work and completion survive
save/reload without repeating a paid build or adopting an unrelated project.

Repair orders cover personally owned, carried basic clothing, padded coats,
rain cloaks, baskets and sacks that are worn enough for the ordinary repair
rules. For example, "repair my basket", "repair two padded coats", or "keep
repairing basic garments". Adults and elders collect available materials and
work at their household's House for baskets or Tailor Shop for the other items.
Eight steps finish one repair and consume its reserved materials. Walking,
collecting supplies and starting a job earn no repair progress. Cancel or
replace an order to release unused materials immediately. Urgent survival also
releases them; the order resumes with its remaining repairs when the need passes.
Saving during a repair preserves its work and reservations. A repeating task
waits for another matching worn item after the current ones are repaired. Ordinary
repair also considers the next worn item when the preferred one lacks materials,
carrying space or a reachable private work site. When both can proceed, the
equipped carry aid still comes first.

Tool repair orders name a supported material and tool, such as "repair my
wooden axe", "repair two stone pickaxes", or "keep repairing iron knives".
Adults and elders prepare real supplies, walk to their household's Blacksmith
and repair their own carried tools. Only finished repairs count; pickup,
gathering and travel earn no progress. Worn wooden, stone and iron axes and
pickaxes, wooden and iron hoes and sickles, wooden and stone hammers, and iron
knives are supported. Fully broken tools need replacement in both ordinary
repair and your orders. Borrowed, reserved,
stored and promised tools are unavailable to these personal orders. The agent
may put spare cargo in household storage to make room for supplies, preserving
all matching worn personal tools and any tools needed to gather materials.
Excess repair materials can also be stowed, while the amount needed for the
repair stays carried. Reserved and promised cargo remain protected.
Preparation waits for supplies or another gathering tool if gathering would
break a requested tool. Cancellation
keeps already collected goods and spent gathering wear. Quantities, queues,
survival interruptions and preparation survive save/reload.

Field orders cover tilling, planting, tending and harvesting for adults and
elders whose household has a Farmhouse. Use "till two fields", "plant grain",
"plant two fields of potatoes", "tend cultivated greens", or "keep harvesting
fields of grain". With no quantity, one field is the task. Quantities must name
fields: "harvest five potatoes" is not understood rather than being treated as
five fields. Grain, potatoes and cultivated greens are supported. An omitted
crop for tending or harvesting allows any suitable household crop; a named crop
must match. Add "at (12, 4)" or "at tile 12,4" to work only at that location.
An unavailable or unsuitable tile leaves the order waiting, even if another
field could take the work. Quantities and repetition keep the exact location
when there is no further matching work to do there.

Agents use real walking routes and planting stock. Planting orders skip a seed
lot with an occupied pickup route when another usable seed is accessible.
Tilling and tending require
a usable carried hoe; a carried sickle speeds harvesting under the ordinary
rules. Orders can request work even when the Town already has enough food.
Only completed field work counts, with normal tool wear and seed consumption.
Harvests stay on the field as household property, with planting stock reserved
for another cycle. Missing tools, stock, access, suitable fields or routes leave
the task blocked with a reason. Cancellation or replacement releases unused
planting stock and removes unfinished tilling; spent tool wear remains. Urgent
survival interrupts work before its remaining task resumes. Queues, partial
work, stock reservations and progress survive save/reload.

Shelter orders accept "seek shelter", "take cover" and "shelter in my House",
with optional exact coordinates. They finish on actual arrival in permitted
cover, including valid natural cover during a storm. Reaching shelter does
not claim that warmth has recovered. Household access and storm-guest
invitations remain authoritative. An agent keeps its selected building or
natural cover; lost permission or a changed target leaves a visible blocker.
Own and invited homes are known destinations. Other shelters must be locally
observable; a supplied distant coordinate is visited and checked on arrival.
Water and other impassable sites leave the order blocked while the world
continues running and saving.

Adults can follow "light a fire", "tend the fire" or "light the fire in my
House", with optional coordinates. These use normal fuel collection,
ownership, carrying and access rules. Only a real ignition after spending
one eligible owned wood counts. Borrowed, reserved and delivery-promised wood
cannot be spent. An already-lit hearth waits until another ignition is
possible; it is not refuelled or credited as the order's work. Children may
seek shelter but cannot tend fires. Missing usable known wood leaves the
fire task blocked until a supply becomes available.

Both actions perform one task. Counts, repetition, durations and "warm up"
remain unsupported. Urgent food can interrupt either action, while urgent
cold still allows protective work. Queues, cancellation, travel and completion
survive save/reload. Finished progress remains recorded after weather changes,
fire expiry or removal of the building.

Weapon-repair orders remain outside the current catalogue.

A recognized new order replaces the current order unless **Queue** is selected.
The replacement runs first; existing queued orders keep their place, targets and
remaining amounts. **Cancel task** stops a waiting or active order. An order
the game cannot understand preserves the current task and its queue. Explicit
source names must match exactly; the game does not quietly choose another
source or food. Unsupported, mixed, negated or incomplete requests close as not
understood and appear in the Event Log. The parser also refuses counted travel
such as "go get 2 berries" instead of guessing a task. A recognized task that
cannot be done yet stays pending, with model retries on the usual schedule.
Blocked orders and orders cancelled by a game error appear in the Event Log;
the same problem for the same agent and target appears at most once a world day.
Urgent survival can interrupt it before it resumes; waiting for a model reply
does not stop the agent from following its task or handling an urgent need.
Deterministic local choices do not claim the model heard a message, and
suggestions do not block recognized orders. Hands-on Windows paired-client
checks remain pending in the
[playtest list](../../playtest/alpha-1.md), the
[food-order checklist](../../playtest/alpha-1.md) and the
[order list checklist](../../playtest/alpha-1.md).
