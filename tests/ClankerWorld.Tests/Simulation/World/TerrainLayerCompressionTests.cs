using System.Text.Json;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TerrainLayerCompressionTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new WorldTerrainChunkCodec() },
    };

    [Theory]
    [InlineData(WorldSizePreset.Small, 700_000)]
    [InlineData(WorldSizePreset.Medium, 2_450_000)]
    public void RepeatedNativeTerrainWritesAvoidLayerCopiesAndKeepBorrowedMutationsAndConcurrentWriters(
        WorldSizePreset size, int bytesPerWriteBudget)
    {
        var geography = new GeographyOptions("map-layer-digest-allocation", size);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var map = world.ExportState().Map;
        var expected = Encode(map);
        for (var warm = 0; warm < 5; warm++) Assert.Equal(expected, Encode(map));
        const int samples = 10;
        byte[] actual = [];
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++) actual = Encode(map);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, actual);

        Func<WorldMap, byte[]>[] layers =
        [
            value => value.ElevationLevels!, value => value.HydrologyKinds!,
            value => value.SurfaceKinds!, value => value.VegetationKinds!,
        ];
        foreach (var layer in layers)
        {
            var data = layer(map);
            var original = data[^1];
            data[^1] = original == 0 ? (byte)1 : (byte)0;
            var changed = Encode(map);
            Assert.False(expected.AsSpan().SequenceEqual(changed));
            Assert.Equal(data, layer(JsonSerializer.Deserialize<WorldMap>(changed, Options)!));
            data[^1] = original;
            Assert.Equal(expected, Encode(map));
        }

        // New input arrays exercise concurrent first writes as well as warm hits.
        var fresh = map with
        {
            ElevationLevels = map.ElevationLevels!.ToArray(), HydrologyKinds = map.HydrologyKinds!.ToArray(),
            SurfaceKinds = map.SurfaceKinds!.ToArray(), VegetationKinds = map.VegetationKinds!.ToArray(),
        };
        Parallel.For(0, 4, _ => Assert.Equal(expected, Encode(fresh)));
        Assert.Equal(expected, Encode(fresh));
        Assert.True(allocated <= (long)bytesPerWriteBudget * samples,
            $"{size} terrain writes allocated {allocated / samples:N0} bytes each; budget {bytesPerWriteBudget:N0}.");
    }

    private static byte[] Encode(WorldMap map) => JsonSerializer.SerializeToUtf8Bytes(map, Options);
}
