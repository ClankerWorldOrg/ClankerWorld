---
title: Pixel art and generated images
type: game-design
status: active
updated: 2026-11-14
---

# Pixel art and generated images

This page covers the game's pixel art and invention pictures.

### Agreed

- **Pixel art** is the visual style, with **32×32-pixel ground tiles**. The
  art is drawn in code at startup rather than loaded from PNG files (October
  1); an exported mod may still carry approved image files. Taller agents/trees and multi-tile
  buildings can use larger transparent images anchored to logical tiles.
- **Straight top-down** is the chosen art perspective for now: show the tops of
  objects, not isometric sides. Isometric would need a visual comparison before
  reconsideration, and no second isometric asset library is currently planned.
- **Agreed on October 2:** computment likes the map's square-tile look, which
  gives the game its own character, so keep it. Coastlines and rivers keep
  their stepped, tile-by-tile shapes, and forests keep one tree to a tile on
  their square forest ground; none of them should be smoothed into curves or
  scattered. All art stays straight top-down: buildings show their roofs, not
  front walls drawn at an angle.
- For a given terrain surface, start with roughly **two texture tiles**—for
  example, clean grass and a subtly different grass tile. Small ground details
  belong mainly in those textures, rather than being a large set of separate
  decorative world objects. Functional transitions/edges are separate.
- The September 29 prototype grain, striped transition texture, and large
  circular rain/storm overlays are rejected. Keep terrain visually calm and
  weather readable without opaque repeating circles. **Agreed on October 8
  ([#1254](https://github.com/compoodment/ClankerWorld/issues/1254)):** the
  map's weather overlay goes through the next art review round, with options
  to choose from, like every other picture. **Agreed on October 8
  ([#1325](https://github.com/compoodment/ClankerWorld/issues/1325)):**
  computment chose look B, crisp pixel streaks: straight one-pixel rain that
  lands as a small burst, sharp storm streaks in gusts, and small snowflake
  crosses blown by the wind. No cloud shadows.
- **Agreed on October 9 (direct owner request,
  [visual polish review](../../../tools/ArtPreview/Proposed/Polish.md)):** the
  map gets touches that only show, more clearly, what the world already
  records:
  - agents and animals glide between tiles, with a small bob on each step,
    instead of jumping;
  - Houses with someone inside and a working forge send up a column of
    smoke that leans with the wind;
  - after snowfall the open ground turns white, with grass tips and trodden
    Roads showing, and anyone crossing it leaves footprints that fade; in
    autumn a few leaves lie under broadleaf and orchard trees;
  - dawn has a rose glow and dusk a gold one;
  - stores show piles of logs, crates and sacks beside the door that grow
    with what they hold;
  - the camera eases when **Find** moves it or the view zooms;
  - a finished building gets a ring of dust and a few twinkles;
  - after snowfall roofs keep snow on their shaded side and a dusting on
    their sunny side, following each roof's shape;
  - where someone died, a grave marked by a wooden cross or a headstone
    stays for a year of game time, then fades;
  - weather fades in and out instead of switching at once.

  Puddles after rain are postponed: none of the three rounds of drawings
  matched what computment has in mind, and whether puddles belong in the
  game at all is left open for later.
- Ground remains square-tiled, but permanent **black tile-border grid lines**
  are not part of the intended presentation; they would clash with textures.
- **Agreed on October 8
  ([#1271](https://github.com/compoodment/ClankerWorld/issues/1271)):**
  built-in art stays drawn in code only. The image rules (file format,
  metadata and size limits) apply only to images a mod carries; they are in
  [Inventions and mods](../inventions-and-mods.md#inventions-mods-and-technology).
  Aseprite is an optional editor/source format, not a requirement for
  computment or for playing the game.
- Agent inventions can include their own pictures. An exported mod carries its
  approved assets with it; art must not become executable authority.
- **Invention pictures, agreed on October 8
  ([#1270](https://github.com/compoodment/ClankerWorld/issues/1270)):** an
  invention's picture is built only from approved code-drawn parts and
  recolouring, chosen by the agent's model. When that fails, the invention
  shows a plain code-drawn placeholder, so a working invention is never
  blocked by its art. Invention pictures are never AI-generated images.
- Visual/content variety must be **bounded and supportable** across the game,
  not only for buildings. Do not imply that every theoretical combination of
  shape, material, style, state, and invention needs bespoke art or a bespoke
  simulation rule. This is a production constraint, not a ban on agents making
  genuinely new things.
- For the **first complete game**, each supported design can have **one
  standard appearance**. Two agents building the same size/type of house need
  not get different roof colors, decorations, or whole new sprites. Extra
  visual variants across buildings and other categories are optional work for
  after that complete game, not part of its required art workload. Agents are
  the exception: a few appearance variants are wanted for their population.
  The approved art settles the counts: one design for each building, object
  and item, two texture variants for each ground surface and six agent
  appearance variants ([style guide](../../development/art-style.md)).
- Agent inventions may introduce **genuinely new designs** rather than being
  permanently limited to the original art catalogue. Each new design still
  needs a valid visual/gameplay representation; this does not authorize every
  theoretical combination.
- **Agreed after the October 1 art review:** the game keeps its current
  pixel-art character, made more consistent by the
  [pixel-art style guide](../../development/art-style.md): ramps built on the
  existing colours, light from the north-west, calm ground with soft
  patches, outlined sprites and roof materials. computment reviewed every
  texture beside a proposed redraw and approved 193 of 216 pictures as drawn,
  with no rejections. Approved: the ground tiles for grass, forest, sand,
  rock, snow and tundra; water; Roads and the plank bridge, with a street
  running up to the deck; trees and natural sites, including picked and
  depleted states; crop fields and orchard stages; House, Warehouse,
  Blacksmith, Silo and Tailor Shop roofs and the Store, Market, Town Hall,
  Port and Clinic; agents facing all eight directions, with walking,
  carrying, working, talking and hurt poses and a first horse; and item
  icons, including those for water, wild greens, potatoes, porridge, stew,
  meals and diamonds. The coin icon is gold.
- **Changes asked in that review:** mountains, peaks and hills are redrawn as
  one landform spanning many tiles rather than a picture per tile, and how
  they generate is to be rethought too
  ([#628](https://github.com/compoodment/ClankerWorld/issues/628)); the Main Menu valley keeps its look
  with better mountains; tilled soil needs more than plain lines; the grain
  icon must read as grain as clearly as before; and the Farmhouse loses its
  painted cart, because handcarts will be real vehicles.
- **Agreed after the second round of the art review (October 1):** computment
  approved 211 of 222 pictures, with no rejections. Mountains, peaks and
  hills are drawn from the world's elevation as one landform spanning many
  tiles: a range reads as one mass with a snowy crest, and hills are soft
  foothill shading with no rings ("I like that mountain is a range"). Also
  approved: the Main Menu valley with its new mountains; tilled soil and the
  scrub, dry brush, desert brush and tundra snow ground; the clearer grain
  icon; the Farmhouse with grain sheaves and sacks instead of a cart; every
  building footprint, the Workshop, the Restaurant, a handcart vehicle and a
  rowing boat; chickens, sheep and cows; the remaining tree, plant, outcrop
  and crop states; 43 more item icons; and part of the interface icon set.
  Animals, handcarts and boats face all eight directions, like agents. How
  mountains generate was then decided in [#628](https://github.com/compoodment/ClankerWorld/issues/628)
  and is recorded in [The world](../world.md#river-crossings-and-visible-forests-and-mountains).
- **Agreed after the third round of the art review (October 1):** computment
  approved 99 pictures; the others were the icon options not chosen, the old
  snow edge shown for comparison, and the Market plaza. Approved: the Market
  as a timber hall with no stalls inside; a Port where six boats moor bow-in,
  three along each side of the pier; chickens, sheep, cows, the horse with and
  without a rider, handcarts and boats drawn in all eight directions; the
  redrawn Hammer, Map, Pencil and Play icons, the Sun and Speech icons kept
  close to today's, the Rain, Snow and Storm icons built on the new cloud, and
  the solid-headed Arrow; three desert cacti, a barrel cactus, a saguaro and a
  prickly pear, standing on some of the desert's cactus cover; and a soft snow
  edge that thins into clumps and frost, shaded like a low drift, instead of
  ending in a flat white band. The plaza was "a bit too big for the eight
  stalls"; in the fourth round computment kept a shorter plaza but wanted
  the open column on each side back, so eight stalls take a 7×4 plaza.
- **Night lights, agreed on October 3:** at night, buildings in use and
  fires warm the ground around them. computment's rules:
  - Roofs never glow. Light comes out of a building's windows and open door
    onto the ground beside its walls. Windows are on the **front and both
    sides, not the back**, for every building with windows, the Farmhouse
    included; a front only one tile wide has room just for the door.
  - A **House is lit only while someone is inside**; nobody keeps a lamp
    burning in an empty house. Work buildings light while someone is inside
    or a job runs there, and the Blacksmith's forge glows in its yard while
    it works. A **Warehouse has no windows**: only a lantern by its loading
    doors, lit while someone fetches or stores goods. A Silo and Market
    stalls stay dark. A Port's lantern on the end of its pier burns every
    night, except in an abandoned Town, where it stays dark until the Town
    is resettled (agreed October 7).
  - Light must **not look circular** and must plainly **come from
    something**: a window, a door, a fire or a lantern fitting that is drawn,
    never a bare bright dot. Each pool has a ragged edge that **moves
    slightly**; fires flicker faster. Light is stepped on the art's pixel grid,
    warms the ground without hiding its texture, and fades in and out with
    dusk and dawn.
  - Every design gets lights, including the Market, Town Hall, Port, Clinic
    and Restaurant designs approved on October 1. Night lights are only for
    looks: night still sets no visibility rule.
- **Street lanterns, agreed on October 3:** agents build street lanterns
  beside Roads; they need **no fuel** and light themselves at dusk. Two
  designs: **B, a round stone lamp with an open flame**, and **C, a lantern
  hung from an arm over the Road** ([Towns](../towns.md#shared-town-projects)).
  **Agreed on October 7:** in an abandoned Town they stay dark until the
  Town is resettled.
- **Agreed after the fourth round of the art review (October 7):**
  - **Animals.** computment approved:
    - a chick, lamb, calf and foal in all eight facings, with no adult gear
      (the calf has no horns, the chick no comb, the foal no saddle);
    - a horse with a bare back for wild and unsaddled horses. A ridden horse
      always shows its saddle.
    - A sheep in a household looks **shorn for the first half of each wool
      cycle** and woolly after that; wild sheep stay woolly.
  - **Animal yard.** Option A: a split-rail pen on trampled earth, with an
    open gate on the door side, a water trough by the gate and hay in the far
    corner.
  - **Item icons.** Ten new icons: iron fittings, refined gold, a plain gold
    ornament, saddle, cooked eggs, and milk, berry and fruit porridge, rich
    meal and leather sack. The crude wooden axe and pickaxe keep the wooden
    axe and pickaxe icons.
  - **Construction sites.** A building under construction shows three
    stages before its finished picture:
    1. a cleared, staked-out site with materials piled beside it;
    2. a footing, sill and posts with the floor half laid;
    3. the walls up and the roof half on from the back, with a ladder.

    Fences, piers, bridges and lanterns follow the same order with their own
    parts.
  - **16 px vehicles and animals.** At mid zoom the handcart (empty,
    loaded and pulled) and the rowing boat (rowing and at rest) use 16 px
    versions halved from their approved drawings, approved in all eight
    facings ([#914](https://github.com/compoodment/ClankerWorld/issues/914)).
    Every animal's 16 px drawing, in all eight facings, is approved too.
  - **Abandoned Towns.** Buildings in an abandoned Town look **neglected**:
    faded, mossy, weedy, with the door boarded. Once the Town has stayed
    abandoned for a **full season** they look **falling apart**, with a hole
    in the roof, fallen planks and a sapling. Fences, piers and bridges lose
    rails or planks instead. A revived Town looks lived in again.

  The pictures and notes are in `tools/ArtPreview/Proposed/Round4.md`.
- **Agreed on October 7: animals step as they walk.** computment chose
  option **B**: each time an animal moves to a new tile, a front hoof shows
  just ahead of its chest on one side and the hind hoof just behind its rump
  on the other, then the pair swaps on the next step, and the tail swings.
  Hens and chicks show one foot behind them at a time and bob their heads. An
  animal that stops stands in its approved standing drawing; legs show only
  while it walks. The rejected option A was a legless head nod and sway. The
  pictures and notes are in `tools/ArtPreview/Proposed/AnimalWalk.md`.
- Art is drawn in code, not stored as image files. computment prefers this.
  Approved art waits in `tools/ArtPreview/Proposed/` until the feature that
  uses it lands.
- Worn everyday, cold or wet clothing **does not change an agent's map sprite
  appearance**. Clothing has item icons and gameplay effects. Armor imagery
  may be prompted as new art later; whether worn armor is shown on agents and
  the exact generation/approval workflow remain open.

### Suggestion

Use a small set of supported visual families for built-in content, then add a
new family when an invention truly needs a new silhouette. Reusing an image
need not mean two creations behave identically.

The earlier suggestions here are settled: built-in art is drawn in code
rather than exported as spritesheets
([#1271](https://github.com/compoodment/ClankerWorld/issues/1271)), the
[style guide](../../development/art-style.md) and its art reviews are the
reference instead of a separate reference scene, and invention pictures use
approved code-drawn parts with no AI images
([#1270](https://github.com/compoodment/ClankerWorld/issues/1270)).

### Still to decide

Still open: quality criteria for the images a mod carries, beyond their
format, metadata and size limits. Animation timing is an implementation detail checked in the
art reviews. The first complete game does **not** require extra cosmetic
variants just for variety.
