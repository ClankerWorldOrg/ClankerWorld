using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ApplyGatheringInventory(IInventoryWorldWorld commandContext, ToolGatheringPlan plan, string actor, string itemKind, string? deliveryBuildingId) => new PreparedCommand(inventory =>
            {
                var updated = inventory;
                var wornToolLotId = plan.ToolLotId;
                if (wornToolLotId is not null)
                {
                    var tool = updated.GetLot(wornToolLotId);
                    if (tool.Quantity > 1)
                    {
                        wornToolLotId = commandContext.UsedToolLotId(tool.Id, commandContext.WorldTick, actor);
                        updated = InventoryAuthority.SplitLot(updated, tool.Id, 1, wornToolLotId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    }
                }

                var materialLotId = commandContext.MaterialLotId(commandContext.WorldTick, actor);
                updated = commandContext.MarkBuildingMaterialDelivery(InventoryAuthority.AddLot(updated, materialLotId,
                    itemKind, actor, plan.Quantity, commandContext.WorldTick), materialLotId, deliveryBuildingId);
                if (plan.TreeSeedQuantity > 0)
                {
                    var seedLotId = commandContext.TreeSeedLotId(commandContext.WorldTick, actor);
                    updated = commandContext.MarkBuildingMaterialDelivery(InventoryAuthority.AddLot(updated, seedLotId,
                        TreeGrowthRules.TreeSeedItem, actor, plan.TreeSeedQuantity, commandContext.WorldTick), seedLotId,
                        deliveryBuildingId);
                }
                if (wornToolLotId is not null && plan.WearLossBasisPoints > 0)
                    updated = InventoryAuthority.WearSingleUnit(updated, wornToolLotId, plan.WearLossBasisPoints, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                return updated;
            });

    internal static Command ApplyToolWork(IInventoryWorldWorld commandContext, string actor, ToolWorkPlan[] plans) => new PreparedCommand(inventory => commandContext.ApplyToolWorkToInventory(inventory, actor, commandContext.WorldTick, plans));

    internal static Command CompletePlannedToolRepair(IInventoryWorldWorld commandContext, string repairId, Action<string> recordrepairId, string actor, IReadOnlyList<PlannedRepairMaterial> materials, List<string> reservations) => new PreparedCommand(current =>
            {
                var updated = current;
                var currentTool = updated.GetLot(repairId);
                if (currentTool.Quantity > 1)
                {
                    repairId = RecordPreparedValue(commandContext.RepairedToolLotId(currentTool.Id, commandContext.WorldTick, actor), recordrepairId);
                    updated = InventoryAuthority.SplitLot(updated, currentTool.Id, 1, repairId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                }

                foreach (var input in materials)
                {
                    var material = updated.GetLot(input.StockId);
                    var reservationId = commandContext.ToolRepairReservationId(commandContext.WorldTick, actor, repairId, input.ItemKind);
                    var materialId = material.Id;
                    if (currentTool.OwnerId != actor)
                    {
                        // Repairing a borrowed household tool, the member gives their materials to the household.
                        var giftId = commandContext.ToolRepairGiftId(commandContext.WorldTick, actor, repairId, input.ItemKind);
                        updated = InventoryAuthority.Transfer(updated, giftId, actor, currentTool.OwnerId, material.Id,
                            input.Quantity, "equipment_repair_materials", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                        if (input.Quantity < material.Quantity) materialId = DerivedId.Create($"{material.Id}#transfer:{giftId}");
                    }
                    updated = InventoryAuthority.Reserve(updated, reservationId, currentTool.OwnerId, materialId,
                        input.Quantity, "equipment_repair", checked(commandContext.WorldTick + 1), holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    reservations.Add(reservationId);
                }

                return InventoryAuthority.RepairSingleUnit(updated, repairId, 10_000, reservations.Select(Identity.Parse).ToArray(), holdAccess: InventoryHoldRules.BorrowedBy(actor));
            });
}
