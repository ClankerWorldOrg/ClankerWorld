using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using FoodHarvestEffect = ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
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
}
