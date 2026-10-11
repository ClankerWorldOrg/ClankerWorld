using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed class ExpansionGoodsQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpansionSelectionKeepsTheOldSourceOrderCapacityAndReservationRules(bool townStock)
    {
        using var generated = NormalPathWorld.CreateGenerated("expansion-goods-query", _ => new Idle());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Society.Society.Inhabitants.First(person => person.HouseholdId == "household:camp-alpha").Id;
        var household = state.Society.Society.GetInhabitant(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == household &&
            building.InstanceId == "first-town-house-a");
        var warehouse = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var building = townStock ? warehouse : house;
        var owner = building.HouseholdId ?? building.TownId!;
        var other = state.Society.Society.GetHousehold(household).MemberIds.First(id => id != actor);
        var source = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(building.Position, point) is >= 2 and < 8 &&
            state.Inhabitants.All(person => person.Position != point) &&
            state.WorldSimulation.Buildings.All(item => !WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == item.DefinitionId), item).Contains(point)) &&
            state.Map.Resources.All(resource => resource.Position != point));
        var ground = new InventoryGroundPosition(source.X, source.Y);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryFixture.AddLot(inventory, "a-warehouse", "wood", warehouse.TownId!, 4, storageBuildingId: warehouse.InstanceId);
        inventory = InventoryFixture.AddLot(inventory, "b-at-site", "wood", owner, 4,
            groundPosition: new(building.Position.X, building.Position.Y));
        inventory = InventoryFixture.AddLot(inventory, "c-stored-at-site", "wood", owner, 4, storageBuildingId: building.InstanceId);
        inventory = InventoryFixture.AddLot(inventory, "d-carried", "wood", owner, 4, groundPosition: ground);
        inventory = InventoryFixture.Relocate(inventory, "controlled-custody", "d-carried", owner, 4, carrierId: other);
        inventory = InventoryFixture.AddLot(inventory, "e-reserved", "wood", owner, 4, groundPosition: ground);
        inventory = InventoryFixture.Reserve(inventory, "held-other-work", owner, "e-reserved", 4, "other-work", inventory.WorldTick + 100);
        inventory = InventoryFixture.AddLot(inventory, "f-damaged", "wood", owner, 4, conditionBasisPoints: 0, groundPosition: ground);
        inventory = InventoryFixture.AddLot(inventory, "g-other-owner", "wood", other, 4, groundPosition: ground);
        inventory = InventoryFixture.AddLot(inventory, "z-own-source", "wood", owner, 5, groundPosition: ground);
        inventory = InventoryFixture.Reserve(inventory, "held-partial-work", owner, "z-own-source", 2, "other-work", inventory.WorldTick + 100);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor
                ? person with { Position = building.Position, Equipment = null, Project = null, LastDecisionContext = null }
                : person).ToArray(),
        };
        // Only inventory and physical positions are controlled; Town, household,
        // building and land authority come from the normal founder setup.
        using var world = Restore(state);
        AssertSelection(world, actor, building, "z-own-source", 3);
        var request = new GoodsRequest(GoodsUse.ReachableHoldings, actor, GoodsOwners.One(owner), GoodsKinds.One("wood"));
        Assert.Equal(3, world.RecheckGoods(request, "z-own-source")!.Quantity);
        var held = InventoryFixture.Reserve(inventory, "held-rest", owner, "z-own-source", 3, "other-work", inventory.WorldTick + 100);
        using var reserved = Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = held } } });
        Assert.Null(reserved.RecheckGoods(request, "z-own-source"));
        AssertSelection(reserved, actor, building, townStock ? null : "a-warehouse", townStock ? 0 : 4);
        var full = InventoryFixture.AddLot(inventory, "full-cargo", "stone", actor,
            PersonalEquipmentRules.FreeCapacity(inventory, actor, null));
        using var loaded = Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = full } } });
        AssertSelection(loaded, actor, building, null, 0);
        Assert.NotNull(loaded.RecheckGoods(request, "z-own-source"));
    }

    private static void AssertSelection(PrivateWorldRuntime world, string actor, PlacedBuilding building,
        string? expectedId, int expectedQuantity)
    {
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var selected = Invoke(world, "ExpansionSharedMaterialSource", actor, building, "wood") as InventoryLot;
        Assert.Equal(OldSource(world, actor, building)?.Id, selected?.Id);
        Assert.Equal(expectedId, selected?.Id);
        var pickup = ((InventoryLot? Source, int Quantity))Invoke(world, "ExpansionSharedMaterialPickup",
            actor, building, new ContentQuantity("wood", 20))!;
        Assert.Equal(expectedId, pickup.Source?.Id);
        Assert.Equal(expectedQuantity, pickup.Quantity);
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var reload = Restore(PrivateWorldRuntimeCodec.Decode(before));
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.Equal(selected?.Id, (Invoke(reload, "ExpansionSharedMaterialSource", actor, building, "wood") as InventoryLot)?.Id);
    }

    private static InventoryLot? OldSource(PrivateWorldRuntime world, string actor, PlacedBuilding building)
    {
        var owner = building.HouseholdId ?? building.TownId!;
        var site = new InventoryGroundPosition(building.Position.X, building.Position.Y);
        var position = world.Inhabitants.Single(person => person.InhabitantId == actor).Position;
        var inventory = world.Society.Inventory;
        int Available(InventoryLot lot) => lot.FreshnessBasisPoints == 0 || lot.ConditionBasisPoints == 0 ? 0 :
            lot.Quantity - inventory.Reservations.Where(reservation => reservation.LotId == lot.Id &&
                InventoryRules.IsActive(reservation.State)).Sum(reservation => reservation.Quantity);
        var own = inventory.Lots.Where(lot => lot.OwnerId == owner && lot.ItemKind == "wood" &&
                lot.StorageBuildingId != building.InstanceId && lot.DeliveryBuildingId is null && lot.CarrierId is null &&
                lot.GroundPosition != site && lot.ContainerLotId is null && Available(lot) > 0)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).Where(lot => !(bool)Invoke(world, "OnBorrowedMarketStall", lot)! &&
                ((List<GridPoint>)Invoke(world, "FindUnoccupiedRoute", actor, position,
                    Invoke(world, "HouseholdStockPosition", lot)!,
                    lot.StorageBuildingId is null && lot.GroundPosition is null ? 1 : 0, true, 0)!).Count > 0);
        var warehouse = building.HouseholdId is null ? [] :
            (IEnumerable<InventoryLot>)Invoke(world, "AvailableWarehouseStock", actor, "wood")!;
        return own.Concat(warehouse).FirstOrDefault(lot => !(bool)Invoke(world, "IsActiveTownProjectDelivery", lot.Id)! &&
            (int)Invoke(world, "PickupCarryCapacity", actor, lot, building.Position, 0)! > 0);
    }

    private static object? Invoke(PrivateWorldRuntime world, string name, params object[] arguments) =>
        typeof(PrivateWorldRuntime).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length &&
                method.GetParameters().Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(arguments[index])).All(match => match))
            .Invoke(world, arguments);

    private static PrivateWorldRuntime Restore(PrivateWorldRuntimeState state) =>
        PrivateWorldRuntime.Restore(state, _ => new Idle());

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == "safe_idle").ToArray() } }, cancellationToken);
    }
}
