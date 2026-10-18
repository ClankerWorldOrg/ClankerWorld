using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ColumnWrappingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(256)]
    [InlineData(512)]
    public void WrappingKeepsArbitraryColumnsRowsAndNarrowMapNeighborOrder(int width)
    {
        var map = new WorldMap(width, 3, 0,
            Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, width)
                .Select(x => new TerrainTile(new(x, y), TerrainKind.Meadow))).ToArray(), [], [], "column-wrapping")
        { WrapsEastWest = true };
        var bounded = map with { WrapsEastWest = false };
        foreach (var x in new[] { int.MinValue, -3 * width - 1, -width, -1, 0, 1, width - 1, width, width + 1, 3 * width + 1, int.MaxValue })
            foreach (var y in new[] { int.MinValue, -1, 0, 2, 3, int.MaxValue })
            {
                var point = new GridPoint(x, y);
                var expected = (int)(((long)x % width + width) % width);
                Assert.Equal(new(expected, y), map.WrapColumn(point));
                Assert.Equal(point, bounded.WrapColumn(point));
            }
        GridPoint[] neighbors = width switch
        {
            1 => [new(0, 0), new(0, 2)],
            2 => [new(0, 0), new(1, 1), new(0, 2), new(1, 0), new(1, 2)],
            _ => [new(0, 0), new(1, 1), new(0, 2), new(width - 1, 1),
                new(1, 0), new(1, 2), new(width - 1, 2), new(width - 1, 0)],
        };
        Assert.Equal(neighbors, map.FootNeighbors(new(0, 1)));
        Assert.Equal(neighbors.Length, neighbors.Distinct().Count());
        foreach (var widthWithoutColumns in new[] { 0, -1 })
        {
            var empty = map with { Width = widthWithoutColumns };
            Assert.Equal(new(-1, 1), empty.WrapColumn(new(-1, 1)));
            Assert.Equal(new(int.MaxValue, 1), empty.WrapColumn(new(int.MaxValue, 1)));
        }
    }
}
