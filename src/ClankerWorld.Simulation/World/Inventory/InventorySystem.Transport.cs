using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command MoveCartInventory(string actor, string cart, GridPoint to, int wear) => new PreparedCommand(inventory => InventoryAuthority.MoveHandcart(inventory, actor, cart, new(to.X, to.Y), wear));

    internal static Command TransferCartInventory(IInventoryTransportWorld commandContext, string actor, string recipient, InventoryLot cart) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, $"cart-gift:{commandContext.WorldTick}:{actor}", actor, recipient, cart.Id, 1, "handcart_given", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command CollectCartMaterial(IInventoryTransportWorld commandContext, HandcartBuildMaterialActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, $"cart-material:{commandContext.WorldTick}:{plan.Actor}", plan.Pickup.OwnerId, plan.Actor, plan.Pickup.StockId, plan.Quantity, "handcart_material_collected", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command RepairCartInventory(IInventoryTransportWorld commandContext, HandcartRepairActionPlan plan, InventoryLot cart, string actor, List<string> reservations) => new PreparedCommand(inventory =>
{
    var updated = inventory;
    foreach (var material in plan.Materials)
    {
        var id = commandContext.CartRepairReservationId(commandContext.WorldTick, actor, material.ItemKind);
        updated = InventoryAuthority.Reserve(updated, id, actor, material.StockId, material.Quantity, "equipment_repair", commandContext.WorldTick + 1, holdAccess: InventoryHoldRules.BorrowedBy(actor));
        reservations.Add(id);
    }

    return InventoryAuthority.RepairSingleUnit(updated, cart.Id, 10_000, reservations, holdAccess: InventoryHoldRules.BorrowedBy(actor));
});

    internal static Command LoadCartInventory(IInventoryTransportWorld commandContext, InventoryLot cart, InventoryLot lot, string actor, int quantity) => new PreparedCommand(inventory =>
{
    var updated = inventory;
    var cargoId = lot.Id;
    if (lot.OwnerId != actor)
    {
        var operation = $"cart-pickup:{commandContext.WorldTick}:{actor}";
        updated = InventoryAuthority.Transfer(updated, operation, lot.OwnerId, actor, lot.Id, quantity, "handcart_pickup", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor));
        cargoId = quantity == lot.Quantity ? lot.Id : DerivedId.Create($"{lot.Id}#transfer:{operation}");
    }

    return InventoryAuthority.LoadHandcart(updated, $"load:{commandContext.WorldTick}:{actor}", actor, cart.Id, cargoId, quantity);
});

    internal static Command UnloadCartInventory(IInventoryTransportWorld commandContext, HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot, string actor, int quantity) => new PreparedCommand(inventory => InventoryAuthority.UnloadHandcart(inventory, $"unload:{commandContext.WorldTick}:{actor}", actor, cart.Id, lot.Id, quantity, plan.Choice.Kind == HandcartCargoKind.UnloadGround));

    internal static Command MoveGroundCargo(IReadOnlyList<string> remaining, GridPoint position, bool preserveCustody) => new PreparedCommand(inventory => InventoryAuthority.MoveGroundCargo(inventory, remaining, new(position.X, position.Y), preserveCustody, holdAccess: remaining.SelectMany(id => InventoryHoldRules.Family(InventoryIndex.For(inventory), inventory.GetLot(id))).Distinct().ToArray()));
}
