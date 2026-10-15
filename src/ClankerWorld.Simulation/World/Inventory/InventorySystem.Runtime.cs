using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command CompleteRecipeInventory(IInventoryRuntimeWorld commandContext, WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knifePlan, long targetTick, PlacedBuilding? productionBuilding, string productionOwner) => new PreparedCommand(inventory =>
{
    var current = inventory;
    foreach (var reservationId in job.InputReservationIds.Order(StringComparer.Ordinal))
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
            current = InventoryAuthority.AddLot(current, unitIndex == 0 ? outputLotId : commandContext.ProductionVesselUnitId(outputLotId, unitIndex), output.ResourceId, output.ResourceId == InventoryContainerRules.Handcart ? job.WorkerId : productionOwner, vessel ? 1 : output.Amount, targetTick, storageBuildingId: output.ResourceId != InventoryContainerRules.Handcart && productionBuilding?.HouseholdId is not null ? productionBuilding.InstanceId : null, groundPosition: output.ResourceId == InventoryContainerRules.Handcart ? new InventoryGroundPosition(productionBuilding!.Position.X, productionBuilding.Position.Y) : null);
        }
    }

    return knifePlan is null ? current : commandContext.ApplyToolWorkToInventory(current, job.WorkerId, targetTick, [knifePlan]);
});

    internal static Command CollectHarvestedFood(IInventoryRuntimeWorld commandContext, string agentId, string itemKind, int harvestYield, string harvestedLotId) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory, harvestedLotId, itemKind, agentId, harvestYield, commandContext.WorldTick));

    internal static Command ReserveHarvestedOrchardSeed(IInventoryRuntimeWorld commandContext, string agentId, int seedQuantity, string lotId) => new PreparedCommand(inventory => InventoryAuthority.Reserve(InventoryAuthority.AddLot(inventory, lotId, TreeGrowthRules.OrchardSeedItem, agentId, seedQuantity, commandContext.WorldTick), commandContext.OrchardReplantingReservationId(lotId), agentId, lotId, 1, "orchard_replanting", long.MaxValue, holdAccess: InventoryHoldRules.BorrowedBy(agentId)));

    internal static Command TakeHouseholdServingFromPot(string actor, InventoryLot food, string operationId, string householdId, string potId) => new PreparedCommand(inventory => InventoryAuthority.TakeFromContainer(inventory, operationId, householdId, actor, potId, food.Id, 1, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command TakeHouseholdServingFromStock(string actor, InventoryLot food, string operationId, string purpose, string householdId) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operationId, householdId, actor, food.Id, 1, purpose, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command AddDeveloperStock(WorldDeveloperEdit edit, string actor, bool individualVessels, string lotId) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory, lotId, edit.Value, actor, individualVessels ? 1 : edit.Amount));

    internal static Command DiscardDeveloperStock(string actor, InventoryLot lot, int amount) => new PreparedCommand(inventory => InventoryAuthority.Discard(inventory, actor, lot.Id, amount));

    internal static Command AdvanceInventoryClock(IInventoryRuntimeWorld commandContext) => new PreparedCommand(inventory => InventoryAuthority.ProcessSpoilage(inventory, commandContext.WorldTick, 0));

    internal static Command SpoilPerishableStock(IInventoryRuntimeWorld commandContext, HashSet<string> shelteredOwners) => new PreparedCommand(inventory => InventoryAuthority.ProcessSpoilage(inventory, commandContext.WorldTick, 4, commandContext.PerishableKinds, shelteredOwners, commandContext.FoodSpoilageRates));

    internal static Command ApplyPlannedEquipmentPickup(IInventoryRuntimeWorld commandContext, string actor, PlannedEquipmentPickup pickup, bool keepOwner) => new PreparedCommand(inventory => keepOwner ? InventoryAuthority.Relocate(inventory, $"equipment:{commandContext.WorldTick}:{actor}:{pickup.ItemKind}", pickup.StockId, pickup.OwnerId, 1, actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)) : InventoryAuthority.Transfer(inventory, $"equipment:{commandContext.WorldTick}:{actor}:{pickup.ItemKind}", pickup.OwnerId, actor, pickup.StockId, 1, "equipment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));
}
