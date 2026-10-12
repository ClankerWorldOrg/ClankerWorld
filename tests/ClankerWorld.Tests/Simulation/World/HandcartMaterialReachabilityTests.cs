using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HandcartMaterialReachabilityTests
{
    [Theory]
    [InlineData("unreachable", false, false)]
    [InlineData("missing", false, false)]
    [InlineData("reserved", false, false)]
    [InlineData("reachable", false, true)]
    [InlineData("unreachable", true, false)]
    [InlineData("reserved", true, false)]
    [InlineData("reachable", true, true)]
    public async Task CartMaterialsAreCollectedOrKeptOnlyWhenTheRemainingSetCanBeReached(
        string fittings, bool alreadyCarried, bool finishable)
    {
        using var setup = NormalPathWorld.CreateGenerated("cart-set-unfinished", _ => new CartChoice("safe_idle"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var house = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-house-b");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        var household = smith.HouseholdId!;
        var remote = new GridPoint(238, 85);
        Assert.True(state.Map.IsBuildable(remote));
        Assert.False(state.Map.IsReachableOnFoot(smith.Position, remote));
        Assert.True(state.Map.FootDistance(smith.Position, remote) > 16);
        var inventory = state.Society.Society.Inventory;
        Assert.DoesNotContain(inventory.Lots, lot => lot.ItemKind == "iron_fittings");
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !(lot.OwnerId == actor || lot.OwnerId == household) ||
                lot.ItemKind is not ("wood" or "rope")).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "cart-route-wood", "wood", alreadyCarried ? actor : household, 4,
            groundPosition: alreadyCarried ? null : new(smith.Position.X, smith.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "cart-route-rope", "rope", alreadyCarried ? actor : household, 1,
            groundPosition: alreadyCarried ? null : new(smith.Position.X, smith.Position.Y));
        if (fittings != "missing")
        {
            var position = fittings == "unreachable" ? remote : smith.Position;
            inventory = InventoryAuthority.AddLot(inventory, "cart-route-fittings", "iron_fittings", household, 2,
                groundPosition: new(position.X, position.Y));
            if (fittings == "reserved") inventory = InventoryAuthority.Reserve(inventory, "cart-route-held", household,
                "cart-route-fittings", 2, "other-work", long.MaxValue);
        }
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear) with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? smith.Position : person.Position,
                FullnessBasisPoints = 10_000,
                Survival = person.Survival is { } survival ? survival with
                { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000, IllnessBasisPoints = 0 } : null,
                LastDecisionContext = null,
                Project = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        };
        var action = alreadyCarried ? "supply_workstation:wood" : "collect_handcart_material:wood";
        var chooser = new CartChoice(action);
        var replayChooser = new CartChoice(action);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? chooser : new CartChoice("safe_idle"));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? replayChooser : new CartChoice("safe_idle"));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var family = world.Society.Inventory.Lots.Where(lot => lot.Id == "cart-route-wood" || lot.ProvenanceLotId == "cart-route-wood").ToArray();
        var carried = family.Where(lot => lot.OwnerId == actor && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor)).Sum(lot => lot.Quantity);
        Assert.Equal(4, family.Sum(lot => lot.Quantity));
        Assert.Equal(finishable, chooser.Offered.Contains(alreadyCarried ? "collect_handcart_material:iron_fittings" : "collect_handcart_material:wood"));
        if (alreadyCarried && !finishable)
        {
            Assert.InRange(carried, 0, 3);
            Assert.Contains(family, lot => lot.OwnerId == household && lot.StorageBuildingId == house.InstanceId);
            Assert.Contains(world.ExportState().Events.Where(item => item.Detail.Contains("cart-route-wood", StringComparison.Ordinal)),
                item => item.Kind == "workstation_supplied" &&
                item.Detail == $"{actor}:cart-route-wood:4:{house.InstanceId}");
        }
        else Assert.Equal(finishable ? 4 : 0, carried);
        if (fittings != "missing")
        {
            var lot = world.Society.Inventory.GetLot("cart-route-fittings");
            var position = fittings == "unreachable" ? remote : smith.Position;
            Assert.Equal((household, 2, new InventoryGroundPosition(position.X, position.Y)),
                (lot.OwnerId, lot.Quantity, lot.GroundPosition));
            if (fittings == "reserved") Assert.Equal(InventoryReservationState.Reserved,
                world.Society.Inventory.GetReservation("cart-route-held").State);
        }
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? new CartChoice(action) : new CartChoice("safe_idle"));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
        reload.Validate();
    }

    private sealed class CartChoice(string preferred) : IDecisionProvider
    {
        public ConcurrentQueue<string> Offered { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            foreach (var candidate in observation.Candidates) Offered.Enqueue(candidate.Id);
            var chosen = observation.Candidates.FirstOrDefault(item => item.Id == preferred) ??
                observation.Candidates.Single(item => item.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind,
                ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                chosen.Id, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == chosen.Id ? 1d : 0d)));
        }
    }
}
