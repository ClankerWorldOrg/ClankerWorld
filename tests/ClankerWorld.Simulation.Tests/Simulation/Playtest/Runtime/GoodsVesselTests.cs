using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class GoodsVesselTests
{
    [Fact]
    public void RefillSkipsBrokenReservedAndFullJugsButPhysicalReturnKeepsDamagedProperty()
    {
        using var generated = NormalPathWorld.CreateGenerated("query-vessel-refill", _ => new Idle());
        var state = generated.ExportState();
        var actor = state.Society.Society.Inhabitants.First().Id;
        var inventory = state.Society.Society.Inventory;
        foreach (var (id, water) in new[] { ("vessel-a-broken", 0), ("vessel-b-reserved", 1), ("vessel-c-full", 4), ("vessel-d-ready", 1) })
        {
            inventory = InventoryFixture.AddLot(inventory, id, "water_jug", actor, 1);
            if (water > 0) inventory = InventoryFixture.AddLot(inventory, id + "-water", "fresh_water", actor, water, containerLotId: id);
        }
        inventory = InventoryFixture.Reserve(inventory, "vessel-reservation", actor, "vessel-b-reserved-water", 1, "cook", long.MaxValue);
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == "vessel-a-broken" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray() };
        using var world = Restore(state, inventory);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var request = new GoodsRequest(GoodsUse.VesselFor, actor, GoodsOwners.One(actor), GoodsKinds.One("water_jug"),
            ExtraUnits: 1, Explain: true, ContentsKind: "fresh_water");
        var answer = world.FindGoods(request);
        var ready = Assert.Single(answer.Matches.Where(match => match.Lot.Id.StartsWith("vessel-", StringComparison.Ordinal)));
        Assert.Equal(("vessel-d-ready", 2), (ready.Lot.Id, ready.MoveUnits));
        Assert.Equal(ready, world.RecheckGoods(request, ready.Lot.Id));
        Assert.Contains(("vessel-a-broken", GoodsReason.Damaged), answer.Excluded);
        Assert.Contains(("vessel-b-reserved", GoodsReason.Reservation), answer.Excluded);
        Assert.Contains(("vessel-c-full", GoodsReason.CarryRoom), answer.Excluded);
        var returns = request with { Use = GoodsUse.ReturnVessel, ExtraUnits = 0, ContentsKind = null };
        Assert.NotNull(world.RecheckGoods(returns, "vessel-a-broken"));
        Assert.Null(world.RecheckGoods(returns, "vessel-b-reserved"));
        Assert.NotNull(world.RecheckGoods(returns, "vessel-c-full"));
        Assert.Throws<ArgumentException>(() => world.FindGoods(request with { Use = GoodsUse.Holdings }));
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void EatingAggregatesUnreservedPersonalMilkAndExcludesAssignedDeliveriesAndIngredients()
    {
        using var generated = NormalPathWorld.CreateGenerated("query-eating", _ => new Idle());
        var state = generated.ExportState();
        var actor = state.Society.Society.Inhabitants.First().Id;
        var home = state.Society.Society.GetInhabitant(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == home &&
            state.WorldContent!.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("house")));
        var inventory = state.Society.Society.Inventory;
        inventory = InventoryFixture.AddLot(inventory, "eat-a-pot", "storage_pot", actor, 1);
        inventory = InventoryFixture.AddLot(inventory, "eat-a-milk", "milk", actor, 2, containerLotId: "eat-a-pot");
        inventory = InventoryFixture.Reserve(inventory, "eat-reserved", actor, "eat-a-milk", 1, "cook", long.MaxValue);
        inventory = InventoryFixture.AddLot(inventory, "eat-b-milk", "milk", actor, 1);
        inventory = InventoryFixture.AddLot(inventory, "eat-c-delivery", "storage_pot", home, 1);
        inventory = InventoryFixture.AddLot(inventory, "eat-c-milk", "milk", home, 2, containerLotId: "eat-c-delivery");
        inventory = InventoryFixture.Transfer(inventory, "eat-pickup", home, actor, "eat-c-delivery", 1, "haul", destinationDeliveryBuildingId: house.InstanceId);
        inventory = InventoryFixture.AddLot(inventory, "eat-flour", "flour", actor, 1);
        using var world = Restore(state, inventory);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var request = new GoodsRequest(GoodsUse.Eat, actor, GoodsOwners.One(actor), GoodsKinds.One("milk"), Explain: true);
        var answer = world.FindGoods(request);
        Assert.Equal(2, answer.Matches.Where(match => match.Lot.Id.StartsWith("eat-", StringComparison.Ordinal)).Sum(match => match.Quantity));
        Assert.Contains(("eat-c-milk", GoodsReason.Delivery), answer.Excluded);
        Assert.Null(world.RecheckGoods(request with { Kinds = GoodsKinds.One("flour") }, "eat-flour"));
        foreach (var match in answer.Matches)
        {
            Assert.Equal(match, world.RecheckGoods(request, match.Lot.Id));
            Assert.Equal(match.Quantity, InventoryFixture.Reserve(inventory, "eat-proof:" + match.Lot.Id, actor,
                match.Lot.Id, match.Quantity, "meal", long.MaxValue).GetReservation("eat-proof:" + match.Lot.Id).Quantity);
        }
        Assert.True(FoodRules.IsFood("flour"));
        Assert.False(FoodRules.IsEdible("flour"));
        Assert.All(FoodRules.EdibleKinds, kind => Assert.True(InventoryContainerRules.Allows("storage_pot", kind)));
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var reload = Restore(world.ExportState(), world.Society.Inventory);
        Assert.Equal(answer.Matches, reload.FindGoods(request).Matches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CargoRetainsBrokenCartsAndSpoiledGoodsButNeverMovesAReservedFamily(bool reserved)
    {
        using var generated = NormalPathWorld.CreateGenerated("query-physical-cargo", _ => new Idle());
        var state = generated.ExportState();
        var actor = state.Society.Society.Inhabitants.First().Id;
        var point = state.Inhabitants.First(person => person.InhabitantId == actor).Position;
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "cargo-cart", "handcart", actor, 1,
            groundPosition: new(point.X, point.Y));
        inventory = InventoryFixture.AddLot(inventory, "cargo-flour", "flour", actor, 2, containerLotId: "cargo-cart");
        inventory = InventoryFixture.AddLot(inventory, "cargo-wood", "wood", actor, 1, containerLotId: "cargo-cart");
        if (reserved) inventory = InventoryFixture.Reserve(inventory, "cargo-reserved", actor, "cargo-wood", 1, "repair", long.MaxValue);
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == "cargo-cart" ? lot with { ConditionBasisPoints = 0 } :
            lot.Id == "cargo-flour" ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray() };
        using var world = Restore(state, inventory);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var request = new GoodsRequest(GoodsUse.Cargo, actor, GoodsOwners.One(actor), GoodsKinds.One("flour"), ContainerId: "cargo-cart");
        if (reserved)
        {
            Assert.Empty(world.FindGoods(request).Matches);
            Assert.Null(world.RecheckGoods(request, "cargo-flour"));
        }
        else
        {
            var match = Assert.Single(world.FindGoods(request).Matches);
            Assert.Equal(2, match.Quantity);
            Assert.Equal(match, world.RecheckGoods(request, match.Lot.Id));
            var moved = InventoryFixture.UnloadHandcart(inventory, "cargo-unload", actor, "cargo-cart", match.Lot.Id, match.Quantity, ontoGround: true);
            Assert.Equal(0, moved.GetLot("cargo-flour").FreshnessBasisPoints);
            Assert.Null(moved.GetLot("cargo-flour").ContainerLotId);
        }
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static PrivateWorldRuntime Restore(PrivateWorldRuntimeState state, InventoryCheckpoint inventory)
    {
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        return PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state)), _ => new Idle());
    }

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1, new Dictionary<string, double> { ["safe_idle"] = 1 }));
    }
}
