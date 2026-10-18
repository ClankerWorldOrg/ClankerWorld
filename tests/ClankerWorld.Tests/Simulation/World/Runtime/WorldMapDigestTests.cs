using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldMapDigestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerrainDigestPreservesCanonicalUtf8AcrossLongTextAndReorderedInputs(bool reverse)
    {
        var tiles = Enum.GetValues<TerrainKind>().Select((terrain, index) =>
            new TerrainTile(new(index, 0), terrain)).ToArray();
        MapObject[] objects =
        [
            new("z-last", "marker", new(9, 0)),
            new("a-" + string.Concat(Enumerable.Repeat("é\U0001f332水", 3000)), "grove", new(8, 0)),
            new("m-label", "label-\ud800", new(4, 0)),
        ];
        MapResource[] resources =
        [
            new("z-resource", "stone", new(2, 0), false, NaturalObjectKind: "rock"),
            new("a-resource", "tree", new(1, 0), true, TreeKind: "oak"),
        ];
        if (reverse)
        {
            Array.Reverse(tiles);
            Array.Reverse(objects);
            Array.Reverse(resources);
        }
        var map = new WorldMap(10, 1, 7, tiles, objects, resources, string.Empty)
        {
            ClimateZones = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
        };
        const string expected = "feb1b52a44fd8780c372a2ee34ffb52b473df8703a754d90b8c13a88f5a4b883";
        Assert.Equal(expected, MapManifestCodec.Digest(map));
        Assert.Equal(expected, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            MapManifestCodec.Encode(map))));
    }

    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void TerrainDigestAvoidsFullTextAndUtf8Copies(WorldSizePreset size)
    {
        var geography = new GeographyOptions("map-layer-digest-allocation", size);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var map = world.ExportState().Map;
        var expected = MapManifestCodec.Digest(map);
        for (var warm = 0; warm < 5; warm++) _ = MapManifestCodec.Digest(map);
        const int samples = 10;
        var bytesPerDigestBudget = 64L * map.Width * map.Height;
        string? actual = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++) actual = MapManifestCodec.Digest(map);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, actual);
        Assert.True(allocated <= bytesPerDigestBudget * samples,
            $"{size} terrain digests allocated {allocated / samples:N0} bytes each; budget {bytesPerDigestBudget:N0}.");
    }
}
