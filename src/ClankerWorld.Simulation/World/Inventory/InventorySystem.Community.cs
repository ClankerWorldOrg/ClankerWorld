using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId) => new PreparedCommand(inventory =>
                plan.Choice.Kind == PersonalGoodsKind.Collect
                    ? InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity, plan.Actor,
                        holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor))
                    : InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity,
                        storageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command SetReservationDeadline(IReadOnlyList<Identity> ids, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, ids, deadline));

    internal static Command ReleaseUnavailablePausedInputs(IReadOnlyList<Identity> ids) => new PreparedCommand(inventory =>
            {
                foreach (var id in ids)
                    if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "paused_work_materials_unavailable");
                return inventory;
            });

    internal static Command CommitDepartureAllowance(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);

    internal static Command ReleaseDepartingFarmInputs(WorldProductionJob job) => new PreparedCommand(inventory =>
                    {
                        foreach (var id in job.InputReservationIds)
                            if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                                inventory = InventoryAuthority.ReleaseReservation(inventory, id, "personal_work_stopped_on_leaving");
                        return inventory;
                    });

    internal static Command HoldDepartingProjectInputs(Identity[] heldReservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, heldReservations));

    internal static Command RestoreDeliveryOwnership(IInventoryCommunityWorld commandContext, string transition, string actor, InventoryLot lot, string householdId) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                    IdentityRules.PrefixedReference($"{transition}-delivery:", $"{actor}:{commandContext.WorldTick}:{lot.Id}"), actor, householdId, lot.Id, lot.Quantity,
                    "delivery_ownership_restored", holdAccess: InventoryHoldRules.DeliveryFor(actor, lot.DeliveryBuildingId)));

    internal static Command RestoreDeliveryCustody(IInventoryCommunityWorld commandContext, string transition, string actor, InventoryLot lot, string householdId, string carrier) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                    IdentityRules.PrefixedReference($"{transition}-custody:", $"{actor}:{commandContext.WorldTick}:{lot.Id}"), lot.Id, householdId, lot.Quantity, carrier, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
}
