using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(AnimalProductKind.Milk)]
    [InlineData(AnimalProductKind.Product)]
    [InlineData(AnimalProductKind.Hide)]
    public async Task ProductChoiceRetainsStockAcrossRollbackReplayAndReload(AnimalProductKind kind)
    {
        var (state, actor) = ProductPlanFixture(kind);
        var chooser = new ProductPlanChooser(actor, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ProductPlanChooser(actor, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<AnimalProductActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.Equal(kind == AnimalProductKind.Hide ? WorldRuntime.AnimalSharedRules.AnimalHideId(plan.Choice.AnimalId!) : "product-plan-stock", plan.StockId);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var stock = world.Society.Inventory.GetLot(plan.StockId);
        if (kind == AnimalProductKind.Milk)
        {
            Assert.Equal(2, stock.Quantity);
            Assert.Single(step.Events, item => item.Kind == "milk_drunk" && item.Detail == actor);
        }
        else
        {
            Assert.True(PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, stock, actor));
            Assert.Equal(kind == AnimalProductKind.Hide ? actor : plan.OwnerId, stock.OwnerId);
            Assert.Single(step.Events, item => item.Kind == (kind == AnimalProductKind.Hide ? "animal_hide_collected" : "animal_product_collected"));
            if (kind == AnimalProductKind.Product)
            {
                Assert.Null(world.Animals.Single().ReadyProductLotId);
                Assert.NotEqual(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(plan.ReservationId!).State);
            }
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(AnimalProductKind.Milk)]
    [InlineData(AnimalProductKind.Product)]
    [InlineData(AnimalProductKind.Hide)]
    public async Task ANewLotDoesNotReplaceProductStockAndLossRefusesReadOnly(AnimalProductKind kind)
    {
        var (state, actor) = ProductPlanFixture(kind);
        var chooser = new ProductPlanChooser(actor, kind, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalProductActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-product-stock", plan.ItemKind, plan.OwnerId, plan.Quantity,
            groundPosition: kind == AnimalProductKind.Milk ? null : new(plan.Position.X, plan.Position.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        // Spoiling retains the ready-product cross-link, so this is a valid saved boundary.
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == plan.StockId ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray() };
        using var spoiled = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(spoiled.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, spoiled.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(spoiled.ExportState()));
    }

    private static (WorldRuntimeState State, string Actor) ProductPlanFixture(AnimalProductKind kind)
    {
        var (state, actor, home, yard) = CreateYard("animal-product-plans");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var animal = new AnimalState("product-plan-animal", "Férn 🌿", "sheep", "female", -(long)AnimalRules.Definition("sheep").AdultDays * day,
            yard.Position, kind == AnimalProductKind.Hide ? "wild-herd" : "household:" + home,
            kind == AnimalProductKind.Hide ? null : home, kind == AnimalProductKind.Hide ? null : yard.InstanceId,
            CareUntilTick: day, DiedTick: kind == AnimalProductKind.Hide ? 0 : null);
        var inventory = state.Society.Society.Inventory with { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        var stockId = kind == AnimalProductKind.Hide ? WorldRuntime.AnimalSharedRules.AnimalHideId(animal.Id) : "product-plan-stock";
        var owner = kind == AnimalProductKind.Milk ? actor : kind == AnimalProductKind.Hide ? animal.Id : home;
        inventory = InventoryAuthority.AddLot(inventory, stockId, kind == AnimalProductKind.Milk ? "milk" : kind == AnimalProductKind.Hide ? "hide" : "wool", owner,
            kind == AnimalProductKind.Milk ? 3 : kind == AnimalProductKind.Hide ? 1 : 2,
            groundPosition: kind == AnimalProductKind.Milk ? null : new(yard.Position.X, yard.Position.Y));
        if (kind == AnimalProductKind.Product)
        {
            const string reservation = "product-plan-reservation";
            inventory = InventoryAuthority.Reserve(inventory, reservation, home, stockId, 2, "animal-product:" + animal.Id, long.MaxValue);
            animal = animal with { ReadyProductLotId = stockId, ReadyProductReservationId = Identity.Parse(reservation) };
        }
        state = At(state, actor, yard.Position, inventory, kind == AnimalProductKind.Milk ? [] : [animal]);
        state = FoodCapacityTestFixture.WithFullness(state, actor, kind == AnimalProductKind.Milk ? 1_000 : 9_500);
        return (state, actor);
    }

    private sealed class ProductPlanChooser(string actor, AnimalProductKind kind, bool choose) : IDecisionProvider
    {
        public AnimalProductActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item =>
                item.ActionPlan is AnimalProductActionPlan plan && plan.Choice.Kind == kind) : null;
            if (selected is not null) Plan = Assert.IsType<AnimalProductActionPlan>(selected.ActionPlan);
            var candidate = choose ? selected?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                candidate, 1, new Dictionary<string, double> { [candidate] = 1 }));
        }
    }
}
