using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class WorldStorageOrderTests
{
    [Theory]
    [InlineData("crude_wooden_axe", "store my crude wooden axe")]
    [InlineData("crude_wooden_pickaxe", "store crude wooden pickaxes")]
    [InlineData("clothing", "store basic garments")]
    [InlineData("padded_coat", "store my padded coats at home")]
    [InlineData("basket", "please store baskets in my House now")]
    [InlineData("stone_pickaxe", "store stone pickaxes")]
    [InlineData("iron_knife", "store iron knives")]
    public async Task EquipmentStorageMovesTheExactUnwornGoodsAndPreservesTheirCondition(string kind, string text)
    {
        var state = Prepared();
        var actor = Actor(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "equipment-storage", kind, actor, 2, conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "equipment-other", "sack", actor, 1);
        using var world = Restore(WithInventory(state, inventory));
        var receipt = Submit(world, actor, "equipment", text);
        Assert.Equal("store_equipment", Order(world, receipt).Action);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var order = Order(world, receipt);
        Assert.Equal(("finished", 1, "storage_loads", kind),
            (order.Status, order.CompletedUnits, order.ProgressUnit, order.TargetEquipmentKind));
        Assert.Null(order.TargetMaterialKind);
        Assert.Null(order.TargetFoodKind);
        var lot = world.Society.Inventory.GetLot("equipment-storage");
        Assert.Equal((actor, House, 2, 3_000, (string?)null),
            (lot.OwnerId, lot.StorageBuildingId, lot.Quantity, lot.ConditionBasisPoints, lot.CarrierId));
        Assert.Null(world.Society.Inventory.GetLot("equipment-other").StorageBuildingId);
        var projected = Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Instructions,
            item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal(kind, projected.TargetEquipmentKind);
        Assert.Single(world.ExportState().Events, item => item.Kind == "personal_goods_stored");
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Theory]
    [InlineData("padded_coat")]
    [InlineData("basket")]
    public async Task EquipmentStorageLeavesWornGoodsWithTheirWearer(string kind)
    {
        var state = Prepared();
        var actor = Actor(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "equipment-worn", kind, actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "equipment-spare", kind, actor, 1);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Equipment = kind == "basket" ? new(CarryAidLotId: "equipment-worn") : new(ClothingLotId: "equipment-worn"),
            } : person).ToArray(),
        };
        using var world = Restore(state);
        var receipt = Submit(world, actor, "worn", $"store two {kind.Replace('_', ' ')}");
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(("doing", 1, "equipment_items"),
            (Order(world, receipt).Status, Order(world, receipt).CompletedUnits, Order(world, receipt).ProgressUnit));
        Assert.Equal(House, world.Society.Inventory.GetLot("equipment-spare").StorageBuildingId);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("blocked", Order(world, receipt).Status);
        Assert.Contains("not worn", Order(world, receipt).BlockedReason, StringComparison.Ordinal);
        Assert.True(PersonalEquipmentRules.IsCarried(world.Society.Inventory.GetLot("equipment-worn"), actor));
        Assert.True(PersonalEquipmentRules.IsSelected(world.Agents.Single(person => person.AgentId == actor).Equipment, "equipment-worn"));
        using var restored = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(1, Order(restored, receipt).CompletedUnits);
    }

    [Theory]
    [InlineData("store tools")]
    [InlineData("store stone sword")]
    [InlineData("store stone hoe")]
    [InlineData("store basket and sack")]
    [InlineData("store basket in the Warehouse")]
    [InlineData("store basket at another House")]
    public void EquipmentStorageRejectsUnsupportedTargetsWithoutReplacingTheCurrentOrder(string text)
    {
        var state = Prepared();
        using var world = Restore(state);
        var current = Submit(world, Actor(state), "current-equipment", "keep storing baskets until cancelled");
        var rejected = Submit(world, Actor(state), "unsupported-equipment", text);
        Assert.Equal("not_understood", Order(world, rejected).Status);
        Assert.Equal("waiting", Order(world, current).Status);
        world.Validate();
    }

    [Fact]
    public void EquipmentStorageRefusesInvalidSavedTargetsAndProgress()
    {
        var state = Prepared();
        using var world = Restore(state);
        var receipt = Submit(world, Actor(state), "equipment-validation", "store two stone pickaxes");
        var saved = world.ExportState();
        var valid = Order(world, receipt);
        Assert.Equal("store_equipment", valid.Action);
        foreach (var invalid in new OrderSaveChange[]
{
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetEquipmentKind"] = null }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetEquipmentKind"] = "stone_hoe" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetFoodKind"] = "berries" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetMaterialKind"] = "stone" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetCropKind"] = "greens" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetResourceId"] = "stone" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["TargetPosition"] = new GridPoint(10_000_001, 1) }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["RequestedUnits"] = 0 }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["CompletedUnits"] = -1 }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["ProgressUnit"] = "collection_loads" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["ProgressUnit"] = "material_items" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["LastEffectId"] = "store:personal:unearned" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["QuantityIsExplicit"] = false, ["ProgressUnit"] = "storage_loads" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["CompletedUnits"] = 1, ["LastEffectId"] = "collect:personal:wrong-action" })
})
            Assert.Throws<InvalidDataException>(() => OrderSaveMutation.Decode(saved, invalid));
    }
}
