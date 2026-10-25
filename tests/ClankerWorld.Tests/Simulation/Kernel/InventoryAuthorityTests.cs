using ClankerWorld.Simulation;
using System.Text;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class InventoryAuthorityTests
{
    [Fact]
    public void PickingUpPartOfAFieldHarvestKeepsTheRemainderAtTheFieldAcrossReload()
    {
        var harvest = InventoryAuthority.CreateGenesis([new("field-harvest", "grain", "farm-household", 6,
            10_000, 10_000, 0, GroundPosition: new(4, 5))]);
        var before = InventoryCheckpointCodec.Encode(harvest);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(harvest, "theft", "outsider",
            "carrier", "field-harvest", 2, "pickup"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(harvest));
        var pickedUp = InventoryAuthority.Transfer(harvest, "pickup", "farm-household", "carrier",
            "field-harvest", 2, "pickup", destinationDeliveryBuildingId: "farmhouse");
        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(pickedUp));
        var remainder = restored.GetLot("field-harvest");
        Assert.Equal(4, remainder.Quantity);
        Assert.Equal(new InventoryGroundPosition(4, 5), remainder.GroundPosition);
        var carried = Assert.Single(restored.Lots, lot => lot.OwnerId == "carrier");
        Assert.Equal(2, carried.Quantity);
        Assert.Null(carried.GroundPosition);
        Assert.Equal("farmhouse", carried.DeliveryBuildingId);
        Assert.Equal("field-harvest", carried.ProvenanceLotId);
        Assert.Throws<InvalidDataException>(() => InventoryAuthority.AddLot(restored, "conflicting-position", "grain",
            "farm-household", 1, storageBuildingId: "silo", groundPosition: new(4, 5)));
    }

    [Fact]
    public void EitherPartyCanDeclineWithoutTransferringOrRetainingReservations()
    {
        var offered = InventoryAuthority.CreateDirectBarterOffer(Genesis(),
            new DirectBarterProposal(Identity.Parse("decline"), 1, "alpha", "bravo", "alpha-wood", 1, "bravo-food", 1, 20));
        offered = InventoryAuthority.AcceptDirectBarterOffer(offered, "decline", 1, "alpha");
        var bytes = InventoryCheckpointCodec.Encode(offered);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.CancelDirectBarterOffer(offered, "decline", 2, "alpha"));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.CancelDirectBarterOffer(offered, "decline", 1, "outsider"));
        Assert.Equal(bytes, InventoryCheckpointCodec.Encode(offered));
        var cancelled = InventoryAuthority.CancelDirectBarterOffer(offered, "decline", 1, "alpha");
        Assert.Equal(DirectBarterState.Cancelled, cancelled.GetOffer("decline").State);
        Assert.All(cancelled.Reservations, item => Assert.Equal(InventoryReservationState.Released, item.State));
        Assert.Equal(offered.Lots, cancelled.Lots);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.AcceptDirectBarterOffer(cancelled, "decline", 1, "bravo"));
    }

    [Fact]
    public void ExpiredBarterCannotAcquireEitherPartysAcceptance()
    {
        var offered = InventoryAuthority.CreateDirectBarterOffer(Genesis(),
            new DirectBarterProposal(Identity.Parse("expiry"), 1, "alpha", "bravo", "alpha-wood", 1, "bravo-food", 1, 2));
        var expired = offered with { WorldTick = 3 };
        var before = InventoryCheckpointCodec.Encode(expired);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.AcceptDirectBarterOffer(expired,
            "expiry", 1, "alpha"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(expired));
        var released = InventoryAuthority.ReleaseExpiredReservations(expired, 3);
        Assert.Equal(DirectBarterState.Cancelled, released.GetOffer("expiry").State);
        Assert.All(released.Reservations, item => Assert.Equal(InventoryReservationState.Released, item.State));
        Assert.Equal(InventoryCheckpointCodec.Encode(released), InventoryCheckpointCodec.Encode(InventoryAuthority.ReleaseExpiredReservations(released, 3)));
    }

    [Fact]
    public void LotSplitSpoilageAndSaveRestoreRemainCanonicalAndDoNotDecayTwice()
    {
        var genesis = Genesis();
        var split = InventoryAuthority.SplitLot(genesis, "alpha-berries", 2, "alpha-berries:split");
        var spoiled = InventoryAuthority.ProcessSpoilage(split, 3, 100);
        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(spoiled));
        var processedAgain = InventoryAuthority.ProcessSpoilage(restored, 3, 100);
        var fullySpoiled = InventoryAuthority.ProcessSpoilage(processedAgain, 6, 100);

        Assert.Equal(2, split.GetLot("alpha-berries").Quantity);
        Assert.Equal("alpha-berries", split.GetLot("alpha-berries:split").ProvenanceLotId);
        Assert.All(spoiled.Lots.Where(lot => lot.ItemKind == "berries"), lot => Assert.Equal(300, lot.FreshnessBasisPoints));
        Assert.Equal(InventoryDigest.State(restored), InventoryDigest.State(processedAgain));
        Assert.Equal(InventoryDigest.Events(restored.Events), InventoryDigest.Events(processedAgain.Events));
        Assert.All(fullySpoiled.Lots.Where(lot => lot.ItemKind == "berries"), lot => Assert.Equal(0, lot.FreshnessBasisPoints));
        Assert.Equal(InventoryDigest.State(spoiled), InventoryDigest.State(restored));

        var foodKinds = new HashSet<string>(StringComparer.Ordinal) { "food" };
        var exposed = InventoryAuthority.ProcessSpoilage(genesis, 100, 4, foodKinds);
        var protectedOwners = new HashSet<string>(StringComparer.Ordinal) { "bravo" };
        var stored = InventoryAuthority.ProcessSpoilage(genesis, 100, 4, foodKinds, protectedOwners);
        Assert.Equal(9_600, exposed.GetLot("bravo-food").FreshnessBasisPoints);
        Assert.Equal(9_800, stored.GetLot("bravo-food").FreshnessBasisPoints);
        Assert.Equal(10_000, stored.GetLot("alpha-wood").FreshnessBasisPoints);
    }

    [Fact]
    public void SplitCannotRemoveReservedStockOrChangeTheRejectedCheckpoint()
    {
        var reserved = InventoryAuthority.Reserve(
            InventoryAuthority.CreateGenesis([new InventoryLot("wood", "wood", "owner", 10, 10_000, 10_000, 0)]),
            "job-input", "owner", "wood", 7, "building", 100);
        reserved = reserved with { Reservations = [reserved.GetReservation("job-input") with { State = InventoryReservationState.Committed }] };
        var before = InventoryCheckpointCodec.Encode(reserved);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.SplitLot(reserved, "wood", 8, "wood-split"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(reserved));
        var restored = InventoryCheckpointCodec.Decode(before);
        var split = InventoryAuthority.SplitLot(restored, "wood", 3, "wood-split");
        Assert.Equal(7, split.GetLot("wood").Quantity);
        Assert.Equal(reserved.Reservations, split.Reservations);
        Assert.Equal(10, split.Lots.Sum(lot => lot.Quantity));
    }

    [Fact]
    public void SplitPreservesBothBarterPartiesReservationsAndTheOfferCanStillSettleAfterReload()
    {
        var genesis = InventoryAuthority.CreateGenesis(
        [
            new InventoryLot("wood", "wood", "alpha", 10, 10_000, 10_000, 0),
            new InventoryLot("food", "food", "bravo", 5, 10_000, 10_000, 0),
        ]);
        var offered = InventoryAuthority.CreateDirectBarterOffer(genesis,
            new DirectBarterProposal(Identity.Parse("offer"), 1, "alpha", "bravo", "wood", 7, "food", 3, 100));
        var before = InventoryCheckpointCodec.Encode(offered);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.SplitLot(offered, "wood", 4, "wood-split"));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.SplitLot(offered, "food", 3, "food-split"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(offered));

        var split = InventoryAuthority.SplitLot(offered, "wood", 3, "wood-split");
        split = InventoryAuthority.SplitLot(split, "food", 2, "food-split");
        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(split));
        var settled = InventoryAuthority.AcceptDirectBarterOffer(
            InventoryAuthority.AcceptDirectBarterOffer(restored, "offer", 1, "alpha"), "offer", 1, "bravo");
        Assert.Equal(DirectBarterState.Settled, settled.GetOffer("offer").State);
        Assert.Equal("bravo", settled.GetLot("wood").OwnerId);
        Assert.Equal("alpha", settled.GetLot("food").OwnerId);
        Assert.Equal("alpha", settled.GetLot("wood-split").OwnerId);
        Assert.Equal("bravo", settled.GetLot("food-split").OwnerId);
    }

    [Fact]
    public void ReleasedReservationsAllowSplittingAndUnusableUnreservedLotsKeepTheirExistingSplitBehavior()
    {
        var reserved = InventoryAuthority.Reserve(
            InventoryAuthority.CreateGenesis([new InventoryLot("wood", "wood", "owner", 10, 10_000, 10_000, 0)]),
            "job-input", "owner", "wood", 7, "building", 100);
        var released = InventoryAuthority.ReleaseReservation(reserved, "job-input");
        Assert.Equal(2, InventoryAuthority.SplitLot(released, "wood", 8, "wood-split").GetLot("wood").Quantity);
        var spoiled = InventoryAuthority.CreateGenesis([new InventoryLot("spoiled", "food", "owner", 10, 0, 0, 0)]);
        Assert.Equal(2, InventoryAuthority.SplitLot(spoiled, "spoiled", 8, "spoiled-split").GetLot("spoiled").Quantity);
    }

    [Fact]
    public void PartialReleaseFreesOnlySelectedUnitsAndKeepsRemainingClaimsUsableAcrossReload()
    {
        var inventory = InventoryAuthority.CreateGenesis(
        [
            new("seeds", "orchard_seed", "owner", 5, 10_000, 10_000, 0),
            new("wood", "wood", "owner", 2, 10_000, 10_000, 0),
        ]);
        inventory = InventoryAuthority.Reserve(inventory, "planting", "owner", "seeds", 5, "orchard_replanting", 100);
        inventory = InventoryAuthority.Reserve(inventory, "work", "owner", "wood", 2, "building", 100);
        var released = InventoryAuthority.ReleaseReservationQuantity(inventory, "planting", 2, "food_recovery");
        Assert.Equal(inventory.Lots, released.Lots);
        Assert.Equal(inventory.GetReservation("work"), released.GetReservation("work"));
        Assert.Equal(3, released.GetReservation("planting").Quantity);
        Assert.Equal(InventoryReservationState.Reserved, released.GetReservation("planting").State);

        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(released));
        var stored = InventoryAuthority.Transfer(restored, "store-seeds", "owner", "household", "seeds", 2,
            "food_recovery", destinationGroundPosition: new(4, 5));
        Assert.Equal(3, stored.GetLot("seeds").Quantity);
        Assert.Equal(5, stored.Lots.Where(lot => lot.ItemKind == "orchard_seed").Sum(lot => lot.Quantity));
        var beforeRefusal = InventoryCheckpointCodec.Encode(stored);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Transfer(stored, "extra-seed", "owner",
            "household", "seeds", 1, "food_recovery", destinationGroundPosition: new(4, 5)));
        Assert.Equal(beforeRefusal, InventoryCheckpointCodec.Encode(stored));

        var planted = InventoryAuthority.ConsumeReservation(stored, "planting");
        Assert.Equal(InventoryReservationState.Completed, planted.GetReservation("planting").State);
        Assert.Equal(2, planted.Lots.Where(lot => lot.ItemKind == "orchard_seed").Sum(lot => lot.Quantity));
        Assert.Equal(inventory.GetReservation("work"), planted.GetReservation("work"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    public void InvalidPartialReleaseLeavesTheCheckpointUnchanged(int quantity)
    {
        var inventory = InventoryAuthority.Reserve(InventoryAuthority.CreateGenesis(
            [new("seeds", "orchard_seed", "owner", 5, 10_000, 10_000, 0)]),
            "planting", "owner", "seeds", 5, "orchard_replanting", 100);
        var before = InventoryCheckpointCodec.Encode(inventory);
        Assert.Throws<InvalidOperationException>(() =>
            InventoryAuthority.ReleaseReservationQuantity(inventory, "planting", quantity, "food_recovery"));
        Assert.Equal(before, InventoryCheckpointCodec.Encode(inventory));
    }

    [Fact]
    public void ReservationExpiryReleasesOnlyTheReservedQuantity()
    {
        var reserved = InventoryAuthority.Reserve(Genesis(), "reserve-1", "alpha", "alpha-berries", 1, "meal", 2);
        var atExpiry = InventoryAuthority.ReleaseExpiredReservations(reserved, 2);
        Assert.Equal(InventoryReservationState.Reserved, atExpiry.GetReservation("reserve-1").State);
        Assert.Equal(InventoryReservationState.Completed, InventoryAuthority.ConsumeReservation(atExpiry, "reserve-1").GetReservation("reserve-1").State);
        var released = InventoryAuthority.ReleaseExpiredReservations(reserved, 3);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.ConsumeReservation(reserved with { WorldTick = 3 }, "reserve-1"));

        Assert.Equal(InventoryReservationState.Reserved, reserved.GetReservation("reserve-1").State);
        Assert.Equal(InventoryReservationState.Released, released.GetReservation("reserve-1").State);
        Assert.Contains(released.Events, item => item is { Kind: "reservation_released", Detail: "reserve-1" });
    }

    [Fact]
    public void ExactRevisionAcceptanceSettlesAtomicallyAndRejectsStaleAcceptanceWithoutMutation()
    {
        var proposal = new DirectBarterProposal(Identity.Parse("offer-1"), 3, "alpha", "bravo", "alpha-wood", 2, "bravo-food", 1, 10);
        var offered = InventoryAuthority.CreateDirectBarterOffer(Genesis(), proposal);
        var firstAccepted = InventoryAuthority.AcceptDirectBarterOffer(offered, "offer-1", 3, "alpha");
        var beforeStaleState = InventoryDigest.State(firstAccepted);
        var beforeStaleEvents = InventoryDigest.Events(firstAccepted.Events);

        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.AcceptDirectBarterOffer(firstAccepted, "offer-1", 2, "bravo"));
        Assert.Equal(beforeStaleState, InventoryDigest.State(firstAccepted));
        Assert.Equal(beforeStaleEvents, InventoryDigest.Events(firstAccepted.Events));

        var settled = InventoryAuthority.AcceptDirectBarterOffer(firstAccepted, "offer-1", 3, "bravo");
        Assert.Equal(DirectBarterState.Settled, settled.GetOffer("offer-1").State);
        Assert.Equal(InventoryReservationState.Completed, settled.GetReservation("offer-1:first").State);
        Assert.Equal(InventoryReservationState.Completed, settled.GetReservation("offer-1:second").State);
        Assert.Equal("bravo", settled.GetLot("alpha-wood").OwnerId);
        Assert.Equal("alpha", settled.GetLot("bravo-food").OwnerId);
        Assert.Contains(settled.Events, item => item is { Kind: "barter_settled", Detail: "offer-1:r3" });
    }

    [Fact]
    public void CheckpointCodecRoundTripsDelimiterBearingAndUnicodeValues()
    {
        var checkpoint = new InventoryCheckpoint(
            7,
            [
                new InventoryLot("lot|1", "berries,🍓\nkind", "owner|1", 2, 10_000, 9_000, 7, "source|lot"),
                new InventoryLot("other-lot", "food", "other,owner", 1, 10_000, 10_000, 7),
            ],
            [new InventoryReservation(Identity.Parse("reserve|1"), "owner|1", "lot|1", 1, "meal\nwith|separators", 12, true, InventoryReservationState.Reserved)],
            [new DirectBarterOffer(Identity.Parse("offer|1"), 2, "owner|1", "other,owner", "lot|1", 1, "other-lot", 1, 20, DirectBarterState.Open, ["owner|1", "other\nowner"])],
            [new InventoryEvent(1, 7, "event|kind", "detail,with\nseparators")]);

        var restored = InventoryCheckpointCodec.Decode(InventoryCheckpointCodec.Encode(checkpoint));

        Assert.Equal(checkpoint.WorldTick, restored.WorldTick);
        Assert.Equal(checkpoint.Lots, restored.Lots);
        Assert.Equal(checkpoint.Reservations, restored.Reservations);
        var expectedOffer = checkpoint.Offers.Single();
        var actualOffer = restored.Offers.Single();
        Assert.Equal(expectedOffer.Id, actualOffer.Id);
        Assert.Equal(expectedOffer.Revision, actualOffer.Revision);
        Assert.Equal(expectedOffer.FirstPartyId, actualOffer.FirstPartyId);
        Assert.Equal(expectedOffer.SecondPartyId, actualOffer.SecondPartyId);
        Assert.Equal(expectedOffer.FirstLotId, actualOffer.FirstLotId);
        Assert.Equal(expectedOffer.FirstQuantity, actualOffer.FirstQuantity);
        Assert.Equal(expectedOffer.SecondLotId, actualOffer.SecondLotId);
        Assert.Equal(expectedOffer.SecondQuantity, actualOffer.SecondQuantity);
        Assert.Equal(expectedOffer.ExpiryTick, actualOffer.ExpiryTick);
        Assert.Equal(expectedOffer.State, actualOffer.State);
        Assert.Equal(
            expectedOffer.AcceptedBy.OrderBy(id => id, StringComparer.Ordinal),
            actualOffer.AcceptedBy);
        Assert.Equal(checkpoint.Events, restored.Events);
        Assert.Equal(InventoryDigest.State(checkpoint), InventoryDigest.State(restored));
    }

    private static InventoryCheckpoint Genesis() => InventoryAuthority.CreateGenesis(
    [
        new InventoryLot("alpha-berries", "berries", "alpha", 4, 10_000, 600, 0),
        new InventoryLot("alpha-wood", "wood", "alpha", 2, 10_000, 10_000, 0),
        new InventoryLot("bravo-food", "food", "bravo", 1, 10_000, 10_000, 0),
    ]);
}
