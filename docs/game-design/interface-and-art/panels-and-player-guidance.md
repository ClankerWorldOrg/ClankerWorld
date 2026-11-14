---
title: Panels and player guidance
type: game-design
status: active
updated: 2026-11-14
---

# Panels and player guidance

This page covers inspection panels, player guidance and orders.

For menus, controls and Developer tools, see [Main Menu, world view, and controls](menus-and-world-view.md).

## Agreed

- **Agreed after the October 2 panel review:** computment accepted redesigns
  of the panels that had not been touched yet, shown as before-and-after game
  screenshots, with these changes of their own:
  - The selected-tile card is headed by the ground (Meadow, Forest) with a
    picture of the tile. **Climate** is its own labelled row, so it cannot be
    read as describing the whole world.
  - The Agents list shows a portrait row per agent with **Hungry**, **Cold**
    and **Ill** tags. The Event Log has an icon per kind of event, a heading
    per day and a **Find** button kept clear of the scrollbar. The controls
    list draws keys as keycaps with the letter centred on the key.
  - World Info's World page is a "today" card and a grid of counts with icons.
    Its Towns page shows residents' portraits, stores as item slots and
    projects with progress bars, without the note about Town borders.
  - Memories has tabs and a card per entry with a five-step sureness meter.
    **Agreed on October 8
    ([#1276](https://github.com/compoodment/ClankerWorld/issues/1276)):** the
    tabs are **All**, **Memories**, **Beliefs** and **Maps**, and the reader
    has no search.
    The Family Tree uses small portrait boxes and opens beside the Profile.
  - The Profile's model line names the model's provider plainly instead of
    "chosen by". Model choices are labelled **Provider**, **API key**,
    **Model** and, **agreed on October 9 (the owner in chat)**, **Thinking**,
    and an agent's model settings link to the main model settings
    instead of offering to delete a key. Add an agent says **Click on land to
    place them**.
  - The Mod Library lists mods as cards with a status tag. Confirmation
    dialogs centre their text and have a framed close button inside the frame.
    Status messages show a tick or a warning sign.
  - **Agreed on October 16 (the owner in chat, #2359):** selecting a world in
    Load World shows its save timeline directly. Load opens the chosen save,
    or the newest save when none is chosen. The Pause Menu's Load Save opens
    the current world's timeline directly.
  - Load Save draws a world's save branches as a large timeline with the
    chosen save described underneath and the list behind a **Timeline / List**
    switch (option C of three, for #680). After two more rounds of drawings,
    computment chose this look:
    - Each branch is a thick line in its own colour, with a numbered badge and
      its save count on the left. A new branch bends down from the save it
      grew from.
    - A thin season bar runs across the top, with a tick per day, each
      season's icon and name ("Spring · year 2") and day numbers where there
      is room. A faint wash of each season's colour sits behind the lanes.
    - Older saves are open points with a dot of the branch's colour, and
      autosaves are small diamonds. Each branch's newest save is a solid point
      under a small hanging banner in the branch's colour.
    - Save names sit in small tags below the line, or above it when the space
      below is taken. The chosen save's tag turns orange, with corner marks
      around its point.
    - **You are here** is an orange marker with a person at the end of a dotted line.
    - The row above the timeline holds a key to the points (newest on its
      branch, save, autosave) beside the switch, instead of a line of counts.
    - **Agreed later on October 2:** Save World shows the same timeline under
      its **New save** box, with the same switch. With no save chosen, the
      card underneath says where the new save goes: on along a branch, or
      starting a new one. Choosing a save offers **Overwrite**. Autosaves are
      drawn but can't be chosen there, because they can't be overwritten.
- **Agreed on September 30:** panels and confirmation dialogs fit what they
  hold rather than keeping a fixed size with empty space. A panel with long
  text stays on screen and scrolls that text; a long dialog message wraps.
- **Agreed after the September 30 agent card review:** computment chose the
  two-step card (option D of four mockups). Selecting an agent opens a small
  **quick card** beside them with their name, what they are doing, bars for
  fullness, warmth and illness, and **Profile** and **Speak** buttons.
  **Profile** docks a larger panel on the left of the screen: portrait, a
  pencil to rename, age, diet and the other bars, belongings, work and
  learning, private thoughts, people, **Memories**, **Family** and **Model**,
  and a message box with a **Suggest** or **Order** choice. Clicking the
  thoughts, or **Read all**, opens every recent thought in a larger reader
  beside the Profile, grouped by day; Memories opens beside it too. The
  Profile's back button returns to the quick card; a deceased agent's
  historical Profile opens directly and simply closes. This refines the
  selected-agent popup described further down this list.
- **Agreed on September 30:** buttons are consistent across the whole game.
  Every panel closes with one small square button showing a pixel ×. A screen
  reached from another shows a pixel back chevron in that same place, instead
  of a separate Back button. The Main Menu and Pause Menu use the same choice
  buttons, other buttons share one height, and confirming something that
  cannot be undone uses a red button. The hover readout names what is under
  the pointer without map coordinates.
- **Agreed after the September 30 nameplate review:** the map shows an agent's
  name only while that agent is selected or hovered, as light pixel letters
  with a dark edge and no box; the selected agent's name is gold. Names carry
  no activity symbol. Buildings show no names on the map; hovering a building
  still names it. computment chose the outlined-text look (option C) with the
  only-when-needed rule (option E) from the nameplate mockups.
- **Agreed on October 8
  ([#1247](https://github.com/compoodment/ClankerWorld/issues/1247)):** an
  agent waiting for a slow model shows a small marker on the map, so a slow
  provider doesn't make it look frozen. The Event Log adds one line only when
  the provider actually fails. What the agent may do while it waits is in
  [The world](../world.md#world-time-pausing-and-slow-models).
  The owner selected **C · Circling spark** from the October 8 marker review
  on [#1315](https://github.com/compoodment/ClankerWorld/issues/1315), recorded
  in its discussion. The proposed short delay is two seconds; it remains
  provisional for playtesting.
- **Agreed after the September 30 building panel review:** selecting a
  building opens a small **quick card** beside it, like an agent's: its roof,
  name and owner (household or Town), one line on what is happening there (who
  is inside, or who is making what, with a progress bar and time left), what
  it stores as item icons with the count in the corner, and a **Details**
  button. **Details** docks a larger panel on the left, like an agent's
  Profile: owner, who may use it, when it was built and which side its door
  faces; storage as a larger icon grid with each item's name; work in progress
  with what it uses; and who lives there or is inside. Details shows recent
  recorded storage additions and removals. Workstation recipes show their material
  quantities without granting agent knowledge or production access. A
  storage-space bar shows recorded occupancy whenever a building records a
  storage limit. When the owner is paired, Details also offers signed building
  removal and owner-change actions. A confirmation explains that removal keeps
  the Town border and
  Roads; the server refuses a change while stock, deliveries or active work
  remain. Moving a private building between households keeps its Town title and
  assignment; moving a Warehouse changes its recorded Town assignment but not
  either border. Refusals appear in plain language. Hovering a building still
  names it.
- **Agreed on September 30:** items have their own pixel-art icons, drawn by
  hand on a 16-pixel grid in a few shades with a complete dark outline and
  shown only at whole-number sizes so they stay crisp. An item without its own
  icon yet shows a plain crate, so new items are still counted.
- **Agreed on September 30:** map Filters start off, Town borders included.
  Town borders show as a pale dashed line along the border's edge (option B of
  four looks). Add Agent placement still shows Town borders and household
  property while placing, without switching the Filters on.
- **Agreed on October 1:** stripe land with competing household claims, and
  clicking a tile lists each household's claim.
- Following the Filters above, Town title and household use rights show as
  separate overlays; the tile list also names the Town title, and Add Agent
  placement shows these records.
- Adding an adult agent opens a flow to select a provider, one of its stored
  API credentials or a newly entered one, and a model, then place the agent in
  the world. Existing credentials can be reused by multiple agents. A player
  may also add another key for the **same provider** and choose which key that
  agent uses; there is no single-key-per-provider restriction. The model is
  chosen from the game's own short list for that provider, newest at the top.
  Listed models the selected key can't use are shown greyed out, and the
  player can always type any other model name. The flow also sets the agent's
  **Thinking** level. The agent
  chooses its own name after placement; the player can rename it later. Agents
  should choose full names with a surname and may choose a middle name. Do not
  include every existing agent name in the naming prompt merely to avoid
  collisions. Under the October 3 decision in
  [Agents and social life](../agents-and-families.md#starting-agents-families-and-life-stages),
  the world rejects an already-taken chosen first name and asks the
  agent once more; if that also fails, the agent keeps a placeholder name the
  player can change. The comparison covers every agent in the world, including
  those who have died; a different surname or middle name does not make a taken
  first name available. Temporary unnamed placeholders do not reserve a first
  name. Player renaming follows the same first-name check, as recorded in
  [Player guidance and orders](#player-guidance-and-orders). **Agreed on
  October 8 ([#1273](https://github.com/compoodment/ClankerWorld/issues/1273)):**
  the naming request gives no cultural context. It carries only the naming
  rules, the suggested first letter
  ([#496](https://github.com/compoodment/ClankerWorld/issues/496)) and, for a
  child, its parents' surnames.
- **Agreed on September 30:** before the player confirms, Add Agent shows which
  household and which Town the new agent will belong to. One shared rule
  decides this, checked in this order: household property first, then
  unclaimed land inside a Town, then land outside both. The preview is not a
  reservation: when the player confirms, the game checks the same map again and
  tells the player if the placement changed. If claims or Towns overlap so that
  the answer is ambiguous, the placement is refused and the game explains why.
  It is never decided by list order or distance. How claims and use rights
  work, and how Add Agent uses them, is in
  [Town land and household use rights](../towns/land-and-roads.md#town-land-and-household-use-rights).
- Selecting an agent opens an **interactive info popup near that agent**. The
  player can change that agent's provider and model there, including choosing
  an appropriate stored/new credential when needed. **Agreed on October 8
  ([#1275](https://github.com/compoodment/ClankerWorld/issues/1275)):** after
  such a change, the game refuses any reply to a model request sent before
  it ([other requests in flight](../world.md#world-time-pausing-and-slow-models)).
  Agent inspection does not pause the simulation. This is a selected-agent
  panel, not a fleeting mouse-hover tooltip that disappears when reaching for
  its controls. Its core
  view shows name, age/life stage, household, current activity, needs, and the
  agent's latest **private** thought or intention. A **small scrollable history
  of recent private thoughts** is available there too. **Agreed on October 8
  ([#1273](https://github.com/compoodment/ClankerWorld/issues/1273)):** each
  ordinary decision carries a short in-character thought of up to 160
  characters, and the eight newest are kept with the world save; both numbers
  are provisional. The player can inspect them, but other agents do not
  automatically know them. A separate **Memories** section lets the player inspect what this agent remembers or
  believes happened, including mistaken beliefs; it is different from the game's
  record of actual world events. Inventory, relationships, and model settings can expand
  from the same popup. A **Family Tree** action opens a larger interactive
  graphical view; selecting a person in the tree opens that person's agent
  info popup, including for deceased relatives. The tree distinguishes
  parent-child ancestry and partnerships; household membership is displayed
  separately, never as proof of biological family. Unrelated starter
  housemates must not be drawn as relatives. **Agreed on October 8
  ([#1273](https://github.com/compoodment/ClankerWorld/issues/1273)):** the
  tree shows only ancestry and partners; guardianship shows on the Profile
  and in the Event Log instead.

### Player guidance and orders

**Agreed on October 1, during the owner design session:**

- The player is an **outside observer whom agents can hear**, rather than a
  physical character in the world or an impulse agents mistake for their own
  thought. An agent can distinguish the observer's guidance from its own
  intentions.
- **Suggest** sends the player's actual words to the agent's own model at its
  next personal-model decision. The agent may accept, modify or reject the
  suggestion. A new message asks for one fresh decision; any brief reply is
  part of that response. For example, "Growing potatoes could help your
  household through winter" conveys the reasoning, not just a farming task hint.
- A recognized **Order** takes priority over the agent's ordinary plans. The
  agent obeys within the game's physical and access rules; its model cannot
  refuse the order merely because it prefers another activity. An order cannot
  create goods, grant ownership or make an impossible action happen.
- The player may deliberately tell an agent something that agent has not
  discovered, such as the location of a berry patch across a river. The agent
  learns that the observer told it something; the statement can be true or
  false and is not automatically firsthand knowledge or a verified world
  fact. It still needs to travel there and verify the claim. Merely inspecting
  the map or another agent's private thoughts teaches no agent anything.
- The practical order catalogue covers movement, shelter, eating, gathering,
  carrying, storage, farming, cooking, crafting, repairs and building work,
  as those tasks become supported by the game. A recognized task uses the
  normal task system and its physical, resource and access rules.
- Conversations, relationships, inventions/mods and combat are agreed parts
  of the intended game, with their full systems planned for later alpha work.
  Their supported actions may become **Orders** too: "Talk to Ari", "Propose
  marriage" or "Work on a bridge invention" can require the addressed agent
  to attempt the activity once the game supports it. An order does not
  guarantee another person's agreement or a successful invention. Each
  system's participation and validation rules still apply; later development
  does not make these features provisional or restrict them to Suggest.
- When an order does not specify its target, the agent chooses a suitable
  target using its own knowledge and the normal access and travel rules. It
  may also explore to find new resources rather than always returning to the
  same known plant. Exploration discovers things through actual travel and
  observation; it does not grant hidden map knowledge. An explicitly named
  target takes precedence over this free target choice.
- Urgent survival needs may **temporarily interrupt an order**. For example,
  an agent gathering wood may seek warmth during a dangerous storm, then
  resume the pending order when the emergency passes. Ordinary preferences
  cannot interrupt it on the same basis.
- An order means **one task by default**, unless it specifies a quantity or
  repetition. "Gather food" finishes after one normal gathering job; "Make
  two sacks" finishes after two sacks; "Keep gathering food" continues until
  cancelled. Emergency interruptions preserve the outstanding task, quantity
  or ongoing instruction.
- A **recognized new Order replaces the previous order by default**, including
  an ongoing or waiting order. The player can explicitly choose **Queue** to have the
  new order done afterward instead. Pending and ongoing orders can be
  cancelled. For example, "Make two sacks" replaces "Keep gathering food"
  unless the player chooses Queue.
- **Agreed on October 2:** an order the game cannot understand leaves the
  current order running. The failed new instruction is reported as not
  understood; a typo does not cancel a task already in progress.
- The agent's own model sees an order's **original words and the task the
  game understood** in its next ordinary request. The wording supplies
  context, such as why the household needs food; it does not let the model
  refuse an otherwise valid order merely because it prefers another plan.
  A new active order asks for one fresh decision. Following its steps does not
  add per-tick model requests or a separate paid acknowledgement.
- The agent card shows orders and their status: **waiting, doing,
  interrupted, blocked with a reason, finished, cancelled or not understood**.
  For example, "Waiting for cloth: needs two pieces" explains a blocked job.
  A brief natural reply may accompany the agent's ordinary model response;
  the game does not generate an extra paid reply just to acknowledge a click.
- Normal direct agent edits are **names and model/key settings**, alongside
  the existing Add Agent and Suggest/Order controls. Needs, skills, goods,
  memories, relationships and world objects change through simulation actions.
  Direct manipulation of those fields belongs in developer tools, whose
  first set is agreed in [Main Menu, world view, and controls](menus-and-world-view.md#agreed). This preserves the separately agreed
  founder setup, world settings and Mod Library controls.
- **Player renaming uses the same first-name check as model naming**, against
  every other named living or deceased agent in the world. A taken first name is
  rejected; the player supplies another name, without a model retry. An agent
  may keep their own first name while changing the rest of their name. Renaming
  keeps the same person,
  relationships and memories; old conversation text remains as originally
  spoken rather than being rewritten to use the new name.
  **Agreed with computment on October 3:** a surname change for a married agent
  updates both spouses together, keeping the other spouse's first and middle
  names. The name check happens before either change; refusal leaves both
  names unchanged. The original marriage surname conversation stays as it was.
  **Agreed on October 8
  ([#1273](https://github.com/compoodment/ClankerWorld/issues/1273)):** the
  renamed agent is told. Its model gets a permanent memory of the change, such
  as "I was renamed on day 22 to Mira Hale", rather than just seeing a new
  name.

**Existing agreed handling, from September 30:** an order the game cannot act
on is accepted and closed at once as not understood, without a model request.
It does not hold up later suggestions or orders. A recognized task that cannot
be carried out yet keeps waiting, including across saves. Blocked tasks retry
on the agent's usual decision schedule, without paid polling on every tick.

**These are intended rules.** [What works today](../../what-works/orders-and-goods.md)
describes the supported food orders and the remaining catalogue.
