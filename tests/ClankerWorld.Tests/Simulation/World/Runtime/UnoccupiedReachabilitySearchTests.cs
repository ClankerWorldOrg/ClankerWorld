using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class UnoccupiedReachabilitySearchTests
{
    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    [InlineData(12, false, false)]
    [InlineData(12, true, false)]
    [InlineData(12, false, true)]
    [InlineData(12, true, true)]
    public void EveryOriginMatchesAFreshForwardRouteIncludingOccupiedStartsAndWrappedCorners(
        int width, bool wraps, bool swimming)
    {
        var map = new WorldMap(width, 7, 0,
            [.. from y in Enumerable.Range(0, 7)
                from x in Enumerable.Range(0, width)
                select new TerrainTile(new(x, y), y == 3 ? TerrainKind.River :
                    x == 5 && y < 3 ? TerrainKind.Lake : TerrainKind.Meadow)], [], [], string.Empty)
        {
            WrapsEastWest = wraps,
            BridgeDecks = new Dictionary<GridPoint, BridgeAxis> { [new(width / 2, 3)] = BridgeAxis.NorthSouth },
        };
        HashSet<GridPoint> occupied = [new(0, 1), new(width - 1, 2), new(width / 2, 4)];
        Func<GridPoint, IEnumerable<GridPoint>>? neighbors = swimming ? point => SwimmingRules.Neighbors(map, point) : null;
        foreach (var destination in new GridPoint[] { new(0, 0), new(width / 2, 3), new(width - 1, 6), new(0, 1) })
            foreach (var range in new[] { 0, 1, 2 })
            {
                var shared = new UnoccupiedReachabilitySearch(map, occupied, destination, range, swimming);
                foreach (var tile in map.Tiles.Reverse())
                {
                    using var forward = new UnoccupiedRouteSearch(map, tile.Position, occupied, (_, _) => 1, neighbors);
                    Assert.Equal(forward.RouteTo(destination, range).Count > 0, shared.CanReachFrom(tile.Position));
                }
                Assert.InRange(shared.ExpandedTiles, 0, map.Width * map.Height);
            }
    }

    [Fact]
    public void BlockedDestinationDoesNotSearchTheSourceSideAgainForEachOrigin()
    {
        var map = new WorldMap(32, 8, 0,
            [.. from y in Enumerable.Range(0, 8)
                from x in Enumerable.Range(0, 32)
                select new TerrainTile(new(x, y), x == 2 && y != 4 ? TerrainKind.Peak : TerrainKind.Meadow)],
            [], [], string.Empty);
        var destination = new GridPoint(0, 4);
        HashSet<GridPoint> occupied = [new(2, 4)];
        var search = new UnoccupiedReachabilitySearch(map, occupied, destination, 0);
        foreach (var origin in map.Tiles.Where(tile => tile.Position.X > 2).Select(tile => tile.Position))
            Assert.False(search.CanReachFrom(origin));
        Assert.Equal(16, search.ExpandedTiles);
        // Moving the blocker away opens the same passage, without a stale answer.
        var reopened = new UnoccupiedReachabilitySearch(map, [], destination, 0);
        Assert.True(reopened.CanReachFrom(new(31, 4)));
    }
}
