using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OwnerQueuedInstruction? PendingInstructionFor(IControlWorld world, string agentId) =>
        world.Orders.Instructions.Values
            .Where(item => item.TargetAgentId == agentId &&
                item.Kind == OwnerInstructionKind.Order &&
                item.Task is not null && world.IsActiveOrder(item.Task.Status) &&
                !world.Orders.Completed.Contains(item.InstructionId))
            .OrderBy(item => item.SubmissionSequence)
            .FirstOrDefault();

    internal static void ReplacePendingOrders(IControlWorld world, string agentId)
    {
        foreach (var instruction in world.Orders.Instructions.Values
                     .Where(item => item.TargetAgentId == agentId &&
                         item.Kind == OwnerInstructionKind.Order && item.Task is { } order &&
                         world.IsActiveOrder(order.Status) && !world.Orders.Completed.Contains(item.InstructionId))
                     .OrderBy(item => item.SubmissionSequence).ToArray())
        {
            CancelActiveOrder(world, instruction, "replaced", "Replaced by a newer order.");
        }
        world.MarkCheckpointCurrent();
    }

    internal static void CancelActiveOrder(IControlWorld world, OwnerQueuedInstruction instruction, string cause, string? reason)
    {
        world.Orders.RecordCancellation(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(instruction, OrderStore.PrepareOrderStatus(instruction.Task!, "cancelled", reason, false)));
        world.Orders.CompleteInstruction(instruction.InstructionId);
        world.CancelRepairForOrder(instruction);
        world.CancelFieldWorkForOrder(instruction);
        world.CancelProductionForOrder(instruction);
        world.CancelConstructionForOrder(instruction);
        world.CancelExpansionForOrder(instruction);
        world.CancelAnimalSupplyForOrder(instruction);
        world.CancelBoatTravelForOrder(instruction);
        world.CancelKnowledgeWritingForOrder(instruction);
        world.MarkCheckpointCurrent();
        world.AppendEvent("instruction_order_cancelled", $"{instruction.TargetAgentId}:{instruction.InstructionId}:{cause}");
    }

    internal static bool HasNewObserverGuidanceFor(IControlWorld world, string agentId) =>
        ObserverGuidanceInstructionsFor(world, agentId).Any(item => item.GuidancePromptedTick is null);

    internal static OwnerQueuedInstruction[] ObserverGuidanceInstructionsFor(IControlWorld world, string agentId)
    {
        var messages = world.Orders.Instructions.Values
            .Where(item => item.TargetAgentId == agentId &&
                !world.Orders.Completed.Contains(item.InstructionId) &&
                (item.Kind == OwnerInstructionKind.Suggestion ||
                    item.InstructionId == PendingInstructionFor(world, agentId)?.InstructionId))
            .OrderBy(item => item.SubmissionSequence)
            .ToList();
        var selected = messages.Take(AgentObservation.MaximumObserverGuidanceCount).ToList();
        if (messages.Count > selected.Count &&
            PendingInstructionFor(world, agentId) is { } operativeOrder &&
            selected.All(item => item.InstructionId != operativeOrder.InstructionId))
        {
            selected[^1] = operativeOrder;
            selected.Sort((left, right) => left.SubmissionSequence.CompareTo(right.SubmissionSequence));
        }

        return selected.ToArray();
    }

    internal static CognitionObserverGuidance[] ObserverGuidanceFor(IControlWorld world, string agentId) =>
        ObserverGuidanceInstructionsFor(world, agentId)
            .Select(item => new CognitionObserverGuidance(
                item.InstructionId,
                item.IssuerId,
                item.TargetAgentId,
                world.ToWireValue(item.Kind),
                item.Text,
                item.SubmittedTick,
                item.RunEpoch,
                item.SubmissionSequence,
                item.Kind == OwnerInstructionKind.Order
                    ? world.UnderstoodTaskFor(item.Task?.Action)
                    : null,
                item.ObserverReply is null))
            .ToArray();

    internal static OrderTask? ParseOrderTask(IControlWorld world, string text, string actor)
    {
        return world.ParseMarriageOrder(text, actor) ?? world.ParseKnowledgeReadOrder(text) ?? world.ParseTalkOrder(text, actor) ?? world.ParseCartCargoOrder(text, actor) ?? world.ParseCartOrder(text, actor) ?? world.ParseAnimalOrder(text, actor) ?? world.ParseGuardianOrder(text, actor) ?? OrderTaskParser.Parse(text, world.Map.Resources, world.FoodKnowledgeKind,
            WorldProductionOrderCatalog.Available(world.Content), WorldDeliveryOrderCatalog.AvailableInputs(world.Content),
            WorldBuildingOrderCatalog.Available(world.Content),
            world.Simulation.Buildings.Where(port => world.Port(port.InstanceId) is not null).ToArray());
    }

    internal static void CloseOrdersNotUnderstood(IControlWorld world, string agentId)
    {
        foreach (var order in world.Orders.Instructions.Values
                     .Where(item => item.TargetAgentId == agentId &&
                         item.Kind == OwnerInstructionKind.Order &&
                         !world.Orders.Completed.Contains(item.InstructionId) &&
                         item.Task?.Action == "unknown")
                     .OrderBy(item => item.SubmissionSequence)
                     .ToArray())
        {
            world.Orders.CloseUnrecognizedOrder(order.IdempotencyKey, OrderStore.PrepareTaskBinding(order, OrderStore.PrepareQueuedStatus(order.Task!, "not_understood")));
            world.Orders.CompleteInstruction(order.InstructionId);
            world.AppendEvent("instruction_not_understood", $"{agentId}:{order.InstructionId}");
        }
        world.MarkCheckpointCurrent();
    }
}
