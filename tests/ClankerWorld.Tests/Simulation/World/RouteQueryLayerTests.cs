using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RouteQueryLayerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RouteQueriesRecheckBorrowedWaterAndPeakLayersAndKeepPublicRefusals(bool water)
    {
        var tiles = Enumerable.Range(0, 6).Select(index =>
            new TerrainTile(new(index % 3, index / 3), TerrainKind.Meadow)).ToArray();
        var elevation = Enumerable.Repeat((byte)100, tiles.Length).ToArray();
        var hydrology = new byte[tiles.Length];
        var surface = new byte[tiles.Length];
        var map = new WorldMap(3, 2, 0, tiles, [], [], string.Empty)
        {
            ElevationLevels = elevation,
            HydrologyKinds = hydrology,
            SurfaceKinds = surface,
        };
        GridPoint[] direct = [new(0, 0), new(1, 0), new(2, 0)];
        Assert.Equal(direct, DeterministicRouteFinder.Find(map, direct[0], direct[^1]));
        if (water)
        {
            hydrology[1] = (byte)WaterKind.Ocean;
            surface[1] = (byte)SurfaceKind.Water;
        }
        else elevation[1] = 255;
        GridPoint[] detour = [new(0, 0), new(0, 1), new(1, 1), new(2, 1), new(2, 0)];
        Assert.Equal(detour, DeterministicRouteFinder.Find(map, direct[0], direct[^1]));
        Assert.False(map.CanFootStep(direct[0], direct[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FootStepCost(direct[0], direct[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FootTravelCost(direct[1]));
        elevation[1] = 100;
        hydrology[1] = (byte)WaterKind.Land;
        surface[1] = (byte)SurfaceKind.Grass;
        Assert.Equal(direct, DeterministicRouteFinder.Find(map, direct[0], direct[^1]));
    }
}
