using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId)
    {
        if (FarmWorkFor(actor) is { Work: { OrderInstructionId: null } work } field &&
            (work.HoeLotId == lotId || work.SickleLotId == lotId))
            CancelFarmWork(field);
    }

    private PlacedBuilding? CustodyOrderHouse(OwnerQueuedInstruction instruction, string? ownerId)
    {
        var order = instruction.Task!;
        if (ownerId is null || HouseForHousehold(ownerId) is not { } house ||
            order.TargetPosition is { } requested && house.Position != requested) return null;
        if (order.TargetStorageBuildingId is not null &&
            (house.InstanceId != order.TargetStorageBuildingId || house.HouseholdId != order.TargetStorageOwnerId ||
             house.Position != order.TargetStoragePosition)) return null;
        return house;
    }

    private OwnerQueuedInstruction BindCustodyOrder(OwnerQueuedInstruction instruction, PlacedBuilding house,
        string? lotId = null)
    {
        var current = instructionsByIdempotency[instruction.IdempotencyKey];
        var order = current.Task!;
        if (order.TargetStorageBuildingId is null)
            order = order.UpdateDefinition(definition => definition switch
{
    StoreMaterialOrderDefinition task => task with { TargetStorageBuildingId = house.InstanceId, TargetStorageOwnerId = house.HouseholdId, TargetStoragePosition = house.Position },
    StoreEquipmentOrderDefinition task => task with { TargetStorageBuildingId = house.InstanceId, TargetStorageOwnerId = house.HouseholdId, TargetStoragePosition = house.Position },
    StoreGoodsOrderDefinition task => task with { TargetStorageBuildingId = house.InstanceId, TargetStorageOwnerId = house.HouseholdId, TargetStoragePosition = house.Position },
    ReturnBorrowedOrderDefinition task => task with { TargetStorageBuildingId = house.InstanceId, TargetStorageOwnerId = house.HouseholdId, TargetStoragePosition = house.Position },
    DeliverStockOrderDefinition task => task with { TargetStorageBuildingId = house.InstanceId, TargetStorageOwnerId = house.HouseholdId, TargetStoragePosition = house.Position },
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
        if (lotId is not null) order = order.UpdateDefinition(definition => definition switch
{
    ReturnBorrowedOrderDefinition task => task with { TargetLotId = lotId },
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
        current = current with { Task = order };
        instructionsByIdempotency[current.IdempotencyKey] = current;
        checkpointSchemaVersion = StateSchemaVersion;
        return current;
    }

    private IEnumerable<InventoryLot> ReturnOrderGoods(OwnerQueuedInstruction instruction) =>
        ReturnableBorrowedGoods(instruction.TargetAgentId).Where(lot =>
            lot.ItemKind == instruction.Task!.TargetItemKind &&
            (instruction.Task.TargetLotId is null || lot.Id == instruction.Task.TargetLotId) &&
            (instruction.Task.TargetStorageOwnerId is null || lot.OwnerId == instruction.Task.TargetStorageOwnerId));

    private CognitionCandidate? ReturnOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!ReadyForBriefInteraction(actor)) return null;
        foreach (var lot in ReturnOrderGoods(instruction).OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            if (CustodyOrderHouse(instruction, lot.OwnerId) is not { } house || DestinationRoom(house.InstanceId) <= 0 ||
                !VesselFits(lot, DestinationRoom(house.InstanceId)) ||
                !IsWithinInteractionRange(person.Position, house.Position, 1) &&
                Reach(actor, house.Position, ReachPurpose.HouseholdPickup, 1, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            return new("return_borrowed", "Carry borrowed goods back to their owning household's House without changing ownership.", 0, lot.Id);
        }
        return null;
    }

    private void ExecuteReturnOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = ReturnOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, ReturnOrderBlockedReason(instruction, person));
            return;
        }
        var lot = ReturnOrderGoods(instruction).Single(item => item.Id == lotId);
        var house = CustodyOrderHouse(instruction, lot.OwnerId)!;
        instruction = BindCustodyOrder(instruction, house, lotId);
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (ReturnBorrowedGoods(instruction.TargetAgentId, lotId, remaining,
                order.TargetStorageBuildingId, order.TargetStorageOwnerId, order.TargetStoragePosition) is not { } effect) return;
        var receipt = BorrowedReturnReceipt(effect.MoveId);
        CreditOrderEffect(instruction, Identity.Parse(receipt), order.ProgressUnit == "return_loads" ? 1 : effect.Quantity);
        // Another load may use another lot, but it must keep the original owner and House.
        var current = instructionsByIdempotency[instruction.IdempotencyKey];
        instructionsByIdempotency[instruction.IdempotencyKey] = current with
        {
            Task = current.Task!.UpdateDefinition(definition => definition switch
{
    ReturnBorrowedOrderDefinition task => task with { TargetLotId = null },
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
        };
    }

    private string ReturnOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!AgePermitsCandidate(actor, order.Action)) return "This agent is too young to return borrowed goods.";
        if (order.TargetStorageOwnerId is { } owner && CustodyOrderHouse(instruction, owner) is null)
            return "The selected return House moved, changed owner or is no longer available; the order keeps its original destination.";
        var lots = ReturnOrderGoods(instruction).ToArray();
        if (lots.Length == 0)
            return "No matching borrowed goods are available; the selected goods must still be carried, unreserved and free of delivery promises.";
        var destinations = lots.Select(lot => (Lot: lot, House: CustodyOrderHouse(instruction, lot.OwnerId)))
            .Where(item => item.House is not null).ToArray();
        if (destinations.Length == 0) return "The goods' owning household has no House at the requested destination.";
        if (destinations.All(item => DestinationRoom(item.House!.InstanceId) <= 0 ||
                !VesselFits(item.Lot, DestinationRoom(item.House!.InstanceId))))
            return "The owning House needs room for the returned goods, including each whole vessel and all its contents.";
        if (!ReadyForBriefInteraction(actor)) return "The agent needs warmth before returning borrowed goods.";
        return "No open walking route reaches the owning household's House right now.";
    }

    private static void ValidateCustodyOrderBindings(SocietyCheckpoint society,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        // Removal, reassignment and expansion are legitimate world changes. They
        // block the saved destination at execution time rather than corrupting it.
        foreach (var instruction in instructions)
            if (instruction.Task is
                {
                    Action: "store_material" or "store_equipment" or "store_goods" or "return_borrowed",
                    TargetStorageOwnerId: { } owner
                } &&
                !society.Households.Any(household => household.Id == owner))
                throw new InvalidDataException("A custody order references an unknown owning household.");
    }
}
