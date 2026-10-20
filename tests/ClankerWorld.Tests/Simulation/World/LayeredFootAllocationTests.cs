using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class LayeredFootAllocationTests
{
    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void LayeredFootChecksAvoidAllocatingAnUnusedLegacyTerrainProjection(WorldSizePreset size)
    {
        var map = GeneratedWorldMapGenerator.Generate(new("layered-foot-allocation", size));
        var point = map.Tiles.First(tile => map.IsBuildable(tile.Position)).Position;
        Assert.True(map.IsPassable(point));
        var cold = map with { };
        Assert.NotSame(map, cold);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var passable = cold.IsPassable(point);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(passable);
        const int budget = 16 * 1024;
        Assert.True(allocated <= budget,
            $"{size} layered foot check allocated {allocated:N0} bytes; budget {budget:N0}.");
    }
}
