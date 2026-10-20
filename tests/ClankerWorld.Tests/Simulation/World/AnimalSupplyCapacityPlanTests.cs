using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetainedCareFeedRefusesReducedLoadedRoomWhileItsSmallerChoiceRemains(bool swim)
    {
        var (state, actor, home, yard) = CreateYard("cart-set-unfinished");
        using (var setup = WorldRuntime.Restore(state, _ => new AnimalChooser()))
        {
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
            state = setup.ExportState();
        }
        var source = swim ? new GridPoint(105, 49) : state.Map.FootNeighbors(yard.Position)
            .First(point => state.Map.IsBuildable(point) && state.Agents.All(person => person.Position != point));
        Assert.Equal(!swim, state.Map.IsReachableOnFoot(source, yard.Position));
        Assert.True(SwimmingRules.IsReachable(state.Map, source, yard.Position));
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "care-plan-ballast", "stone", actor, 2);
        inventory = InventoryAuthority.AddLot(inventory, "care-plan-grain", "grain", home, 4,
            groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, "care-plan-jug", "water_jug", home, 1,
            storageBuildingId: yard.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "care-plan-water", "fresh_water", home, 2,
            storageBuildingId: yard.InstanceId, containerLotId: "care-plan-jug");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var animal = new AnimalState("care-plan-target", "Moss", "cow", "female", -7L * day,
            yard.Position, WorldRuntime.AnimalSharedRules.AnimalHerdId(home), home, yard.InstanceId, CareUntilTick: 0);
        state = At(state, actor, source, inventory, [animal]);
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with { Survival = new(), Equipment = null }).ToArray(),
        };
        var policy = new MarketRulesPolicy { Choose = (_, _) => null };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), policy.CreateProvider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.Single(policy.OfferedTo(actor).Select(candidate => candidate.ActionPlan)
            .OfType<AnimalSupplyActionPlan>(), candidate => candidate.Choice.StockId == "care-plan-grain" &&
                candidate.Choice.AnimalId == animal.Id && candidate.Choice.Action == "care");
        Assert.Equal(2, plan.Quantity);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "care-plan-added-stone", "stone", actor, 1);
        var changedState = checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } };
        var changedPolicy = new MarketRulesPolicy { Choose = (_, _) => null };
        using var changed = WorldRuntime.Restore(changedState, changedPolicy.CreateProvider);
        var before = WorldRuntimeCodec.Encode(changed.ExportState());
        var check = changed.CheckActionPlan(plan);
        Assert.Equal(before, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Empty(changed.ExportState().AnimalWorld!.SupplyTrips);
        Assert.Equal(4, changed.Society.Inventory.GetLot("care-plan-grain").Quantity);
        Assert.Null(changed.Society.Inventory.GetLot("care-plan-grain").Holds);
        Assert.True((await changed.AdvanceOneTickAsync()).Advanced);
        var fresh = Assert.Single(changedPolicy.OfferedTo(actor).Select(candidate => candidate.ActionPlan)
            .OfType<AnimalSupplyActionPlan>(), candidate => candidate.Choice.StockId == plan.Choice.StockId &&
                candidate.Choice.AnimalId == animal.Id && candidate.Choice.Action == "care");
        Assert.Equal(plan.CandidateId, fresh.CandidateId);
        Assert.Equal(swim ? 1 : 2, fresh.Quantity);
        Assert.True(changed.CheckActionPlan(fresh).Allowed);
        if (swim) Assert.Equal(ActionRefusalReason.Capacity, check.Refusal?.Reason);
        else Assert.True(check.Allowed);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(changed.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(changed.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }
}
