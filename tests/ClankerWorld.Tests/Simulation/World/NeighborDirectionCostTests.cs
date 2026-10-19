using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class NeighborDirectionCostTests
{
    [Theory]
    [InlineData(1, false, 0, 1, 100)]
    [InlineData(1, true, 0, 1, 100)]
    [InlineData(2, false, 1, 1, 141)]
    [InlineData(2, true, 1, 1, 141)]
    [InlineData(2, true, 1, 0, 100)]
    [InlineData(3, true, 2, 1, 141)]
    [InlineData(3, false, 2, 1, 0)]
    [InlineData(32, true, 31, 1, 141)]
    [InlineData(32, true, 31, 0, 100)]
    [InlineData(32, false, 31, 1, 0)]
    [InlineData(1, true, 0, 0, 0)]
    public void StepCostsKeepCardinalDiagonalAndRefusedWrappedGeometry(
        int width, bool wraps, int x, int y, int expected)
    {
        var tiles = Enumerable.Range(0, width * 2).Select(index =>
            new TerrainTile(new(index % width, index / width), TerrainKind.Meadow)).ToArray();
        var map = new WorldMap(width, 2, 0, tiles, [], [], "neighbor-cost") { WrapsEastWest = wraps };
        var origin = new GridPoint(0, 0);
        var destination = new GridPoint(x, y);
        Assert.Equal(expected > 0, map.CanFootStep(origin, destination));
        if (expected == 0)
        {
            Assert.DoesNotContain(destination, map.FootNeighbors(origin));
            Assert.Equal("destination", Assert.Throws<ArgumentOutOfRangeException>(() =>
                map.FootStepCost(origin, destination)).ParamName);
            return;
        }
        Assert.Contains(destination, map.FootNeighbors(origin));
        Assert.Equal(expected, map.FootStepCost(origin, destination));
    }
    [Fact]
    public void InvalidCoordinatesAreRefusedBeforeDistanceArithmetic()
    {
        var map = new WorldMap(1, 1, 0,
            [new TerrainTile(new(0, 0), TerrainKind.Meadow)], [], [], "step-bounds")
        { WrapsEastWest = true };
        var origin = new GridPoint(0, 0);
        foreach (var outside in new GridPoint[]
        {
            new(int.MinValue, 0), new(int.MaxValue, 0),
            new(0, int.MinValue), new(0, int.MaxValue),
        })
        {
            Assert.False(map.CanFootStep(origin, outside));
            Assert.False(map.CanFootStep(outside, origin));
            Assert.Equal("destination", Assert.Throws<ArgumentOutOfRangeException>(() =>
                map.FootStepCost(origin, outside)).ParamName);
        }
    }

    [Fact]
    public void NeighborsRecheckTheOriginAfterEnumerationStarts()
    {
        var tiles = Enumerable.Range(0, 9).Select(index =>
            new TerrainTile(new(index % 3, index / 3), TerrainKind.Meadow)).ToArray();
        var elevations = new byte[tiles.Length];
        var map = new WorldMap(3, 3, 0, tiles, [], [], "live-origin")
        { ElevationLevels = elevations };
        using var neighbors = map.FootNeighbors(new(1, 1)).GetEnumerator();
        Assert.True(neighbors.MoveNext());
        Assert.Equal(new GridPoint(1, 0), neighbors.Current);
        elevations[4] = byte.MaxValue;
        Assert.False(neighbors.MoveNext());
        Assert.Empty(map.FootNeighbors(new(1, 1)));
    }

}
