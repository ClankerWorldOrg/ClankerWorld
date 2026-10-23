
namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static void CreditOrderEffect(IOrderLifecycleWorld world, OwnerQueuedInstruction instruction, Identity effectId, int units)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        if (units <= 0 || order.LastEffectId == effectId || !world.IsActiveOrder(order.Status)) return;
        var completed = Math.Min(1_000_000, order.CompletedUnits + units);
        var finished = !order.RepeatUntilCancelled && completed >= order.RequestedUnits;
        world.Orders.RecordProgress(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, (OrderStore.PrepareCreditOrderEffectOrderTask(order, completed, effectId)).Transition(finished ? OrderStatus.Finished : OrderStatus.Doing)));
        if (finished)
        {
            world.AppendEvent(new InstructionAppliedEventPayload(current.TargetAgentId, current.InstructionId, order.Action));
            world.AppendEvent(new InstructionOrderFinishedEventPayload(current.TargetAgentId, current.InstructionId, effectId));
            world.PruneClosedOrderHistory();
        }
        else
        {
            world.AppendEvent(new InstructionOrderProgressEventPayload(current.TargetAgentId, current.InstructionId, completed));
        }
        world.MarkCheckpointCurrent();
    }


    internal static void SetOrderStatus(IOrderLifecycleWorld world,
        OwnerQueuedInstruction instruction,
        OrderStatus status,
        string? reason,
        bool waitForDecision = false)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        if (current.Task is not { } order || !world.IsActiveOrder(order.Status)) return;
        if (status == OrderStatus.Interrupted && world.BoundProductionJob(current) is { } production)
            world.PauseProductionOrderJob(current, production);
        if (status == OrderStatus.Interrupted)
        {
            world.PauseConstructionForOrder(current);
            world.PauseExpansionForOrder(current);
        }
        if (order.LifecycleStatus == status && order.BlockedReason == reason &&
            order.WaitForDecisionAfterFailure == waitForDecision) return;
        world.Orders.RecordStatus(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, order.Transition(status, reason, waitForDecision)));
        world.MarkCheckpointCurrent();
        world.AppendEvent(new InstructionOrderStatusEventPayload(current.TargetAgentId, current.InstructionId, status.ToWireValue()));
    }

}
