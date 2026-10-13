using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests.Simulation.Kernel;

public sealed class InventoryHoldTests
{
    [Theory]
    [InlineData(InventoryHoldPurpose.MarketStall)]
    [InlineData(InventoryHoldPurpose.Delivery)]
    [InlineData(InventoryHoldPurpose.SupplyTrip)]
    [InlineData(InventoryHoldPurpose.Borrowed)]
    public void HoldsRequireTheExactPurposeAndHolderForMovementAndConsumption(InventoryHoldPurpose purpose)
    {
        var holder = new string('h', 128);
        var hold = new InventoryHold(purpose, holder);
        var held = InventoryAuthority.HoldStock(Stock(), "grain", hold);
        var before = InventoryCheckpointCodec.Encode(held);
        IReadOnlyList<InventoryHold>[] refusals = [[], [hold with { HolderId = "other" }],
            [hold with { Purpose = (InventoryHoldPurpose)(((int)purpose + 1) % 4) }]];
        foreach (var access in refusals)
        {
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(held, "move", "grain", "owner", 2, holdAccess: access));
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(held, "give", "owner", "other", "grain", 2, "gift", holdAccess: access));
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(held, "input", "owner", "grain", 2, "meal", 10, holdAccess: access));
            Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Discard(held, "owner", "grain", 2, access));
        }
        Assert.Equal(before, InventoryCheckpointCodec.Encode(held));
        var restored = InventoryCheckpointCodec.Decode(before);
        Assert.Equal(before, InventoryCheckpointCodec.Encode(restored));
        var moved = InventoryAuthority.Relocate(restored, "move", "grain", "owner", 2, holdAccess: [hold]);
        Assert.Equal(hold, Assert.Single(moved.GetLot("grain").Holds!));
        Assert.Equal(hold, Assert.Single(moved.GetLot("grain#move:move").Holds!));
        var reserved = InventoryAuthority.Reserve(restored, "input", "owner", "grain", 2, "meal", 10, holdAccess: [hold]);
        var consumed = InventoryAuthority.ConsumeReservation(reserved, "input");
        Assert.Equal(2, consumed.GetLot("grain").Quantity);
        Assert.Equal(InventoryCheckpointCodec.Encode(consumed), InventoryCheckpointCodec.Encode(
            InventoryAuthority.ConsumeReservation(InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(reserved)), "input")));
        Assert.Equal(2, InventoryAuthority.Transfer(restored, "give", "owner", "other", "grain", 2, "gift", holdAccess: [hold])
            .GetLot("grain#transfer:give").Quantity);
        Assert.Null(InventoryAuthority.ReleaseStock(restored, "grain", hold).GetLot("grain").Holds);
    }

    [Fact]
    public void VesselAndContentsProtectEachOtherAndEveryHoldMustMatch()
    {
        var stock = InventoryAuthority.CreateGenesis([
            new("jug", "water_jug", "owner", 1, 10000, 10000, 0),
            new("water", "fresh_water", "owner", 4, 10000, 10000, 0, ContainerLotId: "jug")]);
        var delivery = new InventoryHold(InventoryHoldPurpose.Delivery, "house");
        var trip = new InventoryHold(InventoryHoldPurpose.SupplyTrip, "person");
        var held = InventoryAuthority.HoldStock(InventoryAuthority.HoldStock(stock, "jug", delivery), "water", trip);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(held, "move", "jug", "owner", 1, holdAccess: [delivery]));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(held, "drink", "owner", "water", 1, "drink", 10, holdAccess: [trip]));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.TakeFromContainer(held, "take", "owner", "owner", "jug", "water", 1));
        var moved = InventoryAuthority.Relocate(held, "move", "jug", "owner", 1, carrierId: "person", holdAccess: [delivery, trip]);
        Assert.Equal("person", moved.GetLot("water").CarrierId);
        var reserved = InventoryAuthority.Reserve(held, "drink", "owner", "water", 1, "drink", 10, holdAccess: [delivery, trip]);
        Assert.Equal(3, InventoryAuthority.ConsumeReservation(reserved, "drink").GetLot("water").Quantity);
    }

    [Fact]
    public void ANewPromiseCannotBeBypassedByAnEarlierReservation()
    {
        var reserved = InventoryAuthority.Reserve(Stock(), "input", "owner", "grain", 2, "meal", 10);
        var held = InventoryAuthority.HoldStock(reserved, "grain", new(InventoryHoldPurpose.Delivery, "house"));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.ConsumeReservation(held, "input"));
    }

    private static InventoryCheckpoint Stock() => InventoryAuthority.CreateGenesis([
        new("grain", "grain", "owner", 4, 10000, 10000, 0)]);
}
