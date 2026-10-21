using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ValidatedFootCostTests
{
    [Fact]
    public void FootCostsReadEveryBorrowedElevationAfterLegalAndRefusedQueries()
    {
        var elevation = new byte[9];
        var tiles = Enumerable.Range(0, 9).Select(index =>
            new TerrainTile(new(index % 3, index / 3), TerrainKind.Meadow)).ToArray();
        var map = new WorldMap(3, 3, 0, tiles, [], [], "cost-query") { ElevationLevels = elevation };
        var origin = new GridPoint(1, 1);
        var direct = new GridPoint(2, 1);
        var diagonal = new GridPoint(2, 2);
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            Array.Fill(elevation, (byte)value);
            if (value >= 245)
            {
                Assert.Empty(map.FootNeighbors(origin));
                Assert.Equal("point", Assert.Throws<ArgumentOutOfRangeException>(() => map.FootTravelCost(direct)).ParamName);
                Assert.Equal("destination", Assert.Throws<ArgumentOutOfRangeException>(() => map.FootStepCost(origin, diagonal)).ParamName);
                continue;
            }
            var neighbors = map.FootNeighbors(origin).ToArray();
            Assert.Equal(8, neighbors.Length);
            Assert.Contains(direct, neighbors);
            Assert.Contains(diagonal, neighbors);
            Assert.Equal(value >= 215 ? 200 : 100, map.FootTravelCost(direct));
            Assert.Equal(value >= 215 ? 200 : 100, map.FootStepCost(origin, direct));
            Assert.Equal(value >= 215 ? 282 : 141, map.FootStepCost(origin, diagonal));
        }
        Array.Fill(elevation, (byte)128);
        Assert.Equal(100, map.FootTravelCost(direct));
        Assert.Equal(141, map.FootStepCost(origin, diagonal));
    }
}
