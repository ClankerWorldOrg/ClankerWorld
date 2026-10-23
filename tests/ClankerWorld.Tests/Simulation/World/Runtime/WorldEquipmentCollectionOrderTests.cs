using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class WorldCollectionOrderTests
{
    [Theory]
    [InlineData("crude_wooden_axe")]
    [InlineData("crude_wooden_pickaxe")]
    [InlineData("clothing")]
    [InlineData("padded_coat")]
    [InlineData("rain_cloak")]
    [InlineData("basket")]
    [InlineData("sack")]
    [InlineData("wooden_axe")]
    [InlineData("stone_axe")]
    [InlineData("iron_axe")]
    [InlineData("stone_pickaxe")]
    [InlineData("iron_pickaxe")]
    [InlineData("wooden_hoe")]
    [InlineData("iron_hoe")]
    [InlineData("stone_hammer")]
    [InlineData("wooden_hammer")]
    [InlineData("wooden_sickle")]
    [InlineData("iron_sickle")]
    [InlineData("wooden_pickaxe")]
    [InlineData("iron_knife")]
    public async Task EquipmentCollectionMovesTheExactKindWithoutEquippingOrRepairing(string kind)
    {
        var state = Prepared();
        var actor = Actor(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "equipment-source", kind, actor, 2, conditionBasisPoints: 3_000, storageBuildingId: House);
        using var world = Restore(WithInventory(state, inventory));
        var receipt = Submit(world, actor, "equipment", $"collect my {kind.Replace('_', ' ')}");
        Assert.Equal("collect_equipment", Order(world, receipt).Action);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var order = Order(world, receipt);
        Assert.Equal(("finished", 1, "collection_loads", kind),
            (order.Status, order.CompletedUnits, order.ProgressUnit, order.TargetEquipmentKind));
        Assert.Null(order.TargetMaterialKind);
        Assert.Null(order.TargetFoodKind);
        var lot = world.Society.Inventory.GetLot("equipment-source");
        Assert.Equal((actor, 2, 3_000), (lot.OwnerId, lot.Quantity, lot.ConditionBasisPoints));
        Assert.True(PersonalEquipmentRules.IsCarried(lot, actor));
        Assert.Null(world.Agents.Single(person => person.AgentId == actor).Equipment);
        var projected = Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Instructions,
            item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal(kind, projected.TargetEquipmentKind);
        Assert.Single(world.ExportState().Events, item => item.Kind == "personal_goods_collected");
        using var restored = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(order, Order(restored, receipt));
        restored.Validate();
    }

    [Theory]
    [InlineData("collect equipment")]
    [InlineData("collect tools")]
    [InlineData("collect axe")]
    [InlineData("collect stone sword")]
    [InlineData("collect stone hoe")]
    [InlineData("collect basket and sack")]
    [InlineData("collect basket from the Warehouse")]
    [InlineData("collect 0 baskets")]
    [InlineData("collect 1.5 sacks")]
    public void EquipmentCollectionRejectsUnsupportedSubjectsWithoutReplacingTheCurrentOrder(string text)
    {
        var state = Prepared();
        using var world = Restore(state);
        var current = Submit(world, Actor(state), "equipment-current", "keep collecting baskets until cancelled");
        var rejected = Submit(world, Actor(state), "equipment-unsupported", text);
        Assert.Equal("waiting", Order(world, current).Status);
        Assert.Equal("not_understood", Order(world, rejected).Status);
        world.Validate();
    }

    [Fact]
    public void EquipmentCollectionRefusesInvalidSavedTargetsAndProgress()
    {
        var state = Prepared();
        using var world = Restore(state);
        var receipt = Submit(world, Actor(state), "equipment-validation", "collect two padded coats");
        var saved = world.ExportState();
        var valid = Order(world, receipt);
        Assert.Equal("collect_equipment", valid.Action);
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
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["ProgressUnit"] = "repairs" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["ProgressUnit"] = "material_items" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["LastEffectId"] = "collect:personal:unearned" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["QuantityIsExplicit"] = false, ["ProgressUnit"] = "collection_loads" }),
    new OrderSaveChange(valid, new Dictionary<string, object?> { ["CompletedUnits"] = 1, ["LastEffectId"] = "repair:tool:wrong-action" })
})
            Assert.Throws<InvalidDataException>(() => OrderSaveMutation.Decode(saved, invalid));
    }
}
