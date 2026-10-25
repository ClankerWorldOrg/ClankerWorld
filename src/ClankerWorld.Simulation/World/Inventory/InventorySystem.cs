using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal abstract class Command
    {
        private protected Command() { }
        internal abstract InventoryCheckpoint Transform(InventoryCheckpoint inventory);
    }

    private sealed class PreparedCommand(Func<InventoryCheckpoint, InventoryCheckpoint> transform) : Command
    {
        internal override InventoryCheckpoint Transform(InventoryCheckpoint inventory) => transform(inventory);
    }

    private static T RecordPreparedValue<T>(T value, Action<T> record)
    {
        record(value);
        return value;
    }

    private static T RecordPreparedOutputs<T>(Func<T> operation, Action record)
    {
        try { return operation(); }
        finally { record(); }
    }

    internal static SocietyOperationResult Execute(IInventoryExecutionWorld world, SocietyCheckpoint checkpoint, Command command)
    {
        var equipmentBefore = world.Agents.Values.ToDictionary(person => person.AgentId, person => person.Equipment, StringComparer.Ordinal);
        var updated = InventoryStorageHistory.RecordTransition(checkpoint.Inventory, MarketStockHolds.Update(command.Transform(checkpoint.Inventory), world.Towns));
        foreach (var building in world.Simulation.Buildings)
        {
            var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
            if (BuildingStorageRules.Capacity(definition, building) is not { } capacity)
                continue;
            var before = checkpoint.Inventory.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) + world.ReservedShopStorageSpace(building.InstanceId, checkpoint.Inventory);
            var after = updated.Lots.Where(lot => lot.StorageBuildingId == building.InstanceId).Sum(lot => lot.Quantity) + world.ReservedShopStorageSpace(building.InstanceId, updated);
            if (after > capacity && after > before)
                throw new InvalidOperationException("The building's storage is full; carry the remaining stock or expand it first.");
        }

        foreach (var person in world.Agents.Values)
        {
            var before = PersonalEquipmentRules.CarriedQuantity(checkpoint.Inventory, person.AgentId, equipmentBefore[person.AgentId]) + world.ReservedShopCarrySpace(person.AgentId, checkpoint.Inventory);
            var after = PersonalEquipmentRules.CarriedQuantity(updated, person.AgentId, person.Equipment) + world.ReservedShopCarrySpace(person.AgentId, updated);
            if (after > before && after > PersonalEquipmentRules.Capacity(updated, person.AgentId, person.Equipment) + world.HorseCargoCapacity(person.AgentId))
                throw new InvalidOperationException("The person is carrying as much as they can; store or set down a load first.");
        }

        return new SocietyOperationResult(checkpoint with { Inventory = updated }, null, []);
    }
    internal static Command CompleteRecipeInventory(IInventoryRuntimeWorld commandContext, WorldProductionJob job, RecipeDefinition recipe, string productionOwner, long targetTick, PlacedBuilding? productionBuilding, ToolWorkPlan? knifePlan) => new PreparedCommand(inventory =>
            {
                var current = inventory;
                foreach (var reservationId in job.InputReservationIds.Order())
                {
                    current = InventoryAuthority.ConsumeReservation(current, reservationId);
                }

                for (var outputIndex = 0; outputIndex < recipe.Outputs.Count; outputIndex++)
                {
                    var output = recipe.Outputs[outputIndex];
                    var outputLotId = commandContext.ProductionOutputLotId(job.JobId, outputIndex);
                    var vessel = InventoryContainerRules.IsContainer(output.ResourceId);
                    for (var unitIndex = 0; unitIndex < (vessel ? output.Amount : 1); unitIndex++)
                    {
                        current = InventoryAuthority.AddLot(
                            current,
                            unitIndex == 0 ? outputLotId : commandContext.ProductionVesselUnitId(outputLotId, unitIndex),
                            output.ResourceId,
                            output.ResourceId == InventoryContainerRules.Handcart ? job.WorkerId : productionOwner,
                            vessel ? 1 : output.Amount,
                            targetTick,
                            storageBuildingId: output.ResourceId != InventoryContainerRules.Handcart && productionBuilding?.HouseholdId is not null
                                ? productionBuilding.InstanceId : null,
                            groundPosition: output.ResourceId == InventoryContainerRules.Handcart
                                ? new InventoryGroundPosition(productionBuilding!.Position.X, productionBuilding.Position.Y) : null);
                    }
                }

                return knifePlan is null ? current : commandContext.ApplyToolWorkToInventory(current, job.WorkerId,
                    targetTick, [knifePlan]);
            });

    internal static Command AdvanceInventoryClock(IInventoryRuntimeWorld commandContext) => new PreparedCommand(inventory => InventoryAuthority.ProcessSpoilage(inventory, commandContext.WorldTick, 0));

    internal static Command SpoilPerishableStock(IInventoryRuntimeWorld commandContext, HashSet<string> shelteredOwners) => new PreparedCommand(inventory => InventoryAuthority.ProcessSpoilage(inventory, commandContext.WorldTick, 4,
                commandContext.PerishableKinds, shelteredOwners, commandContext.FoodSpoilageRates));

    internal static Command ApplyPlannedEquipmentPickup(bool keepOwner, string operationId, PlannedEquipmentPickup pickup, string actor) => new PreparedCommand(inventory => keepOwner
                ? InventoryAuthority.Relocate(inventory, operationId, pickup.StockId, pickup.OwnerId, 1, actor, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                : InventoryAuthority.Transfer(inventory, operationId,
                    pickup.OwnerId, actor, pickup.StockId, 1, "equipment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command AddDeveloperStock(string lotId, WorldDeveloperEdit edit, string actor, bool individualVessels) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory, lotId, edit.Value, actor,
                            individualVessels ? 1 : edit.Amount));

    internal static Command DiscardDeveloperStock(string actor, InventoryLot lot, int amount) => new PreparedCommand(inventory => InventoryAuthority.Discard(inventory, actor, lot.Id, amount));

    internal static Command CollectHarvestedFood(IInventoryRuntimeWorld commandContext, string harvestedLotId, string itemKind, string agentId, int harvestYield) => new PreparedCommand(inventory => InventoryAuthority.AddLot(
                inventory, harvestedLotId,
                itemKind,
                agentId,
                harvestYield,
                commandContext.WorldTick));

    internal static Command ReserveHarvestedOrchardSeed(IInventoryRuntimeWorld commandContext, string lotId, string agentId, int seedQuantity) => new PreparedCommand(inventory => InventoryAuthority.Reserve(
                    InventoryAuthority.AddLot(inventory, lotId, TreeGrowthRules.OrchardSeedItem, agentId,
                        seedQuantity, commandContext.WorldTick), commandContext.OrchardReplantingReservationId(lotId),
                    agentId, lotId, 1, "orchard_replanting", long.MaxValue, holdAccess: InventoryHoldRules.BorrowedBy(agentId)));

    internal static Command TakeHouseholdServingFromPot(string operationId, string householdId, string actor, string potId, InventoryLot food) => new PreparedCommand(inventory => InventoryAuthority.TakeFromContainer(inventory,
                    operationId, householdId, actor, potId, food.Id, 1, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command TakeHouseholdServingFromStock(string operationId, string householdId, string actor, InventoryLot food, string purpose) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                    operationId, householdId, actor, food.Id, 1, purpose, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
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
    internal static Command ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));

    internal static Command HoldOrderReservations(IReadOnlyList<Identity> reservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, reservations));

    internal static Command ReleaseExpansionOrderInputs(BuildingExpansionJob job) => new PreparedCommand(inventory =>
            {
                foreach (var id in job.InputReservationIds)
                    if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is
                        { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "owner_expansion_stopped");
                return inventory;
            });

    internal static Command TransferOrderDelivery(OrderSystem.DeliveryOrderPlan plan, string operation, string actor) => new PreparedCommand(inventory => plan.TakeFromContainer
                ? InventoryAuthority.TakeFromContainer(inventory, operation, plan.Carrier.OwnerId, actor,
                    plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity, destinationDeliveryBuildingId: plan.Destination.InstanceId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                : InventoryAuthority.Transfer(inventory, operation, plan.Carrier.OwnerId,
                    plan.DirectDelivery ? plan.DestinationOwnerId : actor, plan.Carrier.Id, plan.TransferQuantity,
                    plan.DirectDelivery ? "owner_stock_delivered" : "owner_stock_picked_up",
                    destinationStorageBuildingId: plan.DirectDelivery ? plan.Destination.InstanceId : null,
                    destinationDeliveryBuildingId: plan.DirectDelivery ? null : plan.Destination.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, plan.DirectDelivery ? plan.Destination.InstanceId : null)));

    internal static Command OrdersExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));

    internal static Command OrdersHoldOrderReservations(IReadOnlyList<Identity> reservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, reservations));

    internal static Command ReleaseProductionOrderInputs(WorldProductionJob job) => new PreparedCommand(inventory =>
                {
                    foreach (var id in job.InputReservationIds)
                        if (inventory.Reservations.FirstOrDefault(item => item.Id == id) is
                            { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                            inventory = InventoryAuthority.ReleaseReservation(inventory, id, "production_order_cancelled");
                    return inventory;
                });
}
