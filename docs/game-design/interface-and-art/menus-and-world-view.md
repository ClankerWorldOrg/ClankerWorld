---
title: Main Menu, world view, and controls
type: game-design
status: active
updated: 2026-11-14
---

# Main Menu, world view, and controls

This page covers menus, the world view, controls and settings.

### Agreed

- Launch into the **Main Menu** with **New World**, **Load World** when worlds
  exist, **Settings**, **Mod Library**, **Quit Game**, and a separate **Continue** action for the
  most recently played world.
- **New World** offers world size and climate choices, advanced generation
  controls, preview/reroll and wrapping. **Agreed on October 1
  ([#648](https://github.com/compoodment/ClankerWorld/issues/648)):** it offers
  no survival grace period or extra starting supplies for now; a gentler start
  may be added once the default game is finished. The ordinary game has **one
  supported first-Town setup mode**. The nothing-start challenge mode was
  removed from the plan for now. Generating the world creates the map and opens
  it paused; four starting agents must be configured before starting time. The
  first-Town siting and generated building layout flow are in
  [Agents and social life](../agents-and-families.md#agreed-starter-town-and-remaining-choices).
  API keys are not a prerequisite for reaching the world view.
- Enter a visually coherent generated world. The player can see its whole
  geography from the start; there is no player fog of war. Individual agents
  may know only what they have experienced or learned.
- **Agreed after reviewing the tile-inspection prototype:** selecting ground
  should open an inspection panel with meaningful, facts recorded by the game.
  Keep terrain kind separate from generated climate, elevation, water layout,
  surface and vegetation cover; show present objects, resources, buildings,
  regional weather and soil moisture from their own observations. Omit absent
  or unprojected facts, such as exact temperature or a fertility the game does
  not know, from the panel; never infer them from terrain color or fill the
  panel with `none` and `unavailable` labels. Player inspection does not teach
  an agent those facts. **Agreed on October 8
  ([#1276](https://github.com/compoodment/ClankerWorld/issues/1276)):** the
  tile card shows **Fertility** when the game knows it and has no separate
  biome field. Its layout is the one chosen in the October 2 panel review
  in [Panels and player guidance](panels-and-player-guidance.md#agreed).
- One continuous pixel-art world view supports mouse-wheel zoom and WASD
  panning. On an east/west-wrapped world, the camera should pan continuously
  across the seam in either direction. There is **no separate simplified
  regional view or second regional texture set**. Zoom-out stops at a
  readability/performance limit. After the September 29 playtest, the cap must
  also keep black space beyond the north/south map edges out of view on Small
  maps, and maximum zoom-in should feel consistent across render resolutions.
  The numbers agreed for that limit on October 8, and when they are tuned,
  are under [Leaning toward](#leaning-toward) below.
- Hovering a ground tile shows a square tile highlight. When an agent overlaps
  that pointer location, **agent hover/selection takes priority** over the
  ground tile. This is pointer hit-testing priority, distinct from making the
  agent's clickable footprint spill into neighboring tiles.
- An always-accessible **top-left Map button** expands a world overview. It
  shows the current camera rectangle; dragging it or clicking the overview
  moves the main camera. The overview can be drawn from world data rather than
  a second library of game textures.
- The top bar also shows a **pause/resume-time control**, in-world date and
  time, agent population, a **World Info** button, a **Filters** button, an
  **Event Log** button, an **Add Agent** button near the menu button, and the
  menu button at top-right. Date display format and 24-hour/AM-PM time format
  belong in UI Settings.
- **Agreed on October 1
  ([#640](https://github.com/compoodment/ClankerWorld/issues/640)):** the date
  uses season names by default, such as **Autumn 2, Year 1 · 14:20**, because
  each 10-day month is one season: Spring, Summer, Autumn and Winter. Game
  Settings also offers numeric dates (DD-MM-YYYY, MM-DD-YYYY or YYYY-MM-DD) for
  players who prefer them, and keeps the 24-hour or 12-hour time choice. Agent
  ages show as life stage plus days, such as **Adult · 22 days**.
- **Agreed after the September 29 style review:** the Event Log button shows
  how many events arrived since the player last opened the log. Because a
  world can hold several Towns, the top bar has no single-Town button; Town
  facts belong in World Info.
- Important events belong in the **Event Log**, not optional out-of-view
  event pop-ups/notices. Clicking the top-bar Event Log button opens the log;
  clicking a located event moves the camera to where it happened. Opening the
  log does not pause the world. This supersedes the earlier configurable
  notification proposal; an agent's inspectable info panel is not an event
  notice. **Agreed on October 8
  ([#1276](https://github.com/compoodment/ClankerWorld/issues/1276)):** the
  log has an icon for each kind of event and a heading for each day, shows the
  newest first and has no filters. Only an event with a place has a **Find**
  button. The game keeps the newest 1,024 events in each of its event records
  and archives older ones; that number is provisional. **Agreed on October 9
  (owner request in chat):** each line says who was involved and what
  happened, in plain words without legal or code terms. Births, deaths,
  marriages, skills learned, animals and scouting trips appear. Routine steps
  that a later line already reports do not: meals, forage trips, tending a
  crop, loading and parking a cart, starting a recipe, picking up goods on the
  way to a delivery, each Town project work shift, and reading or passing on a
  case file.
- World Info should let the player inspect discovered capabilities and other
  world information. Separate map filters show Town title, household land use,
  disputed land, household property and Town borders; these are the first set
  of overview and filter layers. The UI must not invent
  ownership or borders that agents have not established.
- **Agreed on September 30:** World Info lists discovered capabilities from
  recorded discoveries only. When nothing has been discovered it says
  **No recorded discoveries**, rather than listing every built-in recipe as
  known. Opening World Info never gives any agent knowledge. Experimental or
  unvalidated proposals are shown separately from usable discoveries.
- The top-right menu button opens the **Pause Menu** and pauses the world. It
  contains **Save World**, **Load Save**, **Settings**, **Mod Library**, then **Quit to Menu**
  at the bottom. There is **no Quit Game action in the Pause Menu**; Quit Game
  belongs to the Main Menu. Settings use categories on the left and selected
  controls on the right. Quit to Menu and Main Menu Quit Game ask for
  confirmation.
- Main Menu Settings must retain the **Main Menu background/context**, not
  reveal the in-world UI underneath. Selecting the already-active Settings
  category leaves its controls visible; category buttons are selectors, not
  open/close toggles.
- **Reaffirmed after the September playtest:** the Pause Menu is compact, not
  the current three-row button grid. Keep the order above so Quit to Menu is
  always last/bottom. It has one **Settings** entry; **Game**
  and **World** remain selectable categories inside Settings for a loaded world.
  Main Menu Settings still exposes Game only. **Remove the current player-facing
  `Create` building-design workbench entirely**; computment considers it a
  prototype from before the revised agent-driven invention vision. This does
  not remove agents' ability to invent. **Agreed on October 8
  ([#1268](https://github.com/compoodment/ClankerWorld/issues/1268)):**
  agents' designs show in the Mod Library.
- Game Settings offers **Fullscreen** and a **Window Size** choice of fixed
  sizes. The October 1 playtest below removed the earlier **Render
  Resolution** and **UI Scale** controls, and the interface's whole-step
  growth there sets the scaling and pixel-art rules. Start in fullscreen by
  default, while respecting a saved player choice to use a window. Main Menu
  Settings needs one compact back chevron in the close-button position; omit
  duplicate large back navigation. Load World does not need the pause/save
  helper sentence. Player-facing wording should be short and ordinary; use
  **Agent model**. The optional model-call limit needs a clear unit and consequence.
- **Agreed after the September 29 style review:** the interface uses the
  **Timber & Parchment** look chosen from three mockup directions: wooden
  frames around parchment panels, ink text, bevelled pixel buttons and a green
  main action. Game Settings offers a **Theme** of **Light**, **Dark** (dark
  wood with cream text) or **Match system**. The rest of the interface icon set
  is drawn and chosen in the ordinary art reviews.
- **Accessibility, agreed on October 8
  ([#1253](https://github.com/compoodment/ClankerWorld/issues/1253)):** every
  screen meets one baseline. It never relies on colour alone, it can be used
  fully from the keyboard, any flashing can be switched off, and its text
  contrast is checked in both the Light and Dark themes. Separate
  high-contrast and colour-blind modes are not part of the baseline.
- **Agreed after the September 29 background review:** the Main Menu
  background is a side-view pixel-art valley chosen from three mockup
  directions: mountains, patchwork fields, a river and small Towns built from
  the game's own building types. It animates gently (drifting clouds, chimney
  smoke, birds, river sparkle). The Light theme shows a clear morning and the
  Dark theme a dusk with lit windows, lanterns, stars and fireflies.
- **Agreed after the September 29 logo review:** the Main Menu shows a logo
  and no slogan. Chosen from five options, it has wood-grain letters (without
  angled 3D) either side of a friendly robot waving in front of a small
  planet; the robot and planet also serve as the app icon. A status line
  appears only when something needs attention. **Agreed on October 15, by the
  owner in chat:** joining a world on another computer, including pairing,
  happens in the launcher, so the Main Menu never asks to connect this device
  ([Where the game runs](../world.md#where-the-game-runs)).
- **Agreed after the September 30 font review:** text uses pixel fonts,
  chosen from six options shown on real game screens. Body text is **Fusion
  Pixel 12px**. Titles, panel headings, section labels, dialog titles and the
  Main Menu's choices use **Timber**: thin capitals drawn from the logo's
  letter shapes. Base text sizes are whole multiples of 12 px and are drawn
  without smoothing, and UI Scale uses only whole-number steps, so letters
  stay crisp. computment prefers this look over a smoother font chosen for
  easy reading.
- [The September 30 UI Scale choices](../../archive/interface-and-art-history.md#september-30-ui-scale) were replaced by the October 1 decision below.
  Top-bar panels open under the button that opened them,
  except the Event Log, which sits at the right edge of the screen, and the
  mouse wheel over a panel never zooms the map behind it.
- **Agreed after the October 1 playtest** (replacing the UI Scale choices
  in [Interface and art history](../../archive/interface-and-art-history.md#september-30-ui-scale)): **there is no UI Scale setting.** The interface grows with the
  screen in whole steps, so pixel letters always stay crisp: 100% on small
  screens, 200% at 1080p and 1440p and 300% at 4K, one step lower whenever
  the menus would have less than 960 × 540 interface pixels. computment
  tried a 150% size and found its pixel letters too uneven, and found 100%
  too small and anything above 200% unnecessary at 1440p. **Render
  Resolution is removed**: the game always draws at the screen's own
  resolution. Settings is titled simply **Settings**, with its sections in
  their own boxes. On a narrow layout the top bar still shows icons only.
- **Agreed on October 1:** agents inside a building are not drawn shrunk onto
  its tile. They are hidden from the map while inside, the building shows a
  small badge with how many people are in it, and its card names them.
- **Agreed on October 1:** Developer tools leave the pause menu for their own
  panel on a key, and become genuinely useful: for example tile coordinates,
  a performance readout, jumping to any agent and showing an agent's planned
  path.
- **The first Developer tools, agreed on October 1
  ([#650](https://github.com/compoodment/ClankerWorld/issues/650)):** **F12**
  opens the panel. It keeps today's tools (life-speed override, recovery for a
  lost model reply, paused world editing and paired-device management) and adds
  tile coordinates and facts, a performance readout, jumping to any agent and
  showing an agent's planned path. It also offers **direct edits**: set an
  agent's needs, give or remove goods, add or remove a skill, and start or end
  a relationship. Every direct edit is written to the Event Log as a developer
  edit, so playtest results are not mixed up with normal play. Time tools, such
  as stepping one tick, are not in the first set.
- **Agreed on October 10
  ([#1566](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1566)):**
  **F12** works only in **Developer mode**. The launcher's Settings turn it on
  and pass it to the game as a start option; a game started without the
  launcher uses the same start option, with no in-game switch. A game the
  launcher started offers **Quit to Launcher** on its Main Menu.
- **Agreed on October 16 ([#2346](https://github.com/ClankerWorldOrg/ClankerWorld/issues/2346)):**
  paused world editing uses named terrain, resource and object choices, followed
  by clicking or dragging tiles, a preview and **Apply**. The server validates
  the whole selection, saves accepted edits and records them as **Developer edit**;
  a refusal explains why and changes nothing. The owner approved restoring
  these native server operations, including validation, saving and replay,
  as part of the same change. Editing remains in Developer tools and requires pause.
For the Add Agent placement order and inspection panels, see [Panels and player guidance](panels-and-player-guidance.md).

### Leaning toward

Provisionally, the zoom-out floor stays as it is until Large worlds have been
measured, and is then tuned. Computment suggested Small/Medium zoom-out around
70% of map-fit scale and a shared performance cap for Large/Huge/Mega; that
exact percentage and visible-tile budget remain preferred starting points for
tuning, not fixed finished-game numbers. **Agreed on October 8
([#1276](https://github.com/compoodment/ClankerWorld/issues/1276)):** Small and
Medium maps zoom out until the view spans 70% of the map, and Large, Huge and
Mega maps share a floor of 8 pixels per tile. Both numbers are provisional and
are tuned once Large worlds have been measured.

Game Settings apply across worlds/on this installation: UI date and time
display formats, graphics/display preferences, and stored provider credentials.
**Agreed on October 1
([#639](https://github.com/compoodment/ClankerWorld/issues/639)):** the
AI-usage meter and limit also belong in Game Settings, because one limit covers
every world on the installation. The Main Menu's Settings entry opens Game
Settings. World Settings belong to the current save: autosave on/off, interval
and rotation, and Jev's per-world configuration. Selecting either category
within Pause Menu Settings keeps the world paused. World generation choices
such as size, climate and wrapping are chosen before creation and should be
inspectable afterward, not silently mutable settings. Jev's on/off switch for
an existing world is accepted; the transition behavior for other in-flight
tasks remains open. **Agreed on October 8
([#1276](https://github.com/compoodment/ClankerWorld/issues/1276)):** Game
Settings holds the window, theme, weather effects, date and time, the AI-usage
limit and keys; World Settings holds autosaves, the routine helper and agents'
models. Settings also gets a read-only view of the world's generation choices:
size, climate, wrapping and seed. World Settings must be absent
from Main Menu Settings while no world is loaded; opening Main Menu settings
must never enter a world.
The routine helper choice is Off, Jev or OpenAI Decisions, each with its own
model list; the owner approved Decisions details on October 6
(see [Thinking, memories and social life](../agents-and-families.md#thinking-memories-and-social-life)).


**Agreed (confirmed 30 September):** placing an agent with Add Agent on a
household's property joins that household without its consent, as recorded in
[Towns](../towns/buildings-and-animals.md#buildings-land-towns-and-animals). **Agreed on October 8
([#1274](https://github.com/compoodment/ClankerWorld/issues/1274)):** the
rest of this paragraph is agreed too, including the refusals it lists.

In the **Add Agent** placement view, show exclusive household use areas and
Town borders so computment can see the new agent's initial affiliation.
Within the agreed placement order in [Panels and player guidance](panels-and-player-guidance.md#agreed), placement on a tile assigned
exclusively to one household **forces the new agent into that household**
(and its enclosing Town, if any); this player setup action does not seek
household consent. Placement elsewhere within a
Town joins the Town but no household; placement on unclaimed land
outside both starts an independent agent. Location establishes **starting
social membership**, not biological ancestry or permanent membership based on
where the agent later walks. A player-added adult could be a new unrelated
family line even when placed inside an existing household. Add Agent is
refused on an unbuildable, occupied or disputed tile, on a tile where another
agent stands unless it is a House, and into a full House. A later voluntary
move by an adult who has no household into a household's House needs every
adult member's agreement, as agreed in
[Towns](../towns/buildings-and-animals.md#buildings-land-towns-and-animals).
This forced Add Agent membership is distinct from inviting a nonmember to
visit a House. The agreed first-Town household setup in
[Agents and social life](../agents-and-families.md#agreed-starter-town-and-remaining-choices)
is separate from this later Add Agent placement rule.

### Still to decide

The family tree's line styles and navigation are still open. The player's
role, direct edits, suggestions and order rules are agreed in
[Player guidance and orders](panels-and-player-guidance.md#player-guidance-and-orders). Computment may
provide a UI drawing.
