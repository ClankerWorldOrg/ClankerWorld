using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// Projects generated 2D geography into the current physical-map contract and
/// places the ordinary empty starting site on a connected buildable patch.
/// Climate is retained independently from its provisional ground appearance.
/// </summary>
public static class GeneratedWorldMapGenerator
{
    private const int StartingSiteWidth = 6;
    private const int StartingSiteHeight = 5;

    public static WorldMap Generate(GeographyOptions options, bool includeLegacyBedroll = false)
        => GenerateCore(options, includeLegacyBedroll);

    private static WorldMap GenerateCore(GeographyOptions options, bool includeLegacyBedroll)
    {
        ArgumentNullException.ThrowIfNull(options);
        // This bridge still allocates one object per tile for the old
        // physical-map contract. The compact geography source supports all
        // presets; larger playable maps need a compact save/projection first.
        if (options.Size > WorldSizePreset.Medium)
            throw new NotSupportedException("Large generated worlds require the compact playable-map contract.");
        var geography = GeographyGenerator.Generate(options);
        var width = geography.Width;
        var height = geography.Height;
        var tiles = new TerrainTile[checked(width * height)];
        var climateZones = new byte[tiles.Length];
        var elevationLevels = new byte[tiles.Length];
        var hydrologyKinds = new byte[tiles.Length];
        var surfaceKinds = new byte[tiles.Length];
        var vegetationKinds = new byte[tiles.Length];
        var forestPotential = new bool[tiles.Length];
        var trialBalancedVisibility = options.BalancedVisibilityVersion == GeographyGenerator.CurrentBalancedVisibilityVersion &&
            options.Size is WorldSizePreset.Small or WorldSizePreset.Medium &&
            options.ClimateMode == ClimateMode.Balanced;
        var forestRainfall = options.ForestCover switch
        {
            GenerationAmount.Low => 175,
            GenerationAmount.High => 125,
            GenerationAmount.Normal when trialBalancedVisibility => 135,
            _ => 150,
        };
        // Pass 1, elevation and water, comes from the geography. The flattened
        // terrain summary and the forest potential are read from it directly.
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var tile = geography.At(x, y);
                var forested = tile.Rainfall >= forestRainfall && tile.Climate is ClimateZone.Tropical or ClimateZone.Temperate;
                var kind = tile.Water switch
                {
                    WaterKind.Ocean => TerrainKind.Ocean,
                    WaterKind.Lake => TerrainKind.Lake,
                    WaterKind.River => TerrainKind.River,
                    _ when tile.Elevation >= WorldMap.PeakElevationThreshold => TerrainKind.Peak,
                    _ when tile.Elevation >= WorldMap.MountainElevationThreshold => TerrainKind.Mountain,
                    _ when tile.Climate is ClimateZone.Polar or ClimateZone.Cold => TerrainKind.Snow,
                    _ when tile.Climate == ClimateZone.Dry => TerrainKind.Sand,
                    _ when forested => TerrainKind.Forest,
                    _ => TerrainKind.Meadow,
                };
                var index = y * width + x;
                climateZones[index] = (byte)tile.Climate;
                elevationLevels[index] = tile.Elevation;
                hydrologyKinds[index] = (byte)tile.Water;
                forestPotential[index] = kind == TerrainKind.Forest;
                tiles[index] = new TerrainTile(new GridPoint(x, y), kind);
            }

        // Pass 2, surface: sand comes only from the explicit desert and beach
        // rules, never from simply touching a river or lake.
        ClassifySurfaces(options, geography, surfaceKinds, width, height);
        var template = StartingSiteMapGenerator.Generate(options.Seed, includeLegacyBedroll);
        var origin = FindStartingSiteOrigin(hydrologyKinds, elevationLevels, surfaceKinds, width, height,
            template.Resources.Select(item => item.Position).ToArray());
        // Forest floor marks the groves that object placement fills with trees,
        // outside the starting clearing so the first Town keeps open ground.
        MarkForestGroves(options, surfaceKinds, forestPotential, origin, width, height);
        // Pass 3, vegetation eligibility: cover follows climate and the surface
        // beneath it, so an open beach carries no forest or grass cover.
        ClassifyVegetation(geography, surfaceKinds, forestPotential, vegetationKinds, width, height);

        // Pass 4, object placement, checks every tree and plant against the
        // surface and vegetation chosen above.
        var objects = Array.Empty<MapObject>();
        var resources = template.Resources.Select(item => item with
        {
            Position = new GridPoint(item.Position.X + origin.X, item.Position.Y + origin.Y),
            IsRenewable = item.Id == "timber-tree" || item.IsRenewable,
            TreeKind = item.Id == "timber-tree" ? "broadleaf" : item.TreeKind,
            NaturalObjectKind = item.Id switch
            {
                "berry-patch" => "berry_bush",
                "grain-seed-patch" => "wild_seed_patch",
                "wild-greens-patch" => "wild_greens",
                "medicinal-herb-patch" => "medicinal_herb_patch",
                _ => null,
            },
        }).ToArray();
        // Grove trees come straight after the starter sites so every
        // forest-floor tile gets its tree before other sites use the budget.
        List<MapResource> groves = PlaceGroveTrees(options, geography, surfaceKinds,
            width, objects, resources);
        var placed = resources.Concat(groves).ToArray();
        var distributed = GenerateResourceSites(options, geography, hydrologyKinds, elevationLevels,
            surfaceKinds, vegetationKinds, objects, placed);
        // Rare deposits come before the scattered filler trees so that a
        // forested chunk cannot crowd out the map's only iron or gold.
        List<MapResource> geology = GenerateGeologySites(options, geography, objects,
            placed.Concat(distributed).ToArray());
        // Mountains gather into a few massifs, so make sure the starting
        // clearing still has stone it can walk to.
        geology.AddRange(EnsureStoneNearStart(geography, origin, objects,
            placed.Concat(distributed).Concat(geology).ToArray()));
        List<MapResource> trees = GenerateTrees(options, geography, surfaceKinds, vegetationKinds,
            width, height, objects, placed.Concat(distributed).Concat(geology).ToArray());
        List<MapResource> orchards = GenerateOrchards(options, geography, surfaceKinds, vegetationKinds, width, height, objects,
            placed.Concat(distributed).Concat(geology).Concat(trees).ToArray());
        var allResources = placed.Concat(distributed).Concat(geology).Concat(trees).Concat(orchards)
            .ToArray();
        var withoutDigest = new WorldMap(width, height, 0, tiles, objects, allResources, string.Empty)
        {
            ClimateZones = climateZones,
            ElevationLevels = elevationLevels,
            HydrologyKinds = hydrologyKinds,
            SurfaceKinds = surfaceKinds,
            VegetationKinds = vegetationKinds,
            WrapsEastWest = options.WrapEastWest,
        };
        var map = withoutDigest with { ManifestDigest = MapManifestCodec.Digest(withoutDigest) };
        var validation = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Generated world is invalid: {validation.Failure}");
        return map;
    }

    private static void ClassifySurfaces(GeographyOptions options, GeneratedGeography geography, byte[] surfaces,
        int width, int height)
    {
        // Beaches form in stretches along the ocean rather than outlining
        // every shore. Rivers and lakes keep grass banks.
        var beach = GeographyGenerator.LayerNoise(options.Seed, "beach", TerrainPlacementRules.BeachNoiseFrequency,
            width, options.WrapEastWest);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var tile = geography.At(x, y);
                surfaces[y * width + x] = (byte)(
                    tile.Water != WaterKind.Land ? SurfaceKind.Water :
                    tile.Elevation >= WorldMap.MountainElevationThreshold ? SurfaceKind.Rock :
                    tile.Climate is ClimateZone.Polar or ClimateZone.Cold ? SurfaceKind.Snow :
                    tile.Climate == ClimateZone.Dry && tile.Rainfall <= TerrainPlacementRules.DesertMaximumRainfall
                        ? SurfaceKind.Sand :
                    tile.Elevation < TerrainPlacementRules.BeachMaximumElevation &&
                        HasCardinalNeighbor(geography, x, y, options.WrapEastWest, water => water == WaterKind.Ocean) &&
                        beach(x, y) >= TerrainPlacementRules.BeachNoiseThreshold ? SurfaceKind.Sand :
                    tile.Climate == ClimateZone.Dry ? SurfaceKind.DryScrub :
                    // Forest floor is marked later, only where a grove's trees
                    // will stand. FertileSoil is assigned only at a saved
                    // fertile-land site; rainfall is not a fertility model.
                    SurfaceKind.Grass);
            }
    }

    private static void MarkForestGroves(GeographyOptions options, byte[] surfaces, bool[] forestPotential,
        GridPoint startingSite, int width, int height)
    {
        var grove = GeographyGenerator.LayerNoise(options.Seed, "forest-grove", TerrainPlacementRules.GroveNoiseFrequency,
            width, options.WrapEastWest);
        var columns = (width + GeographyGenerator.ChunkSize - 1) / GeographyGenerator.ChunkSize;
        var rows = (height + GeographyGenerator.ChunkSize - 1) / GeographyGenerator.ChunkSize;
        var candidates = new List<(float Score, int Index)>[columns * rows];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (!forestPotential[index] || surfaces[index] != (byte)SurfaceKind.Grass ||
                    x >= startingSite.X && x < startingSite.X + StartingSiteWidth && y >= startingSite.Y && y < startingSite.Y + StartingSiteHeight)
                    continue;
                var score = grove(x, y);
                if (score < TerrainPlacementRules.GroveNoiseThreshold) continue;
                var chunk = y / GeographyGenerator.ChunkSize * columns + x / GeographyGenerator.ChunkSize;
                (candidates[chunk] ??= []).Add((score, index));
            }
        // The strongest grove cores in each chunk become forest floor, capped
        // so that each one can hold a tree within the chunk resource budget.
        foreach (var chunk in candidates)
            if (chunk is not null)
                foreach (var (_, index) in chunk.OrderByDescending(item => item.Score).ThenBy(item => item.Index)
                             .Take(TerrainPlacementRules.MaximumForestFloorTilesPerChunk))
                    surfaces[index] = (byte)SurfaceKind.ForestFloor;
    }

    private static void ClassifyVegetation(GeneratedGeography geography, byte[] surfaces, bool[] forestPotential,
        byte[] vegetation, int width, int height)
    {
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var tile = geography.At(x, y);
                vegetation[index] = (byte)(
                    tile.Water != WaterKind.Land || tile.Elevation >= WorldMap.MountainElevationThreshold
                        ? VegetationCover.None :
                    tile.Climate == ClimateZone.Dry && surfaces[index] == (byte)SurfaceKind.Sand &&
                        tile.Rainfall <= TerrainPlacementRules.DesertMaximumRainfall && tile.Temperature >= 92
                        ? VegetationCover.Cactus :
                    tile.Climate == ClimateZone.Dry ? VegetationCover.Scrub :
                    tile.Climate is ClimateZone.Polar or ClimateZone.Cold ? VegetationCover.Tundra :
                    // An open beach is bare sand, not a strip of forest or meadow.
                    !TerrainPlacementRules.CanHoldOrdinaryVegetation((SurfaceKind)surfaces[index])
                        ? VegetationCover.None :
                    forestPotential[index] ? VegetationCover.Forest : VegetationCover.Grass);
            }
    }

    private static List<MapResource> PlaceGroveTrees(GeographyOptions options, GeneratedGeography geography,
        byte[] surfaceKinds, int width, IReadOnlyList<MapObject> startingSite, IReadOnlyList<MapResource> existing)
    {
        var occupied = startingSite.Select(item => item.Position)
            .Concat(existing.Select(item => item.Position)).ToHashSet();
        var trees = new List<MapResource>();
        for (var index = 0; index < surfaceKinds.Length; index++)
        {
            if (surfaceKinds[index] != (byte)SurfaceKind.ForestFloor) continue;
            var position = new GridPoint(index % width, index / width);
            if (occupied.Contains(position)) continue;
            // Temperate groves mix in some conifers, as scattered trees do.
            var random = Pcg32XshRrV1.Create(options.Seed, $"grove-tree:{position.X},{position.Y}");
            var treeKind = geography.At(position.X, position.Y).Climate == ClimateZone.Temperate &&
                random.NextUInt() % 3 == 0 ? "conifer" : "broadleaf";
            trees.Add(new MapResource($"grove-tree-{position.X}-{position.Y}", "construction", position, true, treeKind));
            occupied.Add(position);
        }
        return trees;
    }

    private static bool IsAdjacentToWater(GeneratedGeography geography, int x, int y, bool wrap) =>
        HasCardinalNeighbor(geography, x, y, wrap, water => water != WaterKind.Land);

    private static bool HasCardinalNeighbor(GeneratedGeography geography, int x, int y, bool wrap,
        Func<WaterKind, bool> matches)
    {
        foreach (var (dx, dy) in new (int X, int Y)[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
        {
            var nextX = x + dx;
            var nextY = y + dy;
            if (wrap) nextX = (nextX % geography.Width + geography.Width) % geography.Width;
            if (nextX >= 0 && nextX < geography.Width && nextY >= 0 && nextY < geography.Height &&
                matches(geography.At(nextX, nextY).Water))
                return true;
        }
        return false;
    }

    private static List<MapResource> GenerateResourceSites(GeographyOptions options, GeneratedGeography geography,
        byte[] hydrologyKinds, byte[] elevationLevels, byte[] surfaceKinds, byte[] vegetationKinds,
        IReadOnlyList<MapObject> startingSite, IReadOnlyList<MapResource> existing)
    {
        const int spacing = 16;
        var width = geography.Width;
        var height = geography.Height;
        var occupied = startingSite.Select(item => item.Position)
            .Concat(existing.Select(item => item.Position)).ToHashSet();
        var perChunk = existing.GroupBy(item =>
                (item.Position.X / GeographyGenerator.ChunkSize, item.Position.Y / GeographyGenerator.ChunkSize))
            .ToDictionary(group => group.Key, group => group.Count());
        var sites = new List<MapResource>();
        for (var top = 0; top < height; top += spacing)
            for (var left = 0; left < width; left += spacing)
            {
                var sitesInCell = options.ResourceAbundance switch
                {
                    ResourceAbundance.Sparse => (left / spacing + top / spacing) % 2 == 0 ? 1 : 0,
                    ResourceAbundance.Normal => 1,
                    ResourceAbundance.Abundant => 2,
                    _ => throw new ArgumentOutOfRangeException(nameof(options)),
                };
                var random = Pcg32XshRrV1.Create(options.Seed, $"resource-site:{left},{top}");
                var chunk = (left / GeographyGenerator.ChunkSize, top / GeographyGenerator.ChunkSize);
                // A few candidates let coast and mountains leave some cells
                // empty without ever placing a site on water or high ground.
                for (var site = 0; site < sitesInCell &&
                     perChunk.GetValueOrDefault(chunk) < TerrainPlacementRules.GeneratedResourcesPerChunk; site++)
                {
                    for (var attempt = 0; attempt < 12; attempt++)
                    {
                        var x = left + (int)(random.NextUInt() % (uint)Math.Min(spacing, width - left));
                        var y = top + (int)(random.NextUInt() % (uint)Math.Min(spacing, height - top));
                        var position = new GridPoint(x, y);
                        var index = y * width + x;
                        var surface = (SurfaceKind)surfaceKinds[index];
                        var vegetation = (VegetationCover)vegetationKinds[index];
                        if (!IsGeneratedBuildable(hydrologyKinds[index], elevationLevels[index], surface) ||
                            occupied.Contains(position)) continue;
                        var selection = random.NextUInt() % 4;
                        var tile = geography.At(x, y);
                        // The kind of site follows the climate; dry and cold
                        // lands lean towards stone.
                        var resourceKind = vegetation == VegetationCover.Forest ? "construction" : tile.Climate switch
                        {
                            ClimateZone.Dry => selection == 0 ? "fiber" : "stone",
                            ClimateZone.Polar or ClimateZone.Cold => selection == 0 ? "food" : "stone",
                            _ => selection switch
                            {
                                0 => "grain_seed",
                                1 => "food",
                                2 => "fiber",
                                _ => "cultivated_green_seed",
                            },
                        };
                        // Everything but a stone outcrop grows from the soil,
                        // so it looks for another tile instead of standing on sand.
                        if (resourceKind != "stone" && !TerrainPlacementRules.CanHoldOrdinaryVegetation(surface))
                            continue;
                        var renewable = resourceKind is "construction" or "food" or "fiber" or "grain_seed" or "cultivated_green_seed";
                        var id = site == 0 ? $"wild-{left}-{top}" : $"wild-{left}-{top}-{site}";
                        sites.Add(new MapResource(id, resourceKind, position, renewable,
                            resourceKind == "construction" ? TreeKindFor(tile.Climate) : null,
                            NaturalObjectFor(resourceKind, tile, x, y, geography, options.WrapEastWest)));
                        occupied.Add(position);
                        perChunk[chunk] = perChunk.GetValueOrDefault(chunk) + 1;
                        break;
                    }
                }
            }
        return sites;
    }

    private static List<MapResource> GenerateTrees(GeographyOptions options, GeneratedGeography geography,
        byte[] surfaceKinds, byte[] vegetationKinds,
        int width, int height, IReadOnlyList<MapObject> startingSite, IReadOnlyList<MapResource> existing)
    {
        // Scattered, individually harvestable trees on forest grass and
        // meadows share the chunk resource budget with the grove trees placed
        // earlier. They are objects, not a second meaning of forest ground.
        var occupied = startingSite.Select(item => item.Position)
            .Concat(existing.Select(item => item.Position)).ToHashSet();
        var perChunk = existing.GroupBy(item =>
                (item.Position.X / GeographyGenerator.ChunkSize,
                    item.Position.Y / GeographyGenerator.ChunkSize))
            .ToDictionary(group => group.Key, group => group.Count());
        var candidates = new List<(GridPoint Position, uint Score, string Kind)>();
        var trees = new List<MapResource>();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var position = new GridPoint(x, y);
                var index = y * width + x;
                var vegetation = (VegetationCover)vegetationKinds[index];
                if (occupied.Contains(position) || surfaceKinds[index] != (byte)SurfaceKind.Grass ||
                    vegetation is not (VegetationCover.Forest or VegetationCover.Grass))
                    continue;
                var random = Pcg32XshRrV1.Create(options.Seed, $"tree:{x},{y}");
                var chance = vegetation == VegetationCover.Forest
                    ? TerrainPlacementRules.ForestGrassTreePercent : TerrainPlacementRules.MeadowTreePercent;
                if (random.NextUInt() % 100 >= chance) continue;
                var climate = geography.At(x, y).Climate;
                var treeKind = climate == ClimateZone.Cold ||
                    (climate == ClimateZone.Temperate && random.NextUInt() % 3 == 0) ? "conifer" : "broadleaf";
                candidates.Add((position, random.NextUInt(), treeKind));
            }
        // If an unusually dense chunk reaches its bound, seeded ranking keeps
        // the remaining trees spread through it rather than filling its north edge.
        foreach (var candidate in candidates.OrderBy(item => item.Score).ThenBy(item => item.Position.Y).ThenBy(item => item.Position.X))
        {
            var position = candidate.Position;
            var chunk = (position.X / GeographyGenerator.ChunkSize, position.Y / GeographyGenerator.ChunkSize);
            if (perChunk.GetValueOrDefault(chunk) >= TerrainPlacementRules.GeneratedResourcesPerChunk) continue;
            trees.Add(new MapResource($"tree-{position.X}-{position.Y}", "construction", position, true, candidate.Kind));
            perChunk[chunk] = perChunk.GetValueOrDefault(chunk) + 1;
        }
        return trees;
    }

    private static string TreeKindFor(ClimateZone climate) =>
        climate == ClimateZone.Cold ? "conifer" : "broadleaf";

    private static string? NaturalObjectFor(string resourceKind, GeographyTile tile, int x, int y,
        GeneratedGeography geography, bool wrap) => resourceKind switch
        {
            "food" => (unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ tile.Rainfall) & 1) == 0
                ? "berry_bush" : "wild_greens",
            "fiber" => IsAdjacentToWater(geography, x, y, wrap) ? "reeds" : "fiber_plant",
            "stone" => "stone_outcrop",
            "grain_seed" or "cultivated_green_seed" => "wild_seed_patch",
            _ => null,
        };

    private static List<MapResource> GenerateOrchards(GeographyOptions options, GeneratedGeography geography,
        byte[] surfaceKinds, byte[] vegetationKinds, int width, int height, IReadOnlyList<MapObject> startingSite,
        IReadOnlyList<MapResource> existing)
    {
        // One generic fruit tree per suitable chunk, after woodland trees have
        // been placed. Species, density and seasonality are not final content.
        var occupied = startingSite.Select(item => item.Position)
            .Concat(existing.Select(item => item.Position)).ToHashSet();
        var perChunk = existing.GroupBy(item =>
                (item.Position.X / GeographyGenerator.ChunkSize,
                    item.Position.Y / GeographyGenerator.ChunkSize))
            .ToDictionary(group => group.Key, group => group.Count());
        var orchards = new List<MapResource>();
        for (var top = 0; top < height; top += GeographyGenerator.ChunkSize)
            for (var left = 0; left < width; left += GeographyGenerator.ChunkSize)
            {
                var chunk = (left / GeographyGenerator.ChunkSize, top / GeographyGenerator.ChunkSize);
                if (perChunk.GetValueOrDefault(chunk) >= TerrainPlacementRules.GeneratedResourcesPerChunk)
                    continue;
                var random = Pcg32XshRrV1.Create(options.Seed, $"orchard:{left},{top}");
                for (var attempt = 0; attempt < 96; attempt++)
                {
                    var x = left + (int)(random.NextUInt() %
                        (uint)Math.Min(GeographyGenerator.ChunkSize, width - left));
                    var y = top + (int)(random.NextUInt() %
                        (uint)Math.Min(GeographyGenerator.ChunkSize, height - top));
                    var position = new GridPoint(x, y);
                    var index = y * width + x;
                    if (surfaceKinds[index] != (byte)SurfaceKind.Grass ||
                        vegetationKinds[index] != (byte)VegetationCover.Grass || occupied.Contains(position) ||
                        geography.At(x, y).Climate is not (ClimateZone.Temperate or ClimateZone.Tropical))
                        continue;
                    orchards.Add(new MapResource($"orchard-{left}-{top}", "fruit", position, true, "orchard"));
                    occupied.Add(position);
                    break;
                }
            }
        return orchards;
    }

    private static List<MapResource> GenerateGeologySites(GeographyOptions options,
        GeneratedGeography geography, IReadOnlyList<MapObject> startingSite, IReadOnlyList<MapResource> existing)
    {
        var columns = (geography.Width + GeographyGenerator.ChunkSize - 1) / GeographyGenerator.ChunkSize;
        var rows = (geography.Height + GeographyGenerator.ChunkSize - 1) / GeographyGenerator.ChunkSize;
        var chunkCount = columns * rows;
        var occupied = startingSite.Select(item => item.Position)
            .Concat(existing.Select(item => item.Position)).ToHashSet();
        var resourcesByChunk = existing.GroupBy(item =>
                (item.Position.X / GeographyGenerator.ChunkSize, item.Position.Y / GeographyGenerator.ChunkSize))
            .ToDictionary(group => group.Key, group => group.Count());
        var additions = new List<MapResource>();
        var count = options.ResourceAbundance == ResourceAbundance.Abundant ? 2 : 1;

        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var chunkX = chunkIndex % columns;
            var chunkY = chunkIndex / columns;
            if (options.ResourceAbundance == ResourceAbundance.Sparse && (chunkX + chunkY) % 2 != 0)
                continue;
            AddGeologySite("stone_outcrop", "stone", (tile, x, y) =>
                    tile.Water == WaterKind.Land && tile.Elevation is >= 205 and < WorldMap.PeakElevationThreshold,
                chunkX, chunkY, chunkIndex);
        }

        foreach (var (naturalKind, resourceKind) in new[]
        {
            ("iron_outcrop", "iron_ore"),
            ("gold_outcrop", "gold_ore"),
            ("diamond_outcrop", "diamond"),
        })
        {
            var random = Pcg32XshRrV1.Create(options.Seed, "natural-deposit-order:" + naturalKind);
            var startChunk = (int)(random.NextUInt() % (uint)chunkCount);
            for (var site = 0; site < count; site++)
            {
                for (var offset = 0; offset < chunkCount; offset++)
                {
                    var chunkIndex = (startChunk + offset + site) % chunkCount;
                    var chunkX = chunkIndex % columns;
                    var chunkY = chunkIndex / columns;
                    if (TryGeologySite(naturalKind, resourceKind,
                        (tile, _, _) => tile.Water == WaterKind.Land &&
                            tile.Elevation is >= 215 and < WorldMap.PeakElevationThreshold,
                        chunkX, chunkY, chunkIndex))
                        break;
                }
            }
        }

        for (var site = 0; site < count; site++)
        {
            var random = Pcg32XshRrV1.Create(options.Seed, $"natural-deposit-order:clay:{site}");
            var startChunk = (int)(random.NextUInt() % (uint)chunkCount);
            for (var offset = 0; offset < chunkCount; offset++)
            {
                var chunkIndex = (startChunk + offset + site) % chunkCount;
                var chunkX = chunkIndex % columns;
                var chunkY = chunkIndex / columns;
                if (TryGeologySite("clay_bank", "clay", (tile, x, y) =>
                        tile.Water == WaterKind.Land && tile.Elevation < 185 &&
                        IsAdjacentToWater(geography, x, y, options.WrapEastWest),
                    chunkX, chunkY, chunkIndex))
                    break;
            }
        }
        return additions;

        void AddGeologySite(string naturalKind, string resourceKind,
            Func<GeographyTile, int, int, bool> suitable, int chunkX, int chunkY, int generationIndex) =>
            TryGeologySite(naturalKind, resourceKind, suitable, chunkX, chunkY, generationIndex);

        bool TryGeologySite(string naturalKind, string resourceKind,
            Func<GeographyTile, int, int, bool> suitable, int chunkX, int chunkY, int generationIndex)
        {
            var chunk = (chunkX, chunkY);
            if (resourcesByChunk.GetValueOrDefault(chunk) >= TerrainPlacementRules.GeneratedResourcesPerChunk)
                return false;
            var left = chunkX * GeographyGenerator.ChunkSize;
            var top = chunkY * GeographyGenerator.ChunkSize;
            var width = Math.Min(GeographyGenerator.ChunkSize, geography.Width - left);
            var height = Math.Min(GeographyGenerator.ChunkSize, geography.Height - top);
            var random = Pcg32XshRrV1.Create(options.Seed,
                $"natural-site:{naturalKind}:{generationIndex}:{chunkX},{chunkY}");
            for (var attempt = 0; attempt < 128; attempt++)
            {
                var x = left + (int)(random.NextUInt() % (uint)width);
                var y = top + (int)(random.NextUInt() % (uint)height);
                var position = new GridPoint(x, y);
                if (occupied.Contains(position) || !suitable(geography.At(x, y), x, y)) continue;
                additions.Add(new MapResource($"geology-{naturalKind}-{x}-{y}", resourceKind,
                    position, false, NaturalObjectKind: naturalKind));
                occupied.Add(position);
                resourcesByChunk[chunk] = resourcesByChunk.GetValueOrDefault(chunk) + 1;
                return true;
            }

            var start = (int)(random.NextUInt() % (uint)(width * height));
            for (var offset = 0; offset < width * height; offset++)
            {
                var local = (start + offset) % (width * height);
                var x = left + local % width;
                var y = top + local / width;
                var position = new GridPoint(x, y);
                if (occupied.Contains(position) || !suitable(geography.At(x, y), x, y)) continue;
                additions.Add(new MapResource($"geology-{naturalKind}-{x}-{y}", resourceKind,
                    position, false, NaturalObjectKind: naturalKind));
                occupied.Add(position);
                resourcesByChunk[chunk] = resourcesByChunk.GetValueOrDefault(chunk) + 1;
                return true;
            }
            return false;
        }
    }

    private static List<MapResource> EnsureStoneNearStart(GeneratedGeography geography, GridPoint origin,
        IReadOnlyList<MapObject> startingSite, IReadOnlyList<MapResource> existing)
    {
        // Walk out from the middle of the clearing over dry land, skipping
        // peaks and water, as far as the stone reach allows.
        var width = geography.Width;
        var height = geography.Height;
        var reach = TerrainPlacementRules.FirstTownStoneReach;
        var start = new GridPoint(origin.X + StartingSiteWidth / 2, origin.Y + StartingSiteHeight / 2);
        var reached = new HashSet<GridPoint> { start };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
            foreach (var (dx, dy) in new (int X, int Y)[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                var x = current.X + dx;
                var y = current.Y + dy;
                if (geography.WrapsEastWest) x = (x % width + width) % width;
                var next = new GridPoint(x, y);
                if (x < 0 || x >= width || y < 0 || y >= height || reached.Contains(next) ||
                    Distance(start, next) > reach) continue;
                var tile = geography.At(x, y);
                if (tile.Water != WaterKind.Land || tile.Elevation >= WorldMap.PeakElevationThreshold) continue;
                reached.Add(next);
                queue.Enqueue(next);
            }
        if (existing.Any(resource => resource.Kind == "stone" && reached.Contains(resource.Position)))
            return [];

        // Otherwise an outcrop forms on the highest ground in reach, outside
        // the clearing and in a chunk with room for another site.
        var occupied = startingSite.Select(item => item.Position).Concat(existing.Select(item => item.Position)).ToHashSet();
        var perChunk = existing.GroupBy(item =>
                (item.Position.X / GeographyGenerator.ChunkSize, item.Position.Y / GeographyGenerator.ChunkSize))
            .ToDictionary(group => group.Key, group => group.Count());
        var site = reached
            .Where(point => !occupied.Contains(point) &&
                !(point.X >= origin.X && point.X < origin.X + StartingSiteWidth &&
                  point.Y >= origin.Y && point.Y < origin.Y + StartingSiteHeight) &&
                perChunk.GetValueOrDefault((point.X / GeographyGenerator.ChunkSize, point.Y / GeographyGenerator.ChunkSize)) <
                    TerrainPlacementRules.GeneratedResourcesPerChunk)
            .OrderByDescending(point => geography.At(point.X, point.Y).Elevation)
            .ThenBy(point => Distance(start, point))
            .ThenBy(point => point.Y).ThenBy(point => point.X)
            .Cast<GridPoint?>()
            .FirstOrDefault();
        return site is { } position
            ? [new MapResource($"geology-stone_outcrop-{position.X}-{position.Y}", "stone", position, false,
                NaturalObjectKind: "stone_outcrop")]
            : [];

        int Distance(GridPoint from, GridPoint to)
        {
            var horizontal = Math.Abs(from.X - to.X);
            if (geography.WrapsEastWest) horizontal = Math.Min(horizontal, width - horizontal);
            return Math.Max(horizontal, Math.Abs(from.Y - to.Y));
        }
    }

    private static GridPoint FindStartingSiteOrigin(byte[] hydrologyKinds, byte[] elevationLevels,
        byte[] surfaceKinds, int width, int height, IReadOnlyList<GridPoint> starterSites)
    {
        var centerX = (width - StartingSiteWidth) / 2;
        var centerY = (height - StartingSiteHeight) / 2;
        var maxRadius = width + height;
        for (var radius = 0; radius <= maxRadius; radius++)
            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                var offsetY = radius - Math.Abs(offsetX);
                if (TrySite(centerX + offsetX, centerY + offsetY, hydrologyKinds, elevationLevels,
                    surfaceKinds, width, height, starterSites))
                    return new GridPoint(centerX + offsetX, centerY + offsetY);
                if (offsetY > 0 && TrySite(centerX + offsetX, centerY - offsetY, hydrologyKinds,
                    elevationLevels, surfaceKinds, width, height, starterSites))
                    return new GridPoint(centerX + offsetX, centerY - offsetY);
            }
        throw new GeographyClearingUnavailableException();
    }

    private static bool TrySite(int left, int top, byte[] hydrologyKinds, byte[] elevationLevels,
        byte[] surfaceKinds, int width, int height, IReadOnlyList<GridPoint> starterSites)
    {
        if (left < 0 || top < 0 || left + StartingSiteWidth > width || top + StartingSiteHeight > height)
            return false;
        if (left / GeographyGenerator.ChunkSize != (left + StartingSiteWidth - 1) / GeographyGenerator.ChunkSize ||
            top / GeographyGenerator.ChunkSize != (top + StartingSiteHeight - 1) / GeographyGenerator.ChunkSize)
            return false;
        for (var y = top; y < top + StartingSiteHeight; y++)
            for (var x = left; x < left + StartingSiteWidth; x++)
            {
                var index = y * width + x;
                if (!IsGeneratedBuildable(hydrologyKinds[index], elevationLevels[index],
                    (SurfaceKind)surfaceKinds[index])) return false;
            }
        // The starter berry bush, tree, seeds and herbs grow from the soil,
        // so none of them may land on a beach or desert sand.
        return starterSites.All(site => TerrainPlacementRules.CanHoldOrdinaryVegetation(
            (SurfaceKind)surfaceKinds[(top + site.Y) * width + left + site.X]));
    }

    private static bool IsGeneratedBuildable(byte hydrology, byte elevation, SurfaceKind surface) =>
        hydrology == (byte)WaterKind.Land && elevation < WorldMap.MountainElevationThreshold &&
        surface is SurfaceKind.Grass or SurfaceKind.Sand or SurfaceKind.Snow or
            SurfaceKind.ForestFloor or SurfaceKind.DryScrub or SurfaceKind.FertileSoil;

}
