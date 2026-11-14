---
title: Fertility and household fields
type: architecture
status: active
updated: 2026-11-14
---

# Fertility and household fields

This page describes fertility, field work and food recovery.

## Fertility and household fields

`LandFertility` derives each land tile's fertility from the world seed,
rainfall, climate, surface and nearby rivers or lakes. Generated maps reuse
the selected candidate's rainfall seed, including its east/west wrapping.
Legacy fixture maps keep their original world-seed rainfall. It is derived data,
not a saved object or a resource lot. Grass and meadow commonly support
farming; dry scrub is poor, and sand, rock, snow, mountains and water cannot
be farmed. Inspection uses Poor, Fair, Good and Rich rather than a score.

An adult or elder in a household holding a Farmhouse must stand on a free,
farmable tile with a carried wooden hoe to start tilling. Roads, resource
objects, buildings, reserved expansion tiles and existing fields are
excluded. Tilling takes eight work ticks; planting, tending and harvesting
take four each. These times, growth, yields, storage and planning targets are
provisional. Neighbouring tiles make a field without a fixed shape or size.

Each authoritative field records its household, crop, stage, work, growth
times and replanting reserve. Planting reserves and consumes one carried
grain seed, cultivated-green seed or potato. Moving away, death, lost tools
or urgent needs cancel unfinished work and release its planting input.
Field work uses the existing illness cadence. Only a successful work stroke
advances progress and wears the selected tool. A new planting input claim
lasts until actual completion or interruption, so illness does not make it
expire while the worker is still planting.
Completed harvests remain intact. Fertility and weather affect crop growth
or yield. Each field also saves the tick it was last worked: starting work and
every successful work stroke update it. A field with no work in progress and
no work for a full season (a quarter of the world's year,
`FarmFieldRules.IdleTicksBeforeGrass`) goes back to grass during field
upkeep. Any crop still on it is lost, its replanting reserve is released, and
`field_returned_to_grass` names the field and household. Harvest lots already
on the ground stay, and the land keeps its derived fertility. Retilling the
same tile skips harvest identities retained in inventory or reservation
history, so it cannot replace earlier stock or reuse a released reserve.
Current saves require the worked tick and reject an active-work clock that
disagrees with it. Harvesting creates grain, potatoes or cultivated greens on the
field, and grain and greens also yield two replacement seeds. One usable
planting item is reserved before surplus can be traded. Picking it up for
the next planting releases the reserve and physically carries that item.
Player planting orders choose carried stock first, then the field's replanting
reserve, then other eligible lots in ID order. Shared stock must have an
unoccupied pickup route within its interaction range, checked during both
order selection and execution. An occupied earlier lot cannot hide another
usable seed. Autonomous crop claims keep their terrain eligibility while
temporary occupants pass and the existing physical route retries.

Planning uses the Farmhouse's assigned Town and its living resident roster.
It subtracts usable, unreserved ready-to-eat food owned by those residents or
their households from a trial reserve of two days at two meals per resident
per day. The remaining shortage is split equally among that Town's farming
households with living residents, then rounded up to whole fields using the
existing expected grain yield at each Farmhouse. Enough Town stock stops new
tilling and autonomous planting; existing growing or ready crops can still be
tended or harvested. A Farmhouse without an assigned Town retains a household
budget. Loose food and food in usable vessels count after the existing
condition, freshness and reservation checks; food inside a broken pot does not
cover the reserve or suppress replacement planting. This planning changes no
ownership or private-stock access.
It picks free reachable soil by fertility and distance, favouring
tiles beside the household's existing fields. It can expand during shortages;
the existing household-building planner can establish another Farmhouse for a
household that lacks one and has the materials. Raw grain and potatoes cannot
satisfy this ready-food reserve directly; their prepared meals can, so raw
stock does not stop farmers planting fresh greens. Grain is milled into flour
at the Farmhouse, one grain to one flour.
Field work records the selected carried hoe or sickle lot. An eligible farmer
with a wooden or iron hoe keeps the chosen crop claimed while another resident
briefly occupies the field. Claim eligibility uses the same usable, unreserved,
top-level carried Hoe-family check as field planning; losing a usable hoe
releases the claim. Other household, food, warmth, project and seed checks still apply.
Wooden and iron hoes reduce the work still needed to till and tend, while wooden and iron sickles
reduce harvest work; an iron sickle is faster than a wooden one. Each committed
work tick wears one unit of the selected tool. Interrupted or refused work does
not wear it. A tool in storage, on the ground, in delivery or inside a pot is
not directly usable; it must first be carried at the top level.
If wear breaks a selected tool before the field effect completes, the runtime
stops that work in the same committed action. The crop stays at its earlier
stage and the broken tool stays in the owner's cargo, so the checkpoint remains
valid without waiting for another tick.

Wild berries and greens replenish. Orchard fruit appears in autumn after a
planted orchard matures. Harvesting fruit also produces a distinct orchard
seed with a reserved planting unit. An adult carries that seed to legal free
land and plants a sapling; tree growth, fruiting and the reserve survive reload.
The orchard planting choice requires carrying room to collect a shared seed.
An already carried planting seed remains usable at full capacity. Ordinary
wood-tree seeds remain distinct.

Food-room recovery is admitted and revalidated for children as well as adults
and elders; the final child gate permits that existing self-feeding action.
It frees only the missing physical room and preserves selected equipment,
artifacts, work reservations and delivery loads. Infants remain excluded.
Children use only ordinary unreserved cargo; the planting-reservation exception
below remains an adult action.

Food recovery first sets down ordinary spare cargo. A quantity claim on a loose
stack protects its held units while unrelated unreserved units remain available;
the original claimed source and offer stay intact. Whole vessels with any held
family member remain protected, along with retained repair inputs, worn
equipment, knowledge goods and delivery loads. If that cannot free enough room,
urgent adult food recovery may also select the actor's own orchard propagation
seeds. Their selected planting reservations are released in the same inventory
transition that stores the seeds with the household, after reaching the House
or storage pile. Walking, an unavailable destination or a refused transfer does
not release them. Other reservations, delivery loads and borrowed goods remain
protected; crafting does not use this exception.

The Godot map draws worked soil and crop growth above the terrain, including
wrapped map edges; the overview marks field tiles. Fields join the household
property display, and inspection shows the household, stage, crop, worker and
actual ground stock. This is current household use, not a separate land-title
system.
