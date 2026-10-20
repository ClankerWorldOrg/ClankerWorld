using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class InventoryCustodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingAWholeRemainderDoesNotRequireAnUnusedSplitIdentity(bool reserved)
    {
        var inventory = InventoryAuthority.CreateGenesis(
            [new("stone", "stone", "adult", 8, 7_000, 9_000, 0)]);
        var split = InventoryAuthority.Relocate(inventory, "store", "stone", "adult", 4, storageBuildingId: "house");
        if (reserved)
            split = InventoryAuthority.Reserve(split, "held", "adult", "stone", 2, "work", 20);
        var before = InventoryCheckpointCodec.Encode(split);
        // Another partial move really would reuse the sibling's identity.
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(split,
            "store", "stone", "adult", 2, storageBuildingId: "house"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(split));
        if (reserved)
        {
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(split,
                "store", "stone", "adult", 4, storageBuildingId: "house"));
            Assert.Equal(before, InventoryCheckpointCodec.Encode(split));
            Assert.Equal(InventoryReservationState.Reserved, split.GetReservation("held").State);
            return;
        }
        var moved = InventoryAuthority.Relocate(split, "store", "stone", "adult", 4, storageBuildingId: "house");
        Assert.Equal(8, moved.Lots.Sum(lot => lot.Quantity));
        Assert.Equal(split.Lots.Select(lot => lot.Id), moved.Lots.Select(lot => lot.Id));
        Assert.All(moved.Lots, lot => Assert.Equal(("adult", "house", 7_000, 9_000),
            (lot.OwnerId, lot.StorageBuildingId, lot.ConditionBasisPoints, lot.FreshnessBasisPoints)));
        Assert.Equal(split.GetLot("stone").ProvenanceLotId, moved.GetLot("stone").ProvenanceLotId);
        Assert.Equal(new InventoryStorageChange("house", "stone", 4), Assert.Single(moved.Events[^1].StorageChanges!));
        var saved = InventoryCheckpointCodec.Encode(moved);
        Assert.Equal(saved, InventoryCheckpointCodec.Encode(InventoryCheckpointCodec.Decode(saved)));
    }

    [Fact]
    public void SplittingOrChangingTheOwnerOfStockInPlaceCreatesNoPhysicalStorageChange()
    {
        var inventory = InventoryAuthority.AddLot(InventoryAuthority.CreateGenesis([]), "wood", "wood", "first", 4,
            storageBuildingId: "house");
        Assert.Equal(new InventoryStorageChange("house", "wood", 4), Assert.Single(inventory.Events[^1].StorageChanges!));
        var split = InventoryAuthority.SplitLot(inventory, "wood", 1, "split-wood", holdAccess: StockHoldTestState.Access(inventory, "wood"));
        Assert.Null(split.Events[^1].StorageChanges);
        var transferred = InventoryAuthority.Transfer(split, "gift", "first", "second", "split-wood", 1,
            "gift", destinationStorageBuildingId: "house", holdAccess: StockHoldTestState.Access(split, "split-wood"));
        Assert.Null(transferred.Events[^1].StorageChanges);
        Assert.Equal(4, transferred.Lots.Sum(lot => lot.Quantity));
        Assert.Equal("second", transferred.GetLot("split-wood").OwnerId);
        Assert.Equal("house", transferred.GetLot("split-wood").StorageBuildingId);
        Assert.Equal(InventoryDigest.Events(transferred.Events), InventoryDigest.Events(
            InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(transferred)).Events));
    }

    [Fact]
    public void ABorrowedBrokenAxeCanBeReturnedWithoutBecomingPersonalProperty()
    {
        var inventory = InventoryAuthority.CreateGenesis(
            [new("axe", "stone_axe", "household", 1, 0, 10_000, 0, CarrierId: "departing-adult")]);
        var before = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(inventory, "take", "axe",
            "departing-adult", 1, carrierId: "departing-adult", holdAccess: StockHoldTestState.Access(inventory, "axe")));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(inventory));
        var returned = InventoryAuthority.Relocate(inventory, "return", "axe", "household", 1, storageBuildingId: "old-house", holdAccess: StockHoldTestState.Access(inventory, "axe"));
        Assert.Equal(("household", "old-house", 0),
            (returned.GetLot("axe").OwnerId, returned.GetLot("axe").StorageBuildingId, returned.GetLot("axe").ConditionBasisPoints));
        Assert.Null(returned.GetLot("axe").CarrierId);
    }

    [Fact]
    public void APartialPickupLeavesReservedStockAtItsSourceAndConservesQuantity()
    {
        var inventory = InventoryAuthority.CreateGenesis(
            [new("food", "food", "household", 6, 10_000, 7_000, 0, StorageBuildingId: "house")]);
        inventory = InventoryAuthority.Reserve(inventory, "care", "household", "food", 2, "child_food", 20, holdAccess: StockHoldTestState.Access(inventory, "food"));
        Assert.Null(inventory.Events[^1].StorageChanges);
        var before = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(inventory, "too-much", "food",
            "household", 5, carrierId: "adult", holdAccess: StockHoldTestState.Access(inventory, "food")));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(inventory));
        var moved = InventoryAuthority.Relocate(inventory, "pickup", "food", "household", 4, carrierId: "adult", holdAccess: StockHoldTestState.Access(inventory, "food"));
        var remaining = moved.GetLot("food");
        var carried = Assert.Single(moved.Lots, lot => lot.CarrierId == "adult");
        var pickup = moved.Events[^1];
        Assert.Equal(inventory.EventHistoryFloor + inventory.Events.Count + 1, pickup.EventId);
        Assert.Equal("inventory_relocated", pickup.Kind);
        Assert.Equal(new InventoryStorageChange("house", "food", -4), Assert.Single(pickup.StorageChanges!));
        Assert.Equal((2, "house", "household"), (remaining.Quantity, remaining.StorageBuildingId, remaining.OwnerId));
        Assert.Equal((4, "household", "food", 7_000),
            (carried.Quantity, carried.OwnerId, carried.ProvenanceLotId, carried.FreshnessBasisPoints));
        Assert.Equal(6, moved.Lots.Sum(lot => lot.Quantity));
        Assert.Equal("food", moved.GetReservation("care").LotId);
        Assert.Equal(InventoryReservationState.Reserved, moved.GetReservation("care").State);
        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(moved));
        var fed = InventoryAuthority.ConsumeReservation(restored, "care");
        Assert.Equal(pickup.StorageChanges, restored.Events[^1].StorageChanges);
        Assert.Equal(new InventoryStorageChange("house", "food", -2), Assert.Single(fed.Events[^1].StorageChanges!));
        Assert.Equal(4, fed.Lots.Sum(lot => lot.Quantity));
        Assert.Equal(carried, fed.GetLot(carried.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnershipTransferClearsThePreviousCarrierAndPreservesAnyRemainder(bool partial)
    {
        var inventory = InventoryAuthority.CreateGenesis(
            [new("wood", "wood", "household", 3, 6_000, 10_000, 0, CarrierId: "borrower")]);
        var donated = InventoryAuthority.Transfer(inventory, "donation", "household", "recipient", "wood",
            partial ? 1 : 3, "donation", destinationStorageBuildingId: "recipient-house", holdAccess: StockHoldTestState.Access(inventory, "wood"));
        var received = Assert.Single(donated.Lots, lot => lot.OwnerId == "recipient");
        Assert.Null(received.CarrierId);
        Assert.Equal("recipient-house", received.StorageBuildingId);
        Assert.Equal(6_000, received.ConditionBasisPoints);
        Assert.Equal(3, donated.Lots.Sum(lot => lot.Quantity));
        if (partial)
        {
            var remainder = donated.GetLot("wood");
            Assert.Equal(("household", "borrower", 2), (remainder.OwnerId, remainder.CarrierId, remainder.Quantity));
        }
    }

    [Theory]
    [InlineData("adult", "house", false)]
    [InlineData("adult", null, true)]
    [InlineData(null, "house", true)]
    [InlineData(" adult ", null, false)]
    [InlineData("", null, false)]
    [InlineData(null, " house ", false)]
    public void InvalidDestinationCannotPublishAConflictingOrMalformedLocation(string? carrier,
        string? storage, bool onGround)
    {
        var inventory = InventoryAuthority.CreateGenesis(
            [new("coat", "clothing", "adult", 1, 10_000, 10_000, 0, StorageBuildingId: "source")]);
        var before = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidDataException>(() => InventoryAuthority.Relocate(inventory, "invalid", "coat", "adult", 1,
            carrierId: carrier, storageBuildingId: storage, groundPosition: onGround ? new(2, 3) : null, holdAccess: StockHoldTestState.Access(inventory, "coat")));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(inventory));
    }
}
