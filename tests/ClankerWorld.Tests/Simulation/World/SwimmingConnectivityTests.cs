using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SwimmingConnectivityTests
{
    [Fact]
    public void ColdConnectivityKeepsItsAllocationBoundedWhileVisitingEveryWaterTile()
    {
        var tiles = (from y in Enumerable.Range(0, 32)
                     from x in Enumerable.Range(0, 32)
                     select new TerrainTile(new(x, y), TerrainKind.Lake)).ToArray();
        var map = new WorldMap(32, 32, 0, tiles, [], [], "");
        Assert.True(SwimmingRules.IsSwimmingWater(map, new(0, 0)));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var connected = SwimmingRules.IsReachable(map, new(0, 0), new(31, 31));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(connected);
        const int budget = 32 * 1024;
        Assert.True(allocated <= budget,
            $"Cold swimming connectivity allocated {allocated:N0} bytes; budget {budget:N0}.");
        Assert.True(SwimmingRules.IsReachable(map, new(31, 0), new(0, 31)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void NarrowConnectivityKeepsOceanAndBridgeBarriersSeparateAcrossMapReplacements(int width, bool wraps)
    {
        var tiles = (from y in Enumerable.Range(0, 5)
                     from x in Enumerable.Range(0, width)
                     select new TerrainTile(new(x, y), y == 2 ? TerrainKind.Ocean :
                         y == 1 ? TerrainKind.Lake : TerrainKind.Meadow)).ToArray();
        var closed = new WorldMap(width, 5, 0, tiles, [], [], "") { WrapsEastWest = wraps };
        Assert.True(SwimmingRules.IsReachable(closed, new(0, 0), new(0, 1)));
        Assert.False(SwimmingRules.IsReachable(closed, new(0, 0), new(0, 4)));
        Assert.False(SwimmingRules.IsReachable(closed, new(0, 2), new(0, 2)));
        var opened = closed with
        {
            Tiles = tiles.Select(tile => tile.Position.Y == 2 ? tile with { Terrain = TerrainKind.Lake } : tile).ToArray(),
        };
        Assert.True(SwimmingRules.IsReachable(opened, new(0, 0), new(0, 4)));
        var bridged = opened with
        {
            BridgeDecks = Enumerable.Range(0, width).ToDictionary(x => new GridPoint(x, 1), _ => BridgeAxis.EastWest),
        };
        Assert.False(SwimmingRules.IsReachable(bridged, new(0, 0), new(0, 4)));
        Assert.True(SwimmingRules.IsReachable(opened, new(0, 4), new(0, 0)));
        Assert.False(SwimmingRules.IsReachable(closed, new(0, 0), new(0, 4)));
        Assert.False(SwimmingRules.IsReachable(opened, new(-1, 0), new(0, 0)));
        Assert.False(SwimmingRules.IsReachable(opened, new(0, 0), new(width, 0)));
    }
}
