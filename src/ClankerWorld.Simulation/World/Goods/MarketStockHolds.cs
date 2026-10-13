using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal static class MarketStockHolds
{
    internal static InventoryCheckpoint Update(InventoryCheckpoint inventory, IReadOnlyList<TownRuntimeState> towns)
    {
        var occupied = towns.SelectMany(town => town.Markets).SelectMany(market =>
            market.Occupancies.Where(occupancy => occupancy.EndedTick is null).Select(occupancy =>
                (Occupancy: occupancy, Position: MarketContent.StallSite(market.Site,
                    market.Stalls.Single(stall => stall.BuildingId == occupancy.StallBuildingId).SlotIndex)))).ToArray();
        var current = inventory;
        foreach (var lot in InventoryIndex.For(inventory).Lots)
        {
            var expected = lot.GroundPosition is { } ground ? occupied.Where(item =>
                item.Occupancy.SellerHouseholdId == lot.OwnerId && item.Position == new GridPoint(ground.X, ground.Y))
                .Select(item => new InventoryHold(InventoryHoldPurpose.MarketStall, item.Occupancy.Id)).ToArray() : [];
            foreach (var hold in (lot.Holds ?? []).Where(hold => hold.Purpose == InventoryHoldPurpose.MarketStall).Except(expected))
                current = InventoryAuthority.ReleaseStock(current, lot.Id, hold);
            foreach (var hold in expected.Except(lot.Holds ?? []))
                current = InventoryAuthority.HoldStock(current, lot.Id, hold);
        }
        return current;
    }
}
