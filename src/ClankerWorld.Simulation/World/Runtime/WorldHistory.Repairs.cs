using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public static partial class WorldHistory
{
    public const int RecentRepairReservationLimit = 32;

    private static InventoryCheckpoint CompactRepairReservations(WorldRuntimeState state, List<ArchivedEventBatch> archived)
    {
        var inventory = state.Society.Society.Inventory;
        // Other consumption purposes have their own authority and retention.
        // Active claims never enter the retirement candidates.
        var closed = inventory.Reservations.Where(item => item.Purpose == "equipment_repair" &&
                item.State is InventoryReservationState.Completed or InventoryReservationState.Released)
            .OrderBy(item => item.ExpiryTick).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var required = (state.Towns ?? []).SelectMany(town => town.NonLandHearings.NativeReceipts)
            .SelectMany(receipt => receipt.RepairInputs).Select(input => input.Reservation.Id).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(WorldRuntime.RequiredCartRepairReservations(state.Instructions ?? [], inventory.WorldTick));
        var retired = Retire(closed, RecentRepairReservationLimit, item => required.Contains(item.Id));
        if (retired.Length == 0) return inventory;
        var retiredIds = retired.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return inventory with
        {
            Reservations = inventory.Reservations.Where(item => !retiredIds.Contains(item.Id)).ToArray(),
            ArchivedRepairReservationCount = ArchiveRecords("repair-reservations", retired, inventory.ArchivedRepairReservationCount, archived),
        };
    }
}
