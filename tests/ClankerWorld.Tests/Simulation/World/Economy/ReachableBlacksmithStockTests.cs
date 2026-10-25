using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ReachableBlacksmithStockTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReachableWoodIsDeliveredWithoutMovingAnEarlierOccupiedLotAcrossReload(bool fromWarehouse)
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new HaulChoices(false));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var original = generated.ExportState();
        var smith = original.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var warehouse = original.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var actor = original.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        var blocker = original.Society.Society.Agents.First(person => person.HouseholdId != smith.HouseholdId).Id;
        var blockedPosition = MovementTestWorld.OpenGround(original, blocker, smith.Position);
        const string blockedId = "a-blocked-wood";
        const string reachableId = "b-reachable-wood";
        // Controlled starting stock and occupancy; the runtime performs all hauling below.
        var inventory = original.Society.Society.Inventory with
        {
            Lots = original.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "wood" ||
                lot.OwnerId != smith.HouseholdId && lot.OwnerId != warehouse.TownId).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, blockedId, "wood", smith.HouseholdId!, 4,
            groundPosition: new(blockedPosition.X, blockedPosition.Y));
        inventory = fromWarehouse
            ? InventoryAuthority.AddLot(inventory, reachableId, "wood", warehouse.TownId!, 4, storageBuildingId: warehouse.InstanceId)
            : InventoryAuthority.AddLot(inventory, reachableId, "wood", smith.HouseholdId!, 4,
                groundPosition: new(smith.Position.X, smith.Position.Y));
        var state = RawInventoryTestState.WithInventory(original, inventory) with
        {
            Agents = original.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? smith.Position : person.AgentId == blocker ? blockedPosition : person.Position,
                FullnessBasisPoints = 9_000,
                Project = null,
                LastDecisionContext = null,
            }).ToArray(),
        };
        state = MovementTestWorld.ClearOtherBuildingAccess(state, actor);
        var chooser = new HaulChoices(true);
        using var world = Restore(state, actor, chooser);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 30 && !PickedUp(world); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            foreach (var decision in result.Decisions.Where(decision => decision.AgentId == actor &&
                         decision.Admission.Intention?.CandidateId == "haul_smith_input"))
                Assert.Same(chooser.Plan, decision.ActionPlan);
        }
        var plan = Assert.IsType<SmithDeliveryActionPlan>(chooser.Plan);
        Assert.Equal((reachableId, 4, smith.InstanceId), (plan.StockId, plan.Quantity, plan.SmithId));

        Assert.True(chooser.OfferedSmithHaul);
        Assert.True(PickedUp(world));
        var pickedUp = world.Society.Inventory.GetLot(reachableId);
        Assert.Equal(actor, pickedUp.OwnerId);
        Assert.Equal(smith.InstanceId, pickedUp.DeliveryBuildingId);
        Assert.Null(pickedUp.GroundPosition);
        Assert.Null(pickedUp.StorageBuildingId);
        AssertBlockedUnchanged(world);

        using var replay = Restore(world.ExportState(), actor, new HaulChoices(true));
        for (var tick = 0; tick < 30 && world.Society.Inventory.GetLot(reachableId).StorageBuildingId != smith.InstanceId; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var delivered = world.Society.Inventory.GetLot(reachableId);
        Assert.Equal((smith.HouseholdId, smith.InstanceId, 4), (delivered.OwnerId, delivered.StorageBuildingId, delivered.Quantity));
        Assert.Null(delivered.DeliveryBuildingId);
        Assert.Null(delivered.GroundPosition);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "household_stock_delivered" &&
            item.Detail == $"{actor}:{reachableId}:4:{smith.InstanceId}");
        Assert.Equal(inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity),
            world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Equal(inventory.Reservations, world.Society.Inventory.Reservations);
        Assert.Equal(blockedPosition, world.Agents.Single(person => person.AgentId == blocker).Position);
        AssertBlockedUnchanged(world);
        world.Validate();
        using var final = Restore(world.ExportState(), actor, new HaulChoices(true));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(final.ExportState()));

        bool PickedUp(WorldRuntime runtime) => runtime.ExportState().Events.Any(item =>
            item.Kind == "smith_input_picked_up" && item.Detail == $"{actor}:{reachableId}:4:{smith.InstanceId}");
        void AssertBlockedUnchanged(WorldRuntime runtime)
        {
            var expected = inventory.GetLot(blockedId);
            var actual = runtime.Society.Inventory.GetLot(blockedId);
            Assert.Equal(expected with { LastProcessedTick = actual.LastProcessedTick }, actual);
        }
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, HaulChoices choices)
    {
        var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? choices : new HaulChoices(false));
        world.Validate();
        return world;
    }

    private sealed class HaulChoices(bool haul) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public bool OfferedSmithHaul { get; private set; }
        public ActionPlan? Plan { get; private set; }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var smith = request.Observation.Candidates.Any(candidate => candidate.Id == "haul_smith_input");
            OfferedSmithHaul |= smith;
            if (smith) Plan = request.Observation.Candidates.Single(candidate => candidate.Id == "haul_smith_input").ActionPlan;
            var selected = haul && smith ? "haul_smith_input" : haul &&
                request.Observation.Candidates.Any(candidate => candidate.Id == "haul_household_stock") ? "haul_household_stock" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
