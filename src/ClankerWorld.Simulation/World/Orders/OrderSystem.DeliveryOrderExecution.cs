using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static DeliveryOrderPlan? HouseholdDeliveryOrderPlan(IDeliveryOrderExecutionWorld world, string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (world.HouseholdFor(actor) is not { } householdId) return null;
        if (order.DeliveryPurpose == "household_food")
        {
            if (person.FullnessBasisPoints < NeedThresholds.HouseholdFoodDeliveryFullness || world.HouseForHousehold(householdId) is not { } foodHouse ||
                !world.DeliveryDestinationMatches(order, foodHouse) || world.PersonalSpareFood(actor, order.TargetItemKind, order.DeliveryLotId) is not { } food)
                return null;
            var quantity = Math.Min(maximumQuantity, Math.Min(world.DestinationRoom(foodHouse.InstanceId),
                Math.Min(world.HouseHaulLoadQuantity, world.AvailableLotQuantity(food) - 1)));
            return quantity > 0 ? new("household_food", foodHouse, householdId, food, food, quantity, quantity,
                DirectDelivery: true) : null;
        }
        if (order.TargetBuildingKind is "farmhouse" or "silo")
        {
            if (world.FarmhouseForHousehold(householdId) is null ||
                world.HouseholdBuildingWithTag(householdId, order.TargetBuildingKind!) is not { } requestedFarm ||
                world.FarmStorageFor(householdId, order.TargetItemKind!, requestedFarm.InstanceId) is not { } destination ||
                !world.DeliveryDestinationMatches(order, destination) ||
                world.FarmGrainForDelivery(householdId, actor, order.TargetItemKind, destination.InstanceId, maximumQuantity) is not { } choice ||
                world.PlanFarmStockHaul(actor, destination.InstanceId, choice, maximumQuantity) is not { } plan) return null;
            return new("farm_stock", destination, householdId, plan.Carrier, plan.Resource,
                plan.TransferQuantity, plan.ResourceQuantity,
                TakeFromContainer: !plan.MoveContainerFamily && plan.Resource.ContainerLotId == plan.Carrier.Id);
        }
        if (world.HouseForHousehold(householdId) is not { } house || !world.DeliveryDestinationMatches(order, house)) return null;
        if (order.TargetItemKind == "flour" && world.FarmhouseForHousehold(householdId) is { } farmhouse &&
            world.FarmFlourForHouse(householdId, farmhouse.InstanceId, actor, house.InstanceId, maximumQuantity) is { } flour &&
            world.PlanFarmStockHaul(actor, house.InstanceId, flour, maximumQuantity) is { } flourPlan &&
            order.DeliveryRoute is null or "farm_flour")
            return new("farm_flour", house, householdId, flourPlan.Carrier, flourPlan.Resource,
                flourPlan.TransferQuantity, flourPlan.ResourceQuantity,
                TakeFromContainer: !flourPlan.MoveContainerFamily && flourPlan.Resource.ContainerLotId == flourPlan.Carrier.Id);
        if (world.UnlocatedHouseholdStock(householdId, actor, house.InstanceId, order.TargetItemKind, maximumQuantity) is not { } stock)
            return null;
        var moved = Math.Min(maximumQuantity, world.HouseHaulPickupQuantity(actor, stock, house.InstanceId));
        var resourcesMoved = InventoryContainerRules.IsContainer(stock.ItemKind)
            ? world.DeliveryResourceQuantity(stock, order.TargetItemKind!) : moved;
        return moved > 0 ? new("house_stock", house, householdId, stock, stock, moved, resourcesMoved) : null;
    }


    internal static string? DeliveryRouteForCarried(IDeliveryOrderExecutionWorld world, OrderTask order, PlacedBuilding destination) => order.DeliveryPurpose switch
    {
        "household_stock" when order.TargetBuildingKind is "farmhouse" or "silo" && FarmFieldRules.IsFarmStock(order.TargetItemKind!) => "farm_stock",
        "household_stock" when order.TargetBuildingKind == "house" => order.DeliveryRoute ?? "house_stock",
        "workstation_input" when order.TargetBuildingKind == "blacksmith" &&
            world.BlacksmithInputTarget(destination.InstanceId, order.TargetItemKind!) > 0 => order.DeliveryRoute ?? "blacksmith_input",
        "workstation_input" when world.Content.Recipes.Any(recipe => recipe.WorkstationBuildingId == destination.DefinitionId &&
            recipe.Inputs.Any(input => input.ResourceId == order.TargetItemKind)) => "workstation_input",
        "store_stock" when ShopRules.MaySell("store", order.TargetItemKind!) => "store_stock",
        _ => null,
    };

    internal static DeliveryOrderPlan? CarriedDeliveryOrderPlan(IDeliveryOrderExecutionWorld world, string actor, OrderTask order)
    {
        var inventory = world.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) &&
                     lot.ContainerLotId is null && lot.DeliveryBuildingId is not null &&
                     (order.DeliveryLotId is null || lot.Id == order.DeliveryLotId))
                     .OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            if (world.Simulation.Buildings.SingleOrDefault(building => building.InstanceId == lot.DeliveryBuildingId) is not { } destination ||
                destination.HouseholdId != world.HouseholdFor(actor) || !world.DeliveryDestinationMatches(order, destination) ||
                world.DeliveryRouteForCarried(order, destination) is not { } route ||
                order.DeliveryRoute is not null && order.DeliveryRoute != route ||
                PersonalEquipmentRules.IsSelected(world.Agents[actor].Equipment, lot.Id)) continue;
            var vessel = InventoryContainerRules.IsContainer(lot.ItemKind);
            if (world.UnusableDeliveryStock(inventory, lot) || vessel && world.HasActiveContainerReservation(inventory, lot.Id)) continue;
            var available = vessel ? world.DeliveryResourceQuantity(lot, order.TargetItemKind!)
                : lot.ItemKind == order.TargetItemKind ? world.AvailableLotQuantity(lot) : 0;
            var quantity = order.DeliveryQuantity ?? Math.Min(available, world.DeliveryOrderRemaining(order));
            if (quantity <= 0 || quantity > available || quantity > world.DeliveryOrderRemaining(order) ||
                vessel && quantity != available) continue;
            var physicalQuantity = vessel ? world.HouseDeliveryPhysicalQuantity(inventory, lot) : quantity;
            if (physicalQuantity > world.DestinationRoom(lot.DeliveryBuildingId!, lot)) continue;
            return new(route, destination, destination.HouseholdId!, lot, lot, vessel ? 1 : quantity, quantity, DirectDelivery: true);
        }
        return null;
    }


    internal static CognitionCandidate? DeliveryOrderCandidateFor(IDeliveryOrderExecutionWorld world, OwnerQueuedInstruction instruction, AgentState person) => world.DeliveryPlanFor(instruction, person) is { } plan
            ? new("deliver_stock", "Carry the requested goods to the selected building under its normal stocking rules.", 0, plan.Destination.InstanceId)
            : world.DeliveryGatherFor(instruction, person) is { } gather
                ? new("deliver_stock", "Gather the requested input for the selected workstation.", 0, gather.Building.InstanceId) : null;

    internal static OwnerQueuedInstruction BindDeliveryOrder(IDeliveryOrderExecutionWorld world, OwnerQueuedInstruction instruction, PlacedBuilding destination,
        string ownerId, string route, string? lotId = null, int? quantity = null)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, order.UpdateTypedDefinition<DeliverStockOrderDefinition>(task =>
            OrderStore.PrepareBindDeliveryOrderDeliverStockOrderDefinition(task, order.TargetStorageBuildingId ?? destination.InstanceId, order.TargetStorageOwnerId ?? ownerId, order.TargetStoragePosition ?? destination.Position, order.DeliveryRoute ?? route, lotId ?? order.DeliveryLotId, quantity ?? order.DeliveryQuantity))
);
        world.Orders.RecordDeliveryOrder(current.IdempotencyKey, current);
        world.MarkCheckpointCurrent();
        return current;
    }


    internal static DeliveryNativeEffect? ApplyDeliveryOrderPlan(IDeliveryOrderExecutionWorld world, string actor, AgentState person,
        DeliveryOrderPlan plan)
    {
        var destination = plan.DirectDelivery ? plan.Destination.Position : world.HouseholdStockPosition(plan.Carrier);
        var range = plan.DirectDelivery ? 0 : world.HouseholdStockInteractionRange(plan.Carrier);
        // Moving a selected field tool ends only the ordinary work that used it.
        world.InterruptOrdinaryFieldWorkForCustody(actor, plan.Carrier.Id);
        if (!world.IsWithinInteractionRange(person.Position, destination, range))
        {
            world.MoveToward(actor, person, destination, "household_stock", range);
            return null;
        }
        var operation = $"order-delivery:{world.WorldTick}:{actor}:{plan.Carrier.Id}";
        var movedSource = plan.TakeFromContainer ? plan.Resource : plan.Carrier;
        var movedId = plan.TransferQuantity == movedSource.Quantity ? movedSource.Id :
            DerivedId.Create($"{movedSource.Id}#{(plan.TakeFromContainer ? "taken" : "transfer")}:{operation}");
        world.TransferOrderDelivery(actor, plan, operation);
        world.AppendEvent(plan.DirectDelivery ? new OwnerStockDeliveredEventPayload(actor, movedSource.Id, plan.ResourceQuantity, plan.Destination.InstanceId) : new OwnerStockPickedUpEventPayload(actor, movedSource.Id, plan.ResourceQuantity, plan.Destination.InstanceId));
        return new(operation, movedId, plan.ResourceQuantity, plan.DirectDelivery);
    }


    internal static void ExecuteDeliveryOrderStep(IDeliveryOrderExecutionWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (world.DeliveryPlanFor(instruction, person) is not { } plan)
        {
            if (world.DeliveryGatherFor(instruction, person) is { } gather)
            {
                instruction = world.BindDeliveryOrder(instruction, gather.Building, gather.Building.HouseholdId!, gather.Route);
                world.GatherProjectMaterial(actor, person, instruction.Task!.TargetItemKind!, gather.Source, returnTo: gather.Building.Position);
                return;
            }
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.DeliveryOrderBlockedReason(instruction, person));
            return;
        }
        instruction = world.BindDeliveryOrder(instruction, plan.Destination, plan.DestinationOwnerId, plan.Route,
            plan.DirectDelivery ? plan.Carrier.Id : null,
            plan.DirectDelivery ? plan.ResourceQuantity : null);
        if (world.ApplyDeliveryOrderPlan(actor, person, plan) is not { } effect) return;
        if (!effect.Delivered)
        {
            world.BindDeliveryOrder(instruction, plan.Destination, plan.DestinationOwnerId, plan.Route, effect.LotId, effect.Quantity);
            return;
        }
        world.CreditOrderEffect(instruction, Identity.Parse(world.StockDeliveryReceipt(effect.OperationId)),
            instruction.Task!.ProgressUnit == "delivery_loads" ? 1 : effect.Quantity);
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordDeliveryOrderStep(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateTypedDefinition<DeliverStockOrderDefinition>(task =>
            OrderStore.PrepareExecuteDeliveryOrderStepDeliverStockOrderDefinition(task, null, null))));
    }


    internal static string DeliveryOrderBlockedReason(IDeliveryOrderExecutionWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return "An adult resident is needed to deliver goods.";
        if (order.TargetStorageBuildingId is { } bound &&
            (world.Simulation.Buildings.SingleOrDefault(item => item.InstanceId == bound) is not { } building ||
             !world.DeliveryDestinationMatches(order, building) ||
             (order.DeliveryPurpose == "town_surplus" ? world.TownForResident(actor) != building.TownId : world.HouseholdFor(actor) != building.HouseholdId)))
            return "The selected destination moved, changed owner or is no longer available; the order keeps its original destination.";
        if (order.DeliveryLotId is not null)
            return "The goods for this delivery must still be carried, usable and not reserved, and the destination needs room and an open route for the whole load.";
        if (!world.ReadyForBriefInteraction(actor)) return "The agent needs warmth before delivering goods.";
        return "No matching delivery is available under this building's normal stock, reserve, carrying and access limits.";
    }

}
