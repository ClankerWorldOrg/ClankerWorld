using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;
using System.Globalization;
using ClankerWorld.Simulation.Society;
using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ApplyFarmStockHaul(WorldRuntime.FarmStockHaulPlan plan, string operationId, string householdId, string actor, string purpose, string destinationId) => new PreparedCommand(inventory => plan.MoveContainerFamily
                ? InventoryAuthority.Transfer(inventory, operationId, householdId, actor,
                    plan.Carrier.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                : plan.Resource.ContainerLotId == plan.Carrier.Id
                    ? InventoryAuthority.TakeFromContainer(inventory, operationId, householdId, actor,
                        plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity,
                        destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                    : InventoryAuthority.Transfer(inventory, operationId, householdId, actor,
                        plan.Resource.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command ApplyCheckedTownStockPlan(string operation, TownStockActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                operation, plan.Actor, plan.Choice.TownId, plan.Choice.StockId,
                plan.Quantity, "town_resources_stored", plan.WarehouseId,
                holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.WarehouseId)));

    internal static Command CollectHouseholdHaul(IInventoryEconomyWorld commandContext, HouseholdStockActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                    DerivedId.Create($"house-haul-pickup:{commandContext.WorldTick}:{plan.Actor}"), plan.HouseholdId, plan.Actor, plan.StockId, plan.Quantity,
                    "household_stock_picked_up", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command RecoverHouseDelivery(IInventoryEconomyWorld commandContext, HouseholdStockActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                    DerivedId.Create($"house-delivery-recovery:{commandContext.WorldTick}:{plan.Actor}:{plan.StockId}"), plan.Actor, plan.HouseholdId, plan.StockId, plan.Quantity,
                    "household_delivery_recovered", destinationGroundPosition: new(plan.Destination.X, plan.Destination.Y),
                    holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.DeliveryBuildingId)));

    internal static Command CollectHousePreparationTool(IInventoryEconomyWorld commandContext, HouseholdStockActionPlan plan) => new PreparedCommand(inventory => plan.OwnerId == plan.HouseholdId
                    ? InventoryAuthority.Relocate(inventory, DerivedId.Create($"house-preparation-tool:{commandContext.WorldTick}:{plan.Actor}"), plan.StockId,
                        plan.HouseholdId, plan.Quantity, storageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId))
                    : InventoryAuthority.Transfer(inventory, DerivedId.Create($"house-preparation-tool:{commandContext.WorldTick}:{plan.Actor}"), plan.Actor, plan.HouseholdId,
                        plan.StockId, plan.Quantity, "household_preparation_tool_stored", destinationStorageBuildingId: plan.HouseId,
                        holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command CollectHouseholdBuildingStock(string operation, HouseholdStockActionPlan plan, string eventKind) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operation, plan.OwnerId, plan.HouseholdId,
                    plan.StockId, plan.Quantity, eventKind, plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command DeliverProjectInput(IInventoryEconomyWorld commandContext, ProjectInputActionPlan plan, ProjectInputDelivery delivery) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                        IdentityRules.PrefixedReference("project-delivery:", $"{commandContext.WorldTick}:{plan.Actor}"), plan.Actor, plan.OwnerId, delivery.LotId,
                        delivery.Quantity, "project_contribution", plan.HouseId,
                        destinationGroundPosition: plan.HouseId is null ? new InventoryGroundPosition(plan.Destination!.Value.X, plan.Destination!.Value.Y) : null,
                        holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command CollectWarehouseProjectInput(IInventoryEconomyWorld commandContext, ProjectInputActionPlan plan, ProjectInputPickup pickup) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                        IdentityRules.PrefixedReference("warehouse-pickup:", $"{commandContext.WorldTick}:{plan.Actor}"), pickup.Warehouse.TownId!, plan.Actor,
                        pickup.LotId, pickup.Quantity, "town_resource_collected", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command CollectWaterJug(IInventoryEconomyWorld commandContext, VesselActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                        DerivedId.Create($"water-jug-pickup:{commandContext.WorldTick}:{plan.Actor}"), plan.HouseholdId, plan.Actor, plan.VesselId, 1,
                        "water_jug_collected", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command FillCarriedWaterJug(IInventoryEconomyWorld commandContext, VesselActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory,
                        DerivedId.Create($"{plan.VesselId}#water:{commandContext.WorldTick.ToString(CultureInfo.InvariantCulture)}"), InventoryContainerRules.FreshWater,
                        plan.Actor, plan.Quantity, commandContext.WorldTick, containerLotId: plan.VesselId));

    internal static Command ReturnFilledWaterJug(IInventoryEconomyWorld commandContext, VesselActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                        DerivedId.Create($"water-jug-return:{commandContext.WorldTick}:{plan.Actor}"), plan.Actor, plan.HouseholdId, plan.VesselId, 1,
                        "water_jug_returned", destinationStorageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command CollectEmptyVessel(IInventoryEconomyWorld commandContext, VesselActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                        DerivedId.Create($"empty-vessel-pickup:{commandContext.WorldTick}:{plan.Actor}"), plan.HouseholdId, plan.Actor, plan.VesselId, 1,
                        "empty_vessel_picked_up", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command TakeFoodFromPot(IInventoryEconomyWorld commandContext, VesselActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.TakeFromContainer(inventory,
                        DerivedId.Create($"food-pot-take:{commandContext.WorldTick}:{plan.Actor}"), plan.HouseholdId, plan.Actor, plan.VesselId, plan.Food!.StockId,
                        plan.Quantity, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command StorePlannedPotFood(PlannedPotFood food, VesselActionPlan plan, string operationId) => new PreparedCommand(inventory =>
            {
                var sourceId = food.StockId;
                if (food.OwnerId == plan.Actor)
                {
                    var source = inventory.GetLot(sourceId);
                    inventory = InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, sourceId,
                        plan.Quantity, "food_stored_in_pot", destinationStorageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId));
                    if (plan.Quantity != source.Quantity) sourceId = DerivedId.Create($"{sourceId}#transfer:{operationId}");
                }
                return InventoryAuthority.PutIntoContainer(inventory, operationId, plan.HouseholdId, plan.VesselId, sourceId,
                    plan.Quantity, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor));
            });

    internal static Command CompleteOrnamentWear(IInventoryEconomyWorld commandContext, InventoryLot lot, string actor, string equippedId, Action<string> recordequippedId, AgentState person, PersonalEquipment? previous) => new PreparedCommand(inventory =>
                {
                    if (lot.OwnerId != actor)
                    {
                        var operation = $"ornament-wear:{commandContext.WorldTick}:{commandContext.nextEventId}:{actor}:{lot.Id}";
                        inventory = InventoryAuthority.Transfer(inventory, operation, lot.OwnerId, actor, lot.Id, 1, "ornament_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                        equippedId = RecordPreparedValue(lot.Quantity == 1 ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + operation), recordequippedId);
                    }
                    else if (lot.Quantity > 1)
                    {
                        equippedId = RecordPreparedValue(commandContext.OrnamentUnitId(commandContext.WorldTick, commandContext.nextEventId, actor, lot.Id), recordequippedId);
                        inventory = InventoryAuthority.SplitLot(inventory, lot.Id, 1, equippedId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    }
                    commandContext.agentStore.RecordEquipment(actor, person, (previous ?? new PersonalEquipment()) with { OrnamentLotId = equippedId });
                    return inventory;
                });

    internal static Command CompleteOrnamentGift(IInventoryEconomyWorld commandContext, string operation, string actor, string recipientId, InventoryLot lot, AgentState person) => new PreparedCommand(inventory =>
                {
                    var updated = InventoryAuthority.Transfer(inventory, operation, actor, recipientId, lot.Id, 1, "ornament_given", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    if (person.Equipment?.OrnamentLotId == lot.Id)
                        commandContext.agentStore.RecordEquipment(actor, person, person.Equipment with { OrnamentLotId = null });
                    return updated;
                });

    internal static Command ApplyCheckedWorkstationSupplyPlan(WorkstationSupplyActionPlan plan, string operationId) => new PreparedCommand(inventory => plan.Kind == WorkstationSupplyKind.Carried
                ? plan.StockOwnerId == plan.HouseholdId
                    ? InventoryAuthority.Relocate(inventory, operationId, plan.StockId!, plan.HouseholdId,
                        plan.Quantity, storageBuildingId: plan.BuildingId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.BuildingId))
                    : InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, plan.StockId!,
                        plan.Quantity, "workstation_supplied", plan.BuildingId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.BuildingId))
                : InventoryAuthority.Transfer(inventory, operationId, plan.StockOwnerId!, plan.Actor, plan.StockId!,
                    plan.Quantity, "workstation_input_picked_up", destinationDeliveryBuildingId: plan.BuildingId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command ApplyCargoStorage(IInventoryEconomyWorld commandContext, PlannedCargoMove move, PlannedCargoStorage plan) => new PreparedCommand(inventory =>
                {
                    if (move.OrchardReservationId is { } reservationId && move.ReleaseQuantity > 0)
                        inventory = InventoryAuthority.ReleaseReservationQuantity(inventory, reservationId, move.ReleaseQuantity, "orchard_food_recovery");
                    var access = InventoryHoldRules.DeliveryFor(plan.Actor, plan.StorageBuildingId);
                    InventoryGroundPosition? ground = plan.StorageBuildingId is null ? new InventoryGroundPosition(plan.Destination.X, plan.Destination.Y) : null;
                    return move.OwnerId == plan.HouseholdId
                        ? InventoryAuthority.Relocate(inventory, DerivedId.Create($"spare-cargo:{commandContext.WorldTick}:{plan.Actor}:{move.StockId}"), move.StockId,
                            plan.HouseholdId, move.Quantity, storageBuildingId: plan.StorageBuildingId, groundPosition: ground, holdAccess: access)
                        : InventoryAuthority.Transfer(inventory, DerivedId.Create($"spare-cargo:{commandContext.WorldTick}:{plan.Actor}:{move.StockId}"), plan.Actor,
                            plan.HouseholdId, move.StockId, move.Quantity, "spare_cargo_stored", plan.StorageBuildingId,
                            destinationGroundPosition: ground, holdAccess: access);
                });

    internal static Command ApplyCheckedProjectAssistance(IInventoryEconomyWorld commandContext, string helperId, string recipient, ProjectAssistanceActionPlan plan, int quantity) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("project-share:", $"{commandContext.WorldTick}:{helperId}"),
                helperId, recipient, plan.LotId!, quantity, "project_request_fulfilled",
                plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(helperId, plan.HouseId)));

    internal static Command DeliverExpansionStock(IInventoryEconomyWorld commandContext, string actor, PlacedBuilding building, ExpansionDeliveryStep delivery, string owner) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                            IdentityRules.PrefixedReference("expansion-delivery:", $"{commandContext.WorldTick}:{actor}:{building.InstanceId}:{delivery.LotId}"), actor, owner,
                            delivery.LotId, delivery.Quantity, "building_expansion_material_delivered",
                            destinationStorageBuildingId: building.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, building.InstanceId)));

    internal static Command DeliverExpansionGroundStock(IInventoryEconomyWorld commandContext, string actor, PlacedBuilding building, ExpansionDeliveryStep delivery, string owner) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                            IdentityRules.PrefixedReference("expansion-ground-delivery:", $"{commandContext.WorldTick}:{actor}:{building.InstanceId}:{delivery.LotId}"), actor,
                            owner, delivery.LotId, delivery.Quantity,
                            "building_expansion_material_delivered", destinationGroundPosition: commandContext.ExpansionGroundPosition(building),
                            holdAccess: InventoryHoldRules.DeliveryFor(actor, building.InstanceId)));

    internal static Command CollectExpansionStock(IInventoryEconomyWorld commandContext, string actor, PlacedBuilding building, ExpansionPickupStep pickup) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                            IdentityRules.PrefixedReference("expansion-pickup:", $"{commandContext.WorldTick}:{actor}:{building.InstanceId}:{pickup.LotId}"),
                            pickup.OwnerId, actor, pickup.LotId, pickup.Quantity, "building_expansion_material_picked_up",
                            destinationDeliveryBuildingId: building.HouseholdId is null ? null : building.InstanceId,
                            holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command ApplyCheckedStoreStockPlan(StoreStockActionPlan plan, string operationId) => new PreparedCommand(inventory => plan.DirectDelivery
                ? InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, plan.StockId, plan.Quantity,
                    "store_stock_delivered", destinationStorageBuildingId: plan.StoreId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.StoreId))
                : InventoryAuthority.Transfer(inventory, operationId, plan.HouseholdId, plan.Actor, plan.StockId, plan.Quantity,
                    "store_stock_collected", destinationDeliveryBuildingId: plan.StoreId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command StoreSpareCargo(IInventoryEconomyWorld commandContext, WorldRuntime.SpareCargoMove move, InventoryLot lot, string actor, AgentState state, int quantity, string householdId, (GridPoint Position, int Range, string? StorageBuildingId) destination) => new PreparedCommand(inventory =>
                {
                    if (move.OrchardReservationId is { } reservationId)
                    {
                        var currentLot = inventory.GetLot(lot.Id);
                        var reservation = commandContext.OrchardReservationForFood(inventory, actor, currentLot);
                        if (!commandContext.NeedsUrgentFood(state) || commandContext.PreferredFood(actor, actor).Any() ||
                            reservation is null || reservation.Id != reservationId)
                            throw new InvalidOperationException("The orchard seed is no longer available for food recovery.");
                        var releaseQuantity = Math.Max(0, quantity - (currentLot.Quantity - reservation.Quantity));
                        if (releaseQuantity > 0)
                            inventory = InventoryAuthority.ReleaseReservationQuantity(inventory, reservationId, releaseQuantity,
                                "orchard_food_recovery");
                    }
                    return lot.OwnerId == householdId
                        ? InventoryAuthority.Relocate(inventory, IdentityRules.PrefixedReference("spare-cargo:", $"{commandContext.WorldTick}:{actor}:{lot.Id}"), lot.Id, householdId, quantity,
                            storageBuildingId: destination.StorageBuildingId, groundPosition: destination.StorageBuildingId is null
                                ? new InventoryGroundPosition(destination.Position.X, destination.Position.Y) : null, holdAccess: InventoryHoldRules.DeliveryFor(actor, destination.StorageBuildingId))
                        : InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("spare-cargo:", $"{commandContext.WorldTick}:{actor}:{lot.Id}"), actor, householdId, lot.Id, quantity,
                            "spare_cargo_stored", destination.StorageBuildingId, destinationGroundPosition: destination.StorageBuildingId is null
                                ? new InventoryGroundPosition(destination.Position.X, destination.Position.Y) : null, holdAccess: InventoryHoldRules.DeliveryFor(actor, destination.StorageBuildingId));
                });

    internal static Command StartEquipmentRepair(IInventoryEconomyWorld commandContext, IReadOnlyList<PlannedRepairMaterial> materials, string actor, List<string> ids) => new PreparedCommand(inventory =>
            {
                foreach (var material in materials)
                {
                    var id = commandContext.EquipmentRepairReservationId(commandContext.WorldTick, actor, material.ItemKind);
                    inventory = InventoryAuthority.Reserve(inventory, id, actor, material.StockId, material.Quantity,
                        "equipment_repair", commandContext.WorldTick + 120, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    ids.Add(id);
                }
                return inventory;
            });

    internal static Command ContinueEquipmentRepair(EquipmentRepairWork repair, string actor) => new PreparedCommand(inventory => InventoryAuthority.RepairSingleUnit(inventory, repair.LotId, 6_000, repair.MaterialReservationIds, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command CancelEquipmentRepair(EquipmentRepairWork repair) => new PreparedCommand(inventory =>
            {
                foreach (var id in repair.MaterialReservationIds)
                    if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "equipment_repair_interrupted");
                return inventory;
            });

    internal static Command WearEquippedClothing(InventoryLot garment, AgentState person) => new PreparedCommand(inventory => InventoryAuthority.WearSingleUnit(inventory, garment.Id, 10, holdAccess: InventoryHoldRules.BorrowedBy(person.AgentId)));

    internal static Command WearCarryAid(InventoryLot aid, string actor) => new PreparedCommand(inventory => InventoryAuthority.WearSingleUnit(inventory, aid.Id, 10, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command CollectWrittenKnowledge(IInventoryEconomyWorld commandContext, InventoryLot lot, string actor, AgentKnowledgeArtifact artifact) => new PreparedCommand(inventory => lot.OwnerId == actor
                    ? InventoryAuthority.Relocate(inventory, IdentityRules.PrefixedReference("knowledge-collect:", $"{commandContext.WorldTick}:{actor}:{artifact.Id}"),
                        lot.Id, actor, 1, actor, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                    : InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("knowledge-collect:", $"{commandContext.WorldTick}:{actor}:{artifact.Id}"),
                        lot.OwnerId, actor, lot.Id, 1, "knowledge_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command StoreWrittenKnowledge(IInventoryEconomyWorld commandContext, string actor, AgentKnowledgeArtifact artifact, PlacedBuilding house) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                IdentityRules.PrefixedReference("knowledge-store:", $"{commandContext.WorldTick}:{actor}:{artifact.Id}"), artifact.LotId, actor, 1,
                storageBuildingId: house.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, house.InstanceId)));

    internal static Command ApplyCheckedSmithDeliveryPlan(SmithDeliveryActionPlan plan, string operationId, string purpose) => new PreparedCommand(inventory => plan.Kind == SmithDeliveryKind.Ore
                ? InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, plan.StockId,
                    plan.Quantity, purpose, plan.SmithId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.SmithId))
                : plan.DirectDelivery
                    ? InventoryAuthority.Transfer(inventory, operationId, plan.Actor, plan.HouseholdId, plan.StockId,
                        plan.Quantity, purpose, destinationStorageBuildingId: plan.SmithId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.SmithId))
                    : InventoryAuthority.Transfer(inventory, operationId, plan.OwnerId, plan.Actor, plan.StockId,
                        plan.Quantity, purpose, destinationDeliveryBuildingId: plan.SmithId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
    internal static Command EquipPrivateEquipment(IInventoryEconomyWorld commandContext, string actor, InventoryLot item, bool carryAid, string equippedId, Action<string> recordEquippedId, PersonalEquipment equipment) => new PreparedCommand(inventory =>
{
    if (item.OwnerId != actor)
    {
        var transferId = commandContext.EquipmentTransferId(commandContext.WorldTick, actor, item.Id);
        inventory = InventoryAuthority.Transfer(inventory, transferId, item.OwnerId, actor, item.Id, 1, "equipment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor));
        equippedId = RecordPreparedValue(item.Quantity == 1 ? item.Id : DerivedId.Create(item.Id + "#transfer:" + transferId), recordEquippedId);
    }
    else if (item.Quantity > 1)
    {
        equippedId = RecordPreparedValue(commandContext.EquippedUnitId(commandContext.WorldTick, actor, item.Id), recordEquippedId);
        inventory = InventoryAuthority.SplitLot(inventory, item.Id, 1, equippedId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
    }

    commandContext.agentStore.RecordEquipment(actor, commandContext.agents[actor], carryAid ? equipment with { CarryAidLotId = equippedId } : equipment with { ClothingLotId = equippedId });
    return inventory;
});

}
