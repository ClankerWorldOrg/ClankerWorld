using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
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

}
