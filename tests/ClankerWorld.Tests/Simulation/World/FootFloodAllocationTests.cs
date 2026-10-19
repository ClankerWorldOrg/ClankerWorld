using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FootFloodAllocationTests
{
    [Fact]
    public void CompleteFootTraversalAvoidsAllocatingANeighborIteratorForEveryTile()
    {
        const int width = 128;
        var tiles = (from y in Enumerable.Range(0, width)
                     from x in Enumerable.Range(0, width)
                     select new TerrainTile(new(x, y), TerrainKind.Meadow)).ToArray();
        var map = new WorldMap(width, width, 0, tiles, [], [], "");
        Assert.True(map.IsPassable(new(0, 0)));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var reached = MapAcceptance.ReachableFrom(map, new(0, 0));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(reached.SetEquals(tiles.Select(tile => tile.Position)));
        const int budget = 1024 * 1024;
        Assert.True(allocated <= budget,
            $"Complete foot traversal allocated {allocated:N0} bytes; budget {budget:N0}.");
    }
}
