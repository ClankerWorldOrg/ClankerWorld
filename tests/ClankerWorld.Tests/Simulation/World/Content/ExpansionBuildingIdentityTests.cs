using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class BuildingExpansionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnOfferedExpansionKeepsTheOriginalBuildingAfterPaidRebuilding(bool rebuild)
    {
        using var prepared = PreparedWorld("first-town-house-a", out var actor, out var house, householdWood: 10);
        Assert.Equal(house.HouseholdId, prepared.AddAgent("agent:expansion-rebuild-resident", house.Position));
        var state = prepared.ExportState();
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.StorageBuildingId == house.InstanceId
                ? lot with { StorageBuildingId = null, GroundPosition = new(house.Position.X, house.Position.Y) } : lot).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "000-expansion-retained-wood", "wood", actor, 4);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = house.Entrance!.Value } : person).ToArray(),
            Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => new IdleProvider(id == actor ? "expand_building:" + house.InstanceId : "safe_idle"));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var decision = Assert.Single(step.Decisions, item => item.AgentId == actor);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        var plan = Assert.IsType<ExpansionActionPlan>(decision.ActionPlan);
        Assert.Equal("000-expansion-retained-wood", Assert.Single(plan.Inputs!).LotId);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.Empty(world.WorldSimulation.BuildingExpansions ?? []);
        if (rebuild)
        {
            var removed = world.RemoveBuilding(house.InstanceId, house.TownId, house.HouseholdId);
            Assert.True(removed.Applied, removed.Failure);
            var placed = world.PlaceBuilding(house.InstanceId, house.DefinitionId, house.Position, house.HouseholdId);
            Assert.True(placed.Applied, placed.Failure);
            var replacement = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
            Assert.Equal((house.InstanceId, house.DefinitionId, house.Position, house.TownId, house.HouseholdId, house.Footprint),
                (replacement.InstanceId, replacement.DefinitionId, replacement.Position, replacement.TownId, replacement.HouseholdId, replacement.Footprint));
            Assert.True(replacement.PlacedTick > house.PlacedTick);
        }
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var check = world.CheckActionPlan(plan);
        Assert.Equal(!rebuild, check.Allowed);
        if (rebuild) Assert.Equal(ActionRefusalReason.TargetUnavailable, check.Refusal!.Reason);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(4, world.Society.Inventory.GetLot("000-expansion-retained-wood").Quantity);
        using var restored = Reload(world);
        Assert.Equal(!rebuild, restored.CheckActionPlan(plan).Allowed);
        Assert.Equal(before, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
