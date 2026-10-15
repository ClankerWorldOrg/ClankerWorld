using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal static class StockHoldValidation
{
    internal static void Validate(WorldRuntimeState state)
    {
        var inventory = state.Society.Society.Inventory;
        if (!ReferenceEquals(inventory, MarketStockHolds.Update(inventory, state.Towns ?? [])))
            throw new InvalidDataException("Market stock holds differ from the active stall loans.");
        var trips = state.AnimalWorld?.SupplyTrips ?? [];
        foreach (var lot in inventory.Lots)
        {
            var holds = lot.Holds ?? [];
            if (lot.DeliveryBuildingId is { } delivery && !holds.Contains(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Delivery, delivery)) ||
                lot.CarrierId is { } borrower && borrower != lot.OwnerId && !holds.Contains(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, borrower)))
                throw new InvalidDataException("Physical custody is missing its stock hold.");
            foreach (var hold in holds)
                if (hold.Purpose switch
                {
                    InventoryHoldPurpose.Delivery => lot.DeliveryBuildingId != hold.HolderId,
                    InventoryHoldPurpose.Borrowed => lot.CarrierId != hold.HolderId || lot.OwnerId == hold.HolderId,
                    InventoryHoldPurpose.SupplyTrip => !trips.Any(trip => trip.ActorId == hold.HolderId && trip.LotId == lot.Id),
                    InventoryHoldPurpose.MarketStall => false,
                    _ => true,
                }) throw new InvalidDataException("A stock hold has no matching promise or physical custodian.");
        }
        foreach (var trip in trips)
            if (!(inventory.GetLot(trip.LotId).Holds ?? []).Contains(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, trip.ActorId)))
                throw new InvalidDataException("An animal supply trip is missing its stock hold.");
    }
}
