---
title: How the game works history
type: architecture
status: history
updated: 2026-11-14
---

# How the game works history

This page preserves earlier terrain measurements and comparisons.

Measured over 40 seeds for each of the four default Balanced Small/Medium
settings (50% water, wrapped and bounded), the New World selection always met
both trial targets, compared with 56 misses out of 160 before. Mountains covered
7.2–10.5% of dry land, each world had a massif count within its range, the
smallest massif had 443 tiles and peaks were 10–14% of mountain tiles. The
nearest stone was at most 33 tiles from the starting berry patch. Before this
change the same worlds had 1–65 separate mountain patches, the smallest of a
single tile, and mountains covered 1.4–19% of dry land.

Generation takes about a tenth longer. In four interleaved runs of
`scripts/measure-map-generation.sh` on a shared machine under load, the median
time to generate `probe-a` went from 191 to 211 ms, `probe-b` from 140 to
153 ms and the Dry fixture from 76 to 85 ms (averages of the per-run medians).
These are noisy measurements, not a performance promise.

The October 1 measurements used .NET 10.0.401 and Godot 4.7.2 under WSL, with
two .NET processors. Both Small maps are 256×128, use current hydrology and
the Balanced visibility rules, and were measured before and after this change:

| Seed | Resources before → after | Most resources in a chunk before → after | Forest-grass tiles with trees before → after | Ocean shore with sand before → after | Median generation ms before → after |
| --- | --- | --- | --- | --- | --- |
| `probe-a` | 391 → 1,748 | 56 → 420 | 76/3,913 (1.9%) → 1,377/3,949 (34.9%) | 195/851 (22.9%) → 73/851 (8.6%) | 119.5 → 207.8 |
| `probe-b` | 364 → 1,969 | 56 → 558 | 87/4,500 (1.9%) → 1,636/4,586 (35.7%) | 246/1,226 (20.1%) → 90/1,226 (7.3%) | 146.5 → 116.5 |

Generation used one warmup and five timed runs, reporting the median. Times
include shared-machine scheduling noise; the faster second map does not prove
a generation speedup. The separate uniform-Dry fixture had 39 cactus tiles,
all on desert sand, compared with 48 before, nine outside desert sand.

Godot measurements execute the actual terrain layer's `_Draw` and time CPU
draw-command submission. Each camera uses five warmups and 20 measured draws:

| Seed | Full overview median ms before → after | Detail median ms before → after |
| --- | --- | --- |
| `probe-a` | 3.61 → 6.26 | 6.73 → 6.07 |
| `probe-b` | 3.14 → 6.65 | 8.56 → 8.51 |

The overview covers all 32,768 tiles at size 2; detail covers 96×48 tiles at
size 16. These headless measurements exclude GPU work and frame presentation;
they do not establish Windows frame rates or a hands-on playtest. Denser
resources increased overview CPU cost in this sample.

To repeat generation measurements and write public map fixtures:

```bash
bash scripts/measure-map-generation.sh /tmp/clankerworld-map-measurements current
godot --headless --path src/ClankerWorld.GodotClient -- --measure-map-draw /tmp/clankerworld-map-measurements/current-probe-a.json
```

Use the pinned engine and build the client scripts first. The draw probe exits
with an error if the engine never calls `_Draw`. It does not start a server or
use private saves. Across a separate four-seed, 160-day episode sample, wet
time fell to 78.1% of the previous Tropical level, 70.9% Dry, 75.4% Temperate,
73.3% Cold and 75.7% Polar. Episode durations and neighboring weather explain
why time spent wet is not exactly the same as the base-weight reduction.
`TerrainPlacementTests` checks that map acceptance rejects vegetation on sand
and generated resources leave space in each chunk for later sites. The script
and Godot probe above report generation and draw timings.

The [terrain comparison](../development/assets/terrain-placement-comparison.png) shows a
90×50-tile area of seed `river-world-a` (Small, 50% water, wrapped): the
previous generator on the left, these rules on the right. Dark green is forest
grass, the darker patches with dots are groves, beige is sand, brown is dry
scrub, grey is mountain and the warm band around it is hills. Dark dots are
trees, red dots plants and grey dots stone. It is a generator-layer rendering, not a Godot
screenshot or native playtest.
