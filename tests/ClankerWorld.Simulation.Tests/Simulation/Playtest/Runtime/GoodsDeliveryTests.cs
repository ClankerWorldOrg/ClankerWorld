using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class GoodsDeliveryTests
{
    [Fact]
    public async Task DeliveryRechecksDestinationPromisesAndKeepsWholeVesselsTogether()
    {
        using var generated = NormalPathWorld.CreateGenerated("goods-delivery-boundaries", _ => new Idle());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Inhabitants[0].InhabitantId;
        var household = PaidMarketWorld.HouseholdOf(state, actor);
        var house = PaidMarketWorld.HouseOf(state, household);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                lot.StorageBuildingId != house.InstanceId &&
                (lot.OwnerId != household || lot.ItemKind is not ("wood" or "water_jug" or "fresh_water"))).ToArray(),
        };
        inventory = InventoryFixture.AddLot(inventory, "delivery-carried-stone", "stone", actor, 5);
        inventory = InventoryFixture.AddLot(inventory, "delivery-wood", "wood", household, 5,
            groundPosition: new(house.Position.X, house.Position.Y));
        inventory = InventoryFixture.Reserve(inventory, "delivery-held-wood", household, "delivery-wood", 2,
            "other-work", inventory.WorldTick + 100);
        foreach (var (id, quantity) in new[] { ("fitting", 1), ("oversize", 3), ("reserved", 1) })
        {
            inventory = InventoryFixture.AddLot(inventory, "delivery-" + id + "-jug", "water_jug", household, 1,
                groundPosition: new(house.Position.X, house.Position.Y));
            inventory = InventoryFixture.AddLot(inventory, "delivery-" + id + "-water", "fresh_water", household, quantity,
                containerLotId: "delivery-" + id + "-jug");
        }
        inventory = InventoryFixture.Reserve(inventory, "delivery-held-water", household, "delivery-reserved-water", 1,
            "other-work", inventory.WorldTick + 100);
        state = Prepared(PaidMarketWorld.WithInventory(state, inventory), actor, house.Position);
        using var world = PrivateWorldRuntime.Restore(state, _ => new Idle());
        var request = new GoodsRequest(GoodsUse.Deliver, actor, GoodsOwners.One(household),
            new GoodsKinds(["wood", "fresh_water"]), Destination: house.InstanceId, Explain: true);
        var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var answer = world.FindGoods(request);
        Assert.Equal(new[] { ("delivery-fitting-water", 1, 2), ("delivery-wood", 2, 2) },
            answer.Matches.Select(match => (match.Lot.Id, match.Quantity, match.MoveUnits)).ToArray());
        Assert.Equal(3, answer.Total);
        Assert.Contains(("delivery-oversize-water", GoodsReason.CarryRoom), answer.Excluded);
        Assert.Contains(("delivery-reserved-water", GoodsReason.Reservation), answer.Excluded);
        Assert.Equal(answer.Matches, world.FindGoods(request with { Explain = false }).Matches);
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        foreach (var match in answer.Matches) Assert.Equal(match, world.RecheckGoods(request, match.Lot.Id));
        using var loaded = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new Idle());
        Assert.Equal(answer.Matches, loaded.FindGoods(request).Matches);
        Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(loaded.ExportState()));

        var room = world.DestinationRoom(house.InstanceId);
        inventory = InventoryFixture.AddLot(inventory, "delivery-stored-stone", "stone", household, room - 2,
            storageBuildingId: house.InstanceId);
        inventory = InventoryFixture.AddLot(inventory, "delivery-inbound", "stone", household, 2);
        inventory = InventoryFixture.Transfer(inventory, "delivery-inbound-pickup", household, actor,
            "delivery-inbound", 2, "household_stock_picked_up", destinationDeliveryBuildingId: house.InstanceId);
        using var full = PrivateWorldRuntime.Restore(Prepared(PaidMarketWorld.WithInventory(state, inventory), actor, house.Position), _ => new Idle());
        Assert.Equal(0, full.DestinationRoom(house.InstanceId));
        Assert.Empty(full.FindGoods(request).Matches);
        Assert.Null(full.RecheckGoods(request, "delivery-wood"));
        Assert.Contains(("delivery-wood", GoodsReason.DestinationRoom), full.FindGoods(request).Excluded);
        Assert.Throws<ArgumentException>(() => full.FindGoods(request with { Destination = null }));
    }

    private static PrivateWorldRuntimeState Prepared(PrivateWorldRuntimeState state, string actor, GridPoint position) => state with
    {
        Inhabitants = state.Inhabitants.Select(person => person with
        {
            Position = person.InhabitantId == actor ? position : person.Position,
            Equipment = person.InhabitantId == actor ? null : person.Equipment,
            LastDecisionContext = null,
            Project = null,
            HungerBasisPoints = 9_500,
            Survival = new SurvivalCondition { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000 },
        }).ToArray(),
        JevEnabled = false,
        RoutineHelper = RoutineHelperSettings.Off,
    };

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == "safe_idle" ? 1d : 0d)));
    }
}
