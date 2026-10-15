using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ExtendOrderReservations(IReadOnlyList<string> reservations, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));

    internal static Command HoldOrderReservations(IReadOnlyList<string> reservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, reservations));

    internal static Command ReleaseExpansionOrderInputs(BuildingExpansionJob job) => new PreparedCommand(inventory =>
{
    foreach (var id in job.InputReservationIds)
        if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
            inventory = InventoryAuthority.ReleaseReservation(inventory, id, "owner_expansion_stopped");
    return inventory;
});

    internal static Command OrdersExtendOrderReservations(IReadOnlyList<string> reservations, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));

    internal static Command OrdersHoldOrderReservations(IReadOnlyList<string> reservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, reservations));

    internal static Command ReleaseProductionOrderInputs(WorldProductionJob job) => new PreparedCommand(inventory =>
{
    foreach (var id in job.InputReservationIds)
        if (inventory.Reservations.FirstOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
            inventory = InventoryAuthority.ReleaseReservation(inventory, id, "production_order_cancelled");
    return inventory;
});

    internal static Command TransferOrderDelivery(string actor, OrderSystem.DeliveryOrderPlan plan, string operation) => new PreparedCommand(inventory => plan.TakeFromContainer ? InventoryAuthority.TakeFromContainer(inventory, operation, plan.Carrier.OwnerId, actor, plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity, destinationDeliveryBuildingId: plan.Destination.InstanceId, holdAccess: InventoryHoldRules.BorrowedBy(actor)) : InventoryAuthority.Transfer(inventory, operation, plan.Carrier.OwnerId, plan.DirectDelivery ? plan.DestinationOwnerId : actor, plan.Carrier.Id, plan.TransferQuantity, plan.DirectDelivery ? "owner_stock_delivered" : "owner_stock_picked_up", destinationStorageBuildingId: plan.DirectDelivery ? plan.Destination.InstanceId : null, destinationDeliveryBuildingId: plan.DirectDelivery ? null : plan.Destination.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, plan.DirectDelivery ? plan.Destination.InstanceId : null)));
}
