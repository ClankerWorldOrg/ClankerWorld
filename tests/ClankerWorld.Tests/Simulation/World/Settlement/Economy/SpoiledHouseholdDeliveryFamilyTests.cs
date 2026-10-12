using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class SpoiledHouseholdDeliveryTests
{
    private const string Pot = "00-inflight-pot";
    private const string PotGreens = "inflight-pot-greens";
    private const string ChildReservation = "inflight-pot-contents-claim";

    [Fact]
    public async Task ARealFourUnitPotFamilyRecoversTogetherAndDiscardedDeliveryDoesNotMoveAnyStock()
    {
        var (state, actor, startingSite) = await PickedUpPot();
        var choices = DeliveryPolicy();
        using var world = Restore(state, actor, choices);
        for (var tick = 0; tick < 8 && world.Society.Inventory.GetLot(PotGreens).FreshnessBasisPoints > 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        AssertSpoiledCarriedFamily(world, actor);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var recovered = Restore(WorldRuntimeCodec.Decode(bytes), actor, choices);
        using var replay = Restore(WorldRuntimeCodec.Decode(bytes), actor, DeliveryPolicy());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(recovered.ExportState()));
        var discarded = false;
        for (var tick = 0; tick < 96 && recovered.Society.Inventory.GetLot(Pot).OwnerId == actor; tick++)
        {
            var discardHere = false;
            var position = recovered.Agents.Single(person => person.AgentId == actor).Position;
            if (!discarded && state.Map.FootDistance(position, startingSite) <= 1)
            {
                var before = WorldRuntimeCodec.Encode(recovered.ExportState());
                var rejected = await recovered.AdvanceOneTickAsync(() => false);
                Assert.False(rejected.Advanced);
                Assert.Equal("waiting_for_client", rejected.Outcome);
                Assert.Equal(before, WorldRuntimeCodec.Encode(recovered.ExportState()));
                AssertSpoiledCarriedFamily(recovered, actor);
                discarded = true;
                discardHere = true;
            }
            var beforeInventory = recovered.Society.Inventory;
            var carriedBefore = PersonalEquipmentRules.CarriedQuantity(beforeInventory, actor, null);
            var unrelatedBefore = beforeInventory.GetLot(SecondGreens);
            Assert.True((await recovered.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(recovered.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (recovered.Society.Inventory.GetLot(Pot).OwnerId == Household)
            {
                Assert.Equal(carriedBefore - 4, PersonalEquipmentRules.CarriedQuantity(recovered.Society.Inventory, actor, null));
                var unrelatedAfter = recovered.Society.Inventory.GetLot(SecondGreens);
                Assert.Equal((unrelatedBefore.Id, unrelatedBefore.OwnerId, unrelatedBefore.Quantity,
                        unrelatedBefore.ConditionBasisPoints, unrelatedBefore.StorageBuildingId, unrelatedBefore.DeliveryBuildingId,
                        unrelatedBefore.ContainerLotId, unrelatedBefore.GroundPosition, unrelatedBefore.ProvenanceLotId),
                    (unrelatedAfter.Id, unrelatedAfter.OwnerId, unrelatedAfter.Quantity,
                        unrelatedAfter.ConditionBasisPoints, unrelatedAfter.StorageBuildingId, unrelatedAfter.DeliveryBuildingId,
                        unrelatedAfter.ContainerLotId, unrelatedAfter.GroundPosition, unrelatedAfter.ProvenanceLotId));
            }
            if (discardHere)
            {
                Assert.Equal(Household, recovered.Society.Inventory.GetLot(Pot).OwnerId);
                Assert.Equal(Household, recovered.Society.Inventory.GetLot(PotGreens).OwnerId);
            }
        }
        Assert.Contains(choices.Offers, offer => offer.Contains("recover_household_delivery", StringComparer.Ordinal));
        Assert.True(discarded, "The prepared recovery must be discarded at the actual camp before the next ordinary tick commits it.");
        Assert.Equal((Household, 1, new InventoryGroundPosition(startingSite.X, startingSite.Y)),
            (recovered.Society.Inventory.GetLot(Pot).OwnerId, recovered.Society.Inventory.GetLot(Pot).Quantity,
                recovered.Society.Inventory.GetLot(Pot).GroundPosition));
        Assert.Equal((Household, 3, Pot, 0), (recovered.Society.Inventory.GetLot(PotGreens).OwnerId,
            recovered.Society.Inventory.GetLot(PotGreens).Quantity, recovered.Society.Inventory.GetLot(PotGreens).ContainerLotId,
            recovered.Society.Inventory.GetLot(PotGreens).FreshnessBasisPoints));
        Assert.Null(recovered.Society.Inventory.GetLot(PotGreens).GroundPosition);
        Assert.All(recovered.Society.Inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot), lot =>
        {
            Assert.Null(lot.DeliveryBuildingId);
            Assert.Null(lot.StorageBuildingId);
            Assert.Equal(10_000, lot.ConditionBasisPoints);
            Assert.Null(lot.ProvenanceLotId);
        });
        Assert.Equal(4, recovered.Society.Inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot).Sum(lot => lot.Quantity));
        Assert.Single(recovered.ExportState().Events, item => item.Kind == "household_delivery_recovered" &&
            item.Detail == $"{actor}:{Pot}:4:camp");
        Assert.Equal(2, recovered.Society.Inventory.Events.Count(item => item.Kind == "container_transferred" &&
            item.Detail.Contains(Pot, StringComparison.Ordinal)));
        Assert.Equal(state.Society.Society.Inventory.Reservations, recovered.Society.Inventory.Reservations);
        Assert.Equal(state.Society.Society.Inventory.Lots.Select(lot => (lot.Id, lot.Quantity)),
            recovered.Society.Inventory.Lots.Select(lot => (lot.Id, lot.Quantity)));
        var finalBytes = WorldRuntimeCodec.Encode(recovered.ExportState());
        using var finalReload = Restore(WorldRuntimeCodec.Decode(finalBytes), actor, new DeliveryChoices());
        Assert.Equal(finalBytes, WorldRuntimeCodec.Encode(finalReload.ExportState()));
    }

    [Fact]
    public async Task AnActualUsableChildClaimProtectsItsSpoiledFamilyWhileAnotherDeliveryCanRecover()
    {
        var (pickedUp, actor, startingSite) = await PickedUpPot();
        var inventory = pickedUp.Society.Society.Inventory;
        var originalFamily = inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot).ToArray();
        Assert.True(inventory.GetLot(PotGreens).FreshnessBasisPoints > 0);
        // This deliberate public kernel authority transition uses the exact
        // still-usable carried child. It is not a runtime reservation-command
        // claim, and it changes no stock, owner, position or delivery pointer.
        inventory = InventoryAuthority.Reserve(inventory, ChildReservation, actor, PotGreens, 1,
            "inflight_family_control", long.MaxValue);
        Assert.Equal(originalFamily, inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot));
        var claimed = pickedUp with { Society = pickedUp.Society with { Society = pickedUp.Society.Society with { Inventory = inventory } } };
        var bytes = WorldRuntimeCodec.Encode(claimed);
        var choices = DeliveryPolicy();
        using var collecting = Restore(WorldRuntimeCodec.Decode(bytes), actor, choices);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(collecting.ExportState()));
        for (var tick = 0; tick < 12 && (collecting.Society.Inventory.GetLot(SecondGreens).OwnerId != actor ||
            collecting.Society.Inventory.GetLot(SecondGreens).FreshnessBasisPoints > 0 ||
            collecting.Society.Inventory.GetLot(PotGreens).FreshnessBasisPoints > 0); tick++)
            Assert.True((await collecting.AdvanceOneTickAsync()).Advanced);
        AssertSpoiledCarriedFamily(collecting, actor);
        Assert.Equal((actor, 4, House, 0), (collecting.Society.Inventory.GetLot(SecondGreens).OwnerId,
            collecting.Society.Inventory.GetLot(SecondGreens).Quantity, collecting.Society.Inventory.GetLot(SecondGreens).DeliveryBuildingId,
            collecting.Society.Inventory.GetLot(SecondGreens).FreshnessBasisPoints));
        Assert.Contains(collecting.ExportState().Events, item => item.Kind == "household_stock_picked_up" &&
            item.Detail == $"{actor}:{SecondGreens}:4:{House}");
        var protectedFamily = collecting.Society.Inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot).ToArray();
        var receipt = collecting.Society.Inventory.GetReservation(ChildReservation);
        Assert.Equal(InventoryReservationState.Reserved, receipt.State);
        Assert.Contains(collecting.Society.Inventory.Events, item => item.Kind == "lot_spoiled" && item.Detail == PotGreens);
        var recoveryBytes = WorldRuntimeCodec.Encode(collecting.ExportState());
        using var recovered = Restore(WorldRuntimeCodec.Decode(recoveryBytes), actor,
            DeliveryPolicy());
        for (var tick = 0; tick < 96 && recovered.Society.Inventory.GetLot(SecondGreens).OwnerId == actor; tick++)
            Assert.True((await recovered.AdvanceOneTickAsync()).Advanced);
        Assert.Equal((Household, 4, new InventoryGroundPosition(startingSite.X, startingSite.Y)),
            (recovered.Society.Inventory.GetLot(SecondGreens).OwnerId, recovered.Society.Inventory.GetLot(SecondGreens).Quantity,
                recovered.Society.Inventory.GetLot(SecondGreens).GroundPosition));
        Assert.Null(recovered.Society.Inventory.GetLot(SecondGreens).DeliveryBuildingId);
        for (var tick = 0; tick < 8; tick++) Assert.True((await recovered.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(receipt, recovered.Society.Inventory.GetReservation(ChildReservation));
        Assert.Equal(protectedFamily.Select(lot => (lot.Id, lot.OwnerId, lot.Quantity, lot.ConditionBasisPoints,
                lot.FreshnessBasisPoints, lot.StorageBuildingId, lot.DeliveryBuildingId, lot.ContainerLotId, lot.GroundPosition, lot.ProvenanceLotId)),
            recovered.Society.Inventory.Lots.Where(lot => lot.Id == Pot || lot.ContainerLotId == Pot)
                .Select(lot => (lot.Id, lot.OwnerId, lot.Quantity, lot.ConditionBasisPoints, lot.FreshnessBasisPoints,
                    lot.StorageBuildingId, lot.DeliveryBuildingId, lot.ContainerLotId, lot.GroundPosition, lot.ProvenanceLotId)));
        Assert.Equal(pickedUp.Society.Society.Inventory.Lots.Select(lot => (lot.Id, lot.Quantity)),
            recovered.Society.Inventory.Lots.Select(lot => (lot.Id, lot.Quantity)));
        Assert.Equal(4, PersonalEquipmentRules.CarriedQuantity(recovered.Society.Inventory, actor, null));
        Assert.DoesNotContain(recovered.ExportState().Events, item => item.Kind == "household_delivery_recovered" &&
            item.Detail.Contains(Pot, StringComparison.Ordinal));
        AssertBuildingStillBlocked(recovered, recovered.WorldSimulation.Buildings.Single(building => building.InstanceId == House), false);
        var finalBytes = WorldRuntimeCodec.Encode(recovered.ExportState());
        using var finalReload = Restore(WorldRuntimeCodec.Decode(finalBytes), actor, new DeliveryChoices());
        Assert.Equal(finalBytes, WorldRuntimeCodec.Encode(finalReload.ExportState()));
        Assert.Equal(receipt, finalReload.Society.Inventory.GetReservation(ChildReservation));
    }

    private static async Task<(WorldRuntimeState State, string Actor, GridPoint StartingSite)> PickedUpPot()
    {
        var (state, actor, startingSite) = await PreparedDelivery(wholeFamily: true);
        using var world = Restore(state, actor, DeliveryPolicy());
        for (var tick = 0; tick < 4 && world.Society.Inventory.GetLot(Pot).OwnerId != actor; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(actor, world.Society.Inventory.GetLot(Pot).OwnerId);
        Assert.Equal((actor, Pot, House, 3), (world.Society.Inventory.GetLot(PotGreens).OwnerId,
            world.Society.Inventory.GetLot(PotGreens).ContainerLotId, world.Society.Inventory.GetLot(PotGreens).DeliveryBuildingId,
            world.Society.Inventory.GetLot(PotGreens).Quantity));
        Assert.True(world.Society.Inventory.GetLot(PotGreens).FreshnessBasisPoints > 0);
        Assert.Contains(world.Society.Inventory.Events, item => item.Kind == "container_contents_stored" &&
            item.Detail == $"fill-inflight-pot:{Pot}:{PotGreens}:3");
        Assert.Contains(world.ExportState().Events, item => item.Kind == "household_stock_picked_up" &&
            item.Detail == $"{actor}:{Pot}:1:{House}");
        Assert.Equal(4, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
        return (world.ExportState(), actor, startingSite);
    }

    private static void AssertSpoiledCarriedFamily(WorldRuntime world, string actor)
    {
        Assert.Equal((actor, 1, House), (world.Society.Inventory.GetLot(Pot).OwnerId,
            world.Society.Inventory.GetLot(Pot).Quantity, world.Society.Inventory.GetLot(Pot).DeliveryBuildingId));
        Assert.Equal((actor, 3, Pot, House, 0), (world.Society.Inventory.GetLot(PotGreens).OwnerId,
            world.Society.Inventory.GetLot(PotGreens).Quantity, world.Society.Inventory.GetLot(PotGreens).ContainerLotId,
            world.Society.Inventory.GetLot(PotGreens).DeliveryBuildingId, world.Society.Inventory.GetLot(PotGreens).FreshnessBasisPoints));
        Assert.Contains(world.Society.Inventory.Events, item => item.Kind == "lot_spoiled" && item.Detail == PotGreens);
    }
}
