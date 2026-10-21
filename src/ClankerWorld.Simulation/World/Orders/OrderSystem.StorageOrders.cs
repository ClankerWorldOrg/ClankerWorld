using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
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
