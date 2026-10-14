using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

internal static class StockHoldTestState
{
    internal static IReadOnlyList<InventoryHold> Access(InventoryCheckpoint inventory, string? lotId) => lotId is null ? [] :
        InventoryHoldRules.Family(InventoryIndex.For(inventory), InventoryIndex.For(inventory).Root(inventory.GetLot(lotId)));

    // Fixtures that set physical fields directly also construct the required
    // current-format holds. Production restore never fills in missing holds.
    internal static InventoryCheckpoint Custody(InventoryCheckpoint inventory) => inventory with
    {
        Lots = inventory.Lots.Select(lot =>
        {
            var holds = (lot.Holds ?? []).Where(hold => hold.Purpose is not
                (InventoryHoldPurpose.Delivery or InventoryHoldPurpose.Borrowed)).ToList();
            if (lot.DeliveryBuildingId is { } destination) holds.Add(new(InventoryHoldPurpose.Delivery, destination));
            if (lot.CarrierId is { } borrower && borrower != lot.OwnerId) holds.Add(new(InventoryHoldPurpose.Borrowed, borrower));
            return lot with
            {
                Holds = holds.Count == 0 ? null : new InventoryHoldSet(holds
                .OrderBy(hold => hold.Purpose).ThenBy(hold => hold.HolderId, StringComparer.Ordinal))
            };
        }).ToArray(),
    };
}
