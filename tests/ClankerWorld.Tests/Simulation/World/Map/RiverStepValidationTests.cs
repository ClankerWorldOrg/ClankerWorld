using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class RiverStepValidationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RiverStepsReadCurrentWaterBanksAndBridgeAxes(int riverWidth)
    {
        var width = riverWidth + 4;
        var tiles = Enumerable.Range(0, width * 3)
            .Select(index => new TerrainTile(new(index % width, index / width), TerrainKind.Meadow)).ToArray();
        var water = tiles.Select(tile => (byte)(tile.Position.X >= 2 && tile.Position.X < 2 + riverWidth
            ? WaterKind.River : WaterKind.Land)).ToArray();
        var elevation = new byte[tiles.Length];
        var decks = new Dictionary<GridPoint, BridgeAxis>();
        var map = new WorldMap(width, 3, 0, tiles, [], [], "live-river-steps")
        {
            HydrologyKinds = water,
            ElevationLevels = elevation,
            BridgeDecks = decks,
        };
        var river = Enumerable.Range(2, riverWidth).Select(x => new GridPoint(x, 1)).ToArray();
        var farBank = new GridPoint(2 + riverWidth, 1);
        var wadingCost = riverWidth == 1 ? 200 : WorldMap.TwoTileWadingFootCost;
        AssertCrossing(true, wadingCost);

        elevation[farBank.Y * width + farBank.X] = byte.MaxValue;
        AssertCrossing(false, wadingCost);
        elevation[farBank.Y * width + farBank.X] = 0;
        AssertCrossing(true, wadingCost);

        foreach (var point in river) water[point.Y * width + point.X] = (byte)WaterKind.Lake;
        AssertCrossing(false, wadingCost);
        foreach (var point in river) water[point.Y * width + point.X] = (byte)WaterKind.River;
        AssertCrossing(true, wadingCost);

        foreach (var point in river) decks[point] = BridgeAxis.EastWest;
        AssertCrossing(true, 100);
        foreach (var point in river) decks[point] = BridgeAxis.NorthSouth;
        AssertCrossing(false, 100);
        foreach (var point in river) decks[point] = BridgeAxis.EastWest;
        AssertCrossing(true, 100);

        void AssertCrossing(bool allowed, int riverCost)
        {
            for (var x = 1; x <= riverWidth + 1; x++)
            {
                var from = new GridPoint(x, 1);
                var to = new GridPoint(x + 1, 1);
                Assert.Equal(allowed, map.CanFootStep(from, to));
                Assert.Equal(allowed, map.CanFootStep(to, from));
                if (allowed)
                {
                    Assert.Equal(river.Contains(to) ? riverCost : 100, map.FootStepCost(from, to));
                    Assert.Equal(river.Contains(from) ? riverCost : 100, map.FootStepCost(to, from));
                }
                else
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => map.FootStepCost(from, to));
                    Assert.Throws<ArgumentOutOfRangeException>(() => map.FootStepCost(to, from));
                }
            }
        }
    }
}
