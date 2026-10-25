using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static CognitionCandidate? CollectionOrderCandidateFor(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.ReadyForBriefInteraction(actor) || world.FreeCarryCapacity(actor) <= 0) return null;
        var lot = world.CollectionOrderGoods(instruction, GoodsUse.Recover)
            .OrderBy(item => world.Map.FootDistance(person.Position, world.HouseholdStockPosition(item)))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return lot is null ? null : new(instruction.Task!.Action,
            $"Walk to your own stored or dropped {world.CollectionOrderGoodsName(instruction.Task)} and pick it up within your carrying limit.", 0, lot.Id);
    }


    internal static IEnumerable<InventoryLot> CollectionOrderGoods(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, GoodsUse use = GoodsUse.RecoveryHoldings) => (use == GoodsUse.Recover ? world.RecoverablePersonalGoods(instruction.TargetAgentId)
            : world.PersonalGoodsAwaitingCollection(instruction.TargetAgentId)).Where(lot =>
            (instruction.Task!.Action switch
            {
                "collect_food" => world.IsEdibleFood(lot.ItemKind) &&
                    (instruction.Task.TargetFoodKind is null || lot.ItemKind == instruction.Task.TargetFoodKind),
                "collect_equipment" => lot.ItemKind == instruction.Task.TargetEquipmentKind,
                "collect_goods" => lot.ItemKind == instruction.Task.TargetItemKind,
                _ => lot.ItemKind == instruction.Task.TargetMaterialKind,
            }) &&
            (instruction.Task.TargetPosition is null || world.HouseholdStockPosition(lot) == instruction.Task.TargetPosition));

    internal static void ExecuteCollectionOrderStep(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = world.CollectionOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.CollectionOrderBlockedReason(instruction, person));
            return;
        }
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (world.CollectPersonalGoods(instruction.TargetAgentId, lotId, remaining) is { } effect)
        {
            var receipt = world.PersonalCollectionReceipt(effect.MoveId);
            world.CreditOrderEffect(instruction, Identity.Parse(receipt),
                order.ProgressUnit == "collection_loads" ? 1 : effect.Quantity);
        }
    }


    internal static string CollectionOrderBlockedReason(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var goods = world.CollectionOrderGoodsName(instruction.Task!);
        var pluralGoods = instruction.Task!.Action == "collect_material" ? "materials" : goods;
        if (!world.MayFollowOwnerOrder(actor, instruction.Task!))
            return $"This agent is too young to collect stored {pluralGoods}.";
        if (instruction.Task!.TargetPosition is { } target && !world.Map.Contains(target))
            return "The requested tile is outside this world.";
        if (world.FreeCarryCapacity(actor) <= 0)
            return $"Carrying space is full; make room before collecting more {pluralGoods}.";
        if (!world.CollectionOrderGoods(instruction).Any())
            return instruction.Task.TargetPosition is not null
                ? $"No matching personal {goods} is available at the requested tile; goods must be your own and not reserved or promised for delivery."
                : $"No matching personal {goods} is available to collect; goods must be your own and not reserved or promised for delivery.";
        if (!world.CollectionOrderGoods(instruction).Any(lot => world.VesselFits(lot, world.FreeCarryCapacity(actor))))
            return "The vessel and all its contents must fit together; make more carrying space first.";
        if (!world.ReadyForBriefInteraction(actor))
            return $"The agent needs warmth before collecting {pluralGoods}.";
        return $"No open walking route reaches the agent's personal {goods} right now.";
    }
    internal static void InterruptOrdinaryFieldWorkForCustody(ICustodyOrdersWorld world, string actor, string lotId)
    {
        if (world.FarmWorkFor(actor) is { Work: { OrderInstructionId: null } work } field &&
            (work.HoeLotId == lotId || work.SickleLotId == lotId))
            world.CancelFarmWork(field);
    }


    internal static PlacedBuilding? CustodyOrderHouse(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, string? ownerId)
    {
        var order = instruction.Task!;
        if (ownerId is null || world.HouseForHousehold(ownerId) is not { } house ||
            order.TargetPosition is { } requested && house.Position != requested) return null;
        if (order.TargetStorageBuildingId is not null &&
            (house.InstanceId != order.TargetStorageBuildingId || house.HouseholdId != order.TargetStorageOwnerId ||
             house.Position != order.TargetStoragePosition)) return null;
        return house;
    }


    internal static OwnerQueuedInstruction BindCustodyOrder(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, PlacedBuilding house,
        string? lotId = null)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        if (order.TargetStorageBuildingId is null)
            order = order.UpdateDefinition(definition => definition switch
{
    StoreMaterialOrderDefinition task => OrderStore.PrepareBindCustodyOrderStoreMaterialOrderDefinition(task, house.InstanceId, house.HouseholdId, house.Position),
    StoreEquipmentOrderDefinition task => OrderStore.PrepareBindCustodyOrderStoreEquipmentOrderDefinition(task, house.InstanceId, house.HouseholdId, house.Position),
    StoreGoodsOrderDefinition task => OrderStore.PrepareBindCustodyOrderStoreGoodsOrderDefinition(task, house.InstanceId, house.HouseholdId, house.Position),
    ReturnBorrowedOrderDefinition task => OrderStore.PrepareBindCustodyOrderReturnBorrowedOrderDefinition(task, house.InstanceId, house.HouseholdId, house.Position),
    DeliverStockOrderDefinition task => OrderStore.PrepareBindCustodyOrderDeliverStockOrderDefinition(task, house.InstanceId, house.HouseholdId, house.Position),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
        if (lotId is not null) order = order.UpdateTypedDefinition<ReturnBorrowedOrderDefinition>(task =>
            OrderStore.PrepareBindCustodyOrderReturnBorrowedOrderDefinitionTargetLotId(task, lotId));
        current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, order);
        world.Orders.RecordCustodyOrder(current.IdempotencyKey, current);
        world.MarkCheckpointCurrent();
        return current;
    }


    internal static IEnumerable<InventoryLot> ReturnOrderGoods(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction) => world.ReturnableBorrowedGoods(instruction.TargetAgentId).Where(lot =>
            lot.ItemKind == instruction.Task!.TargetItemKind &&
            (instruction.Task.TargetLotId is null || lot.Id == instruction.Task.TargetLotId) &&
            (instruction.Task.TargetStorageOwnerId is null || lot.OwnerId == instruction.Task.TargetStorageOwnerId));

    internal static CognitionCandidate? ReturnOrderCandidateFor(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.ReadyForBriefInteraction(actor)) return null;
        foreach (var lot in world.ReturnOrderGoods(instruction).OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            if (world.CustodyOrderHouse(instruction, lot.OwnerId) is not { } house || world.DestinationRoom(house.InstanceId) <= 0 ||
                !world.VesselFits(lot, world.DestinationRoom(house.InstanceId)) ||
                !world.IsWithinInteractionRange(person.Position, house.Position, 1) &&
                world.Reach(actor, house.Position, ReachPurpose.HouseholdPickup, 1, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            return new("return_borrowed", "Carry borrowed goods back to their owning household's House without changing ownership.", 0, lot.Id);
        }
        return null;
    }


    internal static void ExecuteReturnOrderStep(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = world.ReturnOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.ReturnOrderBlockedReason(instruction, person));
            return;
        }
        var lot = world.ReturnOrderGoods(instruction).Single(item => item.Id == lotId);
        var house = world.CustodyOrderHouse(instruction, lot.OwnerId)!;
        instruction = world.BindCustodyOrder(instruction, house, lotId);
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (world.ReturnBorrowedGoods(instruction.TargetAgentId, lotId, remaining,
                order.TargetStorageBuildingId, order.TargetStorageOwnerId, order.TargetStoragePosition) is not { } effect) return;
        var receipt = world.BorrowedReturnReceipt(effect.MoveId);
        world.CreditOrderEffect(instruction, Identity.Parse(receipt), order.ProgressUnit == "return_loads" ? 1 : effect.Quantity);
        // Another load may use another lot, but it must keep the original owner and House.
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordReturnOrderStep(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateTypedDefinition<ReturnBorrowedOrderDefinition>(task =>
            OrderStore.PrepareBindCustodyOrderReturnBorrowedOrderDefinitionTargetLotId(task, null))
));
    }


    internal static string ReturnOrderBlockedReason(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.MayFollowOwnerOrder(actor, order)) return "This agent is too young to return borrowed goods.";
        if (order.TargetStorageOwnerId is { } owner && world.CustodyOrderHouse(instruction, owner) is null)
            return "The selected return House moved, changed owner or is no longer available; the order keeps its original destination.";
        var lots = world.ReturnOrderGoods(instruction).ToArray();
        if (lots.Length == 0)
            return "No matching borrowed goods are available; the selected goods must still be carried, unreserved and free of delivery promises.";
        var destinations = lots.Select(lot => (Lot: lot, House: world.CustodyOrderHouse(instruction, lot.OwnerId)))
            .Where(item => item.House is not null).ToArray();
        if (destinations.Length == 0) return "The goods' owning household has no House at the requested destination.";
        if (destinations.All(item => world.DestinationRoom(item.House!.InstanceId) <= 0 ||
                !world.VesselFits(item.Lot, world.DestinationRoom(item.House!.InstanceId))))
            return "The owning House needs room for the returned goods, including each whole vessel and all its contents.";
        if (!world.ReadyForBriefInteraction(actor)) return "The agent needs warmth before returning borrowed goods.";
        return "No open walking route reaches the owning household's House right now.";
    }
    internal static CognitionCandidate? StorageOrderCandidateFor(IStorageOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.ReadyForBriefInteraction(actor) || world.CustodyOrderHouse(instruction, world.HouseholdFor(actor)) is not { } house ||
            world.DestinationRoom(house.InstanceId) <= 0 ||
            !world.IsWithinInteractionRange(person.Position, house.Position, 1) &&
            world.Reach(actor, house.Position, ReachPurpose.HouseholdReturn, 1, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return null;
        var order = instruction.Task!;
        var lot = world.PersonalStorageLots(actor, house.InstanceId)
            .FirstOrDefault(item => item.ItemKind == world.StorageOrderItemKind(order));
        var subject = world.StorageOrderSubject(order);
        return lot is null ? null : new(order.Action, $"Carry your own {subject} to your House and store it as personal property.", 0, lot.Id);
    }


    internal static void ExecuteStorageOrderStep(IStorageOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = world.StorageOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.StorageOrderBlockedReason(instruction, person));
            return;
        }
        var house = world.CustodyOrderHouse(instruction, world.HouseholdFor(instruction.TargetAgentId))!;
        instruction = world.BindCustodyOrder(instruction, house);
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (world.StorePersonalGoods(instruction.TargetAgentId, lotId, remaining,
                order.TargetStorageBuildingId, order.TargetStorageOwnerId, order.TargetStoragePosition) is { } effect)
        {
            // Split inventory identities can grow; keep the saved receipt bounded.
            var receipt = world.PersonalStorageReceipt(effect.MoveId);
            world.CreditOrderEffect(instruction, Identity.Parse(receipt),
                order.ProgressUnit == "storage_loads" ? 1 : effect.Quantity);
        }
    }


    internal static string StorageOrderBlockedReason(IStorageOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        var subject = world.StorageOrderSubject(order);
        if (!world.MayFollowOwnerOrder(actor, order))
            return $"This agent is too young to put {subject} in House storage.";
        if (order.TargetStorageBuildingId is not null && world.CustodyOrderHouse(instruction, world.HouseholdFor(actor)) is null)
            return "The selected House moved, changed owner or is no longer available to this agent; the order keeps its original destination.";
        if (world.CustodyOrderHouse(instruction, world.HouseholdFor(actor)) is not { } house)
            return order.TargetPosition is not null
                ? "The requested tile does not contain a House held by this agent's household."
                : $"The agent needs a House held by their household to store personal {subject}.";
        if (world.DestinationRoom(house.InstanceId) <= 0)
            return $"The House storage is full; make room before storing more {subject}.";
        if (!world.PersonalStorageLots(actor, house.InstanceId).Any(lot => lot.ItemKind == world.StorageOrderItemKind(order)))
            return order.Action == "store_equipment"
                ? "No matching personal equipment is available to store; carry some that is not worn, reserved or promised for delivery."
                : order.Action == "store_material"
                    ? "No matching personal material is available to store; carry some that is not reserved or promised for delivery."
                    : "No matching personal goods fit in storage; carry unreserved goods that are not worn or promised for delivery, and leave room for each whole vessel and its contents.";
        if (!world.ReadyForBriefInteraction(actor))
            return $"The agent needs warmth before storing {subject}.";
        return "No open walking route reaches the agent's House right now.";
    }

}
