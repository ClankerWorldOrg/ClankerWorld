using ClankerWorld.Simulation.Kernel;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ApplyTownSalvagePlan(ITownWorld world, TownStockActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                WorldRuntime.CivicSharedRules.TownSalvageOperationId(world.WorldTick, plan.Actor, plan.CandidateId),
                plan.Choice.TownId, plan.Actor, plan.Choice.StockId, plan.Quantity,
                "town_stock_salvaged", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command ApplyCheckedNonLandWork(string operation, string actor, TownRemedyTerm term, InventoryLot lot, int remaining) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operation, actor,
                        term.BeneficiaryId!, lot.Id, remaining, "voluntary_restitution", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command DeliverNonLandTownGoods(string operation, string actor, PlacedBuilding warehouse, InventoryLot lot, int quantity) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operation, actor, warehouse.TownId!,
                lot.Id, quantity, "town_resources_stored", warehouse.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)));

    internal static Command AcceptFirstTownLayout(InventoryCheckpoint starterInventory) => new PreparedCommand(_ => starterInventory);

    internal static Command CollectLandSalePayment(ITownWorld world, string actor, TownLandTransferRequest request, PlannedEquipmentPickup pickup, LandSaleCollectionActionPlan? plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("land-sale-collect:", $"{world.WorldTick}:{actor}:{request.Id}"),
                pickup.OwnerId, actor, pickup.StockId, plan!.Quantity, "land_sale_payment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command SettleLandSale(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);
}
