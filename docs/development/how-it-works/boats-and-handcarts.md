---
title: Boats and handcarts
type: architecture
status: active
updated: 2026-11-14
---

# Boats and handcarts

This page describes communal boats and physical handcarts.

## Ports and communal boats

`PortContent` supplies four approved shoreline definitions. `PortNavigationRules`
checks the two land tiles, chosen approach, three water rows and six clear
one-tile docking spaces. A Port footprint fits inside the map; its water routes
may cross the east/west seam. Foot construction and supply target the land end,
not the water anchor of a north- or west-facing drawing.

Ports and boats use the same Council-approved `TownProject` ledger
as Halls and Markets. A boat plan pins its completed launch Port and exact
wood/rope/refined-iron budget. Actual delivered lots and completed consumption
receipts pay for one physical `BoatState`; direct unpaid Port placement is
refused. Costs, work and three-tick water steps remain provisional.

Only residents or a Council-adopted typed `TownBoatAccessGrant` may depart in a
Town’s boat. A grant targets one exact visitor or all visitors and is retained
on the ordinary law’s adopted version. Freeform law text supplies no authority.
Repeal ends future permission; it does not abandon a passenger already aboard.
A grant changes no membership, ownership or Warehouse access.

Native personal decisions approach the Port and create a sequenced trip request.
The complete owner grammar also accepts `Travel by boat to Port at (x, y)`.
It resolves that existing Port's instance ID at submission and retains the
requested tile; ambiguous or absent targets and mixed/repeated tasks are not
understood. `boat_order` uses the same departure choices, approach movement and
request creation as ordinary travel. Each ordered request retains its instruction
ID, and the order binds its latest request. Queue admission, physical boats,
permissions, reservations and recovery remain authoritative. Native destination
arrival credits one receipt; a safe return or a cancelled order receives none.
Cancellation/replacement settles only waiting requests, preserving underway
journeys. A settled return or lost permission can retry through the normal queue
when conditions permit, with a new request after the old one settles.
Queue processing chooses the oldest currently usable request, atomically binds
one idle boat and a free destination dock, and boards only that passenger with
their actual carried goods. Blocked requests retain their sequence; cancellation
or lost departure permission settles unused requests. Six moored or incoming
claims exhaust a Port. All free docks are tried for a connected route.

Transport follows saved cardinal water steps while avoiding other boats and
reservations. Passenger cognition permits waiting, eating carried food and
drinking available carried milk while traveling or waiting for landing. Milk
uses the same ownership, freshness, reservation and physical custody checks as
on land; drinking consumes one portion and restores 3,000 fullness points. Candidate construction and action admission both permit that action;
foot work and conversations cannot move the passenger away from the boat.
A blocked arrival retains its reservation for one world day, then reserves a
usable origin for return. When neither landing works, it keeps waiting. Native
death and estate handling keep dropped cargo at the physical boat until landing.
Prepared-tick commit and current-format replay retain boat state with the other
world facts. Save validation binds assets to paid projects and journeys to exact
passengers, requests, docks and connected water routes.

The owner snapshot projects physical boats, cargo, incoming docks and all active
requests, plus recent settled requests. Godot retains one marker per boat,
observes its heading and wrap behavior, uses the approved art unchanged at close
zoom, and shows travel and dock use in tile, Port and Town inspection. Port night
lights use the approved T-head lantern. Medium zoom uses the approved 16-pixel
boat from #914; close zoom retains the unchanged 32-pixel drawing.

Checkpoint compaction retains every active request and the existing 40 recent
closed requests by sequence. It durably archives full older records before
installing the smaller live queue; compact sequence ranges retain gaps around
older active passengers. The `world_history_compacted` diagnostic includes live
boat-request and retired-range counts. See
[Saves and replay](../saves-and-replay.md#ports-and-communal-boats) for the schema,
archive-write ordering and recovery checks.

## Physical handcarts

Orders accept `Attach/Pull [my] cart/handcart` and `Park/Unhitch [my]
cart/handcart`, plus `Repair [my] cart/handcart`, optionally prefixed with
`Please`. A complete exact cart lot ID binds immediately. `at (x, y)` uses the existing strict coordinate parser
and binds only one owned cart at that tile; ambiguous targets are not guessed.
An unspecified attach task selects an accessible, usable owned cart when it
starts; unspecified parking selects the actor's actual attached cart. The first
executed step persists `TargetCartLotId`, so later ownership or route changes
block that cart rather than redirecting the order.

The common order lifecycle handles queue, replacement, cancellation and urgent
survival interruptions. Attach and park steps call `ApplyHandcartCandidate`
and earn one
`cart_tasks` unit only when the selected native hitch changes as requested.
A deterministic per-instruction receipt binds the actor, action and cart.
Strict validation checks the target's identity and cart kind, task shape and
receipt; owner and Godot projections retain the optional target. Neither a
pre-existing attachment nor an unrelated parking effect completes an order.
Repair selects an accessible owned worn or broken cart when no target is named,
then binds it before the first collection or movement step. It collects missing
wood, iron fittings and rope through the existing permitted stock action,
requiring space for the whole remaining set and a clear route to the cart.
It calls the native repair action only with actual carried inputs. Completion
requires the selected cart's condition to improve and records a bounded receipt
tying the instruction, actor, cart and native repair tick together. Strict load
validation also requires the three exact completed material reservations for
that actor and tick. Existing `TargetCartLotId` and `LastEffectId` fields carry
this evidence; no new save field or schema version is added. A healthy cart
cannot count as a repair, and foreign or reserved carts remain blocked.
Collected supplies stay physically carried when a task is cancelled or replaced;
repair reserves and consumes all three inputs atomically, with no unfinished
cart-repair reservation to release. Cargo and hitches retain their normal state.


`InventoryContainerRules.Handcart` is a single ground-position inventory lot.
Its condition, owner and child cargo lots are authoritative inventory facts;
`HandcartHitch` saves only the exclusive cart/puller attachment. The runtime
checks the owner has physically reached the cart, verifies every load source's
position and household authorization, and moves the cart with each admitted
legal cardinal step. Ground cart contents never become carried recipe inputs,
fuel, planting seeds or equipped gear. Loading a partial quantity preserves
the untouched remainder's original location and reservations.
Shared edible food follows the current household-food access rule at both
choice generation and execution. Under hungry-members-first access, a healthy
loader can take only the shared surplus above the protected serving reserve.
The same allowance determines ordinary collection permission and caps a bulk
cart load. Hungry adults retain their existing access; personal food and
non-food cargo keep their ordinary loading rules.

Crafting uses the Blacksmith's handcart recipe with the adult worker's carried
wood, fittings and rope. An adult collects them from household or Town
Warehouse stock only while the whole set is carried or in stock they can
physically reach and collect. Unreachable household stock and goods on an
actively borrowed Market stall do not complete the set. The
Blacksmith, Store and workstation supply hauls leave the carried set with them
rather than returning it to stock. Exact inputs are reserved through the
production job; the personal cart appears on the work site's ground after
completion. Running or paused jobs reserve only their net stored output growth,
excluding ground handcart outputs. Ordinary stored outputs still reserve room
after accounting for consumed inputs already in that building. Repair consumes
three carried material reservations atomically. Unloading can retain
damaged goods on the ground and works after the cart breaks. Unload choices
cover cargo in every owned cart on the actor's current tile; execution resolves
the selected cargo's actual cart and rechecks position, ownership, reservations
and carrying space. An empty cart cannot hide another cart's cargo.
Property transfer
and inheritance keep the entire cart/cargo family at its existing position.
A death, break or ownership change removes the attachment without dropping or
teleporting the goods. Owner observation derives cart inspection from those
same saved inventory lots, rather than maintaining a second cargo ledger.

Owner load/unload orders reuse the attach/park cart binding and the native
inventory actions. They require a specific supported loose good and quantity;
unload defaults to personal carrying space and can explicitly use the cart's
ground tile. Orders retain the selected cart and current source lot, walk to
that cart through legal movement, and limit each transfer to the remaining
requested quantity. Progress comes from the actual change in that cart's
physical cargo quantity, with a receipt bound to instruction, actor, cart,
good, action and cumulative progress. A fully moved source can release its lot
binding for another eligible stack of the same good; partial or blocked sources
stay bound. Urgent needs can interrupt, and cancellation/replacement leave
inventory and reservations untouched. Broken-cart and damaged-cargo unloading
uses the same native recovery operation.

Generic attach, load, pull, park, unload and give controls remain available to
models, but rank below safe idle for the built-in chooser. They do not yet bind
an autonomous delivery task, so selecting them eagerly would repeatedly undo
transfers or pull between arbitrary buildings. Crafting and repair keep their
ordinary priorities; an already attached cart still follows ordinary movement.
