using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Kernel;

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
    internal static OwnerInstructionReceipt SubmitInstruction(IOrderIngressWorld world, OwnerInstructionRequest request)
    {
        var idempotencyKey = request.IdempotencyKey.Trim();
        if (world.Orders.Receipts.TryGetValue(idempotencyKey, out var existing))
        {
            if (existing.RequestDigest != world.InstructionRequestDigest(request))
            {
                throw new InvalidOperationException(
                    "An idempotency key cannot be reused for a different instruction request.");
            }

            return existing.Receipt;
        }

        var targetId = request.TargetAgentId.Trim();
        var target = world.Society.Agents.SingleOrDefault(item => item.Id == targetId);
        if (target is null || target.Status != SocietyAgentStatus.Active)
        {
            throw new ArgumentException($"No active agent with ID '{targetId}' exists.", nameof(request));
        }
        if (LifeStageRules.IsInfant(target.AgeBand))
        {
            throw new ArgumentException("Infants cannot carry out owner instructions; direct care through an adult caregiver.", nameof(request));
        }

        var text = request.Text.Trim();
        var hasPendingOrder = world.PendingInstructionFor(targetId) is not null;
        var parsedOrder = request.Kind == OwnerInstructionKind.Order ? world.ParseOrderTask(text, targetId) : null;
        if (parsedOrder is not null && !request.Queue)
            world.ReplacePendingOrder(targetId);
        if (parsedOrder is not null)
            parsedOrder = parsedOrder.Transition(request.Queue && hasPendingOrder ? OrderStatus.Queued : OrderStatus.Waiting);

        var sequence = world.Orders.BeginSubmissionSequence();
        var instruction = OrderStore.CreateOwnerQueuedInstruction(world.InstructionIdFor(sequence), idempotencyKey, request.IssuerId.Trim(), targetId, request.Kind, text, world.WorldTick, world.Society.RunEpoch, sequence, OwnerInstructionState.Queued, Queue: request.Queue, Task: request.Kind != OwnerInstructionKind.Order ? null : parsedOrder ??
                OrderStore.CreateOrderTask(OrderStore.CreateUnknownOrderDefinition(), "queued", 0, 0, "none", false));
        world.Orders.QueueInstruction(instruction);
        var receipt = OrderStore.CreateOwnerInstructionReceipt(Identity.Parse(instruction.InstructionId), instruction.IdempotencyKey, instruction.SubmittedTick, instruction.RunEpoch, sequence);
        world.Orders.RecordSubmissionReceipt(instruction.IdempotencyKey, OrderStore.CreateRecordedInstructionReceipt(receipt, world.InstructionRequestDigest(request)));
        world.AppendEvent(new InstructionQueuedEventPayload(instruction.TargetAgentId, instruction.InstructionId, world.ToWireValue(instruction.Kind)));
        world.CloseOrdersNotUnderstood(targetId);
        world.PruneClosedOrderHistory();
        return receipt;
    }


    internal static OwnerOrderControlReceipt CancelOrder(IOrderIngressWorld world, OwnerOrderCancelRequest request)
    {
        var key = request.IdempotencyKey.Trim();
        if (world.Orders.Cancellations.TryGetValue(key, out var existing))
        {
            if (!world.Matches(existing, request))
                throw new InvalidOperationException("An idempotency key cannot be reused for a different order cancellation.");
            return existing.Receipt;
        }
        if (!string.Equals(request.WorldId.Trim(), world.Society.WorldId, StringComparison.Ordinal))
            throw new InvalidOperationException("The cancellation belongs to another world.");
        var instruction = world.Orders.Instructions.Values.SingleOrDefault(item =>
            item.InstructionId == request.OrderId.Trim());
        if (instruction is null || instruction.Kind != OwnerInstructionKind.Order ||
            instruction.TargetAgentId != request.TargetAgentId.Trim())
            throw new ArgumentException("The order does not belong to this active agent in this world.", nameof(request));

        var changed = instruction.Task is { } order && world.IsActiveOrder(order.Status) &&
            !world.IsInstructionComplete(instruction);
        var status = instruction.Task?.Status ?? "not_understood";
        if (changed)
        {
            world.CancelActiveOrder(instruction, "owner", null);
            status = "cancelled";
        }
        var receipt = OrderStore.CreateOwnerOrderControlReceipt(Identity.Parse(instruction.InstructionId), status, changed, world.WorldTick, world.NextEventId - 1);
        world.Orders.RecordCancellationReceipt(key, OrderStore.CreateOwnerOrderCancellation(key, request.IssuerId.Trim(), request.WorldId.Trim(), request.TargetAgentId.Trim(), request.OrderId.Trim(), receipt));
        world.PruneClosedOrderHistory();
        world.MarkCheckpointCurrent();
        return receipt;
    }
    internal static bool IsFieldOrder(string action) => action is "till_field" or "plant_field" or "tend_field" or "harvest_field";
    internal static bool IsKnowledgeOrder(string action) => action is "write_knowledge" or "copy_knowledge";
    internal static bool IsShelterOrder(string action) => action is "seek_shelter" or "tend_fire";
    internal static bool IsConversationOrder(string action) => action is "talk_to" or "propose_marriage";
    internal static bool IsTreePlantingOrder(string action) =>
        action is "plant_tree" or "plant_broadleaf" or "plant_conifer" or "plant_orchard";
    internal sealed record ExpansionOrderPlan(PlacedBuilding Building, GridPoint Position,
        BuildingFootprintRevision Footprint);
    internal sealed record MaterialGatherEffect(string LotId, string ItemKind, int Quantity, string ResourceId);
    internal sealed record FoodHarvestEffect(string LotId, string ItemKind, int Quantity, string ResourceId);
    internal sealed record TreeOrderPlan(GridPoint Site, string Species, InventoryLot Seed, bool Carried);
    internal sealed record DeliveryNativeEffect(string OperationId, string LotId, int Quantity, bool Delivered);
    internal sealed record DeliveryOrderPlan(string Route, PlacedBuilding Destination, string DestinationOwnerId,
        InventoryLot Carrier, InventoryLot Resource, int TransferQuantity, int ResourceQuantity,
        bool TakeFromContainer = false, bool DirectDelivery = false);

}
