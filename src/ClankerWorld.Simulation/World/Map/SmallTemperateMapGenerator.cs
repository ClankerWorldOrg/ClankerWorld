using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// Generates the tiny fixed-corpus temperate map of worlds without generated geography, as in most tests. It intentionally has
/// only enough terrain variation to exercise the validation rules.
/// </summary>
public static class SmallTemperateMapGenerator
{
    public const int MaximumAttempts = 32;
    public const string GeneratorId = "temperate-fixture";
    public const string GeneratorVersion = "v1";
    public const string GeneratorConfigDigest = "sha256:temperate-fixture-config-v2";
    public const string FertileLandResourceId = "fertile-land";

    public static WorldMap Generate(string worldSeed, bool includeLegacyBedroll = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);

        var diagnostics = new List<string>();
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var candidate = CreateCandidate(worldSeed, attempt, includeLegacyBedroll);
            var validation = MapAcceptance.Validate(candidate);
            if (validation.IsValid)
            {
                return candidate;
            }

            diagnostics.Add($"attempt {attempt}: {validation.Failure}");
        }

        throw new InvalidOperationException(
            $"No valid temperate fixture map for seed '{worldSeed}' after {MaximumAttempts} attempts: " +
            string.Join("; ", diagnostics));
    }

    private static WorldMap CreateCandidate(string worldSeed, int attempt, bool includeLegacyBedroll)
    {
        const int width = 6;
        const int height = 5;
        var terrain = new TerrainKind[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                terrain[y, x] = TerrainKind.Meadow;
            }
        }

        var safeObstacles = new[]
        {
            new GridPoint(5, 0),
            new GridPoint(5, 4),
            new GridPoint(0, 4),
            new GridPoint(4, 4),
        };
        var random = Pcg32XshRrV1.Create(worldSeed, $"worldgen/attempt:{attempt}");
        var waterIndex = (int)(random.NextUInt() % (uint)safeObstacles.Length);
        var mountainIndex = (waterIndex + 1 + (int)(random.NextUInt() % (uint)(safeObstacles.Length - 1))) %
            safeObstacles.Length;
        terrain[safeObstacles[waterIndex].Y, safeObstacles[waterIndex].X] = TerrainKind.Water;
        terrain[safeObstacles[mountainIndex].Y, safeObstacles[mountainIndex].X] = TerrainKind.Mountain;

        var tiles = new List<TerrainTile>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                tiles.Add(new TerrainTile(new GridPoint(x, y), terrain[y, x]));
            }
        }

        var mapObjects = new[]
        {
            new MapObject("cooking", "cooking", new GridPoint(2, 0)),
            new MapObject("founder-scout", "founder", new GridPoint(0, 0)),
            new MapObject("shelter", "shelter", new GridPoint(0, 1)),
            new MapObject("storage", "storage", new GridPoint(1, 1)),
        };
        if (includeLegacyBedroll)
            mapObjects = [new MapObject("bedroll", "bedroll", new GridPoint(1, 0)), .. mapObjects];
        var resources = new[]
        {
            new MapResource("berry-patch", "food", new GridPoint(4, 1), true, NaturalObjectKind: "berry_bush"),
            new MapResource("timber-tree", "construction", new GridPoint(4, 3), false),
            new MapResource("grain-seed-patch", "grain_seed", new GridPoint(2, 3), true, NaturalObjectKind: "wild_seed_patch"),
            new MapResource("wild-greens-patch", "food", new GridPoint(3, 3), true, NaturalObjectKind: "wild_greens"),
        };
        var withoutDigest = new WorldMap(width, height, attempt, tiles, mapObjects, resources, string.Empty);
        return withoutDigest with { ManifestDigest = MapManifestCodec.Digest(withoutDigest) };
    }
}

/// <summary>The founders' starting site, with its facilities but no pre-created person.</summary>
public static class StartingSiteMapGenerator
{
    public static WorldMap Generate(string worldSeed, bool includeLegacyBedroll = false)
    {
        var fixture = SmallTemperateMapGenerator.Generate(worldSeed, includeLegacyBedroll);
        var objects = fixture.MapObjects
            .Where(item => item.Kind != "founder")
            .Concat([
                new MapObject("second-shelter", "shelter", new GridPoint(3, 0)),
                new MapObject("workshop", "workshop", new GridPoint(0, 2)),
                new MapObject("path", "path", new GridPoint(2, 1)),
            ]).ToArray();
        var candidate = fixture with
        {
            MapObjects = objects,
            Resources = fixture.Resources.Concat([
                new MapResource("medicinal-herb-patch", "medicinal_herbs", new GridPoint(5, 3), true,
                    NaturalObjectKind: "medicinal_herb_patch"),
            ]).ToArray(),
            ManifestDigest = string.Empty,
        };
        var map = candidate with { ManifestDigest = MapManifestCodec.Digest(candidate) };
        var validation = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
        if (!validation.IsValid)
            throw new InvalidOperationException($"The generated starting site is invalid: {validation.Failure}");
        return map;
    }
}
