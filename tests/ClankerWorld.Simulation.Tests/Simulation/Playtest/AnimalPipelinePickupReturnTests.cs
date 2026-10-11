using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MilkStockRequiresAReachableSource(bool disconnected)
    {
        var (state, actor, home, yard) = CreateYard("animal-milk-sale");
        var definition = state.WorldContent!.Buildings.Single(item => item.LocalId == "store-1x1");
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryFixture.AddLot(inventory, "source-store-cost-" + cost.ResourceId,
                cost.ResourceId, home, cost.Amount, groundPosition: new(yard.Position.X, yard.Position.Y));
        using var setup = PrivateWorldRuntime.Restore(At(state, actor, yard.Position, inventory, []), _ => new AnimalChooser());
        foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, yard.Position)))
            if (setup.PlaceBuilding("source-milk-store", definition.CanonicalId, tile.Position, home).Applied) break;
        var store = Assert.Single(setup.WorldSimulation.Buildings, item => item.InstanceId == "source-milk-store");
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        state = setup.ExportState();
        var source = disconnected ? new GridPoint(213, 2) : yard.Position;
        Assert.True(state.Map.IsBuildable(source));
        Assert.Equal(!disconnected, SwimmingRules.IsReachable(state.Map, yard.Position, source));
        inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "source-milk-jug", "water_jug", home, 1,
            groundPosition: new(source.X, source.Y));
        inventory = InventoryFixture.AddLot(inventory, "source-milk", "milk", home, 2, containerLotId: "source-milk-jug");
        state = At(state, actor, yard.Position, inventory, []) with { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        var chooser = new ReturnPickupChooser("animal:milk_stock:");
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)),
            id => id == actor ? chooser : new AnimalChooser());
        if (disconnected)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(chooser.Offered.SelectMany(items => items), id => id.StartsWith("animal:milk_stock:", StringComparison.Ordinal));
            Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot("source-milk-jug").GroundPosition);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "milk_stock_picked_up");
        }
        else await Until(world, () => world.Society.Inventory.GetLot("source-milk-jug").StorageBuildingId == store.InstanceId);
        Assert.Equal((home, 2, "source-milk-jug"), (world.Society.Inventory.GetLot("source-milk").OwnerId,
            world.Society.Inventory.GetLot("source-milk").Quantity, world.Society.Inventory.GetLot("source-milk").ContainerLotId));
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
        world.Validate(); replay.Validate();
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task MilkStockPickupPreservesTheWholeJugsLoadedRoute(int stones, bool swimOnly)
    {
        var (state, actor, home, yard) = CreateYard("cart-set-unfinished");
        var definition = state.WorldContent!.Buildings.Single(item => item.LocalId == "store-1x1");
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryFixture.AddLot(inventory, "return-store-cost-" + cost.ResourceId,
                cost.ResourceId, home, cost.Amount, groundPosition: new(yard.Position.X, yard.Position.Y));
        using var setup = PrivateWorldRuntime.Restore(At(state, actor, yard.Position, inventory, []), _ => new AnimalChooser());
        foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, yard.Position)))
            if (setup.PlaceBuilding("return-milk-store", definition.CanonicalId, tile.Position, home).Applied) break;
        var store = Assert.Single(setup.WorldSimulation.Buildings, item => item.InstanceId == "return-milk-store");
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        state = setup.ExportState();
        var source = swimOnly ? new GridPoint(105, 49) : state.Map.FootNeighbors(store.Position)
            .First(point => state.Map.IsBuildable(point) && state.Inhabitants.All(person => person.Position != point));
        Assert.True(state.Map.IsBuildable(source));
        Assert.Equal(!swimOnly, state.Map.IsReachableOnFoot(source, store.Position));
        Assert.True(SwimmingRules.IsReachable(state.Map, source, store.Position));
        inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray(),
        };
        inventory = InventoryFixture.AddLot(inventory, "return-ballast", "stone", actor, stones);
        inventory = InventoryFixture.AddLot(inventory, "return-milk-jug", InventoryContainerRules.WaterJug,
            home, 1, groundPosition: new(source.X, source.Y));
        inventory = InventoryFixture.AddLot(inventory, "return-milk", "milk", home, 2, containerLotId: "return-milk-jug");
        state = At(state, actor, source, inventory, []) with
        {
            Inhabitants = At(state, actor, source, inventory, []).Inhabitants.Select(person => person with
            {
                Equipment = person.InhabitantId == actor ? null : person.Equipment,
                Survival = new SurvivalCondition(WarmthBasisPoints: 10_000),
            }).ToArray(),
        };
        var choices = new ReturnPickupChooser("animal:milk_stock:");
        var initial = PrivateWorldRuntimeCodec.Encode(state);
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(initial),
            id => id == actor ? choices : new AnimalChooser());
        Assert.Equal(initial, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        world.Pause();
        world.SetRoutineHelper(RoutineHelperSettings.Off);
        world.Resume();
        if (swimOnly && stones == 2)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(choices.Offered.SelectMany(items => items), id => id.StartsWith("animal:milk_stock:", StringComparison.Ordinal));
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "milk_stock_picked_up");
            Assert.Null(world.Society.Inventory.GetLot("return-milk-jug").CarrierId);
            Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot("return-milk-jug").GroundPosition);
        }
        else
        {
            await Until(world, () => world.Society.Inventory.GetLot("return-milk-jug").StorageBuildingId == store.InstanceId);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "milk_stock_picked_up");
            Assert.Equal(store.InstanceId, world.Society.Inventory.GetLot("return-milk").StorageBuildingId);
        }
        Assert.Equal((home, 2, "return-milk-jug"),
            (world.Society.Inventory.GetLot("return-milk").OwnerId,
                world.Society.Inventory.GetLot("return-milk").Quantity,
                world.Society.Inventory.GetLot("return-milk").ContainerLotId));
        Assert.Equal(stones, world.Society.Inventory.GetLot("return-ballast").Quantity);
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved),
            id => id == actor ? new ReturnPickupChooser("animal:milk_stock:") : new AnimalChooser());
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
        world.Validate();
        replay.Validate();
    }

    private sealed class ReturnPickupChooser(string prefix) : IDecisionProvider
    {
        public ConcurrentQueue<string[]> Offered { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Offered.Enqueue(observation.Candidates.Select(item => item.Id).ToArray());
            var choice = observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal)) ??
                observation.Candidates.Single(item => item.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind,
                ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d)));
        }
    }
}
