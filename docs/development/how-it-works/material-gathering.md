---
title: Material gathering
type: architecture
status: active
updated: 2026-11-14
---

# Material gathering

This page describes physical material gathering and tools.

## Material gathering

Material gathering selects available resources reachable from the acting agent,
then checks an actual unoccupied route into harvest range. Heating, help with
another agent's project and Blacksmith ore use this selector; they do not
require a path to the original map anchor. Gathering for one's own project does
not use it yet. This does not change fuel duration or harvest yields.

Shared fuel and equipment also require an unoccupied route to their collection
point. Unreachable stock stays untouched and does not prevent an agent from
using reachable supplies or gathering local fuel instead.

Tools gate and speed real material work. A wooden pickaxe extracts finite
stone, a stone pickaxe extracts iron ore, and an iron pickaxe extracts gold or
diamonds. Axes improve tree-felling output; when no usable axe is available,
agents can still gather one loose fallen-wood item by hand. Each gather action
uses one shared plan for output, tree seeds and tool wear. The runtime checks
that the whole planned load fits before it depletes ecology, then commits the
inventory output and single-unit wear together. A full load or a refused action
does not consume source stock or damage a tool.

An adult carrying a usable, unreserved iron pickaxe can choose actual gold or
diamond mining from a reachable finite outcrop. The complete eight-item trial
load must fit. The decision stops offering more once the adult and their
household together hold eight of that material. These goods remain carried
physical stock.
Ornaments extend this stock path with Blacksmith gold refining, gold
ornaments and optional diamond setting. They reuse normal physical supply,
reserved production and shop exchanges rather than adding a second inventory
or market.

Its worn-item record refers to one exact personally carried ornament. Wearing
does not exempt that unit from cargo or grant protection. Collection, removal,
gifts, sale, storage and estate handling must keep ownership, reservations and
location consistent; automatic storage or payment must protect the selected
unit. Server and client descriptions show the worn kind, without a new sprite.
Wear, removal and gift candidates require a fresh accepted non-fallback
LargeLanguageModel response whose exact offered target is still valid at
admission. Jev, continuing intentions and orders cannot select them. Gift
recipients come from locally observable people rather than a remote world scan.
Living adults in interaction range with free carrying space are filtered before
the eight-recipient limit, in stable agent-ID order. Full-handed neighbors cannot
hide another eligible adult. Gift options remain capped at sixteen across
carried ornament lots, and admission rechecks the eligible recipient and load.

`HouseToolsContent` adds two recipes at the existing House without changing
its building identity. Each crude wooden axe or pickaxe uses three wood held
at that House, with a recipe duration of 24 ticks. Ordinary household supply
brings the wood; once one batch is available, making a needed tool takes precedence over
stocking another batch. Demand is bounded by active adult household members
and counts usable family tools, including borrowed household tools and better
tiers, plus work already committed. These are provisional balance values.

Crude tools occupy tier zero: four wood or stone per extraction and 4,000
condition basis points lost per successful use, compared with six and 2,000
for the Blacksmith's wooden tools. Only tree/wood and stone entry gates accept
the new tier; ore thresholds are unchanged. Existing collection, reservations,
capacity, ecology and single-unit wear rules still apply. Completed House work
emits `house_tool_made` with the exact worker and item kind for readable Event
Log text. The generic item icon remains until crude-tool art is approved.

The Blacksmith makes wooden, stone and iron tools from actual inputs, refines
iron ore into separate refined iron, and repairs one carried worn tool at a
time. Repair consumes the recipe materials carried by that tool's owner; it
does not restore condition for free. Hammer use speeds building work, and an
iron knife speeds food or other preparation recipes. Recipe and field records
keep their exact selected tool lot through save and reload.
Knife-assisted production keeps the full inventory lot reference, including
long identities produced by partial moves. Completing the recipe checks that
this exact knife is still usable and carried by its owner, and wears one unit;
an unavailable knife cancels the work without spending its reserved ingredients.
