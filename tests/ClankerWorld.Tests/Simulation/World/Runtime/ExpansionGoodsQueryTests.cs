using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

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
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == "household:first").Id;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == household &&
            building.InstanceId == "first-town-house-a");
        var warehouse = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var building = townStock ? warehouse : house;
        var owner = building.HouseholdId ?? building.TownId!;
        var other = state.Society.Society.GetHousehold(household).MemberIds.First(id => id != actor);
        var source = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(building.Position, point) is >= 2 and < 8 &&
            state.Agents.All(person => person.Position != point) &&
            state.WorldSimulation.Buildings.All(item => !WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == item.DefinitionId), item).Contains(point)) &&
            state.Map.Resources.All(resource => resource.Position != point));
        var ground = new InventoryGroundPosition(source.X, source.Y);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor && lot.ItemKind != "wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "a-warehouse", "wood", warehouse.TownId!, 4, storageBuildingId: warehouse.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "b-at-site", "wood", owner, 4,
            groundPosition: new(building.Position.X, building.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "c-stored-at-site", "wood", owner, 4, storageBuildingId: building.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "d-carried", "wood", owner, 4, groundPosition: ground);
        inventory = InventoryAuthority.Relocate(inventory, "controlled-custody", "d-carried", owner, 4, carrierId: other);
        inventory = InventoryAuthority.AddLot(inventory, "e-reserved", "wood", owner, 4, groundPosition: ground);
        inventory = InventoryAuthority.Reserve(inventory, "held-other-work", owner, "e-reserved", 4, "other-work", inventory.WorldTick + 100);
        inventory = InventoryAuthority.AddLot(inventory, "f-damaged", "wood", owner, 4, conditionBasisPoints: 0, groundPosition: ground);
        inventory = InventoryAuthority.AddLot(inventory, "g-other-owner", "wood", other, 4, groundPosition: ground);
        inventory = InventoryAuthority.AddLot(inventory, "z-own-source", "wood", owner, 5, groundPosition: ground);
        inventory = InventoryAuthority.Reserve(inventory, "held-partial-work", owner, "z-own-source", 2, "other-work", inventory.WorldTick + 100);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = building.Position, Equipment = null, Project = null, LastDecisionContext = null }
                : person).ToArray(),
        };
        // Only inventory and physical positions are controlled; Town, household,
        // building and land authority come from the normal founder setup.
        using var world = Restore(state);
        AssertSelection(world, actor, building, "z-own-source", 3);
        var request = new GoodsRequest(GoodsUse.ReachableHoldings, actor, GoodsOwners.One(owner), GoodsKinds.One("wood"));
        Assert.Equal(3, world.RecheckGoods(request, "z-own-source")!.Quantity);
        var held = InventoryAuthority.Reserve(inventory, "held-rest", owner, "z-own-source", 3, "other-work", inventory.WorldTick + 100);
        using var reserved = Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = held } } });
        Assert.Null(reserved.RecheckGoods(request, "z-own-source"));
        AssertSelection(reserved, actor, building, townStock ? null : "a-warehouse", townStock ? 0 : 4);
        var full = InventoryAuthority.AddLot(inventory, "full-cargo", "stone", actor,
            PersonalEquipmentRules.FreeCapacity(inventory, actor, null));
        using var loaded = Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = full } } });
        AssertSelection(loaded, actor, building, null, 0);
        Assert.NotNull(loaded.RecheckGoods(request, "z-own-source"));
    }

    private static void AssertSelection(WorldRuntime world, string actor, PlacedBuilding building,
        string? expectedId, int expectedQuantity)
    {
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var selected = Invoke(world, "ExpansionSharedMaterialSource", actor, building, "wood") as InventoryLot;
        Assert.Equal(expectedId, selected?.Id);
        var pickup = ((InventoryLot? Source, int Quantity))Invoke(world, "ExpansionSharedMaterialPickup",
            actor, building, new ContentQuantity("wood", 20))!;
        Assert.Equal(expectedId, pickup.Source?.Id);
        Assert.Equal(expectedQuantity, pickup.Quantity);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var reload = Restore(WorldRuntimeCodec.Decode(before));
        Assert.Equal(before, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.Equal(selected?.Id, (Invoke(reload, "ExpansionSharedMaterialSource", actor, building, "wood") as InventoryLot)?.Id);
    }

    private static object? Invoke(WorldRuntime world, string name, params object[] arguments) =>
        typeof(WorldRuntime).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length &&
                method.GetParameters().Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(arguments[index])).All(match => match))
            .Invoke(world, arguments);

    private static WorldRuntime Restore(WorldRuntimeState state) =>
        WorldRuntime.Restore(state, _ => new Idle());

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == "safe_idle").ToArray() } }, cancellationToken);
    }
}
