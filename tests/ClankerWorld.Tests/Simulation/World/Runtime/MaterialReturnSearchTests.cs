using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MaterialReturnSearchTests
{
    [Fact]
    public void BlockedReturnFromManyHarvestSitesKeepsOneWeightedPickupSearch()
    {
        using var initial = NormalPathWorld.CreateGenerated("social-talk-" + new string('x', 78),
            _ => new DeterministicDecisionProvider());
        var state = initial.ExportState();
        var actor = state.Agents[0].AgentId;
        var position = state.Agents[0].Position;
        // One to three occupants close a natural cul-de-sac. The actor and many
        // trees remain on its open side, as when a child closes a shop's passage.
        var destination = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.FootDistance(position, point) > 2 && state.Map.IsReachableOnFoot(position, point) &&
            state.Map.FootNeighbors(point).Count() is >= 1 and <= 3);
        var blockers = state.Map.FootNeighbors(destination).ToArray();
        var away = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsPassable(point) &&
            !state.Map.IsReachableOnFoot(position, point));
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "return-search-ballast", "stone", actor, 4);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select((person, index) => person with
            {
                Position = index == 0 ? position : index <= blockers.Length ? blockers[index - 1] : away,
                Equipment = null,
                Survival = new(10_000),
            }).ToArray(),
        };
        var occupied = state.Agents.Skip(1).Select(person => person.Position).ToHashSet();
        using var approach = new UnoccupiedRouteSearch(state.Map, position, occupied, state.Map.FootStepCost);
        var sources = state.Map.Resources.Where(source => source.Kind == "construction" &&
                state.Map.IsReachableOnFoot(position, source.Position) &&
                approach.RouteTo(source.Position, 1).Count > 0)
            .Take(16).ToArray();
        Assert.Equal(16, sources.Length);
        using var world = WorldRuntime.Restore(state);
        foreach (var source in sources)
        {
            Assert.False(world.CanReturnWithHarvest(actor, "wood", source, destination));
            // Check after each site: a regression fails immediately, even on a
            // large map, instead of completing a minute-long source scan.
            Assert.InRange(world.RouteSearchesCreated, 0, 2);
        }
    }
}
