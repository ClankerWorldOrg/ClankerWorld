using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class WorldTerrainChunkTests
{
    private static readonly JsonSerializerOptions RawStateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapManifestDigestKeepsItsCanonicalBytesForOrderedAndUnorderedTiles(bool reverse)
    {
        var tiles = Enumerable.Range(0, 4).Select(index =>
            new TerrainTile(new(index % 2, index / 2), TerrainKind.Meadow)).ToArray();
        if (reverse) Array.Reverse(tiles);
        var map = new WorldMap(2, 2, 0, tiles, [], [], string.Empty) { ClimateZones = [0, 1, 2, 3] };
        Assert.Equal("08bb6588cf05686c13dfe64be3b9d4b1044fd00d1379701612aff677ff936bf5", MapManifestCodec.Digest(map));
    }

    [Fact]
    public void MapLayerDigestKeepsItsCanonicalBytesAndValidation()
    {
        var tiles = Enumerable.Range(0, 4).Select(index =>
            new TerrainTile(new(index % 2, index / 2), TerrainKind.Meadow)).ToArray();
        var map = new WorldMap(2, 2, 0, tiles, [], [], string.Empty)
        {
            ClimateZones = [0, 1, 2, 3],
            ElevationLevels = [0, 255, 1, 254],
            HydrologyKinds = [3, 2, 1, 0],
            SurfaceKinds = [0, 1, 2, 3],
            VegetationKinds = [1, 0, 1, 0],
        };
        Assert.Equal("44b1026746e51953ac90ca092febb58325219bcf9f3761b7880ce7f1b56ba387",
            MapLayerManifestCodec.Digest(map));
        Assert.Null(MapLayerManifestCodec.Digest(map with { ClimateZones = null }));
        Assert.Throws<InvalidDataException>(() => MapLayerManifestCodec.Digest(map with { ElevationLevels = [0, 1, 2] }));
    }

    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void MapLayerDigestDoesNotAllocateAConcatenatedCopyOfTheLayers(WorldSizePreset size)
    {
        var geography = new GeographyOptions("map-layer-digest-allocation", size);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var map = world.ExportState().Map;
        var expected = MapLayerManifestCodec.Digest(map);
        Assert.NotNull(expected);
        for (var warm = 0; warm < 5; warm++) _ = MapLayerManifestCodec.Digest(map);
        const int samples = 20;
        const int bytesPerDigestBudget = 128 * 1024;
        string? actual = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++) actual = MapLayerManifestCodec.Digest(map);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, actual);
        Assert.True(allocated <= bytesPerDigestBudget * samples,
            $"{size} map digests allocated {allocated / samples:N0} bytes each; budget {bytesPerDigestBudget:N0}.");
    }

    [Fact]
    public void GeneratedMapRoundTripsThroughCurrentCompactChunks()
    {
        var geography = new GeographyOptions("chunked-world", WorldSizePreset.Small,
            WrapEastWest: true);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var state = world.ExportState();
        var newBytes = WorldRuntimeCodec.Encode(state);
        var rawTerrainBytes = JsonSerializer.SerializeToUtf8Bytes(state.Map.Tiles, RawStateJsonOptions);
        var newText = Encoding.UTF8.GetString(newBytes);

        Assert.DoesNotContain("\"tiles\"", newText, StringComparison.Ordinal);
        Assert.Contains("\"terrainEncoding\":\"terrain-chunks/v1\"", newText, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(newBytes);
        var terrainChunkBytes = Encoding.UTF8.GetByteCount(document.RootElement.GetProperty("state")
            .GetProperty("map").GetProperty("terrainChunks").GetRawText());
        Assert.True(terrainChunkBytes < rawTerrainBytes.Length / 4,
            $"Chunked terrain should materially shrink the raw terrain: {terrainChunkBytes} to {rawTerrainBytes.Length} bytes.");
        var decoded = WorldRuntimeCodec.Decode(newBytes);
        Assert.Equal(state.Map.ManifestDigest, decoded.Map.ManifestDigest);
        Assert.Equal(state.Map.Tiles, decoded.Map.Tiles);
        Assert.Equal(state.Map.ClimateZones, decoded.Map.ClimateZones);
        Assert.Equal(state.Map.ElevationLevels, decoded.Map.ElevationLevels);
        Assert.Equal(state.Map.HydrologyKinds, decoded.Map.HydrologyKinds);
        Assert.Equal(state.Map.SurfaceKinds, decoded.Map.SurfaceKinds);
        Assert.Equal(state.Map.VegetationKinds, decoded.Map.VegetationKinds);
        Assert.Equal(newBytes, WorldRuntimeCodec.Encode(decoded));
    }

    [Fact]
    public void CurrentGeographyWithoutItsSavedLayersIsRefusedAndPreserved()
    {
        var directory = Path.Combine(Path.GetTempPath(), "clankerworld-missing-map-layers-" + Guid.NewGuid().ToString("N"));
        try
        {
            var geography = new GeographyOptions("missing-current-map-layers", WorldSizePreset.Small);
            var file = new WorldStateFile(Path.Combine(directory, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup, newWorldGeography: geography);
            using var created = file.LoadOrCreate(geography.Seed);
            var state = created.ExportState();
            var missingLayersState = state with
            {
                Map = state.Map with
                {
                    ElevationLevels = null,
                    HydrologyKinds = null,
                    SurfaceKinds = null,
                    VegetationKinds = null,
                },
            };
            var encodeError = Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(missingLayersState));
            Assert.Contains("missing required map layers", encodeError.Message, StringComparison.Ordinal);

            var document = JsonNode.Parse(File.ReadAllBytes(file.Path))!.AsObject();
            var map = document["state"]!["map"]!.AsObject();
            map["elevationLevels"] = null;
            map["hydrologyKinds"] = null;
            map["surfaceKinds"] = null;
            map["vegetationKinds"] = null;
            map["mapLayersSha256"] = null;
            var missingLayersBytes = Encoding.UTF8.GetBytes(document.ToJsonString());
            File.WriteAllBytes(file.Path, missingLayersBytes);

            var decodeError = Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(geography.Seed));
            Assert.Contains("missing required map layers", decodeError.Message, StringComparison.Ordinal);
            Assert.Equal(missingLayersBytes, File.ReadAllBytes(file.Path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RecomputedLayerChecksumCannotReplaceDeterministicCurrentGeography()
    {
        var geography = new GeographyOptions("changed-current-map-layer", WorldSizePreset.Small);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var state = world.ExportState();
        var elevation = state.Map.ElevationLevels!.ToArray();
        elevation[0] ^= 1;
        var tamperedState = state with { Map = state.Map with { ElevationLevels = elevation } };

        Assert.Equal(state.Map.ManifestDigest, tamperedState.Map.ManifestDigest);
        var decoded = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(tamperedState));
        Assert.NotEqual(MapLayerManifestCodec.Digest(state.Map), MapLayerManifestCodec.Digest(decoded.Map));
        var error = Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(decoded));
        Assert.Contains("map does not match deterministic regeneration", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DamagedChunkFailsClosedAndLeavesTheAtomicCheckpointUntouched()
    {
        var directory = Path.Combine(Path.GetTempPath(), "clankerworld-chunks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var geography = new GeographyOptions("damaged-chunk", WorldSizePreset.Small);
            var file = new WorldStateFile(Path.Combine(directory, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup, newWorldGeography: geography);
            using var created = file.LoadOrCreate(geography.Seed);
            var bytes = File.ReadAllBytes(file.Path);
            var damaged = JsonNode.Parse(bytes)!.AsObject();
            var first = damaged["state"]!["map"]!["terrainChunks"]!.AsArray()[0]!;
            var payload = Convert.FromBase64String(first["data"]!.GetValue<string>());
            payload[0] ^= 1;
            first["data"] = Convert.ToBase64String(payload);
            var corruptBytes = Encoding.UTF8.GetBytes(damaged.ToJsonString());
            File.WriteAllBytes(file.Path, corruptBytes);

            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(geography.Seed));
            Assert.Equal(corruptBytes, File.ReadAllBytes(file.Path));
            Assert.NotEqual(bytes, corruptBytes);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DamagedButDecompressibleMapLayerFailsItsSavedIntegrityCheck()
    {
        var geography = new GeographyOptions("damaged-map-layer", WorldSizePreset.Small);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(world.ExportState()))!.AsObject();
        var map = document["state"]!["map"]!.AsObject();
        var packed = Convert.FromBase64String(map["elevationLevels"]!.GetValue<string>());
        var elevation = new byte[geography.Size == WorldSizePreset.Small ? 256 * 128 : 0];
        Assert.True(BrotliDecoder.TryDecompress(packed, elevation, out var written));
        Assert.Equal(elevation.Length, written);
        elevation[0]++;
        var corrupted = new byte[BrotliEncoder.GetMaxCompressedLength(elevation.Length)];
        Assert.True(BrotliEncoder.TryCompress(elevation, corrupted, out var compressedLength,
            quality: 4, window: 22));
        map["elevationLevels"] = Convert.ToBase64String(corrupted[..compressedLength]);

        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(
            Encoding.UTF8.GetBytes(document.ToJsonString())));
    }
}
