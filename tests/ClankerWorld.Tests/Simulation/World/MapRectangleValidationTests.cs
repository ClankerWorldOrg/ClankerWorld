using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MapRectangleValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteRectanglesRemainValidRegardlessOfTileOrder(bool reverse)
    {
        var map = SmallTemperateMapGenerator.Generate("rectangle-validation");
        if (reverse) map = map with { Tiles = map.Tiles.Reverse().ToArray() };
        var result = MapAcceptance.Validate(map);
        Assert.True(result.IsValid, result.Failure);
        Assert.Equal(map.ManifestDigest, MapManifestCodec.Digest(map));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("outside-x")]
    [InlineData("outside-y")]
    [InlineData("negative-x")]
    [InlineData("negative-y")]
    public void IncompleteOrUnboundedRectanglesRetainTheirGridRefusal(string damage)
    {
        var map = SmallTemperateMapGenerator.Generate("rectangle-validation");
        var tiles = map.Tiles.ToList();
        switch (damage)
        {
            case "duplicate": tiles[^1] = tiles[0]; break;
            case "missing": tiles.RemoveAt(tiles.Count - 1); break;
            case "extra": tiles.Add(tiles[0]); break;
            case "outside-x": tiles[0] = tiles[0] with { Position = new(map.Width, 0) }; break;
            case "outside-y": tiles[0] = tiles[0] with { Position = new(0, map.Height) }; break;
            case "negative-x": tiles[0] = tiles[0] with { Position = new(-1, 0) }; break;
            case "negative-y": tiles[0] = tiles[0] with { Position = new(0, -1) }; break;
        }
        var result = MapAcceptance.Validate(map with { Tiles = tiles });
        Assert.False(result.IsValid);
        Assert.Equal("The logical grid is not a complete bounded rectangle.", result.Failure);
    }

    [Fact]
    public void BorrowedTilePositionsAreRecheckedAfterSuccessfulValidation()
    {
        var original = SmallTemperateMapGenerator.Generate("rectangle-validation");
        var tiles = original.Tiles.ToArray();
        var map = original with { Tiles = tiles };
        Assert.True(MapAcceptance.Validate(map).IsValid);
        tiles[^1] = tiles[0];
        var result = MapAcceptance.Validate(map);
        Assert.False(result.IsValid);
        Assert.Equal("The logical grid is not a complete bounded rectangle.", result.Failure);
    }

    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void RepeatedValidationKeepsAuxiliaryAllocationSmall(WorldSizePreset size)
    {
        var geography = new GeographyOptions("map-layer-digest-allocation", size);
        using var world = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var map = world.ExportState().Map;
        var digestBytes = AllocatedBy(() => _ = MapManifestCodec.Digest(map));
        var validationBytes = AllocatedBy(() =>
        {
            var result = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
            Assert.True(result.IsValid, result.Failure);
        });
        var auxiliaryBytes = validationBytes - digestBytes;
        var budget = 24L * map.Tiles.Count * 10;
        Assert.True(auxiliaryBytes <= budget,
            $"{size} validation allocated {auxiliaryBytes / 10:N0} bytes beyond its digest; budget {budget / 10:N0}.");
    }

    private static long AllocatedBy(Action operation)
    {
        for (var warm = 0; warm < 5; warm++) operation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < 10; sample++) operation();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
