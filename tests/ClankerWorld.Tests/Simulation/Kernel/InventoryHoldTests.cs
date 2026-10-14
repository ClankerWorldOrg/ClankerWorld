using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using System.Text;
using System.Text.Json.Nodes;

namespace ClankerWorld.Tests.Simulation.Kernel;

public sealed class InventoryHoldTests
{
    private static readonly Lazy<byte[]> NativeWorld = new(() =>
    {
        using var world = NormalPathWorld.CreateGenerated("native-stock-hold-save", _ => new DeterministicDecisionProvider());
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

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

    [Theory]
    [InlineData("storage_pot", false)]
    [InlineData("storage_pot", true)]
    [InlineData("water_jug", false)]
    [InlineData("water_jug", true)]
    [InlineData("handcart", false)]
    [InlineData("handcart", true)]
    public void VesselWearAndRepairRequireTheContainedStocksAuthorization(string kind, bool repair)
    {
        var stock = VesselStock(kind);
        if (repair)
        {
            stock = InventoryAuthority.WearSingleUnit(stock, "vessel", 5_000);
            stock = InventoryAuthority.AddLot(stock, "repair-wood", "wood", "owner", 1);
            stock = InventoryAuthority.Reserve(stock, "repair-input", "owner", "repair-wood", 1, "equipment_repair", 10);
        }
        var hold = new InventoryHold(InventoryHoldPurpose.Delivery, "house");
        stock = InventoryAuthority.HoldStock(stock, "contents", hold);
        var before = InventoryCheckpointCodec.Encode(stock);
        InventoryCheckpoint Apply(IReadOnlyList<InventoryHold> access) => repair
            ? InventoryAuthority.RepairSingleUnit(stock, "vessel", 1_000, [stock.GetReservation("repair-input").Id], access)
            : InventoryAuthority.WearSingleUnit(stock, "vessel", 10_000, access);
        Assert.Throws<InvalidOperationException>(() => Apply([]));
        Assert.Throws<InvalidOperationException>(() => Apply([hold with { HolderId = "other" }]));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(stock));
        var authorized = Apply([hold]);
        Assert.Equal(repair ? 6_000 : 0, authorized.GetLot("vessel").ConditionBasisPoints);
        Assert.Equal(4, authorized.GetLot("contents").Quantity);
        Assert.Equal(hold, Assert.Single(authorized.GetLot("contents").Holds!));
        if (repair)
        {
            Assert.DoesNotContain(authorized.Lots, lot => lot.Id == "repair-wood");
            Assert.Equal(InventoryReservationState.Completed, authorized.GetReservation("repair-input").State);
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    public void DetachedContentsKeepVesselAndOwnHoldsUntilEachIsReleased(bool handcart, int quantity)
    {
        var rootHold = new InventoryHold(InventoryHoldPurpose.Delivery, "house");
        var childHold = new InventoryHold(InventoryHoldPurpose.Borrowed, "loan");
        var stock = InventoryAuthority.HoldStock(VesselStock(handcart ? "handcart" : "storage_pot"), "vessel", rootHold);
        stock = InventoryAuthority.HoldStock(stock, "contents", childHold);
        var detached = handcart
            ? InventoryAuthority.UnloadHandcart(stock, "take", "owner", "vessel", "contents", quantity, holdAccess: [rootHold, childHold])
            : InventoryAuthority.TakeFromContainer(stock, "take", "owner", "owner", "vessel", "contents", quantity, holdAccess: [rootHold, childHold]);
        var taken = Assert.Single(detached.Lots, lot => lot.ItemKind == "berries" && lot.ContainerLotId is null);
        Assert.Equal(quantity, taken.Quantity);
        Assert.Equal("owner", taken.OwnerId);
        Assert.Equal(new[] { rootHold, childHold }, taken.Holds);
        Assert.Equal(4, detached.Lots.Where(lot => lot.ItemKind == "berries").Sum(lot => lot.Quantity));
        var saved = InventoryCheckpointCodec.Encode(detached);
        var restored = InventoryCheckpointCodec.Decode(saved);
        Assert.Equal(saved, InventoryCheckpointCodec.Encode(restored));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(restored, "missing-root", "owner", taken.Id, 1, "meal", 10, holdAccess: [childHold]));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(restored, "missing-child", "owner", taken.Id, 1, "meal", 10, holdAccess: [rootHold]));
        Assert.Equal(saved, InventoryCheckpointCodec.Encode(restored));
        var reserved = InventoryAuthority.Reserve(restored, "authorized", "owner", taken.Id, 1, "meal", 10, holdAccess: [rootHold, childHold]);
        Assert.Equal(3, InventoryAuthority.ConsumeReservation(reserved, "authorized").Lots.Where(lot => lot.ItemKind == "berries").Sum(lot => lot.Quantity));
        var released = InventoryAuthority.ReleaseStock(restored, taken.Id, rootHold);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(released, "still-held", "owner", taken.Id, 1, "meal", 10));
        released = InventoryAuthority.ReleaseStock(released, taken.Id, childHold);
        Assert.Equal(InventoryReservationState.Reserved, InventoryAuthority.Reserve(released, "free", "owner", taken.Id, 1, "meal", 10).GetReservation("free").State);
        if (quantity < 4)
        {
            var remainder = restored.GetLot("contents");
            Assert.Equal("vessel", remainder.ContainerLotId);
            Assert.Equal(childHold, Assert.Single(remainder.Holds!));
            Assert.Equal(rootHold, Assert.Single(restored.GetLot("vessel").Holds!));
        }
    }

    [Theory]
    [InlineData(InventoryHoldPurpose.MarketStall)]
    [InlineData(InventoryHoldPurpose.Delivery)]
    [InlineData(InventoryHoldPurpose.SupplyTrip)]
    [InlineData(InventoryHoldPurpose.Borrowed)]
    public async Task HoldsAndReservationAuthorizationSurviveStrictNativeWorldReload(InventoryHoldPurpose purpose)
    {
        WorldRuntimeState state;
        string? supplyYard = null;
        if (purpose == InventoryHoldPurpose.SupplyTrip)
        {
            var paid = AnimalPipelineTests.CreateYard("native-stock-hold-trip");
            state = paid.State;
            supplyYard = paid.Yard.InstanceId;
        }
        else state = purpose == InventoryHoldPurpose.MarketStall
            ? await PaidMarketWorld.StateAsync() : WorldRuntimeCodec.Decode(NativeWorld.Value);
        const string household = "household:first";
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        if (purpose == InventoryHoldPurpose.MarketStall)
            state = PaidMarketWorld.Borrowing(state, actor, 0);
        var holder = purpose == InventoryHoldPurpose.Delivery ? "first-town-house-a" :
            purpose == InventoryHoldPurpose.MarketStall ? Assert.Single(PaidMarketWorld.Market(state).Occupancies).Id : actor;
        var hold = new InventoryHold(purpose, holder);
        var owner = purpose is InventoryHoldPurpose.SupplyTrip or InventoryHoldPurpose.Delivery ? actor : household;
        var stall = purpose == InventoryHoldPurpose.MarketStall ? PaidMarketWorld.StallTile(state, 0) : (GridPoint?)null;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "native-held-grain", "grain", owner, 4,
            groundPosition: stall is { } point ? new(point.X, point.Y) : null);
        if (purpose == InventoryHoldPurpose.Delivery)
            inventory = InventoryAuthority.AssignDelivery(inventory, "native-held-grain", holder);
        else if (purpose == InventoryHoldPurpose.Borrowed)
            inventory = InventoryAuthority.Relocate(inventory, "native-borrow", "native-held-grain", owner, 4, carrierId: actor);
        else if (purpose == InventoryHoldPurpose.MarketStall)
            inventory = MarketStockHolds.Update(inventory, state.Towns!);
        else
        {
            inventory = InventoryAuthority.HoldStock(inventory, "native-held-grain", hold);
            state = state with
            { AnimalWorld = state.AnimalWorld with { SupplyTrips = [new(actor, "native-held-grain", supplyYard!)] } };
        }
        inventory = InventoryAuthority.Reserve(inventory, "native-held-input", owner, "native-held-grain", 1,
            "meal", inventory.WorldTick + 10, holdAccess: [hold]);
        state = FarmFieldTests.WithInventory(state, inventory);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        Assert.Equal(hold, Assert.Single(reloaded.Society.Inventory.GetLot("native-held-grain").Holds!));
        Assert.Equal(hold, Assert.Single(reloaded.Society.Inventory.GetReservation("native-held-input").HoldAccess!));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(reloaded.Society.Inventory,
            "unrelated-move", "native-held-grain", owner, 1));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var expected = InventoryAuthority.ConsumeReservation(inventory, "native-held-input");
        var actual = InventoryAuthority.ConsumeReservation(reloaded.Society.Inventory, "native-held-input");
        Assert.Equal(WorldRuntimeCodec.Encode(FarmFieldTests.WithInventory(state, expected)),
            WorldRuntimeCodec.Encode(FarmFieldTests.WithInventory(reloaded.ExportState(), actual)));
        var oldDocument = JsonNode.Parse(bytes)!;
        oldDocument["state"]!["schemaVersion"] = 132;
        var oldBytes = Encoding.UTF8.GetBytes(oldDocument.ToJsonString());
        var originalOldBytes = oldBytes.ToArray();
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(oldBytes));
        Assert.Equal(originalOldBytes, oldBytes);
    }

    private static InventoryCheckpoint VesselStock(string kind) => InventoryAuthority.CreateGenesis([
        new("vessel", kind, "owner", 1, 10_000, 10_000, 0,
            GroundPosition: kind == "handcart" ? new(0, 0) : null),
        new("contents", kind == "water_jug" ? "fresh_water" : "berries", "owner", 4, 10_000, 10_000, 0,
            ContainerLotId: "vessel")]);

    private static InventoryCheckpoint Stock() => InventoryAuthority.CreateGenesis([
        new("grain", "grain", "owner", 4, 10000, 10000, 0)]);
}
