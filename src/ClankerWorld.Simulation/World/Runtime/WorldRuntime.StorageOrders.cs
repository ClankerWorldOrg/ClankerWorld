using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? StorageOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!ReadyForBriefInteraction(actor) || CustodyOrderHouse(instruction, HouseholdFor(actor)) is not { } house ||
            DestinationRoom(house.InstanceId) <= 0 ||
            !IsWithinInteractionRange(person.Position, house.Position, 1) &&
            Reach(actor, house.Position, ReachPurpose.HouseholdReturn, 1, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return null;
        var order = instruction.Task!;
        var lot = PersonalStorageLots(actor, house.InstanceId)
            .FirstOrDefault(item => item.ItemKind == StorageOrderItemKind(order));
        var subject = StorageOrderSubject(order);
        return lot is null ? null : new(order.Action, $"Carry your own {subject} to your House and store it as personal property.", 0, lot.Id);
    }

    private void ExecuteStorageOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = StorageOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, StorageOrderBlockedReason(instruction, person));
            return;
        }
        var house = CustodyOrderHouse(instruction, HouseholdFor(instruction.TargetAgentId))!;
        instruction = BindCustodyOrder(instruction, house);
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (StorePersonalGoods(instruction.TargetAgentId, lotId, remaining,
                order.TargetStorageBuildingId, order.TargetStorageOwnerId, order.TargetStoragePosition) is { } effect)
        {
            // Split inventory identities can grow; keep the saved receipt bounded.
            var receipt = PersonalStorageReceipt(effect.MoveId);
            CreditOrderEffect(instruction, Identity.Parse(receipt),
                order.ProgressUnit == "storage_loads" ? 1 : effect.Quantity);
        }
    }

    private string StorageOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        var subject = StorageOrderSubject(order);
        if (!AgePermitsCandidate(actor, order.Action))
            return $"This agent is too young to put {subject} in House storage.";
        if (order.TargetStorageBuildingId is not null && CustodyOrderHouse(instruction, HouseholdFor(actor)) is null)
            return "The selected House moved, changed owner or is no longer available to this agent; the order keeps its original destination.";
        if (CustodyOrderHouse(instruction, HouseholdFor(actor)) is not { } house)
            return order.TargetPosition is not null
                ? "The requested tile does not contain a House held by this agent's household."
                : $"The agent needs a House held by their household to store personal {subject}.";
        if (DestinationRoom(house.InstanceId) <= 0)
            return $"The House storage is full; make room before storing more {subject}.";
        if (!PersonalStorageLots(actor, house.InstanceId).Any(lot => lot.ItemKind == StorageOrderItemKind(order)))
            return order.Action == "store_equipment"
                ? "No matching personal equipment is available to store; carry some that is not worn, reserved or promised for delivery."
                : order.Action == "store_material"
                    ? "No matching personal material is available to store; carry some that is not reserved or promised for delivery."
                    : "No matching personal goods fit in storage; carry unreserved goods that are not worn or promised for delivery, and leave room for each whole vessel and its contents.";
        if (!ReadyForBriefInteraction(actor))
            return $"The agent needs warmth before storing {subject}.";
        return "No open walking route reaches the agent's House right now.";
    }

    private static string? StorageOrderItemKind(OrderTask order) =>
        order.TargetItemKind ?? order.TargetEquipmentKind ?? order.TargetMaterialKind;

    private static string StorageOrderSubject(OrderTask order) =>
        order.Action == "store_equipment" ? "equipment" : order.Action == "store_goods" ? "goods" : "materials";
}
