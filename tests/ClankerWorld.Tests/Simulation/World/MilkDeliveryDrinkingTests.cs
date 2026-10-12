using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MilkDeliveryDrinkingTests
{
    [Theory]
    [InlineData("milk", true, false)]
    [InlineData("bread", true, false)]
    [InlineData("milk", false, false)]
    [InlineData("milk", true, true)]
    public async Task ActualHouseDeliveriesKeepTheirFoodWhileUnassignedMilkRemainsDrinkable(
        string kind, bool assigned, bool deliver)
    {
        using var generated = NormalPathWorld.CreateGenerated("milk-delivery-drinking-audit", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == household &&
            generated.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var point = state.Map.Tiles.Select(tile => tile.Position).First(position => state.Map.IsBuildable(position) &&
            state.Map.IsReachableOnFoot(house.Position, position) && state.Map.FootDistance(house.Position, position) is >= 40 and <= 42 &&
            !state.Agents.Any(person => person.Position == position));
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        var ground = new InventoryGroundPosition(point.X, point.Y);
        var owner = assigned ? household : actor;
        inventory = InventoryAuthority.AddLot(inventory, "promised-food", kind, owner, 2,
            groundPosition: assigned ? ground : null);
        var prepared = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            {
                Equipment = null,
                Position = person.AgentId == actor ? point : person.Position,
                HungerBasisPoints = 9_500,
            }).ToArray(),
        };
        if (assigned)
        {
            using var pickup = Restore(prepared, actor, new ChoiceProvider("haul_household_stock"));
            for (var tick = 0; tick < 12 && !pickup.ExportState().Events.Any(item => item.Kind == "household_stock_picked_up"); tick++)
                Assert.True((await pickup.AdvanceOneTickAsync()).Advanced);
            Assert.Single(pickup.ExportState().Events, item => item.Kind == "household_stock_picked_up");
            prepared = pickup.ExportState();
            Assert.Equal(house.InstanceId, pickup.Society.Inventory.GetLot("promised-food").DeliveryBuildingId);
            Assert.Equal(actor, pickup.Society.Inventory.GetLot("promised-food").OwnerId);
        }
        prepared = prepared with
        {
            Agents = prepared.Agents.Select(person => person.AgentId == actor
                ? person with { HungerBasisPoints = 3_500 } : person).ToArray(),
        };
        var choice = deliver ? "haul_household_stock" : kind == "milk" ? "drink_milk" : "consume_food";
        var policy = new ChoiceProvider(choice);
        using var world = Restore(prepared, actor, policy);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < (deliver ? 160 : 80) && !world.ExportState().Events.Any(item => item.Kind == "milk_drunk") &&
             !(deliver && world.Society.Inventory.GetLot("promised-food").StorageBuildingId == house.InstanceId); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.NotEmpty(policy.Offered);
        var canDrink = kind == "milk" && !assigned;
        if (!deliver) Assert.Equal(canDrink, policy.Offered.Any(candidates => candidates.Contains(choice)));
        Assert.Equal(canDrink ? 1 : 0, world.ExportState().Events.Count(item => item.Kind == "milk_drunk"));
        Assert.Equal(canDrink ? 1 : 2, world.Society.Inventory.GetLot("promised-food").Quantity);
        Assert.Null(world.Society.Inventory.GetLot("promised-food").ContainerLotId);
        if (deliver)
        {
            Assert.Equal(household, world.Society.Inventory.GetLot("promised-food").OwnerId);
            Assert.Equal(house.InstanceId, world.Society.Inventory.GetLot("promised-food").StorageBuildingId);
            Assert.Null(world.Society.Inventory.GetLot("promised-food").DeliveryBuildingId);
            Assert.Single(world.ExportState().Events, item => item.Kind == "household_stock_delivered");
        }
        if (canDrink) Assert.True(world.ExportState().Agents.Single(person => person.AgentId == actor).HungerBasisPoints > 3_500);
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = Restore(WorldRuntimeCodec.Decode(saved), actor, new ChoiceProvider(choice));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, ChoiceProvider policy) =>
        WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? policy : new ActionCoverageRecorder(chooseIdle: true));

    private sealed class ChoiceProvider(string choice) : IDecisionProvider
    {
        public ConcurrentQueue<string[]> Offered { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Offered.Enqueue(observation.Candidates.Select(candidate => candidate.Id).ToArray());
            var selected = observation.Candidates.FirstOrDefault(candidate => candidate.Id == choice) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
