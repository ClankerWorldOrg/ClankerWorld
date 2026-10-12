using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CoLocatedHandcartUnloadTests
{
    private static readonly Lazy<byte[]> Generated = new(() =>
    {
        using var world = NormalPathWorld.CreateGenerated("co-located-cart-audit", new MarketRulesPolicy().CreateProvider);
        world.AdvanceOneTickAsync().AsTask().GetAwaiter().GetResult();
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

    [Theory]
    [InlineData(true, "ground", 4)]
    [InlineData(false, "ground", 4)]
    [InlineData(true, "carried", 1)]
    [InlineData(true, "reserved", 0)]
    [InlineData(true, "foreign", 0)]
    [InlineData(true, "distant", 0)]
    public async Task UnloadUsesTheReachedCargosOwnCartRegardlessOfAnEmptyCartSortingFirst(bool emptyFirst, string mode, int unloaded)
    {
        var state = WorldRuntimeCodec.Decode(Generated.Value);
        var actor = state.Agents[0].AgentId;
        var household = state.Society.Society.GetAgent(actor).HouseholdId;
        var position = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house")).Position;
        var loadedPosition = mode == "distant" ? state.Map.FootNeighbors(position).First() : position;
        var owner = mode == "foreign" ? state.Agents[1].AgentId : actor;
        var emptyId = emptyFirst ? "a-empty-cart" : "z-empty-cart";
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, emptyId, "handcart", actor, 1, groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "m-loaded-cart", "handcart", owner, 1,
            groundPosition: new(loadedPosition.X, loadedPosition.Y));
        inventory = InventoryAuthority.AddLot(inventory, "cart-wood", "wood", owner, 4, containerLotId: "m-loaded-cart");
        if (mode == "carried") inventory = InventoryAuthority.AddLot(inventory, "ballast", "fiber", actor, 7);
        if (mode == "reserved") inventory = InventoryAuthority.Reserve(inventory, "held-cargo", owner, "cart-wood", 1,
            "pending_trade", inventory.WorldTick + 1_000);
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.ItemKind == "handcart" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray() };
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? position : person.Position,
                FullnessBasisPoints = 9_500,
                Equipment = null,
                Project = null,
                Survival = new(),
                LastDecisionContext = null,
            }).ToArray(),
        };
        var target = mode == "carried" ? "unload_handcart:cart-wood" : "unload_handcart_ground:cart-wood";
        var choices = Choices(actor, target);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), choices.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Choices(actor, target).CreateProvider);
        world.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            world.Validate();
            replay.Validate();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var cargo = world.Society.Inventory.Lots.Where(lot => lot.Id == "cart-wood" || lot.ProvenanceLotId == "cart-wood").ToArray();
        Assert.Equal(4, cargo.Sum(lot => lot.Quantity));
        Assert.All(cargo, lot => Assert.Equal(owner, lot.OwnerId));
        Assert.Equal(4 - unloaded, cargo.Where(lot => lot.ContainerLotId == "m-loaded-cart").Sum(lot => lot.Quantity));
        Assert.Equal(unloaded > 0, choices.OfferedTo(actor).Any(candidate => candidate.Id == target));
        Assert.Equal(unloaded > 0 ? 1 : 0, world.ExportState().Events.Count(item => item.Kind == "handcart_unloaded"));
        Assert.Equal(mode == "carried" ? unloaded : 0, cargo.Where(lot => PersonalEquipmentRules.IsCarried(lot, actor)).Sum(lot => lot.Quantity));
        Assert.Equal(mode == "carried" ? 0 : unloaded, cargo.Where(lot => lot.GroundPosition == new InventoryGroundPosition(position.X, position.Y)).Sum(lot => lot.Quantity));
        Assert.All(world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "handcart"), cart => Assert.Equal(0, cart.ConditionBasisPoints));
        Assert.Empty(world.ExportState().HandcartHitches!);
        if (mode == "reserved") Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("held-cargo").State);
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static MarketRulesPolicy Choices(string actor, string target) => new()
    {
        Choose = (id, candidates) => id == actor
            ? candidates.FirstOrDefault(candidate => candidate.Id == target) ?? candidates.Single(candidate => candidate.Id == "safe_idle")
            : candidates.Single(candidate => candidate.Id == "safe_idle"),
    };
}
