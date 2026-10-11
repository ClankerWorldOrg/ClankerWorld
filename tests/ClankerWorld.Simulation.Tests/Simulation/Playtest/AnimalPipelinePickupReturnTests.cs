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
    public async Task OrdinaryMilkStoreStockRequiresAReachableSource(bool disconnected)
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
        var reserve = 2 * state.Society.Society.Inhabitants.Count(person => person.HouseholdId == home &&
            state.Inhabitants.Any(active => active.InhabitantId == person.Id));
        inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryFixture.AddLot(inventory, "source-milk", "milk", home, reserve + 2,
            groundPosition: new(source.X, source.Y));
        state = At(state, actor, yard.Position, inventory, []) with
        { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        var chooser = new ReturnPickupChooser("business_stock_store");
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)),
            id => id == actor ? chooser : new AnimalChooser());
        if (disconnected)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(chooser.Offered.SelectMany(items => items), id => id == "business_stock_store");
            Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot("source-milk").GroundPosition);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "store_stock_collected");
        }
        else await Until(world, () => world.Society.Inventory.Lots.Any(lot => lot.ItemKind == "milk" && lot.StorageBuildingId == store.InstanceId));
        Assert.Equal(reserve + 2, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk").Sum(lot => lot.Quantity));
        Assert.All(world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk"), lot =>
        { Assert.Equal(home, lot.OwnerId); Assert.Null(lot.ContainerLotId); });
        Assert.Equal(disconnected ? 0 : 2, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk" &&
            lot.StorageBuildingId == store.InstanceId).Sum(lot => lot.Quantity));
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
        world.Validate(); replay.Validate();
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task OrdinaryMilkStorePickupKeepsTheLoadedReturnWithinCapacity(int stones, bool swimOnly)
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
        var reserve = 2 * state.Society.Society.Inhabitants.Count(person => person.HouseholdId == home &&
            state.Inhabitants.Any(active => active.InhabitantId == person.Id));
        inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryFixture.AddLot(inventory, "return-ballast", "stone", actor, stones);
        inventory = InventoryFixture.AddLot(inventory, "return-milk", "milk", home, reserve + 2,
            groundPosition: new(source.X, source.Y));
        state = At(state, actor, source, inventory, []) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Inhabitants = At(state, actor, source, inventory, []).Inhabitants.Select(person => person with
            {
                Equipment = person.InhabitantId == actor ? null : person.Equipment,
                Survival = new SurvivalCondition(WarmthBasisPoints: 10_000),
            }).ToArray(),
        };
        var choices = new ReturnPickupChooser("business_stock_store");
        var initial = PrivateWorldRuntimeCodec.Encode(state);
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(initial),
            id => id == actor ? choices : new AnimalChooser());
        Assert.Equal(initial, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        world.Pause();
        world.SetRoutineHelper(RoutineHelperSettings.Off);
        world.Resume();
        if (swimOnly && stones == 4)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(choices.Offered.SelectMany(items => items), id => id == "business_stock_store");
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "store_stock_collected");
            Assert.Null(world.Society.Inventory.GetLot("return-milk").CarrierId);
            Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot("return-milk").GroundPosition);
        }
        else
        {
            await Until(world, () => world.Society.Inventory.Lots.Any(lot => lot.ItemKind == "milk" && lot.StorageBuildingId == store.InstanceId));
            Assert.Contains(world.ExportState().Events, item => item.Kind == "store_stock_collected");
        }
        Assert.Equal(reserve + 2, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk").Sum(lot => lot.Quantity));
        Assert.All(world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk"), lot =>
        { Assert.Equal(home, lot.OwnerId); Assert.Null(lot.ContainerLotId); });
        Assert.Equal(swimOnly ? 4 - stones : 2, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk" &&
            lot.StorageBuildingId == store.InstanceId).Sum(lot => lot.Quantity));
        Assert.Equal(stones, world.Society.Inventory.GetLot("return-ballast").Quantity);
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved),
            id => id == actor ? new ReturnPickupChooser("business_stock_store") : new AnimalChooser());
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
                (prefix == "business_stock_store" ? observation.Candidates.FirstOrDefault(item => item.Id == "haul_household_stock") : null) ??
                observation.Candidates.Single(item => item.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind,
                ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d)));
        }
    }
}
