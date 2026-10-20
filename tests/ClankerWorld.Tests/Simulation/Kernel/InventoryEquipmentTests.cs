using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class InventoryEquipmentTests
{
    [Fact]
    public void IndividualWearAndBrokenRepairKeepTheOtherUnitAndSpendExactInputsAcrossReload()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new("clothing", "clothing", "owner", 2, 10_000, 10_000, 0),
            new("cloth", "cloth", "owner", 3, 10_000, 10_000, 0),
        ]);
        var unchanged = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.WearSingleUnit(inventory, "clothing", 100));
        Assert.Equal(unchanged, InventoryCheckpointCodec.Encode(inventory));
        inventory = InventoryAuthority.SplitLot(inventory, "clothing", 1, "worn-garment");
        inventory = InventoryAuthority.WearSingleUnit(inventory, "worn-garment", 10_000);
        Assert.Equal(10_000, inventory.GetLot("clothing").ConditionBasisPoints);
        Assert.Equal(0, inventory.GetLot("worn-garment").ConditionBasisPoints);
        Assert.Equal(2, inventory.Lots.Where(lot => lot.ItemKind == "clothing").Sum(lot => lot.Quantity));
        inventory = InventoryAuthority.Reserve(inventory, "repair-cloth", "owner", "cloth", 1, "equipment_repair", 10);
        inventory = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(inventory));
        var repaired = InventoryAuthority.RepairSingleUnit(inventory, "worn-garment", 4_000, [Identity.Parse("repair-cloth")]);
        Assert.Equal(4_000, repaired.GetLot("worn-garment").ConditionBasisPoints);
        Assert.Equal(10_000, repaired.GetLot("clothing").ConditionBasisPoints);
        Assert.Equal(2, repaired.GetLot("cloth").Quantity);
        Assert.Equal(InventoryReservationState.Completed, repaired.GetReservation("repair-cloth").State);
        Assert.Equal(InventoryCheckpointCodec.Encode(repaired),
            InventoryCheckpointCodec.Encode(InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(repaired))));
    }

    [Theory]
    [InlineData("outsider", "equipment_repair", 10, false)]
    [InlineData("owner", "production", 10, false)]
    [InlineData("owner", "equipment_repair", 0, true)]
    public void WrongOwnerPurposeOrExpiredInputsCannotRepairOrSpendStock(string materialOwner, string purpose,
        long expiry, bool advance)
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new("gear", "rain_cloak", "owner", 1, 300, 10_000, 0),
            new("cloth", "cloth", materialOwner, 2, 10_000, 10_000, 0),
        ]);
        inventory = InventoryAuthority.Reserve(inventory, "input", materialOwner, "cloth", 1, purpose, expiry);
        if (advance) inventory = inventory with { WorldTick = 1 };
        var bytes = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.RepairSingleUnit(inventory, "gear", 5_000, [Identity.Parse("input")]));
        Assert.Equal(bytes, InventoryCheckpointCodec.Encode(inventory));
    }

    [Fact]
    public void AFailedSecondConsumptionCannotSpendTheFirstRepairInput()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new("gear", "basket", "owner", 1, 500, 10_000, 0),
            new("a-cloth", "cloth", "owner", 1, 10_000, 10_000, 0),
            new("b-rope", "rope", "owner", 1, 10_000, 10_000, 0),
        ]);
        inventory = InventoryAuthority.Reserve(inventory, "a-input", "owner", "a-cloth", 1, "equipment_repair", 10);
        inventory = InventoryAuthority.Reserve(inventory, "b-input", "owner", "b-rope", 1, "equipment_repair", 10);
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "b-rope" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        };
        var bytes = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.RepairSingleUnit(inventory, "gear", 5_000, [Identity.Parse("a-input"), Identity.Parse("b-input")]));
        Assert.Equal(bytes, InventoryCheckpointCodec.Encode(inventory));
        Assert.Equal(InventoryReservationState.Reserved, inventory.GetReservation("a-input").State);
    }
}
