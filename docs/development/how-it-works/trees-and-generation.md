---
title: Trees and generation controls
type: architecture
status: active
updated: 2026-11-14
---

# Trees and generation controls

This page describes trees, planting and world generation controls.

## Trees and planting

Each tree is one map resource with one saved growth record
(`EcologyResource`). Growth, harvest, tile inspection and map art all read that
same record: `TreeGrowthRules.StageOf` turns it into the stage the server sends
as `TreeStage`, and the client draws whatever the server sends. Every tree number
lives in `TreeGrowthRules` and is provisional ([#462](https://github.com/ClankerWorldOrg/ClankerWorld/issues/462)).

- **Wood trees** are `sapling`, `mature` or `stump`. Felling a mature tree gives
  wood and one `tree_seed`; the stump regrows in spring. One tree-seed item
  serves broadleaf and conifer.
- **Planting** is the typed `PlantTree` action. It checks, in order, the
  species (broadleaf, conifer or orchard), that
  the planter is an adult, that the seed lot is the matching seed they own with one
  available for planting, the ground (grass, forest floor or fertile soil; never water, sand,
  rock, snow or dry scrub), buildings, Roads and existing objects, that the
  planter stands on or next to the tile, and the chunk's resource budget. A
  refusal returns a `TreePlantingRefusal` and a one-line reason and changes
  nothing. Success consumes exactly one seed and adds a `planted-tree-{x}-{y}`
  map resource with a sapling growth record, in the same tick.
- **Agents** are offered `plant_tree` while they hold a tree seed, or can collect
  one from accessible household stock with free carrying space. A carried seed
  remains usable with full cargo; the same collection guard covers `replant_tree`.
  The built-in
  site is the nearest reachable open tile outside every Town border, so trees
  do not block building sites. The species follows the nearest wood tree.
  `replant_tree` also uses a tree seed.
  Replanting selects stumps reachable from the acting agent, including on
  disconnected islands, and skips stumps with no unoccupied route into reach.
- **Orchard trees** are `sapling` immediately after planting, then `growing`,
  `fruiting` or `picked`. Fruit is seasonal in
  `EcologyRules`: it ripens only in the tree's recorded season (autumn for new
  worlds) and falls when that season ends. New worlds start in spring, so
  orchards start without fruit.
- **Ecology batches** validate the current configuration, resource bounds,
  unique IDs and every resource before regeneration. They then share the
  standalone regeneration calculation without repeating configuration checks
  for each resource. No validation result is retained across batches: changed
  profile lists and newly loaded inputs are checked again. Standalone
  `EcologyRules.Regenerate` still validates its own configuration, and each
  resource is checked again during regeneration as before.
- **Owner orders** accept bounded tree counts, repeat-until-cancelled work,
  optional broadleaf/conifer/orchard species and an exact planting tile. The
  parser, observer guidance and strict saved-order validation use the same
  actions. Every step checks the live site, seed access and route. An adult
  physically collects personal stored seeds or permitted shared stock before
  planting through `PlantTreeCore`. Exact sites stay outside Town borders and
  never fall back to a different tile. Only a successful sapling creation
  credits the `tree:plant:planted-tree-{x}-{y}` receipt; pickup, movement and
  refusal leave progress unchanged. Queues, cancellation, survival interrupts
  and continuation use the existing owner-order lifecycle. No new checkpoint
  field or growth rule is introduced.
- **Saves.** Planted trees are part of the saved map. On load, the map must
  still match regeneration apart from the settlement's staged sites and valid
  planted trees; each planted tree must be a plantable species on legal ground,
  off Roads and buildings, with its growth record. See
  [saves and replay](../saves-and-replay/formats-and-maps.md#current-formats-and-older-worlds).
- **Art.** `UI/Graphics/TreeArtManifest.cs` in the client is the one list of
  tree art: species, stage, asset ID, sprite, source, licence and review
  status. The map reads its sprites and stage names from it. Broadleaf and
  conifer mature, sapling and stump sprites and the three established orchard
  sprites are approved art from the October 1 review. Orchard saplings reuse
  the approved growing sprite with a separate terrain code, preserving the
  server's saved stage; the tree-seed item has no art yet. The
  [pixel-art style guide](../art-style.md) explains how art is reviewed.
- **Logs.** The server logs `tree_planting` outcomes (planted, refused,
  replanted, seed collected) with the agent ID and a bounded detail.

## Advanced generation controls

New World defaults to 50% water with a 20–80% range. `GenerationAmount`
controls forest cover, mountain relief and river abundance independently;
Normal is zero and omitted from saved JSON. Low/High use forest rainfall
thresholds 175/125 and river catchment thresholds 288/72. Mountain relief sets
how much dry land the [massifs](terrain-and-mountains.md#mountain-massifs) aim to cover: Low 2.5–3.5%,
Normal 7.5–9.5% and High 14–17%, for every climate mode and size. Outside the
visibility trial, Normal keeps a 150 rainfall threshold and river threshold 144.
Resource abundance retains its existing Sparse/Normal/Abundant saved values;
the UI labels them Low/Normal/High.

For Balanced Small/Medium worlds, each feature's target applies only while its
own control is Normal. The versioned Normal trial uses a 135 rainfall threshold.
Low and High remain separate controls. `GeographyCandidateSelector` tries at
most three candidates derived from the requested seed. It selects by unmet
target count, normalized distance from the 20–40% forest and 5–12% mountain
dry-land bands, then the largest connected forest region's share as a
tie-break; attempt number is the final stable tie-break. Mountains no longer
take part in the tie-break: they are already whole massifs, and preferring one
large mountain region would always pick the world with the fewest massifs.
Connected regions use diagonal neighbors, east/west wrapping when enabled, and
no north/south wrapping. Incompatible climate modes have no trial target and
use one candidate. The forest tie-break has no minimum region-size threshold.
Coverage is measured and returned for all settings.
An attempt with
no suitable starting clearing is recorded as failed and omitted from coverage
measurement and ranking; other generated-map validation errors still propagate.
Successful attempts keep their original numbers, and selection continues
through the whole bounded set. If no map remains, preview and creation refuse
without replacing the current world.

Owner world-creation signing uses payload v3 to bind all settings and, for
Create, the candidate attempt, terrain and map-layer digests, and explicit
acceptance of unmet trial targets. Preview reports the selected candidate and
coverage for generated candidates and a separate transient `FailedCandidates`
list with each unavailable attempt and its bounded `no-clearing` reason. Godot
shows these together in attempt order without inventing zero coverage for a
failed map. Older previews that omit the list remain readable. An all-failed
HTTP refusal carries a recognized generation code which the client maps to a
fixed, useful message instead of displaying arbitrary server error text.
Create reruns the bounded selector, checks those signed identities, and builds
the world from its selected map. It refuses a changed preview or an unaccepted
miss. The selected attempt is saved in
`GeographyOptions` with the visibility algorithm version and in the map
manifest; restore regenerates that attempt strictly without searching again.
The transient reports do not change signed creation or saved map authority.
Terrain version 2 adds mountain massifs and applies to every generated world,
whatever its climate mode. Any other saved version is refused rather than
replayed with different terrain rules (see
[saves and replay](../saves-and-replay/formats-and-maps.md#current-formats-and-older-worlds)). Small
and Medium remain the only playable sizes; no continent-count control is exposed
for them. Existing saved water settings are
not rewritten.
