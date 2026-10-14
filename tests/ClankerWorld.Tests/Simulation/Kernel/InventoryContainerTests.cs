using System.Text;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class InventoryContainerTests
{
    [Fact]
    public void PotStoresFoodAndSlowsSpoilageAcrossSmallTickRates()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "alpha", 1, 10_000, 10_000, 0),
            new InventoryLot("loose", "berries", "alpha", 2, 10_000, 10_000, 0),
        ]);
        inventory = InventoryAuthority.PutIntoContainer(inventory, "put", "alpha", "pot", "loose", 1);

        var firstTick = InventoryAuthority.ProcessSpoilage(inventory, 1, 1);
        var secondTick = InventoryAuthority.ProcessSpoilage(firstTick, 2, 1);

        Assert.Equal(9_999, firstTick.GetLot("loose").FreshnessBasisPoints);
        Assert.Equal(10_000, firstTick.Lots.Single(lot => lot.ContainerLotId == "pot").FreshnessBasisPoints);
        Assert.Equal(9_998, secondTick.GetLot("loose").FreshnessBasisPoints);
        Assert.Equal(9_999, secondTick.Lots.Single(lot => lot.ContainerLotId == "pot").FreshnessBasisPoints);
        Assert.Equal(1, secondTick.GetLot("pot").Quantity);
    }

    [Fact]
    public void ContainerTransferMovesItsContentsTogetherAndRejectsLotOnlyMovement()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "alpha", 1, 10_000, 10_000, 0),
            new InventoryLot("berries", "berries", "alpha", 5, 10_000, 10_000, 0),
        ]);
        inventory = InventoryAuthority.PutIntoContainer(inventory, "put", "alpha", "pot", "berries", 3);
        var content = inventory.Lots.Single(lot => lot.ContainerLotId == "pot");
        var beforeRejectedMove = InventoryCheckpointCodec.Encode(inventory);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(
            inventory, "peel", "alpha", "bravo", content.Id, 1, "supply", holdAccess: StockHoldTestState.Access(inventory, content.Id)));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(
            inventory, "split-vessel", "alpha", "bravo", "pot", 2, "supply", holdAccess: StockHoldTestState.Access(inventory, "pot")));
        Assert.Equal(beforeRejectedMove, InventoryCheckpointCodec.Encode(inventory));

        var moved = InventoryAuthority.Transfer(inventory, "move-pot", "alpha", "bravo", "pot", 1, "carry", holdAccess: StockHoldTestState.Access(inventory, "pot"));

        Assert.Equal("bravo", moved.GetLot("pot").OwnerId);
        Assert.Equal("bravo", moved.GetLot(content.Id).OwnerId);
        Assert.Equal("pot", moved.GetLot(content.Id).ContainerLotId);
        Assert.Equal(5, moved.Lots.Where(lot => lot.ItemKind == "berries").Sum(lot => lot.Quantity));
        Assert.Equal(InventoryDigest.State(moved), InventoryDigest.State(
            InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(moved))));
    }

    [Fact]
    public void BorrowedVesselMovesWithItsContentsAndCountsTowardItsCarriersLoad()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "household:a", 1, 10_000, 10_000, 0,
                StorageBuildingId: "house"),
            new InventoryLot("berries", "berries", "household:a", 2, 10_000, 10_000, 0,
                StorageBuildingId: "house", ContainerLotId: "pot"),
        ]);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(
            inventory, "peel", "berries", "household:a", 1, carrierId: "alpha", holdAccess: StockHoldTestState.Access(inventory, "berries")));
        var carried = InventoryAuthority.Relocate(inventory, "borrow", "pot", "household:a", 1, carrierId: "alpha", holdAccess: StockHoldTestState.Access(inventory, "pot"));
        Assert.Equal(new[] { new InventoryStorageChange("house", "berries", -2),
            new InventoryStorageChange("house", "storage_pot", -1) }, carried.Events[^1].StorageChanges);

        Assert.All(carried.Lots, lot =>
        {
            Assert.Equal("household:a", lot.OwnerId);
            Assert.Equal("alpha", lot.CarrierId);
            Assert.Null(lot.StorageBuildingId);
        });
        Assert.Equal(3, PersonalEquipmentRules.CarriedQuantity(carried, "alpha", null));

        // Goods taken out stay with the vessel's carrier until they are stored.
        var taken = InventoryAuthority.TakeFromContainer(carried, "take", "household:a", "household:a", "pot", "berries", 1, holdAccess: StockHoldTestState.Access(carried, "pot"));
        Assert.Equal("alpha", taken.Lots.Single(lot => lot.ItemKind == "berries" && lot.ContainerLotId is null).CarrierId);
        Assert.Equal(3, PersonalEquipmentRules.CarriedQuantity(taken, "alpha", null));

        // New contents join the vessel with its carrier, and a dropped vessel keeps them inside.
        var topped = InventoryAuthority.AddLot(carried, "more-berries", "berries", "household:a", 1, containerLotId: "pot", holdAccess: StockHoldTestState.Access(carried, "pot"));
        Assert.Equal("alpha", topped.GetLot("more-berries").CarrierId);
        var dropped = InventoryAuthority.DropCarrierGoods(topped, "alpha", new InventoryGroundPosition(3, 4), holdAccess: StockHoldTestState.Access(topped, "pot"));
        Assert.Equal(new InventoryGroundPosition(3, 4), dropped.GetLot("pot").GroundPosition);
        Assert.All(dropped.Lots.Where(lot => lot.ContainerLotId == "pot"), lot =>
        {
            Assert.Null(lot.GroundPosition);
            Assert.Null(lot.CarrierId);
        });

        var returned = InventoryAuthority.Relocate(carried, "return", "pot", "household:a", 1, storageBuildingId: "house", holdAccess: StockHoldTestState.Access(carried, "pot"));
        Assert.Equal(new[] { new InventoryStorageChange("house", "berries", 2),
            new InventoryStorageChange("house", "storage_pot", 1) }, returned.Events[^1].StorageChanges);
        Assert.Equal(InventoryCheckpointCodec.Encode(returned),
            InventoryCheckpointCodec.Encode(InventoryStorageHistory.RecordTransition(carried, returned)));
        Assert.All(returned.Lots, lot =>
        {
            Assert.Null(lot.CarrierId);
            Assert.Equal("house", lot.StorageBuildingId);
        });

        Assert.Throws<InvalidDataException>(() => InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "household:a", 1, 10_000, 10_000, 0,
                CarrierId: "alpha"),
            new InventoryLot("berries", "berries", "household:a", 2, 10_000, 10_000, 0, ContainerLotId: "pot"),
        ]));
    }

    [Fact]
    public void OwnersCollectingTheirOwnJugRecordNoSeparateCarrierAndCanStillFillIt()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("jug", InventoryContainerRules.WaterJug, "alpha", 1, 10_000, 10_000, 0,
                StorageBuildingId: "house"),
        ]);

        var collected = InventoryAuthority.Relocate(inventory, "collect", "jug", "alpha", 1, carrierId: "alpha", holdAccess: StockHoldTestState.Access(inventory, "jug"));
        Assert.Null(collected.GetLot("jug").CarrierId);
        Assert.Null(collected.GetLot("jug").StorageBuildingId);
        var filled = InventoryAuthority.AddLot(collected, "water", InventoryContainerRules.FreshWater, "alpha", 2,
            containerLotId: "jug", holdAccess: StockHoldTestState.Access(collected, "jug"));
        Assert.Equal(3, PersonalEquipmentRules.CarriedQuantity(filled, "alpha", null));
    }

    [Fact]
    public void GroundContainerTransfersKeepOneSharedPhysicalLocationForTheFamily()
    {
        var ground = new InventoryGroundPosition(8, 12);
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "alpha", 1, 10_000, 10_000, 0,
                GroundPosition: ground),
            new InventoryLot("berries", "berries", "alpha", 2, 10_000, 10_000, 0,
                GroundPosition: ground),
        ]);
        inventory = InventoryAuthority.PutIntoContainer(inventory, "put", "alpha", "pot", "berries", 1);
        var contained = Assert.Single(inventory.Lots, lot => lot.ContainerLotId == "pot");

        Assert.Equal(ground, inventory.GetLot("berries").GroundPosition);
        Assert.Null(contained.GroundPosition);
        var carried = InventoryAuthority.Transfer(inventory, "carry", "alpha", "bravo", "pot", 1, "pickup", holdAccess: StockHoldTestState.Access(inventory, "pot"));
        Assert.Null(carried.GetLot("pot").GroundPosition);
        Assert.Null(carried.GetLot(contained.Id).GroundPosition);
        Assert.Equal(ground, carried.GetLot("berries").GroundPosition);

        var dropped = InventoryAuthority.Transfer(carried, "drop", "bravo", "alpha", "pot", 1, "drop",
            destinationGroundPosition: ground, holdAccess: StockHoldTestState.Access(carried, "pot"));
        Assert.Equal(ground, dropped.GetLot("pot").GroundPosition);
        Assert.Null(dropped.GetLot(contained.Id).GroundPosition);
    }

    [Fact]
    public void ReservedJugContentsBlockVesselMovementAndCanBeConsumedWithoutLosingTheJug()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [new InventoryLot("jug", InventoryContainerRules.WaterJug, "alpha", 1, 10_000, 10_000, 0)]);
        inventory = InventoryAuthority.AddLot(inventory, "water", InventoryContainerRules.FreshWater, "alpha", 4,
            containerLotId: "jug", holdAccess: StockHoldTestState.Access(inventory, "jug"));
        inventory = InventoryAuthority.Reserve(inventory, "water-input", "alpha", "water", 2, "recipe", 10, holdAccess: StockHoldTestState.Access(inventory, "water"));
        var beforeRejectedMove = InventoryCheckpointCodec.Encode(inventory);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(
            inventory, "move-reserved-jug", "alpha", "bravo", "jug", 1, "carry", holdAccess: StockHoldTestState.Access(inventory, "jug")));
        Assert.Equal(beforeRejectedMove, InventoryCheckpointCodec.Encode(inventory));

        inventory = InventoryAuthority.ConsumeReservation(inventory, "water-input");
        Assert.Equal(1, inventory.GetLot("jug").Quantity);
        Assert.Equal(2, inventory.GetLot("water").Quantity);
        inventory = InventoryAuthority.Reserve(inventory, "last-water", "alpha", "water", 2, "recipe", 10, holdAccess: StockHoldTestState.Access(inventory, "water"));
        inventory = InventoryAuthority.ConsumeReservation(inventory, "last-water");

        Assert.Equal(1, inventory.GetLot("jug").Quantity);
        Assert.DoesNotContain(inventory.Lots, lot => lot.ItemKind == InventoryContainerRules.FreshWater);
        inventory = InventoryAuthority.AddLot(inventory, "refill", InventoryContainerRules.FreshWater, "alpha", 4,
            containerLotId: "jug", holdAccess: StockHoldTestState.Access(inventory, "jug"));
        Assert.Equal(4, inventory.GetLot("refill").Quantity);
    }

    [Fact]
    public void InvalidContainerRelationshipsAreRejectedByCheckpointAndSaveBoundaries()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("jug", InventoryContainerRules.WaterJug, "alpha", 1, 10_000, 10_000, 0),
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "alpha", 1, 10_000, 10_000, 0),
        ]);
        Assert.Throws<InvalidDataException>(() => InventoryAuthority.CreateGenesis(
            [new InventoryLot("loose-water", InventoryContainerRules.FreshWater, "alpha", 1, 10_000, 10_000, 0)]));
        Assert.Throws<InvalidDataException>(() => InventoryAuthority.AddLot(inventory, "bad-water", "fresh_water", "alpha", 1,
            containerLotId: "pot", holdAccess: StockHoldTestState.Access(inventory, "pot")));

        inventory = InventoryAuthority.AddLot(inventory, "water", InventoryContainerRules.FreshWater, "alpha", 1,
            containerLotId: "jug", holdAccess: StockHoldTestState.Access(inventory, "jug"));
        var invalidOwner = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "water" ? lot with { OwnerId = "bravo" } : lot).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => InventoryCheckpointCodec.Encode(invalidOwner));

        var validBytes = Encoding.UTF8.GetString(InventoryCheckpointCodec.Encode(inventory));
        var orphanedBytes = validBytes.Replace("\"containerLotId\":\"jug\"", "\"containerLotId\":\"missing-jug\"", StringComparison.Ordinal);
        Assert.NotEqual(validBytes, orphanedBytes);
        Assert.Throws<InvalidDataException>(() => InventoryCheckpointCodec.Decode(Encoding.UTF8.GetBytes(orphanedBytes)));
    }

    [Fact]
    public void VesselCapacityAndOwnerChecksRejectContainerChangesAtomically()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("pot", InventoryContainerRules.StoragePot, "alpha", 1, 10_000, 10_000, 0),
            new InventoryLot("too-much-food", "berries", "alpha", 9, 10_000, 10_000, 0),
            new InventoryLot("other-food", "berries", "bravo", 1, 10_000, 10_000, 0),
        ]);
        var before = InventoryCheckpointCodec.Encode(inventory);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.PutIntoContainer(
            inventory, "overfill", "alpha", "pot", "too-much-food", 9));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.PutIntoContainer(
            inventory, "wrong-owner", "bravo", "pot", "other-food", 1));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(inventory));
    }

    [Fact]
    public void BrokenVesselCannotSupplyOrAcceptContentsButCanStillMoveAsACompleteFamily()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [new InventoryLot("jug", InventoryContainerRules.WaterJug, "alpha", 1, 10_000, 10_000, 0)]);
        inventory = InventoryAuthority.AddLot(inventory, "water", InventoryContainerRules.FreshWater, "alpha", 2,
            containerLotId: "jug", holdAccess: StockHoldTestState.Access(inventory, "jug"));
        inventory = InventoryAuthority.Reserve(inventory, "input", "alpha", "water", 1, "recipe", 10, holdAccess: StockHoldTestState.Access(inventory, "water"));
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "jug" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        };
        var beforeRejectedOperations = InventoryCheckpointCodec.Encode(inventory);

        var released = InventoryAuthority.ReleaseReservation(inventory, "input");
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(
            released, "new-input", "alpha", "water", 1, "recipe", 10, holdAccess: StockHoldTestState.Access(released, "water")));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.ConsumeReservation(inventory, "input"));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.AddLot(
            inventory, "more-water", InventoryContainerRules.FreshWater, "alpha", 1, containerLotId: "jug", holdAccess: StockHoldTestState.Access(inventory, "jug")));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.TakeFromContainer(
            released, "take", "alpha", "alpha", "jug", "water", 1, holdAccess: StockHoldTestState.Access(released, "jug")));
        Assert.Equal(beforeRejectedOperations, InventoryCheckpointCodec.Encode(inventory));

        var moved = InventoryAuthority.Transfer(released, "move-broken-jug", "alpha", "bravo", "jug", 1, "repair", holdAccess: StockHoldTestState.Access(released, "jug"));
        Assert.Equal("bravo", moved.GetLot("jug").OwnerId);
        Assert.Equal(0, moved.GetLot("jug").ConditionBasisPoints);
        Assert.Equal("bravo", moved.GetLot("water").OwnerId);
        Assert.Equal(10_000, moved.GetLot("water").FreshnessBasisPoints);
        Assert.Equal(2, moved.GetLot("water").Quantity);
    }
}
