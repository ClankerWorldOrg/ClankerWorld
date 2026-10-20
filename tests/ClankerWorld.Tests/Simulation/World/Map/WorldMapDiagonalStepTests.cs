using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldMapDiagonalStepTests
{
    [Theory]
    [InlineData(1, true, 0, 0, 0, 1, false)]
    [InlineData(2, true, 0, 0, 1, 1, true)]
    [InlineData(5, true, 0, 0, 4, 1, true)]
    [InlineData(5, false, 0, 0, 4, 1, false)]
    [InlineData(5, true, 4, 2, 0, 1, true)]
    [InlineData(5, true, 0, 0, 1, 1, true)]
    [InlineData(5, true, 0, 0, 1, 0, false)]
    [InlineData(5, true, 0, 0, 0, 1, false)]
    [InlineData(5, true, 0, 0, 2, 1, false)]
    [InlineData(5, true, 0, 2, 4, 0, false)]
    [InlineData(5, true, -1, 0, 0, 1, false)]
    [InlineData(5, true, 0, 0, -1, 1, false)]
    [InlineData(5, true, 0, 0, 4, 3, false)]
    [InlineData(5, true, int.MinValue, 0, 0, 1, false)]
    [InlineData(5, true, 0, 0, 4, int.MaxValue, false)]
    public void CanonicalEndpointsAndNarrowWrapsKeepTheirDiagonalMeaning(int width, bool wraps,
        int fromX, int fromY, int toX, int toY, bool diagonal)
    {
        var map = new WorldMap(width, 3, 0,
            [.. from y in Enumerable.Range(0, 3) from x in Enumerable.Range(0, width)
                select new TerrainTile(new(x, y), TerrainKind.Meadow)], [], [], string.Empty)
        { WrapsEastWest = wraps };
        Assert.Equal(diagonal, map.IsDiagonalFootStep(new(fromX, fromY), new(toX, toY)));
    }
}
