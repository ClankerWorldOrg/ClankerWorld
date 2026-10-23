using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using static ClankerWorld.Tests.ShelterOrderTestFixture;
using static ClankerWorld.Tests.TownHallStormRefugeTests;

namespace ClankerWorld.Tests;

public sealed class NaturalStormProtectionTests
{
    [Fact]
    public async Task NaturalCoverReducesStormCoolingLessThanAHouse()
    {
        var state = WithStorm(Prepared());
        var actor = Actor(state);
        var home = House(state);
        var forest = state.Map.Tiles.First(tile => state.Map.VegetationAt(tile.Position) == VegetationCover.Forest &&
            state.Map.IsPassable(tile.Position) && state.WorldSimulation!.Buildings.All(building =>
                state.Map.FootDistance(building.Position, tile.Position) > 4)).Position;
        var tree = state.Map.Resources.First(site => site.TreeKind is "broadleaf" or "conifer" &&
            state.WorldSystems!.Ecology.GetResource(site.Id).Quantity > 0 && state.Map.IsPassable(site.Position) &&
            state.Map.VegetationAt(site.Position) != VegetationCover.Forest &&
            state.WorldSimulation!.Buildings.All(building => state.Map.FootDistance(building.Position, site.Position) > 4)).Position;
        var bare = state.Map.Tiles.First(tile => state.Map.IsPassable(tile.Position) &&
            state.Map.VegetationAt(tile.Position) != VegetationCover.Forest &&
            !state.Map.Resources.Any(site => site.Position == tile.Position) &&
            state.WorldSimulation!.Buildings.All(building => state.Map.FootDistance(building.Position, tile.Position) > 4)).Position;
        var bareWarmth = await WarmthAfterTick(state, actor, bare);
        Assert.Equal(22, await WarmthAfterTick(state, actor, forest) - bareWarmth);
        Assert.Equal(22, await WarmthAfterTick(state, actor, tree) - bareWarmth);
        Assert.Equal(45, await WarmthAfterTick(state, actor, home.Position) - bareWarmth);
    }
}
