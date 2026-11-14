---
title: Terrain and mountains
type: architecture
status: active
updated: 2026-11-14
---

# Terrain and mountains

This page describes terrain layers and mountain generation.

### Terrain layers, sand, groves and hills

`GeneratedWorldMapGenerator` builds a map in four separate, deterministic passes,
so each can be checked on its own
([#461](https://github.com/ClankerWorldOrg/ClankerWorld/issues/461)):

1. **Elevation and water** come from the geography generator.
2. **Surface.** Sand comes from two explicit rules, never from simply touching
   water: *desert* (dry climate, rainfall 42 or less) and *beach* (land below
   elevation 175, beside the ocean, where a seeded beach-noise field reaches
   0.3). Rivers and lakes keep grass banks except where a desert reaches them. *Forest floor* marks groves: forest
   tiles whose seeded grove noise reaches 0.15, keeping only the 24 strongest in
   each 64×64 chunk and none in the 6×5 starting clearing.
3. **Vegetation eligibility.** Cover follows climate and the surface beneath
   it, so a beach carries no forest or grass cover. Dry scrub and cactus cover
   on desert sand follow the dry climate; cacti additionally need hot desert
   sand, never dry scrub, ordinary beaches, water or rock. The client draws
   cactus cover as desert brush and puts a cactus sprite on about one cover
   tile in five, chosen from the tile's position, skipping Roads, bridges,
   doorsteps, fields and buildings (`WorldTerrainLayer.CactusAt`). The cacti
   are decoration only; the server does not track them.
4. **Object placement.** The starter berry patch, tree and grain seed patch must
   stand off sand. Every forest-floor tile then gets a tree. Wild sites come
   next, then rare deposits, scattered trees and orchards. A tree or plant
   picks another tile rather than stand on sand, water or mountain rock, and
   map acceptance refuses one that does. Stone outcrops and clay banks are not
   plants and may stand on sand. Eligible forest-grass tiles have a provisional
   35% tree chance; meadows have a 2% chance. Seeded ranking spreads trees
   through any chunk that reaches its budget instead of filling one edge.

The running world places starter clay on unoccupied buildable dry ground beside
a river or lake, reachable on foot from the accepted Town's Warehouse. Placement
and strict reload use the same bank rule. A rocky mountain shore can be walkable but is
not a valid clay resource site. If no valid bank exists, the runtime records
`starting_resource_blocked` for clay and keeps advancing and saving.

Generated sites use at most 1,016 of a chunk's 1,024 resource slots; the validated
configuration ceiling is 2,048. The remaining eight slots stay
free for sites the running world adds, such as the three settlement sites
beside the first Town. Grove surfaces still use the 24-tile limit; the larger
budget supplies the denser trees on forest grass.

World-systems chunk manifests own immutable resource snapshots, including input
supplied through record replacements. An unchanged manifest reuses successful
structural and digest validation by object identity; new or loaded manifests
are fully checked. Current-world bounds and duplicate chunk coordinates remain
checked on every validation, alongside changing weather and ecology. The weak
validation cache is process data only. See [Saves and replay](../saves-and-replay.md)
for the load boundary and [Chunk validation measurements](../chunk-validation-measurements.md)
for the bounded allocation and timing comparison.

**Hills** are dry land below mountain height (215), at least 190 high and within
the hill reach of a connected mountain region (counting diagonal steps as one).
The reach widens with the region's size: one tile for every 7 in the square
root of its tile count, from 3 tiles up to 8, so a 1,000-tile massif has a
4-tile band and a 2,500-tile massif a 7-tile band. Regions join diagonal
neighbours and wrap east/west only on a wrapped map. Hills are a visual layer
only: nothing is saved for them, they keep their own surface, and they cost the
same to walk and build on as grass. `TerrainPlacementRules.ClassifyHills`
(used by `WorldMap.IsHillAt`) and the Godot client's `UI/Map/HillBand.cs`
(used by `WorldTerrainMap.IsHillAt`) apply the same rule to the saved elevation
and water layers; `MountainMassifTests` checks that they mark exactly the same
tiles on generated maps. The client draws mountains, peaks and hills as one
relief layer from the saved elevation (`UI/Map/ReliefRenderer.cs`): it renders
16×16-tile chunks on worker threads, caches one texture per chunk and atlas
size, and shows the per-tile mountain and hill art for a chunk until its relief
is ready. It also warms hills' overview color and shows "Landform: Hills" in
tile inspection. Hill travel cost and passability are not decided.

The client's `LandscapePalette` applies the approved seasonal colour rule to
grass and canopy ramps from the observed world season. Summer retains the base
art. World load prepares four overview textures and the shared 16 px and 32 px
ground and tree atlases; a season change selects those resources without
rebuilding the map or relief chunks. Transition rims, overview trees and tile
inspection use the same treatment. Fruit, trunks, shadows, rock, water and
permanent snow retain their original colours. Night and regional weather still
draw above the landscape, and winter colour alone does not add snow.

The client retains decoded ground, its palette, the overview atlas and warm
relief when only resources change. Its terrain cache compares the world ID,
dimensions, wrapping and actual packed terrain/layer contents, or a retained
copy of legacy tile facts. A resource-manifest change alone does not rebuild
the ground; tree stages, resource quantities and other live overlays still
refresh. Changed ground or layers rebuild even if digest metadata stays the
same, and a new world context or newly available layers rebuild the map.

All of these numbers are **provisional**. They were chosen from fixed-seed
measurements, not owner-reviewed maps, and live in `TerrainPlacementRules`.
Current fixed-world tests require 25–45% forest-grass tree coverage, and every
forest-floor tile holds a tree. Across 160 default Balanced Small and Medium
worlds (wrapped and bounded), hills cover about 3–8% of dry land.

### Mountain massifs

Mountains form as **a few large massifs** instead of scattered patches
([#683](https://github.com/ClankerWorldOrg/ClankerWorld/issues/683), following the
owner's choice on [#628](https://github.com/ClankerWorldOrg/ClankerWorld/issues/628)).
`MountainMassifs` shapes them after water is classified and before rivers are
routed:

1. **Plan.** Each world picks its massif count from its size: 1–2 on Small and
   2–4 on Medium (3–6, 5–10 and 8–16 on the unplayable larger sizes). It aims
   the massifs at a seeded share of dry land: 7.5–9.5% for Normal mountain
   relief, 2.5–3.5% for Low and 14–17% for High. Each massif gets a seeded
   share of that area, but never less than twice the 150-tile minimum.
2. **Place.** Each massif has a centre, a long axis 2.4–3.6 times its width and
   a slight bend along its length. Placement tries 32 centres, each the most
   inland of three random land tiles, and keeps the one whose outline holds the
   most dry land, at least 60%. Massifs stay at least three tiles plus their
   rough edge apart, so two never merge. A massif grows to make up for water
   inside its outline. Where the land is too broken up for the planned size, a
   half-size massif is tried before the massif is dropped.
3. **Shape.** Inside its roughened outline a massif rises as a stretched dome
   from 215 at the edge. Its **crest**, a thin band along the central 60% of
   its length, holds the peaks (245 and up). A peak stays only if its own
   massif surrounds it on all eight sides, so peaks never touch water, lowland
   or the map's edge.
4. **Flatten.** Each massif keeps only its largest connected piece; a piece cut
   off by a coast or lake is lowered back to the ground around it. A massif
   below 150 tiles is flattened completely. Elevation noise outside the massifs
   that reaches 205 is squeezed into 205–214, just below mountain height, so it
   never forms a stray mountain but can still hold stone.
5. **Foothills.** Dry land around each massif rises to at least 190 out to its
   hill reach, then eases back down over two more tiles, so the hill band
   always shows and widens with the massif.

Rivers never form on mountain tiles: they rise at the foot of a massif rather
than cutting it apart. After rivers, a final pass checks that peaks never split
any land: within each piece of dry land, the ground that is not a peak must be
one walkable piece. If peaks enclose some ground, the fewest peaks needed to
reconnect it are lowered to mountain height. Temperature and climate are worked
out last, from the finished elevation, so massifs and their foothills are
cooler than the land around them.

The first Town's starting clearing always has a stone outcrop it can walk to
within 32 tiles, counting diagonal steps as one. If none formed there, one is
placed on the highest ground in reach, outside the clearing. This is in
addition to the stone site the running world adds beside the first Town. Iron,
gold and diamond outcrops still need mountain tiles, so they now gather in the
massifs.


Earlier terrain measurements and comparisons are in [How the game works history](../../archive/how-it-works-history.md).
