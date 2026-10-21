using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FarmStockActionPlanTests
{
    [Fact]
    public async Task AnEarlierHaulerTakingTheSelectedStockRefusesTheLaterPlanWithoutSelectingReplacementGoods()
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new EconomyPlanChoices("", choose: false, candidateId: "haul_farm_grain"));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var household = state.Society.Society.GetAgent(state.Agents[0].AgentId).HouseholdId!;
        var actors = state.Society.Society.GetHousehold(household).MemberIds.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, actors.Length);
        var pile = state.Map.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
            state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        state = FoodCapacityTestFixture.WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "a-competing-haul-grain", FarmFieldRules.Grain, household, 4)) with
        {
            Agents = state.Agents.Select(person => actors.Contains(person.AgentId, StringComparer.Ordinal)
                ? person with { Position = pile, FullnessBasisPoints = 9_000 } : person).ToArray(),
        };
        var providers = actors.ToDictionary(actor => actor, actor => new EconomyPlanChoices(actor, choose: true, candidateId: "haul_farm_grain"), StringComparer.Ordinal);
        using var world = WorldRuntime.Restore(state, actor => providers.GetValueOrDefault(actor) ?? new EconomyPlanChoices("", choose: false, candidateId: "haul_farm_grain"));
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "choice_refused"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var first = Assert.IsType<FarmStockActionPlan>(providers[actors[0]].Plan);
        var second = Assert.IsType<FarmStockActionPlan>(providers[actors[1]].Plan);
        Assert.Equal(("a-competing-haul-grain", 4), (first.ResourceId, first.TransferQuantity));
        Assert.Equal((first.ResourceId, first.TransferQuantity), (second.ResourceId, second.TransferQuantity));
        var name = world.Society.GetAgent(actors[1]).Name;
        var refusal = Assert.Single(world.ExportState().Events, item => item.Kind == "choice_refused");
        Assert.StartsWith(name + " didn't carry crops to farm storage:", refusal.Detail);
        Assert.Equal(actors[0], world.Society.Inventory.GetLot(first.ResourceId).OwnerId);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.OwnerId == actors[1] && lot.DeliveryBuildingId == second.DestinationId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedLoadStaysFixedWhenEarlierStockAppearsAndRefusesWhenItsStockChanges(bool inVessel)
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new EconomyPlanChoices("", choose: false, candidateId: "haul_farm_grain"));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var source = new GridPoint(125, 57);
        var quantity = inVessel ? 2 : 4;
        var inventory = state.Society.Society.Inventory;
        if (inVessel)
            inventory = InventoryAuthority.AddLot(inventory, "b-plan-pot", InventoryContainerRules.StoragePot,
                household, 1, groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, "b-plan-grain", FarmFieldRules.Grain, household, inVessel ? 1 : quantity,
            groundPosition: inVessel ? null : new(source.X, source.Y), containerLotId: inVessel ? "b-plan-pot" : null);
        if (inVessel)
            inventory = InventoryAuthority.AddLot(inventory, "c-plan-grain", FarmFieldRules.Grain, household, 1, containerLotId: "b-plan-pot");
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = source, FullnessBasisPoints = 9_000 } : person).ToArray(),
        };
        var choices = new EconomyPlanChoices(actor, choose: false, candidateId: "haul_farm_grain");
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<FarmStockActionPlan>(choices.Plan);
        Assert.Equal(("b-plan-grain", quantity, inVessel), (plan.ResourceId, plan.ResourceQuantity, plan.MoveContainerFamily));
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-farm-grain", FarmFieldRules.Grain,
            household, 4, groundPosition: new(source.X, source.Y));
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = inVessel
            ? InventoryAuthority.AddLot(inventory, "changed-pot-grain", FarmFieldRules.Grain, household, 1, containerLotId: plan.CarrierId)
            : InventoryAuthority.Reserve(inventory, "other-grain-work", household, plan.ResourceId, 1, "other_work", inventory.WorldTick + 100);
        using var changed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(("b-plan-grain", quantity, inVessel), (plan.ResourceId, plan.ResourceQuantity, plan.MoveContainerFamily));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(changed.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(changed.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }
}
