using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(AnimalTravelKind.Mount, false)]
    [InlineData(AnimalTravelKind.Dismount, false)]
    [InlineData(AnimalTravelKind.Dismount, true)]
    [InlineData(AnimalTravelKind.LeadHome, false)]
    public async Task AnimalTravelKeepsItsTargetAndDismountCargoAcrossRollbackReplayAndReload(AnimalTravelKind kind, bool vessel)
    {
        var (state, actor) = await TravelPlanFixture(kind, vessel);
        var chooser = new TravelPlanChooser(actor, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new TravelPlanChooser(actor, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<AnimalTravelActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var animal = world.Animals.Single();
        if (kind == AnimalTravelKind.Mount) Assert.Equal(actor, animal.RiderId);
        else if (kind == AnimalTravelKind.Dismount)
        {
            Assert.Null(animal.RiderId);
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
            Assert.Equal(8, world.Society.Inventory.GetLot("z-travel-stones").Quantity);
            foreach (var cargo in plan.Dismount!.Cargo)
                Assert.Contains(world.Society.Inventory.Lots, lot => lot.ItemKind == cargo.ItemKind && lot.OwnerId == cargo.OwnerId &&
                    lot.GroundPosition == new InventoryGroundPosition(plan.Position.X, plan.Position.Y));
            if (vessel)
            {
                var drop = Assert.Single(plan.Dismount.Cargo, item => item.StockId == "a-travel-jug");
                Assert.Equal((1, 3, 2), (drop.Quantity, drop.PhysicalQuantity, drop.Family!.Count));
                var water = world.Society.Inventory.GetLot("travel-water");
                Assert.Equal("a-travel-jug", water.ContainerLotId);
                Assert.Equal(2, water.Quantity);
                Assert.Equal(world.Society.Inventory.GetLot("a-travel-jug").GroundPosition, InventoryIndex.For(world.Society.Inventory).Root(water).GroundPosition);
            }
            Assert.Single(step.Events, item => item.Kind == "horse_dismounted");
        }
        else
        {
            Assert.NotEqual(plan.Position, animal.Position);
            Assert.Equal(world.Agents.Single(person => person.AgentId == actor).Position, animal.Position);
            Assert.Equal(plan.Destination, animal.LeadDestination);
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AChangedDismountLoadIsRefusedWithoutDroppingDifferentGoods(bool vessel)
    {
        var (state, actor) = await TravelPlanFixture(AnimalTravelKind.Dismount, vessel);
        var chooser = new TravelPlanChooser(actor, AnimalTravelKind.Dismount, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalTravelActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-dismount-cargo", "stone", actor, 1);
        using var changed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.Capacity, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Equal(actor, changed.Animals.Single().RiderId);
    }

    private static async Task<(WorldRuntimeState State, string Actor)> TravelPlanFixture(AnimalTravelKind kind, bool vessel)
    {
        var (state, actor, home, yard) = CreateYard("animal-horse-cargo");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var position = kind == AnimalTravelKind.LeadHome ? state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) && state.Map.FootDistance(point, yard.Position) == 3 && state.Map.IsReachableOnFoot(point, yard.Position) &&
            state.Agents.All(person => person.AgentId == actor || person.Position != point)) : yard.Position;
        var horse = new AnimalState("travel-plan-horse", "Moss", "horse", "female", -7L * day,
            position, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var inventory = state.Society.Society.Inventory with { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        if (kind != AnimalTravelKind.LeadHome) inventory = InventoryAuthority.AddLot(inventory, "travel-plan-saddle", "saddle", home, 1,
            groundPosition: new(position.X, position.Y));
        state = At(state, actor, position, inventory, [horse]);
        state = state with { Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray() };
        if (kind != AnimalTravelKind.LeadHome)
        {
            using var preparing = WorldRuntime.Restore(state, id => id == actor ? new AnimalChooser("animal_order") : new AnimalChooser());
            preparing.SubmitInstruction(new("travel-plan-saddle-order", "owner", actor, OwnerInstructionKind.Order, "saddle Moss"));
            await Until(preparing, () => preparing.Animals.Single().SaddleLotId is not null);
            if (kind == AnimalTravelKind.Dismount)
            {
                preparing.SubmitInstruction(new("travel-plan-mount-order", "owner", actor, OwnerInstructionKind.Order, "mount Moss"));
                await Until(preparing, () => preparing.Animals.Single().RiderId == actor);
            }
            state = preparing.ExportState();
        }
        if (kind == AnimalTravelKind.Dismount)
        {
            inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "z-travel-stones", "stone", actor, 11);
            if (vessel)
            {
                inventory = InventoryAuthority.AddLot(inventory, "a-travel-jug", "water_jug", actor, 1);
                inventory = InventoryAuthority.AddLot(inventory, "travel-water", "fresh_water", actor, 2, containerLotId: "a-travel-jug");
            }
            state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        }
        return (state, actor);
    }

    private sealed class TravelPlanChooser(string actor, AnimalTravelKind kind, bool choose) : IDecisionProvider
    {
        public AnimalTravelActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item =>
                item.ActionPlan is AnimalTravelActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<AnimalTravelActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
