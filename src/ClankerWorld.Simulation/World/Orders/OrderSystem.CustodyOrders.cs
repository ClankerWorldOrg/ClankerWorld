using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
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
            order = OrderStore.PrepareCustodyDestination(order, house.InstanceId, house.HouseholdId, house.Position);
        if (lotId is not null) order = OrderStore.PrepareStockBinding(order, lotId);
        current = OrderStore.PrepareTaskBinding(current, order);
        world.Orders.RecordCustodyOrder(current.IdempotencyKey, current);
        world.MarkCheckpointCurrent();
        return current;
    }

    internal static IEnumerable<InventoryLot> ReturnOrderGoods(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction) =>
        world.ReturnableBorrowedGoods(instruction.TargetAgentId).Where(lot =>
            lot.ItemKind == instruction.Task!.TargetItemKind &&
            (instruction.Task.TargetLotId is null || lot.Id == instruction.Task.TargetLotId) &&
            (instruction.Task.TargetStorageOwnerId is null || lot.OwnerId == instruction.Task.TargetStorageOwnerId));

    internal static CognitionCandidate? ReturnOrderCandidateFor(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.ReadyForBriefInteraction(actor)) return null;
        foreach (var lot in ReturnOrderGoods(world, instruction).OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            if (CustodyOrderHouse(world, instruction, lot.OwnerId) is not { } house || world.DestinationRoom(house.InstanceId) <= 0 ||
                !world.VesselFits(lot, world.DestinationRoom(house.InstanceId)) ||
                !world.IsWithinInteractionRange(person.Position, house.Position, 1) &&
                world.Reach(actor, house.Position, ReachPurpose.HouseholdPickup, 1, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            return new("return_borrowed", "Carry borrowed goods back to their owning household's House without changing ownership.", 0, lot.Id);
        }
        return null;
    }

    internal static void ExecuteReturnOrderStep(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = ReturnOrderCandidateFor(world, instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, "blocked", ReturnOrderBlockedReason(world, instruction, person));
            return;
        }
        var lot = ReturnOrderGoods(world, instruction).Single(item => item.Id == lotId);
        var house = CustodyOrderHouse(world, instruction, lot.OwnerId)!;
        instruction = BindCustodyOrder(world, instruction, house, lotId);
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (world.ReturnBorrowedGoods(instruction.TargetAgentId, lotId, remaining,
                order.TargetStorageBuildingId, order.TargetStorageOwnerId, order.TargetStoragePosition) is not { } effect) return;
        var receipt = world.BorrowedReturnReceipt(effect.MoveId);
        world.CreditOrderEffect(instruction, receipt, order.ProgressUnit == "return_loads" ? 1 : effect.Quantity);
        // Another load may use another lot, but it must keep the original owner and House.
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordReturnOrderStep(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareStockBinding(current.Task!, null)));
    }

    internal static string ReturnOrderBlockedReason(ICustodyOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.MayFollowOwnerOrder(actor, order)) return "This agent is too young to return borrowed goods.";
        if (order.TargetStorageOwnerId is { } owner && CustodyOrderHouse(world, instruction, owner) is null)
            return "The selected return House moved, changed owner or is no longer available; the order keeps its original destination.";
        var lots = ReturnOrderGoods(world, instruction).ToArray();
        if (lots.Length == 0)
            return "No matching borrowed goods are available; the selected goods must still be carried, unreserved and free of delivery promises.";
        var destinations = lots.Select(lot => (Lot: lot, House: CustodyOrderHouse(world, instruction, lot.OwnerId)))
            .Where(item => item.House is not null).ToArray();
        if (destinations.Length == 0) return "The goods' owning household has no House at the requested destination.";
        if (destinations.All(item => world.DestinationRoom(item.House!.InstanceId) <= 0 ||
                !world.VesselFits(item.Lot, world.DestinationRoom(item.House!.InstanceId))))
            return "The owning House needs room for the returned goods, including each whole vessel and all its contents.";
        if (!world.ReadyForBriefInteraction(actor)) return "The agent needs warmth before returning borrowed goods.";
        return "No open walking route reaches the owning household's House right now.";
    }
}
