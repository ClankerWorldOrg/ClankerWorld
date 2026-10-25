using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ReachableFarmStockTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEarlierBlockedRootDoesNotHideReachableFarmStock(bool inVessel)
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new HaulChoices(false));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var original = generated.ExportState();
        const string actor = NormalPathWorld.Founder1;
        var blocker = original.Agents.First(person => person.AgentId != actor).AgentId;
        var household = Assert.IsType<string>(original.Society.Society.GetAgent(actor).HouseholdId);
        var farmhouse = Assert.Single(original.WorldSimulation!.Buildings, building =>
            building.HouseholdId == household && generated.WorldContent.Buildings.Single(definition =>
                definition.CanonicalId == building.DefinitionId).Tags.Contains("farmhouse"));
        var source = new GridPoint(125, 57);
        var blockedSource = new GridPoint(126, 57);
        var tick = original.Society.Society.WorldTick;
        var quantity = inVessel ? 2 : 4;
        const string blockedRoot = "a-blocked-stock";
        const string reachableRoot = "b-reachable-stock";
        const string blockedGrain = "a-blocked-grain";
        const string reachableGrain = "b-reachable-grain";
        var inventory = original.Society.Society.Inventory;
        if (inVessel)
        {
            inventory = InventoryAuthority.AddLot(inventory, blockedRoot, InventoryContainerRules.StoragePot,
                household, 1, tick, groundPosition: new(blockedSource.X, blockedSource.Y));
            inventory = InventoryAuthority.AddLot(inventory, blockedGrain, "grain", household, quantity,
                tick, containerLotId: blockedRoot);
            inventory = InventoryAuthority.AddLot(inventory, reachableRoot, InventoryContainerRules.StoragePot,
                household, 1, tick, groundPosition: new(source.X, source.Y));
            inventory = InventoryAuthority.AddLot(inventory, reachableGrain, "grain", household, quantity,
                tick, containerLotId: reachableRoot);
        }
        else
        {
            inventory = InventoryAuthority.AddLot(inventory, blockedGrain, "grain", household, quantity,
                tick, groundPosition: new(blockedSource.X, blockedSource.Y));
            inventory = InventoryAuthority.AddLot(inventory, reachableGrain, "grain", household, quantity,
                tick, groundPosition: new(source.X, source.Y));
        }
        var initial = RawInventoryTestState.WithInventory(original, inventory) with
        {
            Agents = original.Agents.Select(person => person.AgentId == actor
                ? person with { Position = source, FullnessBasisPoints = 9_000 }
                : person.AgentId == blocker ? person with { Position = blockedSource } : person).ToArray(),
        };
        var initialGrain = inventory.Lots.Where(lot => lot.ItemKind == "grain").Sum(lot => lot.Quantity);
        var movedRoot = inVessel ? reachableRoot : reachableGrain;
        var blockedFamily = inventory.Lots.Where(lot => lot.Id == blockedGrain || lot.Id == blockedRoot).ToArray();
        var choices = new HaulChoices(true);
        using var world = Restore(initial, actor, choices);
        var beforeRollback = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(beforeRollback, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var count = 0; count < 12 && !world.ExportState().Events.Any(item =>
                 item.Kind == "farm_grain_picked_up" && item.Detail ==
                 $"{actor}:{reachableGrain}:{quantity}:{farmhouse.InstanceId}"); count++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            foreach (var decision in result.Decisions.Where(decision => decision.AgentId == actor &&
                         decision.Admission.Intention?.CandidateId == "haul_farm_grain"))
                Assert.Same(choices.Plan, decision.ActionPlan);
        }
        var selectedPlan = Assert.IsType<FarmStockActionPlan>(choices.Plan);
        Assert.Equal((reachableGrain, quantity, inVessel),
            (selectedPlan.ResourceId, selectedPlan.ResourceQuantity, selectedPlan.MoveContainerFamily));

        Assert.Contains(world.ExportState().Events, item => item.Kind == "farm_grain_picked_up" &&
            item.Detail == $"{actor}:{reachableGrain}:{quantity}:{farmhouse.InstanceId}");
        Assert.Equal(source, world.Agents.Single(person => person.AgentId == actor).Position);
        foreach (var lot in world.Society.Inventory.Lots.Where(lot => lot.Id == movedRoot || lot.Id == reachableGrain))
        {
            Assert.Equal(actor, lot.OwnerId);
            Assert.Equal(farmhouse.InstanceId, lot.DeliveryBuildingId);
            Assert.Null(lot.StorageBuildingId);
            Assert.Null(lot.GroundPosition);
        }
        Assert.Equal(inVessel ? reachableRoot : null, world.Society.Inventory.GetLot(reachableGrain).ContainerLotId);
        AssertBlockedFamilyUnchanged(world, blockedFamily);

        // Continue the real delivery intention in both worlds, without resetting cognition.
        using var replay = Restore(world.ExportState(), actor);
        for (var count = 0; count < 40 && world.Society.Inventory.GetLot(movedRoot).StorageBuildingId != farmhouse.InstanceId; count++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal(farmhouse.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        Assert.NotEqual(source, farmhouse.Position);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal) &&
            item.Detail.EndsWith(":household_stock", StringComparison.Ordinal));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "household_stock_delivered" &&
            item.Detail == $"{actor}:{movedRoot}:{(inVessel ? 1 : quantity)}:{farmhouse.InstanceId}");
        foreach (var lot in world.Society.Inventory.Lots.Where(lot => lot.Id == movedRoot || lot.Id == reachableGrain))
        {
            Assert.Equal(household, lot.OwnerId);
            Assert.Equal(farmhouse.InstanceId, lot.StorageBuildingId);
            Assert.Null(lot.DeliveryBuildingId);
            Assert.Null(lot.GroundPosition);
        }
        Assert.Equal(quantity, world.Society.Inventory.GetLot(reachableGrain).Quantity);
        Assert.Equal(inVessel ? reachableRoot : null, world.Society.Inventory.GetLot(reachableGrain).ContainerLotId);
        if (inVessel) Assert.Equal(1, world.Society.Inventory.GetLot(reachableRoot).Quantity);
        Assert.Equal(initialGrain, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "grain").Sum(lot => lot.Quantity));
        Assert.Equal(inventory.Reservations, world.Society.Inventory.Reservations);
        Assert.Equal(blockedSource, world.Agents.Single(person => person.AgentId == blocker).Position);
        AssertBlockedFamilyUnchanged(world, blockedFamily);
        using var final = Restore(world.ExportState(), actor);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(final.ExportState()));
    }

    private static void AssertBlockedFamilyUnchanged(WorldRuntime world, InventoryLot[] family)
    {
        foreach (var expected in family)
        {
            var actual = world.Society.Inventory.GetLot(expected.Id);
            Assert.Equal(expected with { LastProcessedTick = actual.LastProcessedTick }, actual);
        }
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, HaulChoices? choices = null) =>
        WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor && choices is not null ? choices : new HaulChoices(id == actor));

    private sealed class HaulChoices(bool haul) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        private readonly DeterministicDecisionProvider chooser = new();
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => haul &&
                candidate.Id == "haul_household_stock" && candidate.DestinationId == "first-town-farmhouse")
                ?? request.Observation.Candidates.FirstOrDefault(candidate => haul && candidate.Id == "haul_farm_grain")
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            if (selected.Id == "haul_farm_grain") Plan = selected.ActionPlan;
            return chooser.DecideAsync(request with { Observation = request.Observation with { Candidates = [selected] } },
                cancellationToken);
        }
    }
}
