---
title: Orders and goods
type: product-status
status: active
updated: 2026-11-14
---

# Orders and goods

Instructions, gathering, storage, collection and deliveries.

The agent card's **Speak to them** box sends a message as a **Suggest** or an
**Order**. A new suggestion or recognized order asks for one fresh decision from
the agent's planning model containing the exact words; a queued order reaches it when that order
becomes active. Any brief reply comes in the same response, without a separate
acknowledgement request. Order steps do not cause requests every tick.
The Profile keeps the four newest under **Your messages** and shows whether the
model heard them, whether an order is still open, and any short reply
separately from private thoughts.

The quick card and the Profile name the agent's current order with its status
as the server reports it: queued, waiting, doing, interrupted or blocked, with
progress, the server's reason when it is held up and how many orders wait after
it. With no open order they show how the latest order ended: finished,
cancelled (saying when a newer order replaced it) or not understood. Finished
appears only after the server records the work as done, never when a message is
accepted or a model hears it. **All orders** in the Profile opens a reader with
every open order, in the order they will be done, and the agent's six latest
closed orders, which closed suggestions no longer push out. The game draws
both from the server's snapshot and keeps no order list of its own, so after a
reconnect or reload they show what the server holds.

Adults can be ordered to write field records, draw maps or bind books, with a
count or until cancelled. They use their own learned sites and recipes and real paper or
paper and cloth. Finished items count toward the order; identical accounts
are not written again. More writing waits for new learning, supplies and room
to carry them. Cancelled or replaced orders release their unused reservations.

Book writing, cart repair and tool repair choose an order for their material
pickups that keeps the remaining trips reachable with the added load. They
can fetch a distant ingredient first when collecting nearby supplies would
prevent a river crossing.

Adults can also be ordered to copy a held record, map or book, with a count or
until cancelled. The source must be owned and carried, with every recorded site and recipe
already known. A copy preserves its frozen contents and costs fresh native
materials and work. The order counts only its own finished physical copies.
Lost sources release unused reservations and leave the order waiting for that
same source; identical accounts are not copied again.

Completed production teaches its actual worker a personal recipe account.
Field records and books can carry those recipes; maps carry sites. Reading,
sharing and trading teach only the written contents to the recipient. Neither
a household nor a Town gains shared recipe knowledge, and reading grants no
skill. Profile cards and the agent's model context show their own learned recipes.

Urgently hungry agents may pause an order to drink usable carried milk,
leaving their jug intact and resuming the pending task afterward.
Urgently hungry adults may pause an order to buy ready food at a shop or
Restaurant, including continuing an open food purchase. They need their own
payment and enough carrying room, and keep the outstanding order. Equipment
purchases, raw ingredient shopping and selling goods do not gain this exception.

Named talk orders, such as "Talk to Ari" or "Speak with Ari Vale", resolve an
exact unique name or person ID when submitted. They keep that person through
rename, queueing and reload, walk within ordinary interaction range and create
one real invitation. Both participants use their own conversation models and
allowances, and the invitee may decline. The card reports invitation,
acceptance, suspension and the actual outcome. Finishing means the talk
attempt ended, including refusal; it does not mean an agreement was applied.
Queued work waits for that outcome. An accepted unfinished conversation loads
stopped and needs both people's fresh resume choices. Unavailable people,
models, allowances and routes leave useful blockers. Marriage uses a separate proposal order. [The Windows check](../../playtest/alpha-1.md)
is written, but has not been run.

Reading orders cover personally owned, carried field records, maps and books.
"Read a map", "Read a book" or "Read" selects one eligible item; an exact
artifact ID, lot ID or unique title binds that item even while it is elsewhere.
The task adds only its frozen written sites and original discovery sources,
within the existing knowledge limit. Foreign, borrowed, stored, ground or
reserved items leave a blocker. An unreadable item or one with no new sites
does not finish the task. Only that agent's own successful native read counts;
another person's reading teaches them separately. Queueing, cancellation,
replacement, urgent survival and saving preserve the task and its actual
progress. [The Windows reading-order check](../../playtest/alpha-1.md)
is pending.

"Propose marriage" and "Propose marriage to my partner" attempt the existing
marriage conversation with the adult's eligible current partner. The order
keeps the selected partner, normal travel and invitation rules, and each
participant's own model choices. Only the addressed speaker receives the
requested activity in their conversation context. Refusal and a conversation
without a proposal finish an attempt with their actual outcome. Accepted
marriage waits for the separate shared-surname conversation to finish. Pauses,
failed calls and reload leave it unfinished, with two fresh resume choices
required. Cancellation removes the task while preserving consent and surname
progress; it never changes household admission. Missing or changed eligibility
blocks rather than selecting another person. [The Windows checks](../../playtest/alpha-1.md)
are pending.

Orders currently cover eating carried food, collecting accessible household
food to eat, going to a known food source or discovering one through ordinary
exploration, and gathering berries, fruit or wild greens from a matching source.
Examples include "eat 3 berries", "gather two berries", "gather berries from
berry-patch", "go to berries at (12, 4)" and "keep gathering food until
cancelled". Quantities count food actually eaten or gathered; travel finishes
on arrival at the food source. Eating waits until the agent is hungry enough,
and a full load blocks gathering with a reason.

Orders also gather wood, stone, plant fiber, clay, iron ore, gold ore and diamond
through the ordinary material-gathering rules. Adults and elders can take one
load, a requested quantity, or repeat until cancelled. Quantities count real
goods; the last whole load may exceed the requested number. A usable tool of
the required tier and enough carrying space are still needed, including room
for seeds from a felled tree. Tools wear and sources deplete normally. An agent
may collect an accessible shared tool before gathering.

Use "gather clay", "gather five wood", "keep gathering stone", or an exact
resource identifier after "from". "Gather iron ore at (12, 4)" checks that tile
without choosing a substitute. Unspecified sites use the agent's known or
nearby observed resources, with ordinary exploration when none is reachable.
A remembered source across disconnected land does not stop an untargeted order
from exploring; the agent keeps that memory. The outing saves its requested
material and order link until a legitimately discovered source is usable or
the outing ends. Empty or unsuitable discoveries do not finish the search.
Unknown explicit sites require travel and observation before gathering. Queue,
cancellation, urgent survival interruptions and progress survive save/reload.

Adults and elders can also store their own carried raw materials in their
household's House: "store wood", "store five clay in my House", or "keep storing
stone". Storage preserves personal ownership. A default task puts away one
available lot, limited by House space; an explicit quantity counts goods actually
stored and stops exactly at the requested number. Borrowed, reserved and
promised delivery goods are unavailable to these orders. Walking earns no
progress. Missing goods, a missing House, full storage or a blocked route leaves
the task waiting with a reason. Queueing, cancellation and interrupted progress
survive reload.

Equipment storage accepts the same five clothing/carrying-aid kinds and 13
tool kinds as equipment collection below: "store my basket", "store two iron
knives in my House", or "keep storing padded coats". The agent puts away its
own carried, unworn goods while keeping their ownership and condition. Worn
clothing and carrying aids stay with their wearer. Exact quantities, House
space, travel, queues and cancellation follow the material-storage rules above.
All personal-storage orders accept an optional House tile, such as "in my House
at (12, 4)". They keep the first chosen House and its household owner. If it
moves, disappears or changes owner, or the agent leaves that household, the
task waits without changing destinations.

Orders also support exact-tile movement: "Move to tile (12, 4)", "Go to (12, 4)"
or "Travel to (12, 4)". They use ordinary walking routes and travel delays,
finish only on the requested tile, and wait when it is occupied or unreachable.
Entering another household's House still requires an invitation.
Each order is one trip; repeated or counted tile trips are not understood.
The target survives queueing, urgent survival interruptions and save/reload.
Giving coordinates does not create firsthand map knowledge; the agent learns
the tile only by reaching it.

An adult can also follow "Become guardian for Lina", using the full name of a
child with an open guardian search. The name is resolved once to that child,
so a later rename does not redirect the task. Acceptance uses the search's
current eligibility rules and finishes only after primary care is assigned.
A completed House with room is still required for relocation. A blocked collection
route does not prevent another accepted child with a clear route from moving
home; the blocked move keeps its accepted guardian and retries later. This is one
acceptance, not a repeated task; other non-food task domains remain in
[#587](https://github.com/compoodment/ClankerWorld/issues/587).

Personal collection orders cover the same seven raw materials: "collect my
wood", "collect three clay", or "keep collecting stone". Add "from (12, 4)"
or "at tile 12,4" to collect only from that tile, including storage at a
building's listed coordinates. If the requested goods are unavailable there,
the order waits instead of collecting somewhere else. Adults and elders
walk to their own available goods in current or former household storage,
belongings left to them by a settled will in another household's House, or goods
on the ground, and physically pick them up. Household or other people's goods,
reserved portions and promised deliveries remain unavailable. A default task
collects one load within carrying space; quantities stop exactly at the requested
amount. Travel earns no progress, full hands leave the task waiting, and
queueing, cancellation and partial progress survive reload. Collection grants
no renewed household membership or private-stock access.

Food collection uses the same personal-goods rules: "collect food", "collect
three berries", "collect cultivated greens from (12, 4)", or "keep collecting
fruit". A named kind selects berries, fruit, wild greens, cultivated greens or
one of the seven prepared foods; plain "food" accepts any ready-to-eat food. These tasks collect existing
stored or dropped goods. They do not harvest a food source or eat the pickup.
Raw grain and potatoes use the goods catalogue below. Quantities,
source tiles, carrying limits, queues, interruptions and reload work as above.

Equipment collection covers the five clothing and carrying-aid kinds and the
13 tool kinds described below: "collect my basket", "collect two iron knives"
or "keep collecting padded coats". Add a source tile in the same way as other
collections. The agent picks up its own goods without equipping or repairing
them; their condition stays unchanged. Each unit occupies ordinary carrying
space, and explicit quantities count individual items even when they share a
stored lot. Shared or borrowed equipment remains unavailable to these orders.

Personal-goods collection and storage also cover rope, cloth, refined iron and
gold, workshop tools, grain, flour, potatoes, named planting seeds, medicinal
herbs, bandages, medicine, storage pots, water jugs and gold or diamond ornaments.
Examples are "collect two refined iron", "store three grain seeds in my House
at (12, 4)" and "collect one water jug". Bare seeds, tools and ornaments remain
ambiguous. Pots and jugs move whole, with their contents and ownership intact;
their contents use space but do not add to the number of vessels moved.
Selected equipment and reservations remain protected. Household-owned outputs
are not available to personal collection or storage.

Borrowed-return orders such as "return two borrowed cloth" and "return one
borrowed water jug" put carried household goods back in their owner's House.
This includes collected milk even when a Store or Market stall could stock it
instead. Reservations and committed deliveries remain protected.
They preserve ownership and pin the selected House and source lot while work
is pending. A default task returns one available load; an explicit quantity
caps the last move. Only a physical return earns progress. Unavailable goods,
House space or walking routes leave a visible blocker. Queues, interruptions,
cancellation and reload retain the work already done.
An order to put away a tool currently used for ordinary field work interrupts
that work before moving the tool, keeping the saved work state consistent.

Delivery orders use the existing household and Town supply rules. The commands
distinguish their purpose: "haul four grain to my Farmhouse", "supply two iron
to my Blacksmith", "deliver two berries to my House", "donate two wood to my
Town Warehouse", or "stock three cloth in my Store". Named destinations may
include a listed tile with "at (12, 4)". The selected building and owner stay
fixed through travel, partial progress and reload.

House hauling skips loose supplies it cannot reach or bring home, then tries
another permitted pile. A blocked first pile stays untouched while the agent
collects reachable goods that fit in its load and the House.

Pickup and walking earn no delivery progress. An explicit count measures the
requested goods at the final deposit; a plain request completes one load.
Household supplies retain their real carried delivery assignment between
pickup and deposit. A matching existing shipment can be selected before its
delivery, but its earlier pickup does not count. If space runs out during the
trip, the load waits until it fits. Cancellation leaves actual goods and
completed transfers in place, and never credits a later ordinary delivery to
the cancelled task.

Workstation supplies remain limited by current recipes and ingredient demand.
Farm hauling retains its existing stock-source and storage rules, with the
requested Farmhouse or Silo taking precedence over the ordinary preference.
House food delivery shares only spare carried food. Town donations accept the
normal wood, stone, fiber and tree-seed surplus while keeping the agent's
reserve of four usable units of each kind across their carried lots. Storing
and collecting goods does not create extra reserves. Store stocking keeps its
usable shelf target and food reserves. It keeps one best usable tool in each family;
extra units in the same carried lot can be stocked. Spoiled or broken goods
still take physical storage space but do not satisfy the restocking target.
Its food reserves apply across usable lots of the same owner and kind, so
collecting food in smaller loads does not create extra reserves. Missing demand
or surplus leaves a visible blocker even if the order
asks for more.

A Store-stocking order can pause an ordinary unfinished project, deliver its
goods, then let that project continue with its previous work. A production job
already holding reserved inputs finishes before the agent leaves to stock the
Store. Its goods and payment history remain separate from the delivery count.

Vessels and their contents keep their physical identity and ownership rules.
For a water supply, the count measures water in the delivered jug, not the jug
as an extra item. The whole family must fit and stay unreserved; water cannot
be split to satisfy a smaller count. Grain and flour can use the existing
partial-vessel pickup. No other decanting or arbitrary private-stock access is
introduced by these orders.
