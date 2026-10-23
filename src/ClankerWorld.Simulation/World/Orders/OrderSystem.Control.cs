using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OwnerQueuedInstruction? PendingInstructionFor(IControlWorld world, string agentId) => world.Orders.FirstActiveFor(agentId);

    internal static void CancelActiveOrder(IControlWorld world, OwnerQueuedInstruction instruction, string cause, string? reason)
    {
        world.Orders.RecordCancellation(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, instruction.Task!.Transition(OrderStatus.Cancelled, reason)));
        world.CancelRepairForOrder(instruction);
        world.CancelFieldWorkForOrder(instruction);
        world.CancelProductionForOrder(instruction);
        world.CancelConstructionForOrder(instruction);
        world.CancelExpansionForOrder(instruction);
        world.CancelAnimalSupplyForOrder(instruction);
        world.CancelBoatTravelForOrder(instruction);
        world.CancelKnowledgeWritingForOrder(instruction);
        world.MarkCheckpointCurrent();
        world.AppendEvent(new InstructionOrderCancelledEventPayload(instruction.TargetAgentId, instruction.InstructionId, cause));
    }


    internal static bool HasNewObserverGuidanceFor(IControlWorld world, string agentId) => world.ObserverGuidanceInstructionsFor(agentId).Any(item => item.GuidancePromptedTick is null);

    internal static OwnerQueuedInstruction[] ObserverGuidanceInstructionsFor(IControlWorld world, string agentId)
    {
        var messages = world.Orders.Instructions.Values
            .Where(item => item.TargetAgentId == agentId &&
                !world.IsInstructionComplete(item) &&
                (item.Kind == OwnerInstructionKind.Suggestion ||
                    item.InstructionId == world.PendingInstructionFor(agentId)?.InstructionId))
            .OrderBy(item => item.SubmissionSequence)
            .ToList();
        var selected = messages.Take(AgentObservation.MaximumObserverGuidanceCount).ToList();
        if (messages.Count > selected.Count &&
            world.PendingInstructionFor(agentId) is { } operativeOrder &&
            selected.All(item => item.InstructionId != operativeOrder.InstructionId))
        {
            selected[^1] = operativeOrder;
            selected.Sort((left, right) => left.SubmissionSequence.CompareTo(right.SubmissionSequence));
        }

        return selected.ToArray();
    }


    internal static CognitionObserverGuidance[] ObserverGuidanceFor(IControlWorld world, string agentId) => world.ObserverGuidanceInstructionsFor(agentId)
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
                         !world.IsInstructionComplete(item) &&
                         item.Task?.Action == "unknown")
                     .OrderBy(item => item.SubmissionSequence)
                     .ToArray())
        {
            world.Orders.CloseUnrecognizedOrder(order.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(order, order.Task!.Transition(OrderStatus.NotUnderstood)));
            world.AppendEvent(new InstructionNotUnderstoodEventPayload(agentId, order.InstructionId));
        }
        world.MarkCheckpointCurrent();
    }


    internal static void ReplacePendingOrder(IControlWorld world, string agentId)
    {
        if (world.PendingInstructionFor(agentId) is { Task.Status: not "queued" } instruction)
            world.CancelActiveOrder(instruction, "replaced", "Replaced by a newer order.");
        world.MarkCheckpointCurrent();
    }

}
